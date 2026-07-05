using System.Text.Json;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Cronologia delle esecuzioni (backup e verifiche) su file JSON, tetto 500 voci.
/// Best-effort come LastResultStore: un errore di scrittura non deve mai interrompere un run.
/// </summary>
public sealed class RunHistoryStore
{
    private const int MaxEntries = 500;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Attività pianificate PER JOB (v1.4) rendono normale avere più processi RoboKeep
    // concorrenti (es. due job schedulati alla stessa ora): senza mutua esclusione tra
    // processi, l'Append di uno può sovrascrivere quello scritto un istante prima dall'altro
    // (read-modify-write non atomico su file).
    private static readonly Mutex CrossProcess = new(false, @"Global\RoboKeep.RunHistory");

    private readonly string _path;

    public RunHistoryStore(string path) => _path = path;

    /// <summary>Voci in ordine cronologico inverso (più recente prima), eventualmente filtrate per job.</summary>
    public IReadOnlyList<RunHistoryEntry> List(string? jobName = null)
    {
        var acquired = false;
        try
        {
            try { acquired = CrossProcess.WaitOne(TimeSpan.FromSeconds(3)); }
            catch (AbandonedMutexException) { acquired = true; }
            // Sola lettura: se il mutex non si libera in tempo procediamo comunque senza
            // (nel peggiore dei casi una lettura "a metà", già gestita da LoadRaw).

            var all = LoadRaw();
            var filtered = jobName is null ? all : all.Where(e => e.JobName == jobName);
            return filtered.OrderByDescending(e => e.StartedAt).ToList();
        }
        finally
        {
            if (acquired) CrossProcess.ReleaseMutex();
        }
    }

    /// <summary>Aggiunge una voce (best-effort); oltre il tetto le più vecchie escono.</summary>
    public void Append(RunHistoryEntry entry)
    {
        try
        {
            var acquired = false;
            try
            {
                try { acquired = CrossProcess.WaitOne(TimeSpan.FromSeconds(3)); }
                catch (AbandonedMutexException) { acquired = true; }

                var all = LoadRaw();
                all.Add(entry);
                var trimmed = all.OrderByDescending(e => e.StartedAt).Take(MaxEntries).ToList();

                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_path, JsonSerializer.Serialize(trimmed, Options));
            }
            finally
            {
                if (acquired) CrossProcess.ReleaseMutex();
            }
        }
        catch
        {
            // best-effort: la cronologia non deve mai far fallire un backup.
        }
    }

    private List<RunHistoryEntry> LoadRaw()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return JsonSerializer.Deserialize<List<RunHistoryEntry>>(File.ReadAllText(_path), Options) ?? new();
        }
        catch { return new(); }
    }
}
