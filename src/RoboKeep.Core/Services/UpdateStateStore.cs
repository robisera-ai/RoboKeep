using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>Stato del controllo aggiornamenti.</summary>
/// <param name="LastCheck">Ultimo tentativo eseguito.</param>
/// <param name="LastCheckFailed">true se l'ultimo tentativo non ha portato informazioni (rete
/// assente, timeout, 404): si riprova dopo <see cref="UpdateChecker.RetryInterval"/> invece di
/// aspettare le 24 ore piene, altrimenti un avvio senza rete toglierebbe il controllo per un
/// giorno intero. Un file scritto da una versione precedente contiene <c>ignoredVersion</c> al
/// posto di questo campo: viene ignorato, e la versione messa a tacere torna a essere
/// proposta.</param>
public sealed record UpdateState(DateTime? LastCheck, bool LastCheckFailed);

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

    private static readonly UpdateState Empty = new(null, false);

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

            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(state, Options));
        }
        catch
        {
            // best-effort: al massimo si ricontrolla prima del previsto.
        }
    }
}
