using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class SnapshotChangedUnlinkerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcUnlink_" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    [Fact]
    public void Differs_TrueWhenSizeOrTimeDiffer()
    {
        var t = new DateTime(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(SnapshotChangedUnlinker.Differs(10, t, 10, t));
        Assert.True(SnapshotChangedUnlinker.Differs(10, t, 11, t));
        Assert.True(SnapshotChangedUnlinker.Differs(10, t, 10, t.AddSeconds(5)));
    }

    [Fact]
    public void UnlinkChanged_DeletesOnlyChangedFiles_FromSnapshot()
    {
        var source = Path.Combine(_root, "src");
        var snap = Path.Combine(_root, "snap");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(snap);

        File.WriteAllText(Path.Combine(source, "same.txt"), "AAA");
        File.WriteAllText(Path.Combine(snap, "same.txt"), "AAA");
        var t = new DateTime(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(source, "same.txt"), t);
        File.SetLastWriteTimeUtc(Path.Combine(snap, "same.txt"), t);

        File.WriteAllText(Path.Combine(source, "changed.txt"), "NEWDATA");
        File.WriteAllText(Path.Combine(snap, "changed.txt"), "OLD");

        SnapshotChangedUnlinker.UnlinkChanged(source, snap);

        Assert.True(File.Exists(Path.Combine(snap, "same.txt")));
        Assert.False(File.Exists(Path.Combine(snap, "changed.txt")));
    }
}
