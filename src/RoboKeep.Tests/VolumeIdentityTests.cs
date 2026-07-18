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

    [Fact]
    public void IsNetworkPath_Unc_True()
        => Assert.True(VolumeIdentity.IsNetworkPath(@"\\server\share\cartella"));

    [Fact]
    public void IsNetworkPath_LocalDrive_False()
        => Assert.False(VolumeIdentity.IsNetworkPath(@"C:\Windows"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsNetworkPath_EmptyOrNull_False(string? path)
        => Assert.False(VolumeIdentity.IsNetworkPath(path));

    // --- CheckDestination: unisce identificazione (impura) e decisione (VolumeGuard, pura) ---

    [Fact]
    public void CheckDestination_UncWithStaleId_NoExpectation()
    {
        // IL test di regressione della blindatura: una share di rete non ha un disco removibile
        // da difendere. Anche con un id di volume rimasto attaccato (import, JSON a mano) NON
        // deve essere saltata: ForPath torna null per la rete come per un disco locale staccato,
        // e senza questa esenzione il backup di rete si fermerebbe in silenzio per sempre.
        var check = VolumeIdentity.CheckDestination(@"\\server\share\backup",
            @"\\?\Volume{deadbeef-0000-0000-0000-000000000000}\");
        Assert.Equal(VolumeCheck.NoExpectation, check);
    }

    [Fact]
    public void CheckDestination_LocalMatchingId_Ok()
    {
        var expected = VolumeIdentity.ForPath(Path.GetTempPath());
        Assert.NotNull(expected); // la temp è su un volume locale identificabile
        Assert.Equal(VolumeCheck.Ok,
            VolumeIdentity.CheckDestination(Path.GetTempPath(), expected!.VolumeId));
    }

    [Fact]
    public void CheckDestination_LocalWrongId_WrongDisk()
        => Assert.Equal(VolumeCheck.WrongDisk, VolumeIdentity.CheckDestination(
            Path.GetTempPath(), @"\\?\Volume{deadbeef-0000-0000-0000-000000000000}\"));

    [Fact]
    public void CheckDestination_NoExpectedId_NoExpectation()
        => Assert.Equal(VolumeCheck.NoExpectation,
            VolumeIdentity.CheckDestination(Path.GetTempPath(), null));

    [Fact]
    public void CheckDestination_LocalAbsentWithId_DiskAbsent()
    {
        // Lettera locale non montata + id atteso: resta DiskAbsent (la rete è un altro caso).
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var free = "ZYXWVU".FirstOrDefault(c => !used.Contains(c));
        if (free == '\0') return;
        Assert.Equal(VolumeCheck.DiskAbsent, VolumeIdentity.CheckDestination(
            $@"{free}:\backup", @"\\?\Volume{deadbeef-0000-0000-0000-000000000000}\"));
    }
}
