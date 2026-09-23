using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>Stato del controllo aggiornamenti.</summary>
/// <param name="LastCheck">Ultimo controllo eseguito: si ricontrolla al massimo una volta ogni 24 ore.</param>
/// <param name="IgnoredVersion">Versione che l'utente ha scelto di ignorare ("1.8.0"): non viene
/// piu' proposta; una versione successiva si'.</param>
public sealed record UpdateState(DateTime? LastCheck, string? IgnoredVersion);

/// <summary>
/// Persistenza dello stato del controllo aggiornamenti in un file suo, accanto a
/// <c>config.json</c>. Sta fuori dalle impostazioni perche' viene scritto in momenti qualunque
/// (all'avvio, dal pulsante "Controlla ora" a finestra Impostazioni aperta): salvare l'intera
/// configurazione in quegli istanti renderebbe definitive modifiche che l'utente non ha ancora
/// confermato. Best-effort come <see cref="LastResultStore"/>: un errore di lettura o scrittura
/// non deve mai arrivare a video.
/// </summary>
public sealed class UpdateStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly UpdateState Empty = new(null, null);

    private readonly string _path;

    public UpdateStateStore(string path) => _path = path;

    /// <summary>Stato salvato; vuoto se il file manca o non e' leggibile.</summary>
    public UpdateState Load()
    {
        try
        {
            if (!File.Exists(_path)) return Empty;
            return JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(_path), Options) ?? Empty;
        }
        catch
        {
            return Empty;
        }
    }

    /// <summary>Sostituisce lo stato salvato (best-effort).</summary>
    public void Save(UpdateState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_path, JsonSerializer.Serialize(state, Options));
        }
        catch
        {
            // best-effort: al massimo si ricontrolla prima del previsto.
        }
    }
}
