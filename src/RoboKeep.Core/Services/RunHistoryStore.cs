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

    private readonly string _path;

    public RunHistoryStore(string path) => _path = path;

    /// <summary>Voci in ordine cronologico inverso (più recente prima), eventualmente filtrate per job.</summary>
    public IReadOnlyList<RunHistoryEntry> List(string? jobName = null)
    {
        var all = LoadRaw();
        var filtered = jobName is null ? all : all.Where(e => e.JobName == jobName);
        return filtered.OrderByDescending(e => e.StartedAt).ToList();
    }

    /// <summary>Aggiunge una voce (best-effort); oltre il tetto le più vecchie escono.</summary>
    public void Append(RunHistoryEntry entry)
    {
        try
        {
            var all = LoadRaw();
            all.Add(entry);
            var trimmed = all.OrderByDescending(e => e.StartedAt).Take(MaxEntries).ToList();

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(trimmed, Options));
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
