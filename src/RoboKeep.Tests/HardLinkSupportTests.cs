using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class HardLinkSupportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcHls_" + Guid.NewGuid().ToString("N"));

    public HardLinkSupportTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }

    [Fact]
    public void TempDir_OnNtfs_IsSupported()
        => Assert.True(HardLinkSupport.IsSupported(_dir));

    [Fact]
    public void MissingSubdir_WithExistingNtfsParent_IsSupported()
        => Assert.True(HardLinkSupport.IsSupported(Path.Combine(_dir, "not-created-yet")));

    [Fact]
    public void Whitespace_IsNotSupported()
        => Assert.False(HardLinkSupport.IsSupported("   "));

    [Fact]
    public void LeavesNoProbeFiles()
    {
        HardLinkSupport.IsSupported(_dir);
        Assert.Empty(Directory.GetFiles(_dir));
    }
}
