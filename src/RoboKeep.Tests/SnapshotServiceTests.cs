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
    public async Task StaleInProgress_FromInterruptedRun_IsRemovedOnNextRun()
    {
        var source = Path.Combine(_root, "src7");
        var dest = Path.Combine(_root, "dest7");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");

        // Simula un run interrotto: una .inprogress residua con un timestamp vecchio e del contenuto.
        var stale = Path.Combine(dest, "2026-01-01_000000" + SnapshotName.InProgressSuffix);
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "partial.bin"), "garbage");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());
        await svc.RunVersionedAsync(job);

        Assert.False(Directory.Exists(stale));  // il residuo interrotto è stato rimosso
        Assert.Single(SnapshotDirs(dest));       // esiste un nuovo snapshot completo
        Assert.DoesNotContain(Directory.GetDirectories(dest).Select(Path.GetFileName),
            n => n is not null && SnapshotName.IsInProgress(n!)); // nessuna .inprogress residua
    }

    [Fact]
    public async Task StaleDeletingLeftover_FromInterruptedDelete_IsRemovedOnNextRun()
    {
        var source = Path.Combine(_root, "src9");
        var dest = Path.Combine(_root, "dest9");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");

        // FileSystemDelete rinomina prima di cancellare: una cancellazione interrotta a meta' lascia
        // una cartella ".deleting-…" che nessuna regola tocca piu' e che occupa disco per sempre.
        var leftover = Path.Combine(dest, "2026-01-01_000000.deleting-abcd1234");
        Directory.CreateDirectory(leftover);
        File.WriteAllText(Path.Combine(leftover, "residuo.bin"), "garbage");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        await new SnapshotService(new RobocopyRunner()).RunVersionedAsync(job);

        Assert.False(Directory.Exists(leftover));
        Assert.Single(SnapshotDirs(dest));
    }

    [Fact]
    public async Task RunVersioned_SourceOverride_CopiesFromOverridePath()
    {
        var source = Path.Combine(_root, "srcLive");
        var overrideDir = Path.Combine(_root, "srcFrozen");
        var dest = Path.Combine(_root, "dest8");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(overrideDir);
        File.WriteAllText(Path.Combine(source, "a.txt"), "live");
        File.WriteAllText(Path.Combine(overrideDir, "a.txt"), "frozen");
        File.WriteAllText(Path.Combine(overrideDir, "b.txt"), "solo-nello-snapshot");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());

        await svc.RunVersionedAsync(job, sourceOverride: overrideDir);

        var snap = SnapshotDirs(dest).Single();
        Assert.Equal("frozen", File.ReadAllText(Path.Combine(dest, snap, "a.txt")));
        Assert.True(File.Exists(Path.Combine(dest, snap, "b.txt")));
    }

    [Fact]
    public async Task Adoption_NeverMovesRoboKeepsOwnFolders_IntoTheFirstSnapshot()
    {
        var source = Path.Combine(_root, "src10");
        var dest = Path.Combine(_root, "dest10");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");

        // Copia semplice gia' presente, piu' le cartelle che appartengono a RoboKeep: la copia
        // della configurazione (che nella radice del disco ci deve restare) e i resti di un layout
        // per differenza. Nessuna delle tre esiste nella sorgente: finendo nel conto degli
        // "estranei" impedirebbero l'adozione, e finendo dentro la cartella-data sparirebbero
        // dalla radice — la copia della configurazione e' proprio li' che si va a cercarla.
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, "f.txt"), "vecchio");
        Directory.CreateDirectory(Path.Combine(dest, ConfigMirror.FolderName));
        File.WriteAllText(Path.Combine(dest, ConfigMirror.FolderName, "config.json"), "{}");
        Directory.CreateDirectory(Path.Combine(dest, VersioningLayout.VersionsFolderName));
        File.WriteAllText(Path.Combine(dest, VersioningLayout.VersionsFolderName, "residuo.txt"), "x");

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var lines = new List<string>();
        await new SnapshotService(new RobocopyRunner())
            .RunVersionedAsync(job, new Progress<string>(lines.Add));

        // L'adozione e' avvenuta lo stesso (le cartelle di RoboKeep non contano come estranee):
        // la copia vecchia e' diventata la prima versione, il run ne ha prodotta una seconda.
        var snaps = SnapshotDirs(dest);
        Assert.Equal(2, snaps.Length);
        Assert.Equal("vecchio", File.ReadAllText(Path.Combine(dest, snaps[0], "f.txt")));
        var snap = snaps[1];
        Assert.Equal("v1", File.ReadAllText(Path.Combine(dest, snap, "f.txt")));
        // ...e le cartelle di RoboKeep sono rimaste dove dovevano stare.
        Assert.True(File.Exists(Path.Combine(dest, ConfigMirror.FolderName, "config.json")));
        Assert.True(File.Exists(Path.Combine(dest, VersioningLayout.VersionsFolderName, "residuo.txt")));
        Assert.False(Directory.Exists(Path.Combine(dest, snap, ConfigMirror.FolderName)));
        Assert.False(Directory.Exists(Path.Combine(dest, snap, VersioningLayout.VersionsFolderName)));
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
