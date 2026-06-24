using System.Text.Json;
using System.Text.Json.Serialization;
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>
/// Carica e salva la configurazione applicativa (<see cref="AppConfig"/>) su file JSON.
/// Posizione di default: <c>%ProgramData%\RobocopySW\config.json</c>, così che anche
/// l'attività pianificata (eventualmente altro utente) possa leggerla.
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public ConfigStore(string path) => _path = path;

    /// <summary>Cartella che contiene il file di configurazione (per file affini, es. esiti).</summary>
    public string DirectoryPath => Path.GetDirectoryName(_path) ?? AppContext.BaseDirectory;

    /// <summary>
    /// Percorso di default del file di configurazione: <c>config.json</c> accanto
    /// all'eseguibile (modello "portabile": app, config, log e temp nella stessa cartella).
    /// NB: tenere l'app fuori da <c>C:\Program Files</c>, che è in sola lettura per gli utenti.
    /// </summary>
    public static string DefaultConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    /// <summary>Carica la configurazione; se il file non esiste restituisce una config vuota di default.</summary>
    public AppConfig Load()
    {
        if (!File.Exists(_path))
            return new AppConfig();

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
    }

    /// <summary>Salva la configurazione, creando la cartella se necessario.</summary>
    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(_path, json);
    }
}
