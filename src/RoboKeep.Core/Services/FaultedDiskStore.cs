using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>Un disco messo a riposo dopo un errore hardware.</summary>
/// <param name="VolumeId">Identita' del volume (<c>\\?\Volume{GUID}\</c>): non la lettera, che cambia.</param>
/// <param name="Label">Etichetta, solo per i messaggi.</param>
/// <param name="Root">Radice al momento dell'errore (es. <c>E:\</c>), solo per i messaggi.</param>
/// <param name="Since">Quando.</param>
/// <param name="Detail">Codice e percorso dell'errore.</param>
/// <param name="Notified">true se l'email di questo episodio e' partita davvero: una mail per
/// episodio, non una per job per notte. Nasce false — con l'email spenta o rotta nessuno e' stato
/// avvisato — e diventa true solo a invio riuscito.</param>
public sealed record FaultedDisk(string VolumeId, string Label, string Root, DateTime Since, string Detail, bool Notified = false);

/// <summary>
/// Dischi a riposo, persistiti tra le sessioni: dopo un errore hardware il disco non va toccato
/// finche' qualcuno non l'ha controllato — nemmeno dall'attivita' pianificata della notte dopo,
/// che gira in un altro processo. Identificati per volume, cosi' la lettera che cambia non
/// libera un disco guasto ne' blocca uno sano. Scadono da soli dopo <see cref="ExpiryDays"/>
/// giorni: un blocco dimenticato non deve fermare i backup per sempre. Best-effort come
/// <see cref="LastResultStore"/>: un errore di lettura o scrittura non ferma mai un backup.
/// </summary>
public sealed class FaultedDiskStore
{
    public const int ExpiryDays = 7;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    // Piu' processi RoboKeep possono girare insieme (attivita' pianificate per job): il
    // read-modify-write sul file va serializzato, come in RunHistoryStore.
    private static readonly Mutex CrossProcess = new(false, @"Global\RoboKeep.FaultedDisks");
    private readonly string _path;

    public FaultedDiskStore(string path) => _path = path;

    /// <summary>Dischi ancora a riposo (quelli scaduti vengono scartati).</summary>
    public IReadOnlyList<FaultedDisk> Load(DateTime now) =>
        LoadRaw().Where(d => (now - d.Since).TotalDays < ExpiryDays).ToList();

    public FaultedDisk? Find(string? volumeId, DateTime now) =>
        string.IsNullOrEmpty(volumeId) ? null
            : Load(now).FirstOrDefault(d => string.Equals(d.VolumeId, volumeId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Mette a riposo un disco (sostituisce una voce precedente dello stesso volume).</summary>
    public void Mark(FaultedDisk disk) => Locked(() =>
    {
        var all = LoadRaw().Where(d => !SameVolume(d, disk.VolumeId)).ToList();
        all.Add(disk);
        Save(all);
    });

    /// <summary>Segna che l'utente e' stato avvisato per questo episodio.</summary>
    public void MarkNotified(string volumeId) => Locked(() =>
    {
        var all = LoadRaw();
        var i = all.FindIndex(d => SameVolume(d, volumeId));
        if (i < 0) return;
        all[i] = all[i] with { Notified = true };
        Save(all);
    });

    /// <summary>Riabilita tutti i dischi: l'utente dichiara di averli controllati.</summary>
    public void Clear() => Locked(() => Save(new List<FaultedDisk>()));

    private static bool SameVolume(FaultedDisk d, string volumeId) =>
        string.Equals(d.VolumeId, volumeId, StringComparison.OrdinalIgnoreCase);

    private void Locked(Action action)
    {
        try
        {
            var acquired = false;
            try
            {
                try { acquired = CrossProcess.WaitOne(TimeSpan.FromSeconds(3)); }
                catch (AbandonedMutexException) { acquired = true; }
                action();
            }
            finally { if (acquired) CrossProcess.ReleaseMutex(); }
        }
        catch { /* best-effort: lo stato dei dischi non deve mai far fallire un backup */ }
    }

    private void Save(List<FaultedDisk> all)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(all, Options));
    }

    private List<FaultedDisk> LoadRaw()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return JsonSerializer.Deserialize<List<FaultedDisk>>(File.ReadAllText(_path), Options) ?? new();
        }
        catch { return new(); }
    }
}
