using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>
/// «Ho controllato»: per ogni disco, fino a quale errore del registro di Windows l'utente ha gia'
/// guardato. Gli errori restano nel registro per giorni (RoboKeep ne guarda due settimane), e
/// senza questo l'avviso pre-avvio ricomparirebbe a ogni backup per lo stesso episodio gia'
/// chiarito. Gli errori piu' recenti della data salvata avvisano di nuovo.
/// <para>Il disco si riconosce dall'identita' del volume (<c>\\?\Volume{GUID}\</c>), che per un
/// disco MBR nasce dalla firma scritta sul disco stesso: non dalla lettera, che passa da un disco
/// all'altro. Best-effort come gli altri file di stato: un errore di lettura o scrittura non ferma
/// mai un backup, al massimo l'avviso ricompare.</para>
/// </summary>
public sealed class DiskEventAcknowledgements
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    // Piu' processi RoboKeep possono girare insieme: il read-modify-write va serializzato.
    private static readonly Mutex CrossProcess = new(false, @"Global\RoboKeep.DiskEventAcks");
    private readonly string _path;

    public DiskEventAcknowledgements(string path) => _path = path;

    /// <summary>Fino a quando gli errori di quel disco sono stati controllati; null se mai.</summary>
    public DateTime? Until(string? volumeId) =>
        !string.IsNullOrEmpty(volumeId) && Load().TryGetValue(volumeId, out var until) ? until : null;

    /// <summary>Segna come controllati gli errori di quel disco fino a <paramref name="until"/>.
    /// Non torna mai indietro: una conferma su un riepilogo piu' vecchio non fa ricomparire avvisi
    /// gia' chiusi.</summary>
    public void Acknowledge(string volumeId, DateTime until)
    {
        if (string.IsNullOrEmpty(volumeId)) return;
        var held = false;
        try
        {
            try { held = CrossProcess.WaitOne(TimeSpan.FromSeconds(3)); }
            catch (AbandonedMutexException) { held = true; }
            var all = Load();
            if (all.TryGetValue(volumeId, out var current) && current >= until) return;
            all[volumeId] = until;
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(all, Options));
        }
        catch
        {
            // Non salvato: l'avviso ricomparira', nient'altro.
        }
        finally
        {
            if (held) CrossProcess.ReleaseMutex();
        }
    }

    private Dictionary<string, DateTime> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new(StringComparer.OrdinalIgnoreCase);
            var read = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(_path), Options);
            return read is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(read, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
