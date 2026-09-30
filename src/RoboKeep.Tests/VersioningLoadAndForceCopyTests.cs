using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// La "forza copia" ricopia davvero un file cambiato a parita' di data e dimensione — anche in un
/// job versionato, mettendo da parte la copia precedente invece di riscriverla. Tutto su robocopy
/// reale. (Adozione, "niente di cambiato" e guardia per i job versionati stanno in
/// <see cref="DifferentialSnapshotServiceTests"/>.)
/// </summary>
public sealed class VersioningLoadAndForceCopyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcVfc_" + Guid.NewGuid().ToString("N"));
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");

    public VersioningLoadAndForceCopyTests() => Directory.CreateDirectory(Src);

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;
        Directory.Delete(_root, recursive: true);
    }

    /// <summary>Riscrive il contenuto lasciando identiche dimensione e data di modifica: il caso
    /// dei file (PST, database) che la forza copia esiste per coprire.</summary>
    private static void RewriteKeepingSizeAndTime(string path, string sameLengthContent)
    {
        var mtime = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, sameLengthContent);
        File.SetLastWriteTimeUtc(path, mtime);
    }

    private RobocopyRunner RunnerWithForceCopy() =>
        new(forceCopyPlanner: new ForceCopyPlanner(new ForceCopyHashStore(Path.Combine(_root, "hashes.json"))));

    [Fact]
    public async Task ForceCopy_RecopiesContentChangedWithSameSizeAndTime()
    {
        var file = Path.Combine(Src, "db.dat");
        File.WriteAllText(file, "AAAA");
        var job = new BackupJob { Name = "F", Source = Src, Destination = Dst, Mirror = true, MultiThread = 1 };
        job.ForceCopyFiles = new() { "db.dat" };
        var runner = RunnerWithForceCopy();

        await runner.RunAsync(job);
        RewriteKeepingSizeAndTime(file, "BBBB");
        var second = await runner.RunAsync(job);

        Assert.True(second.Result.Success);
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(Dst, "db.dat")));
    }

    [Fact]
    public async Task ForceCopy_InVersionedJob_CopiesNewContent_AndKeepsThePreviousCopyAsAVersion()
    {
        var file = Path.Combine(Src, "db.dat");
        File.WriteAllText(file, "AAAA");
        File.WriteAllText(Path.Combine(Src, "altro.txt"), "invariato");
        var job = new BackupJob { Name = "VF", Source = Src, Destination = Dst, Versioned = true, MultiThread = 1 };
        job.ForceCopyFiles = new() { "db.dat" };
        var svc = new DifferentialSnapshotService(RunnerWithForceCopy());

        await svc.RunAsync(job);
        await Task.Delay(1100);
        RewriteKeepingSizeAndTime(file, "BBBB");
        await svc.RunAsync(job);

        var versions = VersioningLayout.VersionsDir(Dst);
        var version = Assert.Single(SnapshotName.ListValid(versions));
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(VersioningLayout.CurrentDir(Dst), "db.dat"))); // il backup e' aggiornato
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(versions, version, "db.dat")));                 // e la copia di prima c'e'
        Assert.False(File.Exists(Path.Combine(versions, version, "altro.txt")));                          // solo cio' che e' cambiato
    }

    [Fact]
    public async Task ForceCopySmart_InVersionedJob_WithNothingChanged_LeavesNoNewPointInTime()
    {
        // La lista "forza copia" obbliga a correre anche quando l'anteprima non vede niente; ma se
        // nemmeno l'hash e' cambiato non c'e' niente da raccontare: nessuna cartella, nessun
        // manifest nuovo, nessun residuo.
        File.WriteAllText(Path.Combine(Src, "db.dat"), "AAAA");
        var job = new BackupJob
        {
            Name = "VS", Source = Src, Destination = Dst, Versioned = true, MultiThread = 1, ForceCopySmart = true,
        };
        job.ForceCopyFiles = new() { "db.dat" };
        var svc = new DifferentialSnapshotService(RunnerWithForceCopy());

        await svc.RunAsync(job);
        var versions = VersioningLayout.VersionsDir(Dst);
        var before = Directory.GetFileSystemEntries(versions).OrderBy(n => n).ToArray();
        await Task.Delay(1100);
        var second = await svc.RunAsync(job);

        Assert.True(second.Result.Success);
        Assert.Equal(before, Directory.GetFileSystemEntries(versions).OrderBy(n => n).ToArray());
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(VersioningLayout.CurrentDir(Dst), "db.dat")));
    }

    private string Current => VersioningLayout.CurrentDir(Dst);
    private string Versions => VersioningLayout.VersionsDir(Dst);

    private BackupJob ForcedJob(bool smart = false, int keep = 0)
    {
        var job = new BackupJob
        {
            Name = "FC", Source = Src, Destination = Dst, Versioned = true, MultiThread = 1,
            ForceCopySmart = smart, SnapshotKeepCount = keep, Retries = 0, Wait = 0,
        };
        job.ForceCopyFiles = new() { "db.dat" };
        return job;
    }

    [Fact]
    public async Task ForceCopySmart_WithTheHashChanged_AndNothingElse_KeepsTheOldContentAsAVersion()
    {
        var file = Path.Combine(Src, "db.dat");
        File.WriteAllText(file, "AAAA");
        File.WriteAllText(Path.Combine(Src, "altro.txt"), "invariato");
        var job = ForcedJob(smart: true);
        var svc = new DifferentialSnapshotService(RunnerWithForceCopy());

        await svc.RunAsync(job);
        await Task.Delay(1100);
        RewriteKeepingSizeAndTime(file, "BBBB");
        var second = await svc.RunAsync(job);

        Assert.True(second.Result.Success);
        var version = Assert.Single(SnapshotName.ListValid(Versions));
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Versions, version, "db.dat")));
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(Current, "db.dat")));
    }

    [Fact]
    public async Task PlainForceCopy_WithNothingChanged_CreatesNoNewPointsInTime_ButKeepsARealChange()
    {
        // La forza copia semplice ricopia i suoi file a ogni run. Se ogni run lasciasse una versione
        // con la copia identica, con «tieni 2» bastano due notti per spingere fuori la versione che
        // teneva l'unica copia di un file cancellato.
        var file = Path.Combine(Src, "db.dat");
        File.WriteAllText(file, "AAAA");
        File.WriteAllText(Path.Combine(Src, "altro.txt"), "invariato");
        var job = ForcedJob(keep: 2);
        var svc = new DifferentialSnapshotService(RunnerWithForceCopy());

        await svc.RunAsync(job);
        var afterFirst = VersionCatalog.List(Versions).Select(p => p.Name).ToArray();
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(1100);
            Assert.True((await svc.RunAsync(job)).Result.Success);
        }

        Assert.Equal(afterFirst, VersionCatalog.List(Versions).Select(p => p.Name).ToArray());
        Assert.Empty(SnapshotName.ListValid(Versions));
        Assert.DoesNotContain(Directory.GetDirectories(Versions), d => SnapshotName.IsInProgress(Path.GetFileName(d)));

        // Un cambiamento vero nel file forzato invece si conserva.
        await Task.Delay(1100);
        RewriteKeepingSizeAndTime(file, "BBBB");
        Assert.True((await svc.RunAsync(job)).Result.Success);
        var version = Assert.Single(SnapshotName.ListValid(Versions));
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Versions, version, "db.dat")));
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(Current, "db.dat")));
    }

    [Fact]
    public async Task ForceCopyFileHeldOpen_AndNothingElseChanged_IsReportedInTheOutcome()
    {
        // Il file forzato e' aperto da un altro programma: non si puo' mettere da parte, quindi
        // resta escluso dalla passata. Il run non deve dire "tutto a posto" in silenzio: con
        // «current» ferma per sempre, l'utente deve saperlo.
        File.WriteAllText(Path.Combine(Src, "db.dat"), "AAAA");
        var job = ForcedJob();
        var svc = new DifferentialSnapshotService(RunnerWithForceCopy());
        await svc.RunAsync(job);
        await Task.Delay(1100);

        RobocopyRunResult run;
        using (new FileStream(Path.Combine(Current, "db.dat"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            run = await svc.RunAsync(job);

        var note = Assert.Single(run.Result.VersionNotes);
        Assert.Contains("db.dat", note);
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Current, "db.dat")));
    }

    /// <summary>Finto robocopy: anteprima e prima passata non trovano niente; la passata forzata
    /// (riconosciuta da /IS) scrive una riga e poi si blocca, oppure esce con errore grave.</summary>
    private string FakeRobocopyForcedPass(bool hang)
    {
        var path = Path.Combine(_root, hang ? "fake-forced-hang.cmd" : "fake-forced-fail.cmd");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path,
            "@echo off\r\n" +
            "echo %* | findstr /C:\"/IS\" >nul || exit /b 0\r\n" +
            "echo PASSATA-FORZATA\r\n" +
            (hang ? "ping -n 30 127.0.0.1 >nul\r\nexit /b 0\r\n" : "exit /b 16\r\n"));
        return path;
    }

    private RobocopyRunner FakeRunner(string robocopy) =>
        new(robocopy, new ForceCopyPlanner(new ForceCopyHashStore(Path.Combine(_root, "hashes.json"))),
            _ => DiskMedia.Unknown);

    /// <summary>«current» con la copia vecchia e una sorgente con quella nuova, stessa data e
    /// dimensione: solo la passata forzata ha qualcosa da fare.</summary>
    private void ForcedScenario()
    {
        Directory.CreateDirectory(Current);
        File.WriteAllText(Path.Combine(Current, "db.dat"), "AAAA");
        File.WriteAllText(Path.Combine(Src, "db.dat"), "BBBB");
    }

    [Fact]
    public async Task CancelledDuringTheForcedPass_TheSetAsideCopyIsRecoveredByTheNextRun()
    {
        ForcedScenario();
        var cts = new CancellationTokenSource();
        var progress = new SyncProgress(l => { if (l.Contains("PASSATA-FORZATA")) cts.Cancel(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DifferentialSnapshotService(FakeRunner(FakeRobocopyForcedPass(hang: true)))
                .RunAsync(ForcedJob(), progress, cts.Token));
        Assert.Contains(Directory.GetDirectories(Versions), d => SnapshotName.IsInProgress(Path.GetFileName(d)));

        // Il run dopo (robocopy vero) promuove la cartella interrotta: la copia vecchia c'e'.
        Assert.True((await new DifferentialSnapshotService(RunnerWithForceCopy()).RunAsync(ForcedJob())).Result.Success);
        var version = Assert.Single(SnapshotName.ListValid(Versions));
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Versions, version, "db.dat")));
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(Current, "db.dat")));
        Assert.DoesNotContain(Directory.GetDirectories(Versions), d => SnapshotName.IsInProgress(Path.GetFileName(d)));
    }

    [Fact]
    public async Task FailedForcedPass_PromotesTheVersion_AndAppliesNoRetention()
    {
        ForcedScenario();
        foreach (var old in new[] { "2026-01-01_000000", "2026-01-02_000000" })
        {
            Directory.CreateDirectory(Path.Combine(Versions, old));
            File.WriteAllText(Path.Combine(Versions, old, "vecchio.txt"), old);
        }

        var run = await new DifferentialSnapshotService(FakeRunner(FakeRobocopyForcedPass(hang: false)))
            .RunAsync(ForcedJob(keep: 1));

        Assert.False(run.Result.Success);
        var names = SnapshotName.ListValid(Versions);
        Assert.Equal(3, names.Count);                                  // nessuna ritenzione dopo un fallimento
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Versions, names[0], "db.dat"))); // la nuova, promossa
    }

    private sealed class SyncProgress : IProgress<string>
    {
        private readonly Action<string> _action;
        public SyncProgress(Action<string> action) => _action = action;
        public void Report(string value) => _action(value);
    }
}
