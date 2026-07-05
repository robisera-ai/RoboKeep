using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class SnapshotNameListTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public SnapshotNameListTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void ListValid_OrdersByParsedDate_NewestFirst_IgnoresInProgressAndForeign()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "2026-07-01_100000"));
        Directory.CreateDirectory(Path.Combine(_dir, "2026-07-03_090000"));
        Directory.CreateDirectory(Path.Combine(_dir, "2026-07-02_100000.inprogress"));
        Directory.CreateDirectory(Path.Combine(_dir, "cartella-estranea"));
        var list = SnapshotName.ListValid(_dir);
        Assert.Equal(new[] { "2026-07-03_090000", "2026-07-01_100000" }, list);
    }

    [Fact]
    public void Latest_MissingDir_ReturnsNull()
        => Assert.Null(SnapshotName.Latest(Path.Combine(_dir, "non-esiste")));
}
