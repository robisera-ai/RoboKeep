using System.IO;

namespace RoboKeep.Guide;

/// <summary>Un capitolo della guida: titolo (dalla prima riga <c># ...</c>) e file sorgente.</summary>
public sealed record GuideChapter(string Title, string FilePath);

/// <summary>
/// Accede ai capitoli della guida, file Markdown copiati accanto all'eseguibile in
/// <c>Guide/&lt;lingua&gt;/</c>. La guida esiste in italiano e inglese; per le altre lingue
/// dell'app (es/fr/de) ripiega sull'inglese.
/// </summary>
public static class GuideLibrary
{
    private static string Root => Path.Combine(AppContext.BaseDirectory, "Guide");

    /// <summary>Lingua effettiva della guida per la lingua dell'app (ripiego su "en").</summary>
    public static string ResolveLanguage(string lang)
    {
        var dir = Path.Combine(Root, lang);
        return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.md").Any() ? lang : "en";
    }

    /// <summary>Capitoli della lingua indicata, ordinati per nome file (01-, 02-, ...).</summary>
    public static IReadOnlyList<GuideChapter> Chapters(string lang)
    {
        var dir = Path.Combine(Root, ResolveLanguage(lang));
        if (!Directory.Exists(dir)) return System.Array.Empty<GuideChapter>();

        return Directory.EnumerateFiles(dir, "*.md")
            .OrderBy(p => Path.GetFileName(p), System.StringComparer.OrdinalIgnoreCase)
            .Select(p => new GuideChapter(TitleOf(p), p))
            .ToList();
    }

    /// <summary>Contenuto grezzo di un capitolo (stringa vuota se illeggibile: mai un'eccezione).</summary>
    public static string Read(string filePath)
    {
        try { return File.ReadAllText(filePath); } catch { return ""; }
    }

    private static string TitleOf(string filePath)
    {
        try
        {
            foreach (var line in File.ReadLines(filePath))
                if (line.StartsWith("# ", System.StringComparison.Ordinal))
                    return line[2..].Trim();
        }
        catch { /* titolo di ripiego sotto */ }
        return Path.GetFileNameWithoutExtension(filePath);
    }
}
