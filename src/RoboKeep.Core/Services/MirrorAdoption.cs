namespace RoboKeep.Core.Services;

/// <summary>
/// Riconoscimento usato dall'adozione di una copia semplice: il contenuto sciolto che c'e' nella
/// destinazione e' davvero una copia della sorgente, e quindi si puo' adottare come «current»?
/// Una copia (anche con esclusioni) e' un SOTTOINSIEME della sorgente: ogni file e cartella della
/// destinazione deve esistere nella sorgente allo stesso percorso relativo. Contenuto diverso va
/// bene (e' lo stato precedente, proprio quello da adottare); un percorso che nella sorgente non c'e'
/// no. Basta una voce estranea e non si adotta niente: meglio un primo backup "da zero" che
/// spostare roba altrui.
/// </summary>
public static class MirrorAdoption
{
    /// <summary>
    /// Voci della destinazione che appartengono a RoboKeep e non al backup: le cartelle delle
    /// versioni (<c>current</c>, <c>versions</c>), la copia della configurazione
    /// (<see cref="ConfigMirror.FolderName"/>), i residui di un run interrotto (<c>.inprogress</c>)
    /// o di una cancellazione interrotta (<c>.deleting-…</c>) e i manifest gemelli delle versioni.
    /// Non si adottano e non si contano come "contenuto estraneo": spostare <c>RoboKeep-config</c>
    /// dentro una cartella-versione o dentro <c>current</c> vorrebbe dire darla in pasto al
    /// prossimo <c>/MIR</c>, che la cancellerebbe come file extra.
    /// </summary>
    public static bool IsLayoutEntry(string name) =>
        name.Equals(VersioningLayout.CurrentFolderName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(VersioningLayout.VersionsFolderName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(ConfigMirror.FolderName, StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(VersionManifest.FileSuffix, StringComparison.OrdinalIgnoreCase)
        || SnapshotName.IsInProgress(name)
        || name.Contains(".deleting-", StringComparison.Ordinal);

    /// <summary>Percorsi relativi presenti sotto <paramref name="entries"/> (in destinazione) ma
    /// assenti nella sorgente. Restituisce i primi <paramref name="max"/> per il messaggio e in
    /// <paramref name="count"/> il totale; si ferma presto se ne trova piu' di quanti servono per
    /// decidere (bastano pochi esempi per dire "non e' una copia").</summary>
    public static List<string> ForeignPaths(string dest, string source, IEnumerable<FileSystemInfo> entries,
        int max, out int count)
    {
        var examples = new List<string>();
        count = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var entry in entries)
        {
            var toCheck = entry is DirectoryInfo dir
                ? new[] { dir.FullName }.Concat(Directory.EnumerateFileSystemEntries(dir.FullName, "*", options))
                : new[] { entry.FullName };
            foreach (var path in toCheck)
            {
                var rel = Path.GetRelativePath(dest, path);
                var inSource = Path.Combine(source, rel);
                var isDir = Directory.Exists(path);
                if (isDir ? Directory.Exists(inSource) : File.Exists(inSource)) continue;
                count++;
                if (examples.Count < max) examples.Add(rel);
                if (count >= 1000) return examples; // abbastanza: non e' una copia, inutile contare oltre
            }
        }
        return examples;
    }
}
