namespace RoboKeep.Core.Services;

/// <summary>
/// Regole sui percorsi di un job. Sorgente e destinazione non possono essere la stessa cartella
/// ne' stare una dentro l'altra: un mirror con la destinazione dentro la sorgente copierebbe il
/// backup dentro se stesso a ogni run, e con la sorgente dentro la destinazione cancellerebbe
/// tutto cio' che nella destinazione non e' la sorgente — cioe' quasi tutto.
/// </summary>
public static class JobPaths
{
    /// <summary>true se i due percorsi coincidono o uno contiene l'altro. Confronto per segmenti
    /// (<c>D:\dati-vecchi</c> non e' dentro <c>D:\dati</c>), senza distinguere maiuscole.
    /// Percorsi vuoti o non interpretabili: false (li bocciano altri controlli).</summary>
    public static bool Overlap(string? source, string? destination)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination)) return false;
        try
        {
            var s = Normalize(source);
            var d = Normalize(destination);
            return s.Equals(d, StringComparison.OrdinalIgnoreCase)
                || d.StartsWith(s + '\\', StringComparison.OrdinalIgnoreCase)
                || s.StartsWith(d + '\\', StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>true se le destinazioni di due job coincidono (o stanno una dentro l'altra) SULLO
    /// STESSO DISCO: due mirror cosi' si cancellerebbero i file a vicenda. Con la rotazione dei dischi
    /// la stessa cartella su due dischi diversi non e' un conflitto: ogni job e' legato al suo disco
    /// (<see cref="Models.BackupJob.DestinationVolumeId"/>) e quello del disco assente viene saltato.
    /// Un job non legato a nessun disco (cartella di rete, job di una versione vecchia) scrive su
    /// qualunque disco abbia quella lettera: per prudenza conta come lo stesso.</summary>
    public static bool DestinationsClash(string? destinationA, string? volumeA, string? destinationB, string? volumeB)
    {
        if (!Overlap(destinationA, destinationB)) return false;
        if (string.IsNullOrEmpty(volumeA) || string.IsNullOrEmpty(volumeB)) return true;
        return string.Equals(volumeA, volumeB, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Le coppie di job in conflitto (vedi <see cref="DestinationsClash"/>), ognuna una volta
    /// sola: il runner le rifiuta, e chi importa una configurazione deve saperlo.</summary>
    public static IReadOnlyList<(Models.BackupJob First, Models.BackupJob Second)> DestinationOverlaps(
        IReadOnlyList<Models.BackupJob> jobs)
    {
        var pairs = new List<(Models.BackupJob, Models.BackupJob)>();
        for (var i = 0; i < jobs.Count; i++)
            for (var j = i + 1; j < jobs.Count; j++)
                if (DestinationsClash(jobs[i].Destination, jobs[i].DestinationVolumeId,
                        jobs[j].Destination, jobs[j].DestinationVolumeId))
                    pairs.Add((jobs[i], jobs[j]));
        return pairs;
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path.Trim()).TrimEnd('\\', '/');
}
