using System.Text.Json.Serialization;

namespace RoboKeep.Core.Models;

/// <summary>Frequenza della pianificazione per-job.</summary>
public enum ScheduleKind { None, Daily, Weekly, Monthly }

/// <summary>
/// Voce della cronologia esecuzioni: un backup o una verifica di integrità.
/// Per le verifiche i conteggi sono riusati: FilesCopied = verificati,
/// FilesFailed = differenti+mancanti, FilesSkipped = saltati (file in uso).
/// </summary>
public sealed record RunHistoryEntry(
    string JobName,
    string Kind, // "backup" | "verify" | "skipped"
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Success,
    int ExitCode,
    long FilesCopied,
    long FilesSkipped,
    long FilesExtra,
    long FilesFailed,
    long DirsFailed,
    string? LogPath,
    string? Reason = null)
{
    /// <summary>Perche' un job e' stato saltato (<see cref="Reason"/>). Si salva un codice, non un
    /// testo tradotto: la cronologia si rilegge anche dopo aver cambiato lingua. null = il caso
    /// storico, disco atteso non collegato (le cronologie delle versioni precedenti non hanno il
    /// campo e si leggono cosi').</summary>
    public const string ReasonAlreadyRunning = "already-running";
    public const string ReasonDestinationBusy = "destination-busy";

    /// <summary>I valori ammessi per <see cref="Kind"/>. Sono costanti perché la stringa viaggia
    /// dal Core fino alla griglia della cronologia: un refuso in un confronto non verrebbe
    /// segnalato dal compilatore e la voce ricadrebbe in silenzio sul ramo "backup".</summary>
    public const string KindBackup = "backup";
    public const string KindVerify = "verify";
    public const string KindSkipped = "skipped";
    public const string KindCancelled = "cancelled";

    /// <summary>true se il job e' stato annullato dall'utente a meta'. Ha il suo log (quel che
    /// era stato fatto fino a li'), non e' un successo e non e' nemmeno un errore.</summary>
    [JsonIgnore]
    public bool IsCancelled => Kind == KindCancelled;

    /// <summary>Voce per un job annullato dall'utente: Success = false ma <see cref="IsCancelled"/>
    /// permette alla cronologia di scriverlo come "annullato" invece che come errore.</summary>
    /// <summary>La voce di un backup appena chiuso, con i conteggi e il log del suo esito.</summary>
    public static RunHistoryEntry ForBackup(JobResult r, DateTime finishedAt) =>
        new(r.JobName, KindBackup, r.StartedAt, finishedAt, r.Success, r.ExitCode,
            r.FilesCopied, r.FilesSkipped, r.FilesExtra, r.FilesFailed, r.DirsFailed, r.LogPath);

    public static RunHistoryEntry ForCancelled(string jobName, DateTime startedAt, string? logPath) =>
        new(jobName, KindCancelled, startedAt, DateTime.Now, false, 0, 0, 0, 0, 0, 0, logPath);

    /// <summary>true se il job non è stato eseguito (disco atteso non collegato, o il motivo in
    /// <see cref="Reason"/>). Non guardare <see cref="Success"/> per capirlo: una voce saltata ha
    /// Success = true di proposito, per non comparire come errore.</summary>
    [JsonIgnore]
    public bool IsSkipped => Kind == KindSkipped;

    /// <summary>Voce di cronologia per una verifica integrità: mappa i campi conteggio
    /// secondo la convenzione documentata sopra (Copied=verificati, Failed=differenti+mancanti,
    /// Skipped=saltati). Success = nessun file differente.</summary>
    public static RunHistoryEntry ForVerify(
        string jobName, DateTime startedAt, Services.VerifyResult result, string? logPath = null) =>
        new(jobName, KindVerify, startedAt, DateTime.Now,
            result.Mismatched == 0, 0,
            result.Checked, result.Skipped, 0, result.Mismatched + result.Missing, 0, logPath);

    /// <summary>Exit code delle verifiche INTERROTTE (errore hardware o imprevisto): distingue una
    /// verifica non portata a termine da una completata, che ha sempre 0.</summary>
    public const int VerifyInterruptedExitCode = 16;

    /// <summary>Voce di cronologia per una verifica interrotta (errore hardware, o un errore
    /// qualunque come la sorgente sparita): è un fallimento, e NON conta come "ultima verifica
    /// fatta" ai fini della cadenza.</summary>
    public static RunHistoryEntry ForVerifyInterrupted(string jobName, DateTime startedAt, string? logPath) =>
        new(jobName, KindVerify, startedAt, DateTime.Now, false, VerifyInterruptedExitCode, 0, 0, 0, 0, 0, logPath);

    /// <summary>true per una verifica arrivata in fondo (con o senza differenze trovate).</summary>
    [JsonIgnore]
    public bool IsCompletedVerify => Kind == KindVerify && ExitCode == 0;

    /// <summary>Voce di cronologia per un job saltato: disco atteso non collegato (motivo null),
    /// lo stesso job gia' in esecuzione, o la sua destinazione occupata da un altro backup o da un
    /// ripristino. Success = true: saltare non è fallire, e la cronologia non deve mostrare un
    /// errore. Nessun log associato: non è stato eseguito nulla.</summary>
    public static RunHistoryEntry ForSkipped(string jobName, DateTime when, string? reason = null) =>
        new(jobName, KindSkipped, when, when, true, 0, 0, 0, 0, 0, 0, null, reason);
}
