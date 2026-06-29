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
    /// <summary>Due file differiscono se hanno dimensione diversa o data di modifica diversa (al secondo).</summary>
    public static bool Differs(long sizeA, DateTime mtimeA, long sizeB, DateTime mtimeB)
        => sizeA != sizeB || Trunc(mtimeA) != Trunc(mtimeB);

    private static DateTime Trunc(DateTime t)
        => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, t.Kind);

    /// <summary>
    /// Per ogni file della sorgente presente anche nel clone, se differisce lo cancella dal clone.
    /// I file del clone assenti in sorgente vengono lasciati (robocopy /MIR li rimuoverà).
    /// </summary>
    public static void UnlinkChanged(string sourceDir, string snapshotDir)
    {
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
