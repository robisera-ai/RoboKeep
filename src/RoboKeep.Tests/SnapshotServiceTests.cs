using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class SnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcSnap_" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private static string[] SnapshotDirs(string dest) =>
        Directory.GetDirectories(dest).Select(Path.GetFileName)
            .Where(n => n is not null && !SnapshotName.IsInProgress(n!) && SnapshotName.TryParse(n!, out _))
            .Cast<string>().OrderBy(n => n).ToArray();

    [Fact]
    public async Task TwoRuns_ProduceTwoSnapshots_AndVersionAChangedFile()
    {
        var source = Path.Combine(_root, "src");
        var dest = Path.Combine(_root, "dest");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());

        var r1 = await svc.RunVersionedAsync(job);
        Assert.True(r1.Result.ExitCode <= 7); // robocopy: <8 = ok
        Assert.Single(SnapshotDirs(dest));

        await Task.Delay(1100); // timestamp distinto (naming al secondo)
        File.WriteAllText(Path.Combine(source, "f.txt"), "v2");
        await svc.RunVersionedAsync(job);

        var snaps = SnapshotDirs(dest);
        Assert.Equal(2, snaps.Length);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(dest, snaps[0], "f.txt")));
        Assert.Equal("v2", File.ReadAllText(Path.Combine(dest, snaps[1], "f.txt")));
    }

    [Fact]
    public async Task Retention_KeepCount1_DeletesOlderSnapshot()
    {
        var source = Path.Combine(_root, "src2");
        var dest = Path.Combine(_root, "dest2");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "a");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, SnapshotKeepCount = 1 };
        var svc = new SnapshotService(new RobocopyRunner());

        await svc.RunVersionedAsync(job);
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "f.txt"), "b");
        await svc.RunVersionedAsync(job);

        Assert.Single(SnapshotDirs(dest));
        Assert.Equal("b", File.ReadAllText(Path.Combine(dest, SnapshotDirs(dest)[0], "f.txt")));
    }
}
