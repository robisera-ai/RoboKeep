using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VssSessionProtocolTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public VssSessionProtocolTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Paths_AreInsideSessionDir()
    {
        Assert.Equal(Path.Combine(_dir, "request.json"), VssSessionProtocol.RequestFile(_dir));
        Assert.Equal(Path.Combine(_dir, "ready.json"), VssSessionProtocol.ReadyFile(_dir));
        Assert.Equal(Path.Combine(_dir, "release.flag"), VssSessionProtocol.ReleaseFile(_dir));
        Assert.Equal(Path.Combine(_dir, "source"), VssSessionProtocol.LinkDir(_dir));
    }

    [Fact]
    public void Request_RoundTrips()
    {
        var req = new VssRequest(@"C:\", 1234, new List<string> { "{ID-1}" });
        VssSessionProtocol.WriteRequest(_dir, req);
        var back = VssSessionProtocol.ReadRequest(_dir);
        Assert.Equal(@"C:\", back.Volume);
        Assert.Equal(1234, back.ParentPid);
        Assert.Equal(new[] { "{ID-1}" }, back.StaleShadowIds);
    }

    [Fact]
    public void Ready_Success_RoundTrips()
    {
        VssSessionProtocol.WriteReady(_dir, VssReady.Ok("{SHADOW-ID}", @"D:\sess\source"));
        var back = VssSessionProtocol.ReadReady(_dir)!;
        Assert.True(back.Success);
        Assert.Equal("{SHADOW-ID}", back.ShadowId);
        Assert.Equal(@"D:\sess\source", back.LinkPath);
        Assert.Null(back.Error);
    }

    [Fact]
    public void Ready_Failure_RoundTrips()
    {
        VssSessionProtocol.WriteReady(_dir, VssReady.Fail("WMI 0x80042308"));
        var back = VssSessionProtocol.ReadReady(_dir)!;
        Assert.False(back.Success);
        Assert.Equal("WMI 0x80042308", back.Error);
    }

    [Fact]
    public void ReadReady_MissingFile_ReturnsNull()
        => Assert.Null(VssSessionProtocol.ReadReady(_dir));
}
