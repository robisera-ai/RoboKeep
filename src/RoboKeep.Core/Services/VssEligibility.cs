namespace RoboKeep.Core.Services;

/// <summary>Controllo IO di idoneità VSS: volume locale con lettera e filesystem NTFS
/// (VSS non fotografa exFAT/FAT32 né percorsi di rete).</summary>
public static class VssEligibility
{
    public static bool IsEligible(string sourcePath)
    {
        var root = VssPathMapper.GetVolumeRoot(sourcePath);
        if (root is null) return false;
        try
        {
            var drive = new DriveInfo(root);
            return drive.IsReady
                && string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
