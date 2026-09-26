using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>Stima delle cancellazioni che un mirror farebbe: quanti file spariscono dalla
/// destinazione (<paramref name="Extra"/>) su quanti in tutto (<paramref name="Total"/>), la
/// percentuale che ne risulta e la soglia del job con cui e' stata confrontata.
/// <para><paramref name="PreviousSnapshot"/> e' valorizzato solo per i job con versioni, dove il
/// confronto e' con l'ultima versione e non con la destinazione: la stima e' la stessa, ma il
/// racconto cambia — nessun file esistente viene cancellato, la versione nuova ne avrebbe di
/// meno.</para></summary>
public sealed record MirrorDeleteEstimate(
    string JobName, string Destination, long Extra, long Total, int Percent, int LimitPercent,
    string? PreviousSnapshot = null);

/// <summary>
/// Guardia sulle cancellazioni del mirror: decide se un mirror sta per cancellare cosi' tanto da
/// non essere credibile. Una sorgente svuotata per errore (cartella spostata, unita' di rete non
/// montata che appare vuota, ransomware) si presenta sempre allo stesso modo — quasi tutti i file
/// della destinazione diventano "extra", cioe' da rimuovere — e senza un freno il mirror
/// eseguirebbe la cancellazione senza fiatare.
/// <para>Regola pura, tutta qui: i conteggi arrivano da un'anteprima <c>/L</c> fatta da chi
/// chiama, e questa classe non tocca ne' dischi ne' processi.</para>
/// </summary>
public static class MirrorDeleteGuard
{
    /// <summary>Sotto questo numero di file la guardia non scatta nemmeno al 100 %: una cartella
    /// con pochi file cambia di natura in un pomeriggio, e fermare un backup per tre file
    /// cancellati sarebbe solo un fastidio che insegna a ignorare gli avvisi.</summary>
    public const int MinFiles = 20;

    /// <summary>Exit code registrato per un job fermato dalla guardia: il bit "file falliti" (8),
    /// cosi' anche chi guarda solo il codice — un'attivita' pianificata, uno script — vede un
    /// fallimento e non un successo.</summary>
    public const int BlockedExitCode = 8;

    /// <summary>Stima a partire dai conteggi di un'anteprima: <c>extra</c> sono i file presenti in
    /// destinazione e assenti in sorgente (quelli che il mirror cancellerebbe), il totale e' quello
    /// che la destinazione ha o avra' (extra + invariati + copiati).</summary>
    /// <param name="previousSnapshot">Nome dell'ultima versione, se il confronto e' contro uno
    /// snapshot invece che contro la destinazione (job con versioni).</param>
    public static MirrorDeleteEstimate Estimate(BackupJob job, RobocopyCounts counts, string? previousSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        var extra = counts.FilesExtra;
        var total = counts.FilesExtra + counts.FilesSkipped + counts.FilesCopied;
        // Percentuale arrotondata, non troncata: 1812 file su 2014 sono il 90 %, non l'89 %.
        // Con totale 0 (destinazione vuota, primo run) non c'e' nessuna percentuale da dare.
        var percent = total <= 0 ? 0 : (int)Math.Round(extra * 100.0 / total, MidpointRounding.AwayFromZero);
        return new MirrorDeleteEstimate(job.Name, job.Destination, extra, total, percent,
            job.MirrorDeleteLimitPercent, previousSnapshot);
    }

    /// <summary>Come <see cref="Estimate(BackupJob, RobocopyCounts, string?)"/>, partendo dall'esito
    /// di un'anteprima gia' eseguita (i conteggi sono gli stessi, letti dal <see cref="JobResult"/>).</summary>
    public static MirrorDeleteEstimate Estimate(BackupJob job, JobResult preview, string? previousSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return Estimate(job, new RobocopyCounts(
            preview.DirsCopied, preview.FilesCopied, preview.FilesSkipped, preview.FilesFailed,
            preview.FilesExtra, preview.DirsFailed, preview.DirsExtra), previousSnapshot);
    }

    /// <summary>true se il run non deve partire senza una conferma: soglia attiva, abbastanza file
    /// in gioco e percentuale oltre la soglia.</summary>
    public static bool ShouldBlock(MirrorDeleteEstimate estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        return estimate.LimitPercent > 0
            && estimate.Extra >= MinFiles
            && estimate.Percent >= estimate.LimitPercent;
    }

    /// <summary>Esito di un job fermato dalla guardia: non e' partito (<c>NotStarted</c>), non e'
    /// un successo, e il dettaglio dice i numeri e come sbloccarlo.</summary>
    public static JobResult BlockedResult(MirrorDeleteEstimate estimate, DateTime startedAt)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        return new JobResult
        {
            JobName = estimate.JobName,
            Success = false,
            NotStarted = true,
            DeletionsBlocked = true,
            // Con le versioni nessun file esistente viene cancellato — l'ultima versione resta
            // intatta — e dirlo come una cancellazione sarebbe una bugia che spaventa: la frase
            // cambia, i numeri sono gli stessi.
            DeletionsBlockedDetail = estimate.PreviousSnapshot is { } previous
                ? string.Format(CoreLoc.S("Guard_BlockedVersioned"),
                    estimate.Extra, estimate.Total, estimate.Percent, previous)
                : string.Format(CoreLoc.S("Guard_Blocked"),
                    estimate.Extra, estimate.Total, estimate.Percent, estimate.Destination),
            ExitCode = BlockedExitCode,
            Status = CoreLoc.S("Guard_Status"),
            StartedAt = startedAt,
            FilesExtra = estimate.Extra,
        };
    }
}
