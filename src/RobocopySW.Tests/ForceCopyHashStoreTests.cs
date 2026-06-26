using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

public sealed class ForceCopyHashStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcHash_" + Guid.NewGuid().ToString("N"));
    private string Path_ => System.IO.Path.Combine(_dir, "forcecopy-hashes.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenFileMissing()
    {
        Assert.Empty(new ForceCopyHashStore(Path_).Load("J"));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new ForceCopyHashStore(Path_);
        store.Save("J", new Dictionary<string, string> { [@"C:\a.dat"] = "ABC" });

        var loaded = new ForceCopyHashStore(Path_).Load("J");
        Assert.Equal("ABC", loaded[@"C:\a.dat"]);
    }

    [Fact]
    public void Save_IsolatesByJobName()
    {
        var store = new ForceCopyHashStore(Path_);
        store.Save("J1", new Dictionary<string, string> { [@"C:\a"] = "1" });
        store.Save("J2", new Dictionary<string, string> { [@"C:\b"] = "2" });

        var reread = new ForceCopyHashStore(Path_);
        Assert.Equal("1", reread.Load("J1")[@"C:\a"]);
        Assert.Equal("2", reread.Load("J2")[@"C:\b"]);
        Assert.Empty(reread.Load("J1").Where(kv => kv.Key == @"C:\b"));
    }
}
