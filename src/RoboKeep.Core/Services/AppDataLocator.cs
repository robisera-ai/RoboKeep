namespace RoboKeep.Core.Services;

/// <summary>
/// Risolve la cartella dati di RoboKeep (config, esiti, hash, log/temp di default).
/// Default: %APPDATA%\RoboKeep (per-utente, stabile). Modalità portatile se accanto
/// all'eseguibile esiste un file "portable.flag" (allora i dati stanno lì).
/// </summary>
public static class AppDataLocator
{
    public const string PortableFlagFileName = "portable.flag";
    public const string FolderName = "RoboKeep";

    /// <summary>Parte pura e testabile: data root scelta in base alla presenza del flag.</summary>
    public static string ResolveDataRoot(string exeDir, string appDataDir, bool portableFlagPresent)
        => portableFlagPresent ? exeDir : Path.Combine(appDataDir, FolderName);
}
