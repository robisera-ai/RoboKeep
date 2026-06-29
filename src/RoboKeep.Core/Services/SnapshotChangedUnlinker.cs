namespace RoboKeep.Core.Services;

/// <summary>
/// Pre-passata "rompi-cambiati": prima di lanciare robocopy nel nuovo snapshot (clone hard-link
/// del precedente), cancella dal clone i file la cui controparte in sorgente è cambiata
/// (dimensione o data di modifica diverse). Così l'hard-link si rompe e robocopy ricrea quei
/// file ex novo, lasciando intatta la versione precedente. Cancella un sovrainsieme di ciò che
/// robocopy toccherebbe, quindi nessun file ancora condiviso viene modificato sul posto.
/// </summary>
public static class SnapshotChangedUnlinker
{
    /// <summary>Due file differiscono se hanno dimensione diversa o data di modifica diversa.
    /// Confronto esatto dell'mtime: su NTFS robocopy confronta i timestamp a piena precisione,
    /// quindi troncare ai secondi mancherebbe i cambiamenti sub-secondo (rischio di corruzione
    /// dello snapshot precedente). I file immutati hanno mtime identico (robocopy lo preserva),
    /// quindi l'uguaglianza esatta non causa cancellazioni superflue.</summary>
    public static bool Differs(long sizeA, DateTime mtimeA, long sizeB, DateTime mtimeB)
        => sizeA != sizeB || mtimeA != mtimeB;

    /// <summary>
    /// Per ogni file della sorgente presente anche nel clone, se differisce lo cancella dal clone.
    /// I file del clone assenti in sorgente vengono lasciati (robocopy /MIR li rimuoverà).
    /// NB: non applica le esclusioni del job (ExcludeFiles/ExcludeDirs): cancella un sovrainsieme
    /// sicuro (mai meno del necessario, quindi nessuna corruzione). Conseguenza: un file ESCLUSO che
    /// cambia non viene riportato nel nuovo snapshot (robocopy lo salta e qui e' stato scollegato);
    /// la sua versione precedente resta comunque negli snapshot piu' vecchi. Scelta deliberata:
    /// replicare la semantica glob di robocopy qui rischierebbe, se imperfetta, di reintrodurre corruzione.
    /// </summary>
    public static void UnlinkChanged(string sourceDir, string snapshotDir)
    {
        ArgumentNullException.ThrowIfNull(sourceDir);
        ArgumentNullException.ThrowIfNull(snapshotDir);

        if (!Directory.Exists(sourceDir)) return;

        foreach (var srcFile in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, srcFile);
            var snapFile = Path.Combine(snapshotDir, rel);
            if (!File.Exists(snapFile)) continue;

            var s = new FileInfo(srcFile);
            var d = new FileInfo(snapFile);
            if (Differs(s.Length, s.LastWriteTimeUtc, d.Length, d.LastWriteTimeUtc))
                File.Delete(snapFile);
        }
    }
}
