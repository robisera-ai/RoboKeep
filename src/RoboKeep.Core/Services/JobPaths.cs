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

    private static string Normalize(string path) =>
        Path.GetFullPath(path.Trim()).TrimEnd('\\', '/');
}
