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

    [Fact]
    public async Task FailedRun_LeavesInProgress_NoFinalSnapshot()
    {
        var source = Path.Combine(_root, "missing-src"); // sorgente inesistente: robocopy fallisce
        var dest = Path.Combine(_root, "dest3");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());

        var r = await svc.RunVersionedAsync(job);

        Assert.False(r.Result.Success);
        Assert.Empty(SnapshotDirs(dest)); // nessuno snapshot finale
        Assert.Contains(Directory.GetDirectories(dest).Select(Path.GetFileName),
            n => n is not null && SnapshotName.IsInProgress(n!)); // resta una .inprogress
    }

    [Fact]
    public async Task SnapshotFolder_NotReadOnly_EvenIfSourceRootIsReadOnly()
    {
        var source = Path.Combine(_root, "src4");
        var dest = Path.Combine(_root, "dest4");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "x");
        new DirectoryInfo(source).Attributes |= FileAttributes.ReadOnly; // sorgente "speciale" read-only (es. Desktop)

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());
        await svc.RunVersionedAsync(job);

        new DirectoryInfo(source).Attributes &= ~FileAttributes.ReadOnly; // ripristina per non ostacolare il cleanup

        var snap = SnapshotDirs(dest).Single();
        var attrs = new DirectoryInfo(Path.Combine(dest, snap)).Attributes;
        Assert.False(attrs.HasFlag(FileAttributes.ReadOnly)); // la cartella-data non e' sola-lettura -> Esplora mostra il timestamp
    }
}
