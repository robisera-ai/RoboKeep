using System.Text.Json;
using System.Text.Json.Serialization;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Esporta/importa la configurazione su file JSON (stesso formato di config.json).
/// Le password restano cifrate DPAPI: importate su un altro PC vanno reinserite (garanzia
/// di Windows, non un difetto). Il backup della config corrente lo fa il chiamante prima
/// di sostituire.
/// </summary>
public static class ConfigTransfer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Export(AppConfig config, string path)
        => File.WriteAllText(path, Serialize(config));

    /// <summary>La configurazione come testo JSON, senza toccare il disco. Serve a chi deve
    /// ottenere il contenuto PRIMA di scrivere (vedi <see cref="ConfigMirror"/>): se la GUI
    /// modifica un job mentre si serializza, l'errore resta in memoria e nessun file viene
    /// lasciato a metà.</summary>
    public static string Serialize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return JsonSerializer.Serialize(config, Options);
    }

    /// <summary>Valida e deserializza; lancia se il file non è una configurazione valida.</summary>
    public static AppConfig Import(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json, Options)
            ?? throw new InvalidDataException("Il file non contiene una configurazione valida.");
    }
}
