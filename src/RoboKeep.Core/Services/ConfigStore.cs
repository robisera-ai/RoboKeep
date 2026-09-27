using System.Text.Json;
using System.Text.Json.Serialization;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Carica e salva la configurazione applicativa (<see cref="AppConfig"/>) su file JSON.
/// Il percorso effettivo è determinato da AppDataLocator: <c>%APPDATA%\RoboKeep\config.json</c>
/// in modalità installata, oppure <c>config.json</c> accanto all'eseguibile in modalità
/// portabile (quando la cartella dell'app risulta scrivibile).
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

    /// <summary>Carica la configurazione; se il file non esiste restituisce una config vuota di default.</summary>
    /// <remarks>
    /// A differenza degli altri store (best-effort, es. esiti/ledger) qui un JSON corrotto
    /// DEVE propagare l'eccezione invece di essere silenziosamente sostituito da una config
    /// vuota: se degradassimo a <c>new AppConfig()</c>, il primo Save successivo scriverebbe
    /// una configurazione senza job, cancellando di fatto tutti i job dell'utente senza
    /// alcun avviso. Meglio un crash visibile che una perdita silenziosa di dati.
    /// </remarks>
    public AppConfig Load()
    {
        if (!File.Exists(_path))
            return Migrate(new AppConfig());

        var json = File.ReadAllText(_path);
        return Migrate(JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig());
    }

    /// <summary>
    /// Adegua una configurazione appena caricata ai cambi di default delle versioni nuove. Un
    /// default che cambia non raggiunge chi ha già un file: il valore vecchio e' scritto dentro, e
    /// vince sempre. Qui si distingue "l'ho scelto io" da "era il default di allora", e si tocca
    /// solo il secondo caso, una volta sola (il segno resta nella configurazione).
    /// </summary>
    private static AppConfig Migrate(AppConfig config)
    {
        var s = config.Settings;
        if (!s.MinFreeSpaceMigrated)
        {
            // 1024 MB era il default fino alla 1.8.2: chi l'ha lasciato com'era passa a 10 GB, la
            // soglia che serve anche alla pulizia per spazio. Un valore scelto dall'utente - anche
            // 1024 rimesso a mano dopo questa migrazione - non si tocca piu'.
            if (s.MinFreeSpaceMb == OldMinFreeSpaceDefaultMb)
                s.MinFreeSpaceMb = AppSettings.DefaultMinFreeSpaceMb;
            s.MinFreeSpaceMigrated = true;
        }
        return config;
    }

    /// <summary>Vecchio default di <c>MinFreeSpaceMb</c> (fino alla 1.8.2).</summary>
    private const int OldMinFreeSpaceDefaultMb = 1024;

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
