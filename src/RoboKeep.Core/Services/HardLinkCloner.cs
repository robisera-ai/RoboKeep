namespace RoboKeep.Core.Services;

/// <summary>
/// Clona ricorsivamente una cartella: ricrea le sottocartelle come reali e collega i file
/// con hard-link (zero copia di dati). Sorgente e destinazione devono stare sullo stesso volume NTFS.
/// </summary>
public static class HardLinkCloner
{
    public static void Clone(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destDir, rel));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            HardLink.Create(target, file);
        }
    }
}
