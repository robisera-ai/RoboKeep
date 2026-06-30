using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class SnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcSnap_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        // Il teardown deve togliere il ReadOnly preservato negli snapshot, altrimenti Directory.Delete fallisce.
        foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;
        Directory.Delete(_root, recursive: true);
    }

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

    [Fact]
    public async Task Retention_DeletesOldSnapshot_EvenWithReadOnlyFile()
    {
        var source = Path.Combine(_root, "src5");
        var dest = Path.Combine(_root, "dest5");
        Directory.CreateDirectory(source);
        var ro = Path.Combine(source, "ro.txt");
        File.WriteAllText(ro, "a");
        File.SetAttributes(ro, FileAttributes.ReadOnly);            // file read-only nella sorgente -> copiato read-only
        File.WriteAllText(Path.Combine(source, "other.txt"), "1");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, SnapshotKeepCount = 1 };
        var svc = new SnapshotService(new RobocopyRunner());

        await svc.RunVersionedAsync(job);
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "other.txt"), "2"); // cambia un altro file -> secondo snapshot
        await svc.RunVersionedAsync(job);

        File.SetAttributes(ro, FileAttributes.Normal);              // ripristina per il cleanup

        // keepCount=1: il vecchio snapshot (che contiene un file read-only) DEVE essere cancellato
        Assert.Single(SnapshotDirs(dest));
    }

    [Fact]
    public async Task Retention_PreservesReadOnlyAttribute_OnSurvivingSnapshot()
    {
        var source = Path.Combine(_root, "src6");
        var dest = Path.Combine(_root, "dest6");
        Directory.CreateDirectory(source);
        var ro = Path.Combine(source, "ro.txt");
        File.WriteAllText(ro, "immutabile");
        File.SetAttributes(ro, FileAttributes.ReadOnly);            // file read-only che NON cambia mai
        File.WriteAllText(Path.Combine(source, "other.txt"), "1");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, SnapshotKeepCount = 2 };
        var svc = new SnapshotService(new RobocopyRunner());

        await svc.RunVersionedAsync(job);                            // snapshot 1
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "other.txt"), "2");
        await svc.RunVersionedAsync(job);                            // snapshot 2 (ro.txt hard-linked allo snapshot 1)
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "other.txt"), "3");
        await svc.RunVersionedAsync(job);                            // snapshot 3 -> ritenzione cancella lo snapshot 1

        File.SetAttributes(ro, FileAttributes.Normal);              // ripristina la sorgente per il cleanup

        var snaps = SnapshotDirs(dest);
        Assert.Equal(2, snaps.Length);                              // tenuti gli ultimi 2
        // Cancellare lo snapshot 1 (con cui ro.txt era hard-linkato) NON deve aver spento il ReadOnly
        // sul file superstite: la POSIX-delete cancella il nome senza toccare l'inode condiviso.
        foreach (var snap in snaps)
        {
            var attrs = new FileInfo(Path.Combine(dest, snap, "ro.txt")).Attributes;
            Assert.True(attrs.HasFlag(FileAttributes.ReadOnly), $"ro.txt in {snap} ha perso il ReadOnly");
        }
    }
}
