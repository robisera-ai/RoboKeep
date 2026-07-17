using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VolumeIdentityTests
{
    [Fact]
    public void ForPath_SystemDrive_ReturnsVolumeIdAndLabel()
    {
        var info = VolumeIdentity.ForPath(@"C:\");
        Assert.NotNull(info);
        Assert.StartsWith(@"\\?\Volume{", info!.VolumeId);
        Assert.EndsWith(@"\", info.VolumeId);
        Assert.NotNull(info.Label); // può essere stringa vuota: un volume senza etichetta è legittimo
    }

    [Fact]
    public void ForPath_SubfolderOfSameVolume_ReturnsSameId()
    {
        var root = VolumeIdentity.ForPath(@"C:\");
        var sub = VolumeIdentity.ForPath(@"C:\Windows");
        Assert.NotNull(root);
        Assert.NotNull(sub);
        Assert.Equal(root!.VolumeId, sub!.VolumeId);
    }

    [Fact]
    public void ForPath_UncPath_ReturnsNull()
        => Assert.Null(VolumeIdentity.ForPath(@"\\server\share\cartella"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForPath_EmptyOrNull_ReturnsNull(string? path)
        => Assert.Null(VolumeIdentity.ForPath(path));

    [Fact]
    public void ForPath_UnmountedDriveLetter_ReturnsNull()
    {
        // Cerca una lettera libera partendo dal fondo: se non ce ne sono (improbabile), il
        // test non ha nulla da verificare e passa.
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var free = "ZYXWVU".FirstOrDefault(c => !used.Contains(c));
        if (free == '\0') return;
        Assert.Null(VolumeIdentity.ForPath($@"{free}:\qualsiasi"));
    }
}
