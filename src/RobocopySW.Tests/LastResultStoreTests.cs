using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

public class LastResultStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public LastResultStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RbcLast_" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "lastresults.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_Missing_ReturnsEmpty()
    {
        Assert.Empty(new LastResultStore(_path).Load());
    }

    [Fact]
    public void UpdateThenLoad_RoundTrips()
    {
        var store = new LastResultStore(_path);
        store.Update(new JobLastResult
        {
            JobName = "Documenti",
            Success = true,
            ExitCode = 1,
            FilesCopied = 342,
            FilesSkipped = 5120,
            FilesExtra = 7,
            FilesFailed = 0,
            FinishedAt = new DateTime(2026, 6, 24, 22, 0, 0),
        });

        var map = store.Load();
        Assert.True(map.ContainsKey("Documenti"));
        var r = map["Documenti"];
        Assert.True(r.Success);
        Assert.Equal(342, r.FilesCopied);
        Assert.Equal(5120, r.FilesSkipped);
        Assert.Equal(new DateTime(2026, 6, 24, 22, 0, 0), r.FinishedAt);
    }

    [Fact]
    public void Update_OverwritesSameJob_KeepsOthers()
    {
        var store = new LastResultStore(_path);
        store.Update(new JobLastResult { JobName = "A", FilesCopied = 1 });
        store.Update(new JobLastResult { JobName = "B", FilesCopied = 2 });
        store.Update(new JobLastResult { JobName = "A", FilesCopied = 99 });

        var map = store.Load();
        Assert.Equal(2, map.Count);
        Assert.Equal(99, map["A"].FilesCopied);
        Assert.Equal(2, map["B"].FilesCopied);
    }
}
