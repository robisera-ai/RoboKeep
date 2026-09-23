using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public sealed class InstallKindTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcKind_" + Guid.NewGuid().ToString("N"));
    public InstallKindTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void SelfContained_WhenCoreclrIsNextToTheExe()
    {
        Assert.False(InstallKind.IsSelfContained(_dir));
        File.WriteAllText(Path.Combine(_dir, "coreclr.dll"), "");
        Assert.True(InstallKind.IsSelfContained(_dir));
    }

    [Fact]
    public void MissingFolder_IsNotSelfContained()
        => Assert.False(InstallKind.IsSelfContained(Path.Combine(_dir, "non-esiste")));

    [Fact]
    public void AssetName_MatchesTheInstall()
    {
        Assert.Equal("RoboKeep-1.8.0-win-x64-selfcontained.zip", InstallKind.AssetName(new Version(1, 8, 0), selfContained: true));
        Assert.Equal("RoboKeep-1.8.0-win-x64-framework-dependent.zip", InstallKind.AssetName(new Version(1, 8, 0), selfContained: false));
    }
}
