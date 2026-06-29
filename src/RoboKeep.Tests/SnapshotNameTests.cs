using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class SnapshotNameTests
{
    [Fact]
    public void For_FormatsTimestamp()
        => Assert.Equal("2026-06-28_150000", SnapshotName.For(new DateTime(2026, 6, 28, 15, 0, 0)));

    [Fact]
    public void TryParse_ValidName_ReturnsDate()
    {
        Assert.True(SnapshotName.TryParse("2026-06-28_150000", out var d));
        Assert.Equal(new DateTime(2026, 6, 28, 15, 0, 0), d);
    }

    [Fact]
    public void TryParse_Invalid_ReturnsFalse()
        => Assert.False(SnapshotName.TryParse("latest", out _));

    [Fact]
    public void IsInProgress_DetectsSuffix()
    {
        Assert.True(SnapshotName.IsInProgress("2026-06-28_150000.inprogress"));
        Assert.False(SnapshotName.IsInProgress("2026-06-28_150000"));
    }
}
