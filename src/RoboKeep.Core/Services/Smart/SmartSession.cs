using System.ComponentModel;
using System.Diagnostics;

namespace RoboKeep.Core.Services.Smart;

/// <summary>Esito complessivo della lettura: i dischi e se l'elevazione e' stata negata/fallita.</summary>
public sealed record SmartSnapshot(IReadOnlyList<DiskReport> Disks, bool ElevationDenied, bool HelperFailed);

/// <summary>
/// Lato app: legge subito cio' che non richiede privilegi (elenco dischi, NVMe), poi — se ci sono
/// dischi non NVMe — avvia il helper elevato (prompt UAC, come per VSS), ne legge smart.json e
/// fonde i risultati. Cartella di sessione usa-e-getta sotto <paramref name="sessionRoot"/>.
/// </summary>
public static class SmartSession
{
    private const int ErrorCancelled = 1223;
    private static readonly TimeSpan HelperTimeout = TimeSpan.FromSeconds(60);

    public static async Task<SmartSnapshot> ReadAsync(string sessionRoot, CancellationToken ct = default)
    {
        var disks = await Task.Run(() => SmartReader.ListPhysicalDisks()
            .Select(d => d.Bus == DiskBus.Nvme ? d with { NvmeLog = SmartReader.ReadNvme(d.Number) } : d).ToList(), ct)
            .ConfigureAwait(false);
        if (!disks.Any(d => d.Bus != DiskBus.Nvme)) return new SmartSnapshot(disks, false, false);

        var exe = Environment.ProcessPath;
        if (exe is null) return new SmartSnapshot(disks, false, true);
        var dir = Path.Combine(sessionRoot, Guid.NewGuid().ToString("N"));
        try
        {
            // Cartelle rimaste da letture interrotte (helper ucciso, app chiusa a meta'): si
            // spazzano qui, altrimenti si accumulerebbero senza che nessuno le tolga. La radice
            // puo' non esistere ancora: e' il caso normale della prima lettura.
            try
            {
                foreach (var stale in Directory.EnumerateDirectories(sessionRoot)) Directory.Delete(stale, true);
            }
            catch { }
            Directory.CreateDirectory(dir);
            Process helper;
            try
            {
                helper = await Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = exe, Arguments = $"--smart-helper \"{dir}\"", UseShellExecute = true, Verb = "runas",
                }), ct).ConfigureAwait(false) ?? throw new Win32Exception("avvio fallito");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled) { return new SmartSnapshot(disks, true, false); }
            catch (Win32Exception) { return new SmartSnapshot(disks, false, true); }

            using (helper)
            {
                var exited = await Task.Run(() => helper.WaitForExit((int)HelperTimeout.TotalMilliseconds), ct).ConfigureAwait(false);
                // Scaduto il tempo: il helper va chiuso (con i figli), non abbandonato a tenere
                // aperti i dischi in lettura+scrittura.
                if (!exited) { try { helper.Kill(true); } catch { } }
                var file = Path.Combine(dir, SmartHelper.ResultFile);
                if (!exited || !File.Exists(file)) return new SmartSnapshot(disks, false, true);
                return new SmartSnapshot(Merge(disks, DiskReport.FromJson(File.ReadAllText(file))), false, false);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>Fusione pura: per ogni disco non NVMe prende il blocco ATA letto dal helper.
    /// L'abbinamento vuole numero <em>e</em> seriale, perche' il numero di disco e' solo la
    /// posizione del momento: se un disco viene staccato mentre il helper gira, il numero puo'
    /// finire a un altro disco, e attribuire lo SMART al disco sbagliato e' peggio che non
    /// attribuirlo. Quando il seriale locale manca (box che non lo dichiara) resta il numero.</summary>
    public static List<DiskReport> Merge(IReadOnlyList<DiskReport> local, IReadOnlyList<DiskReport> fromHelper) =>
        local.Select(d => d.Bus != DiskBus.Nvme
            && fromHelper.FirstOrDefault(h => h.Number == d.Number
                && (string.IsNullOrEmpty(d.Serial) || h.Serial == d.Serial)) is { } x
            ? d with { AtaBlock = x.AtaBlock, Error = x.Error } : d).ToList();
}
