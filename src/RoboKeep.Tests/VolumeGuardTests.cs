using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VolumeGuardTests
{
    private const string IdA = @"\\?\Volume{aaaaaaaa-1111-2222-3333-444444444444}\";
    private const string IdB = @"\\?\Volume{bbbbbbbb-1111-2222-3333-444444444444}\";

    private static VolumeInfo Info(string id) => new(id, "ETICHETTA");

    [Fact]
    public void NoExpectedId_NoCheck()
        => Assert.Equal(VolumeCheck.NoExpectation, VolumeGuard.Check(null, Info(IdA)));

    [Fact]
    public void EmptyExpectedId_NoCheck()
        => Assert.Equal(VolumeCheck.NoExpectation, VolumeGuard.Check("   ", Info(IdA)));

    [Fact]
    public void CurrentNotDeterminable_NoCheck()
        => Assert.Equal(VolumeCheck.NoExpectation, VolumeGuard.Check(IdA, null));

    [Fact]
    public void SameId_Ok()
        => Assert.Equal(VolumeCheck.Ok, VolumeGuard.Check(IdA, Info(IdA)));

    [Fact]
    public void SameId_DifferentCase_Ok()
        => Assert.Equal(VolumeCheck.Ok, VolumeGuard.Check(IdA.ToUpperInvariant(), Info(IdA.ToLowerInvariant())));

    [Fact]
    public void DifferentId_WrongDisk()
        => Assert.Equal(VolumeCheck.WrongDisk, VolumeGuard.Check(IdA, Info(IdB)));

    [Fact]
    public void SameLabelDifferentId_WrongDisk()
    {
        // Due dischi possono avere la stessa etichetta: decide solo l'identificativo.
        var current = new VolumeInfo(IdB, "ETICHETTA");
        Assert.Equal(VolumeCheck.WrongDisk, VolumeGuard.Check(IdA, current));
    }
}
