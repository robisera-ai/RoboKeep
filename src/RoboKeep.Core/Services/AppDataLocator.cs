namespace RoboKeep.Core.Services;

/// <summary>
/// Risolve la cartella dati di RoboKeep (config, esiti, hash, log/temp di default).
/// Default: %APPDATA%\RoboKeep (per-utente, stabile). Modalità portatile se accanto
/// all'eseguibile esiste un file "portable.flag" (allora i dati stanno lì).
/// </summary>
public static class AppDataLocator
{
    /// <summary>Nome del file marcatore che attiva la modalità portatile (accanto all'eseguibile).</summary>
    public const string PortableFlagFileName = "portable.flag";

    /// <summary>Nome della sottocartella in %APPDATA% che contiene i dati di RoboKeep.</summary>
    public const string FolderName = "RoboKeep";

    /// <summary>Restituisce la cartella dati: <paramref name="exeDir"/> se il flag è presente,
    /// altrimenti <c>%APPDATA%\RoboKeep</c>.</summary>
    public static string ResolveDataRoot(string exeDir, string appDataDir, bool portableFlagPresent)
        => portableFlagPresent ? exeDir : Path.Combine(appDataDir, FolderName);

    /// <summary>
    /// Risolve e prepara la data root reale: crea la cartella e, in modalità %APPDATA%,
    /// migra (best-effort) config.json e lastresults.json dalla cartella dell'eseguibile
    /// se non sono ancora presenti nella data root. Restituisce il percorso della data root.
    /// </summary>
    public static string PrepareDataRoot()
    {
        var exeDir = AppContext.BaseDirectory;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var portable = File.Exists(Path.Combine(exeDir, PortableFlagFileName));
        var root = ResolveDataRoot(exeDir, appData, portable);

        Directory.CreateDirectory(root);

        if (!portable)
        {
            MigrateFile(exeDir, root, "config.json");
            MigrateFile(exeDir, root, "lastresults.json");
        }
        return root;
    }

    private static void MigrateFile(string fromDir, string toDir, string fileName)
    {
        try
        {
            var src = Path.Combine(fromDir, fileName);
            var dst = Path.Combine(toDir, fileName);
            if (File.Exists(src) && !File.Exists(dst))
                File.Copy(src, dst);
        }
        catch
        {
            // best-effort: la migrazione non deve impedire l'avvio.
        }
    }
}
