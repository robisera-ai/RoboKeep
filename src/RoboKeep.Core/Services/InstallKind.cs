namespace RoboKeep.Core.Services;

/// <summary>
/// Riconosce come e' stata installata l'app, per scaricare il pacchetto giusto: la pubblicazione
/// self-contained porta con se' il runtime (coreclr.dll accanto all'eseguibile), quella
/// framework-dependent no. Best-effort: in dubbio, framework-dependent (il pacchetto piccolo).
/// </summary>
public static class InstallKind
{
    public static bool IsSelfContained(string appDir)
    {
        try { return File.Exists(Path.Combine(appDir, "coreclr.dll")); }
        catch { return false; }
    }

    /// <summary>Nome dell'asset di release per la versione e il tipo di installazione (deve
    /// combaciare con quello prodotto da .github/workflows/release.yml).</summary>
    public static string AssetName(Version version, bool selfContained) =>
        $"RoboKeep-{version.Major}.{version.Minor}.{version.Build}-win-x64-{(selfContained ? "selfcontained" : "framework-dependent")}.zip";
}
