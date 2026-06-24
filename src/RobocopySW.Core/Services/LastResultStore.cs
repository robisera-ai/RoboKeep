using System.Text.Json;
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>
/// Persistenza dell'ultimo esito di ciascun job (file JSON), così che la GUI possa
/// mostrarlo anche per i backup eseguiti dall'attività pianificata (processo separato).
/// </summary>
public sealed class LastResultStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    public LastResultStore(string path) => _path = path;

    /// <summary>Carica la mappa job→ultimo esito (vuota se assente o illeggibile).</summary>
    public Dictionary<string, JobLastResult> Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new Dictionary<string, JobLastResult>();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<string, JobLastResult>>(json, Options)
                   ?? new Dictionary<string, JobLastResult>();
        }
        catch
        {
            return new Dictionary<string, JobLastResult>();
        }
    }

    /// <summary>Aggiorna (best-effort) l'ultimo esito del job indicato.</summary>
    public void Update(JobLastResult result)
    {
        try
        {
            var map = Load();
            map[result.JobName] = result;

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_path, JsonSerializer.Serialize(map, Options));
        }
        catch
        {
            // best-effort: un errore di scrittura non deve interrompere il backup.
        }
    }
}
