using System.Text.Json;

namespace RobocopySW.Core.Services;

/// <summary>
/// Persistenza degli hash dei file in modalità "Forza copia smart", per job, in un file JSON.
/// Struttura: { nomeJob: { percorsoFileAssoluto: hashHex } }. Best-effort: errori ignorati.
/// </summary>
public sealed class ForceCopyHashStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public ForceCopyHashStore(string path) => _path = path;

    /// <summary>Hash noti per il job (percorso file → hash). Vuoto se assente/illeggibile.</summary>
    public IReadOnlyDictionary<string, string> Load(string jobName)
    {
        var all = LoadAll();
        return all.TryGetValue(jobName, out var map) ? map : new Dictionary<string, string>();
    }

    /// <summary>Sostituisce gli hash del job indicato (merge con gli altri job).</summary>
    public void Save(string jobName, IReadOnlyDictionary<string, string> hashes)
    {
        try
        {
            var all = LoadAll();
            all[jobName] = new Dictionary<string, string>(hashes);

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_path, JsonSerializer.Serialize(all, Options));
        }
        catch
        {
            // best-effort: un errore di scrittura non deve interrompere il backup.
        }
    }

    private Dictionary<string, Dictionary<string, string>> LoadAll()
    {
        try
        {
            if (!File.Exists(_path))
                return new();
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                       File.ReadAllText(_path)) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
