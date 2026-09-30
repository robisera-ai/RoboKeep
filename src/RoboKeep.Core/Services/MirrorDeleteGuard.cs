using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>Stima delle cancellazioni che un mirror farebbe: quanti file spariscono dalla
/// destinazione (<paramref name="Extra"/>) su quanti in tutto (<paramref name="Total"/>), la
/// percentuale che ne risulta e la soglia del job con cui e' stata confrontata.
/// <para><paramref name="Versioned"/>: il job tiene le versioni. I numeri sono gli stessi, ma il
/// racconto cambia: i file non spariscono subito, escono dal backup corrente e restano nella
/// versione di quel backup finche' la ritenzione la conserva.</para></summary>
public sealed record MirrorDeleteEstimate(
    string JobName, string Destination, long Extra, long Total, int Percent, int LimitPercent,
    bool Versioned = false);

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
    public static MirrorDeleteEstimate Estimate(BackupJob job, RobocopyCounts counts)
    {
        ArgumentNullException.ThrowIfNull(job);
        var extra = counts.FilesExtra;
        var total = counts.FilesExtra + counts.FilesSkipped + counts.FilesCopied;
        // Percentuale arrotondata, non troncata: 1812 file su 2014 sono il 90 %, non l'89 %.
        // Con totale 0 (destinazione vuota, primo run) non c'e' nessuna percentuale da dare.
        var percent = total <= 0 ? 0 : (int)Math.Round(extra * 100.0 / total, MidpointRounding.AwayFromZero);
        return new MirrorDeleteEstimate(job.Name, job.Destination, extra, total, percent,
            job.MirrorDeleteLimitPercent, job.Versioned);
    }

    /// <summary>Come <see cref="Estimate(BackupJob, RobocopyCounts)"/>, partendo dall'esito
    /// di un'anteprima gia' eseguita (i conteggi sono gli stessi, letti dal <see cref="JobResult"/>).</summary>
    public static MirrorDeleteEstimate Estimate(BackupJob job, JobResult preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return Estimate(job, new RobocopyCounts(
            preview.DirsCopied, preview.FilesCopied, preview.FilesSkipped, preview.FilesFailed,
            preview.FilesExtra, preview.DirsFailed, preview.DirsExtra));
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
            // Con le versioni i file non vengono cancellati subito: dirlo come una cancellazione
            // sarebbe falso. Stessi numeri, frase diversa.
            DeletionsBlockedDetail = string.Format(
                CoreLoc.S(estimate.Versioned ? "Guard_BlockedVersioned" : "Guard_Blocked"),
                estimate.Extra, estimate.Total, estimate.Percent, estimate.Destination),
            ExitCode = BlockedExitCode,
            Status = CoreLoc.S("Guard_Status"),
            StartedAt = startedAt,
            FilesExtra = estimate.Extra,
        };
    }
}
