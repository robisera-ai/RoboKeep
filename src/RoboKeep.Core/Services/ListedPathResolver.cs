using System.Text.RegularExpressions;

namespace RoboKeep.Core.Services;

/// <summary>
/// Riporta ai nomi veri del disco i percorsi letti dall'anteprima di robocopy.
/// <para>L'output di robocopy passa per la code page OEM della console: un carattere che la code
/// page non ha (un'emoji, una lettera di un altro alfabeto) torna come <c>?</c>. Quel percorso non
/// esiste e, preso alla lettera, un file da sovrascrivere sembrerebbe "nuovo" (la copia precedente
/// non finirebbe nella versione) e un file "extra" non verrebbe messo da parte prima che il mirror
/// lo cancelli. Qui ogni gruppo di <c>?</c> sta per uno o piu' caratteri qualunque, e si cercano
/// sul disco le voci che corrispondono.</para>
/// <para>Se ne corrispondono piu' d'una non si sa quale robocopy intendesse: si prendono tutte,
/// decidendo per ciascuna dal disco. Mettere da parte un file in piu' costa una copia nella
/// versione (robocopy lo ricopia in <c>current</c>); perderne uno costerebbe il dato.</para>
/// </summary>
public static class ListedPathResolver
{
    /// <param name="sourceRoot">Radice della sorgente passata a robocopy.</param>
    /// <param name="current">La cartella <c>current</c>.</param>
    /// <param name="unresolved">Riceve i percorsi di cui non si e' trovata nessuna voce sul disco:
    /// chi chiama li tiene fuori dal mirror e li segnala.</param>
    public static IReadOnlyList<ListedChange> Resolve(IReadOnlyList<ListedChange> changes,
        string sourceRoot, string current, List<string> unresolved)
    {
        var result = new List<ListedChange>(changes.Count);
        var seen = new HashSet<(ChangeKind, string)>();

        void Emit(ChangeKind kind, string rel)
        {
            if (seen.Add((kind, rel.ToUpperInvariant()))) result.Add(new ListedChange(kind, rel));
        }

        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case ChangeKind.ExtraFile:
                case ChangeKind.ExtraDir:
                {
                    var isDir = change.Kind == ChangeKind.ExtraDir;
                    if (Exists(current, change.RelativePath, isDir)) { Emit(change.Kind, change.RelativePath); break; }
                    // Extra = c'e' in current e NON nella sorgente: e' il disco a dirlo per ogni candidato.
                    var found = Candidates(current, change.RelativePath, isDir)
                        .Where(rel => !Exists(sourceRoot, rel, isDir)).ToList();
                    if (found.Count == 0) unresolved.Add(change.RelativePath);
                    foreach (var rel in found) Emit(change.Kind, rel);
                    break;
                }

                case ChangeKind.NewDir:
                    if (Exists(sourceRoot, change.RelativePath, isDir: true)) { Emit(change.Kind, change.RelativePath); break; }
                    // Niente da mettere da parte per una cartella nuova: basta il nome vero, se c'e'.
                    foreach (var rel in Candidates(sourceRoot, change.RelativePath, isDir: true))
                        Emit(ChangeKind.NewDir, rel);
                    break;

                default:
                {
                    if (Exists(sourceRoot, change.RelativePath, isDir: false)) { Emit(change.Kind, change.RelativePath); break; }
                    var found = Candidates(sourceRoot, change.RelativePath, isDir: false);
                    if (found.Count == 0) unresolved.Add(change.RelativePath);
                    // Nuovo o da sovrascrivere lo decide comunque il disco, piu' avanti.
                    foreach (var rel in found) Emit(ChangeKind.Overwrite, rel);
                    break;
                }
            }
        }
        return result;
    }

    private static bool Exists(string root, string rel, bool isDir)
    {
        var full = Path.Combine(root, rel);
        return isDir ? Directory.Exists(full) : File.Exists(full);
    }

    /// <summary>Le voci reali sotto <paramref name="root"/> che corrispondono a
    /// <paramref name="rel"/>, segmento per segmento. Vuoto se il percorso non contiene <c>?</c> o
    /// se niente corrisponde.</summary>
    public static IReadOnlyList<string> Candidates(string root, string rel, bool isDir)
    {
        if (!rel.Contains('?')) return Array.Empty<string>();
        var segments = rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var partial = new List<string> { "" };
        for (var i = 0; i < segments.Length; i++)
        {
            var last = i == segments.Length - 1;
            var next = new List<string>();
            foreach (var prefix in partial)
            {
                var dir = Path.Combine(root, prefix);
                if (!segments[i].Contains('?'))
                {
                    var literal = Path.Combine(prefix, segments[i]);
                    if (Exists(root, literal, isDir || !last)) next.Add(literal);
                    continue;
                }
                var pattern = PatternFor(segments[i]);
                try
                {
                    var entries = isDir || !last ? Directory.EnumerateDirectories(dir) : Directory.EnumerateFiles(dir);
                    foreach (var entry in entries)
                    {
                        var name = Path.GetFileName(entry);
                        if (pattern.IsMatch(name)) next.Add(Path.Combine(prefix, name));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Cartella illeggibile: nessun candidato da qui.
                }
            }
            partial = next;
            if (partial.Count == 0) break;
        }
        return partial;
    }

    /// <summary>Un gruppo di k <c>?</c> sta per 1..k caratteri veri (contando un'emoji, che sono
    /// due unita' UTF-16, come uno solo): la console puo' averne stampato uno per carattere o uno
    /// per unita'.</summary>
    private static Regex PatternFor(string segment)
    {
        const string codePoint = @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF])";
        var body = Regex.Replace(Regex.Escape(segment), @"(?:\\\?)+",
            m => $"{codePoint}{{1,{m.Length / 2}}}");
        return new Regex("^" + body + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
