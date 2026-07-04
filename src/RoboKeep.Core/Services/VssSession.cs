using System.ComponentModel;
using System.Diagnostics;

namespace RoboKeep.Core.Services;

/// <summary>
/// Lato client della sessione VSS: lancia il helper elevato (prompt UAC), attende lo snapshot
/// e espone la sorgente rimappata. Il Dispose segnala il rilascio e attende la pulizia.
/// Qualsiasi fallimento diventa <see cref="VssUnavailableException"/>: il chiamante degrada
/// a copia normale, mai bloccare il backup.
/// </summary>
public sealed class VssSession : IAsyncDisposable
{
    private const int ErrorCancelled = 1223; // UAC negato dall'utente
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly string _sessionDir;
    private readonly VssLedger _ledger;
    private readonly Process _helper;

    /// <summary>Percorso del job rimappato dentro lo snapshot (da passare come sourceOverride).</summary>
    public string SnapshotSourcePath { get; }
    public string ShadowId { get; }

    private VssSession(string sessionDir, VssLedger ledger, Process helper, string shadowId, string mapped)
    {
        _sessionDir = sessionDir;
        _ledger = ledger;
        _helper = helper;
        ShadowId = shadowId;
        SnapshotSourcePath = mapped;
    }

    /// <summary>
    /// Apre una sessione: helper elevato (UAC), snapshot del volume di <paramref name="sourcePath"/>.
    /// Lancia <see cref="VssUnavailableException"/> se lo snapshot non è disponibile.
    /// </summary>
    public static async Task<VssSession> OpenAsync(
        string sourcePath, string sessionRoot, VssLedger ledger, CancellationToken ct = default)
    {
        var volume = VssPathMapper.GetVolumeRoot(sourcePath)
            ?? throw new VssUnavailableException(CoreLoc.S("Vss_NotEligible"));

        var exePath = Environment.ProcessPath
            ?? throw new VssUnavailableException("Percorso eseguibile non determinabile.");

        var sessionDir = Path.Combine(sessionRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sessionDir);

        VssSessionProtocol.WriteRequest(sessionDir, new VssRequest(
            volume, Environment.ProcessId, ledger.List().ToList()));

        Process helper;
        try
        {
            helper = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--vss-helper \"{sessionDir}\"",
                UseShellExecute = true, // necessario per il verbo runas
                Verb = "runas",
            }) ?? throw new VssUnavailableException("Avvio del processo elevato fallito.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            TryDeleteDir(sessionDir);
            throw new VssUnavailableException(CoreLoc.S("Vss_UacDenied"), ex);
        }

        // Il timeout parte da qui: Process.Start con runas ritorna solo dopo la risposta
        // al prompt UAC, quindi il tempo di decisione dell'utente non è conteggiato.
        try
        {
            var ready = await WaitForReadyAsync(sessionDir, helper, ct).ConfigureAwait(false);

            if (!ready.Success || ready.ShadowId is null || ready.LinkPath is null)
                throw new VssUnavailableException(ready.Error ?? "Errore snapshot sconosciuto.");

            ledger.Add(ready.ShadowId);
            var mapped = VssPathMapper.MapToSnapshot(sourcePath, ready.LinkPath);
            return new VssSession(sessionDir, ledger, helper, ready.ShadowId, mapped);
        }
        catch
        {
            // Percorso di errore dopo il lancio del helper: se lo snapshot è stato comunque
            // creato (ready.json presente con esito Ok), registra l'ID nel ledger così il
            // residuo verrà eliminato al prossimo run VSS. Chiude anche la finestra
            // "helper morto dopo la creazione ma prima della lettura di ready".
            TrySalvageShadowId(sessionDir, ledger);
            try { VssSessionProtocol.SignalRelease(sessionDir); } catch { }
            helper.Dispose();
            TryDeleteDir(sessionDir);
            throw;
        }
    }

    /// <summary>
    /// Attende ready.json pollando finché non appare, il helper termina o scade il timeout.
    /// Quando il helper risulta terminato, rilegge una volta ready.json prima di arrendersi:
    /// un Fail-ready scritto appena prima dell'uscita del processo va sempre preferito al
    /// messaggio generico "terminato senza esito" (l'helper scrive Fail e poi esce subito,
    /// quindi HasExited e la scrittura del file possono correre appaiati).
    /// </summary>
    private static async Task<VssReady> WaitForReadyAsync(string sessionDir, Process helper, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + ReadyTimeout;
        while (true)
        {
            var ready = VssSessionProtocol.ReadReady(sessionDir);
            if (ready is not null) return ready;

            if (helper.HasExited)
            {
                // Ultima lettura: il file potrebbe essere stato scritto tra il controllo
                // sopra e l'uscita del processo rilevata qui.
                ready = VssSessionProtocol.ReadReady(sessionDir);
                if (ready is not null) return ready;
                throw new VssUnavailableException("Il processo elevato è terminato senza esito.");
            }

            if (DateTime.UtcNow > deadline)
                throw new VssUnavailableException("Timeout in attesa dello snapshot (60s).");

            ct.ThrowIfCancellationRequested();
            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Segnala il rilascio, attende la pulizia del helper e rimuove l'ID dal registro.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            VssSessionProtocol.SignalRelease(_sessionDir);
            var deadline = DateTime.UtcNow + ExitTimeout;
            while (!_helper.HasExited && DateTime.UtcNow < deadline)
                await Task.Delay(PollInterval).ConfigureAwait(false);

            // Se il helper è uscito pulito ha cancellato lo snapshot: togli l'ID dal registro.
            // Se non è uscito, lascia l'ID: verrà ripulito al prossimo run VSS.
            if (_helper.HasExited)
                _ledger.Remove(ShadowId);
        }
        finally
        {
            _helper.Dispose();
            TryDeleteDir(_sessionDir);
        }
    }

    private static void TrySalvageShadowId(string sessionDir, VssLedger ledger)
    {
        try
        {
            var ready = VssSessionProtocol.ReadReady(sessionDir);
            if (ready is { Success: true, ShadowId: not null })
                ledger.Add(ready.ShadowId);
        }
        catch { }
    }

    private static void TryDeleteDir(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }
}
