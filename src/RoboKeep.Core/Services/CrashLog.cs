namespace RoboKeep.Core.Services;

/// <summary>
/// Registro delle eccezioni non gestite: un file di testo <c>crash.log</c> nella cartella dati,
/// in coda, con data, versione e stack completo. Serve a capire dopo un arresto imprevisto
/// cosa e' successo, senza dover cercare nel registro eventi di Windows.
/// Best-effort: non lancia mai (chi lo chiama sta gia' gestendo un errore).
/// </summary>
public static class CrashLog
{
    public const string FileName = "crash.log";

    /// <summary>Scrive l'eccezione nella cartella dati corrente e restituisce il percorso del file (null se non scrivibile).</summary>
    public static string? Write(Exception ex, string? context = null)
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var portable = File.Exists(Path.Combine(exeDir, AppDataLocator.PortableFlagFileName));
            return Write(AppDataLocator.ResolveDataRoot(exeDir, appData, portable), ex, context);
        }
        catch { return null; }
    }

    /// <summary>Scrive l'eccezione in <paramref name="dataRoot"/>\crash.log e restituisce il percorso (null se non scrivibile).</summary>
    public static string? Write(string dataRoot, Exception ex, string? context = null)
    {
        try
        {
            Directory.CreateDirectory(dataRoot);
            var path = Path.Combine(dataRoot, FileName);
            var version = typeof(CrashLog).Assembly.GetName().Version?.ToString(3) ?? "?";
            var text = $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} — RoboKeep {version}"
                + (context is null ? "" : $" — {context}") + Environment.NewLine
                + ex + Environment.NewLine + Environment.NewLine;
            File.AppendAllText(path, text);
            return path;
        }
        catch { return null; }
    }
}
