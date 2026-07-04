namespace RoboKeep.Core.Services;

/// <summary>
/// Mappatura percorsi per VSS (funzioni pure): estrae la radice del volume dalla sorgente
/// e riscrive il percorso del job dentro lo snapshot esposto dal symlink di sessione.
/// </summary>
public static class VssPathMapper
{
    /// <summary>Radice del volume (es. <c>C:\</c>) se il percorso è assoluto con lettera di
    /// unità; null per UNC, percorsi relativi o vuoti (VSS non applicabile).</summary>
    public static string? GetVolumeRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.Length < 3) return null;
        if (!char.IsAsciiLetter(path[0]) || path[1] != ':' || (path[2] != '\\' && path[2] != '/'))
            return null;
        return char.ToUpperInvariant(path[0]) + @":\";
    }

    /// <summary>Riscrive <paramref name="sourcePath"/> (es. <c>C:\Users\x</c>) sotto la radice
    /// dello snapshot (es. <c>D:\sess\source</c> → <c>D:\sess\source\Users\x</c>).</summary>
    public static string MapToSnapshot(string sourcePath, string snapshotRoot)
    {
        var volume = GetVolumeRoot(sourcePath)
            ?? throw new ArgumentException($"Percorso non idoneo a VSS: {sourcePath}", nameof(sourcePath));
        var relative = sourcePath[volume.Length..].TrimEnd('\\', '/');
        return relative.Length == 0 ? snapshotRoot : Path.Combine(snapshotRoot, relative);
    }
}
