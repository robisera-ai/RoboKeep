using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class UpdateStateStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "RbcUpd_" + Guid.NewGuid().ToString("N"), "update-state.json");

    [Fact]
    public void SaveThenLoad_RoundTripsBothFields()
    {
        var path = TempFile();
        try
        {
            var store = new UpdateStateStore(path);
            var when = new DateTime(2026, 9, 23, 12, 30, 0);
            store.Save(new UpdateState(when, "1.8.0"));

            var back = new UpdateStateStore(path).Load();
            Assert.Equal(when, back.LastCheck);
            Assert.Equal("1.8.0", back.IgnoredVersion);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenTheFileIsMissing()
    {
        var state = new UpdateStateStore(TempFile()).Load();
        Assert.Null(state.LastCheck);
        Assert.Null(state.IgnoredVersion);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenTheFileIsCorrupt()
    {
        var path = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ questo non e' json");
            var state = new UpdateStateStore(path).Load();
            Assert.Null(state.LastCheck);
            Assert.Null(state.IgnoredVersion);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Save_CreatesTheFolder_AndOverwritesThePreviousState()
    {
        var path = TempFile();
        try
        {
            var store = new UpdateStateStore(path);
            store.Save(new UpdateState(new DateTime(2026, 1, 1), "1.8.0"));
            store.Save(new UpdateState(new DateTime(2026, 2, 2), null));

            var back = store.Load();
            Assert.Equal(new DateTime(2026, 2, 2), back.LastCheck);
            Assert.Null(back.IgnoredVersion);
        }
        finally { Cleanup(path); }
    }

    private static void Cleanup(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
}
