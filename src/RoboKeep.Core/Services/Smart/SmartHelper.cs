namespace RoboKeep.Core.Services.Smart;

/// <summary>Corpo della modalita' <c>--smart-helper &lt;dir&gt;</c> (processo elevato): elenca i
/// dischi, legge lo SMART ATA di quelli non NVMe, scrive smart.json e termina. Nessuna UI.</summary>
public static class SmartHelper
{
    public const string ResultFile = "smart.json";

    public static int Run(string dir)
    {
        try
        {
            // Una sola lettura ATA per disco: il pass-through e' un comando vero sul dispositivo,
            // non va ripetuto solo per decidere l'errore.
            var reports = SmartReader.ListPhysicalDisks().Select(d =>
            {
                if (d.Bus == DiskBus.Nvme) return d;
                var block = SmartReader.ReadAta(d.Number);
                return d with { AtaBlock = block, Error = block is null ? "ata" : null };
            }).ToList();
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, ResultFile), DiskReport.ToJson(reports));
            return 0;
        }
        catch { return 1; }
    }
}
