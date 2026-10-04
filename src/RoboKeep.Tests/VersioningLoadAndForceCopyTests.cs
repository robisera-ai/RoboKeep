using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// La "forza copia" ricopia davvero un file cambiato a parita' di data e dimensione — anche in un
/// job versionato, mettendo da parte la copia precedente invece di riscriverla. Tutto su robocopy
/// reale. (Adozione, "niente di cambiato" e guardia per i job versionati stanno in
/// <see cref="VersionedBackupServiceTests"/>.)
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
        var svc = new VersionedBackupService(RunnerWithForceCopy());

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
        var svc = new VersionedBackupService(RunnerWithForceCopy());

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
        var svc = new VersionedBackupService(RunnerWithForceCopy());

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
        var svc = new VersionedBackupService(RunnerWithForceCopy());

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
        var svc = new VersionedBackupService(RunnerWithForceCopy());
        await svc.RunAsync(job);
        await Task.Delay(1100);

        RobocopyRunResult run;
        using (new FileStream(Path.Combine(Current, "db.dat"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            run = await svc.RunAsync(job);

        var note = Assert.Single(run.Result.VersionNotes);
        Assert.Contains("db.dat", note);
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Current, "db.dat")));
    }

    [Fact]
    public async Task Accumulate_WithForceCopy_KeepsAFileDeletedFromTheSource()
    {
        // «Accumula» promette che cio' che sparisce dalla sorgente resta nel backup. La passata
        // forzata mette da parte i file che sta per riscrivere: uno che in sorgente non c'e' piu'
        // non verrebbe riscritto, e una volta messo da parte uscirebbe dal backup per finire
        // cancellato dalla ritenzione.
        File.WriteAllText(Path.Combine(Src, "vecchio.pst"), "unica copia");
        File.WriteAllText(Path.Combine(Src, "posta.pst"), "v0");
        var job = new BackupJob
        {
            Name = "AF", Source = Src, Destination = Dst, Versioned = true, Mirror = false,
            MultiThread = 1, SnapshotKeepCount = 1, Retries = 0, Wait = 0,
        };
        job.ForceCopyFiles = new() { "*.pst" };
        var svc = new VersionedBackupService(RunnerWithForceCopy());
        Assert.True((await svc.RunAsync(job)).Result.Success);

        File.Delete(Path.Combine(Src, "vecchio.pst"));
        for (var i = 1; i <= 3; i++)
        {
            await Task.Delay(1100);
            File.WriteAllText(Path.Combine(Src, "posta.pst"), "v" + i);
            Assert.True((await svc.RunAsync(job)).Result.Success);
        }

        Assert.True(File.Exists(Path.Combine(Current, "vecchio.pst")), "il file sparito dalla sorgente e' uscito dal backup");
        Assert.Equal("unica copia", File.ReadAllText(Path.Combine(Current, "vecchio.pst")));
    }

    [Fact]
    public async Task SmartForceCopy_ASameNamedFileDeletedElsewhere_StaysInCurrent()
    {
        // In modalita' smart i filtri sono nomi nudi: "posta.pst" vale per ogni posta.pst
        // dell'albero. Ne cambia uno, l'altro e' stato cancellato dalla sorgente: quello
        // cancellato non va portato fuori dal backup.
        Directory.CreateDirectory(Path.Combine(Src, "A"));
        Directory.CreateDirectory(Path.Combine(Src, "B"));
        File.WriteAllText(Path.Combine(Src, "A", "posta.pst"), "AAAA");
        File.WriteAllText(Path.Combine(Src, "B", "posta.pst"), "BBBB");
        var job = new BackupJob
        {
            Name = "SF", Source = Src, Destination = Dst, Versioned = true, Mirror = false,
            MultiThread = 1, ForceCopySmart = true, Retries = 0, Wait = 0,
        };
        job.ForceCopyFiles = new() { "posta.pst" };
        var svc = new VersionedBackupService(RunnerWithForceCopy());
        Assert.True((await svc.RunAsync(job)).Result.Success);

        await Task.Delay(1100);
        Directory.Delete(Path.Combine(Src, "A"), recursive: true);
        RewriteKeepingSizeAndTime(Path.Combine(Src, "B", "posta.pst"), "CCCC");
        Assert.True((await svc.RunAsync(job)).Result.Success);

        Assert.True(File.Exists(Path.Combine(Current, "A", "posta.pst")), "il file cancellato dalla sorgente e' uscito dal backup");
        Assert.Equal("CCCC", File.ReadAllText(Path.Combine(Current, "B", "posta.pst")));
    }

    [Fact]
    public async Task ForceCopy_AFileTheUserExcludedLater_StaysInCurrent()
    {
        // Robocopy non ripulisce cio' che e' escluso: un file copiato prima che l'utente
        // escludesse la sua cartella resta in «current». La passata forzata rispetta l'esclusione e
        // non lo riscrive: se lo si mettesse da parte uscirebbe dal backup senza tornarci.
        Directory.CreateDirectory(Path.Combine(Src, "Archivio"));
        File.WriteAllText(Path.Combine(Src, "Archivio", "x.pst"), "archiviato");
        File.WriteAllText(Path.Combine(Src, "posta.pst"), "AAAA");
        var job = new BackupJob
        {
            Name = "EX", Source = Src, Destination = Dst, Versioned = true, Mirror = true,
            MultiThread = 1, Retries = 0, Wait = 0,
        };
        job.ForceCopyFiles = new() { "*.pst" };
        var svc = new VersionedBackupService(RunnerWithForceCopy());
        Assert.True((await svc.RunAsync(job)).Result.Success);

        await Task.Delay(1100);
        job.ExcludeDirs = new() { "Archivio" };
        Assert.True((await svc.RunAsync(job)).Result.Success);

        Assert.True(File.Exists(Path.Combine(Current, "Archivio", "x.pst")), "il file escluso e' uscito dal backup");
    }

    [Fact]
    public async Task AForceCopyFileInUse_MatchedByTwoFilters_IsReportedOnce()
    {
        File.WriteAllText(Path.Combine(Src, "posta.pst"), "AAAA");
        var job = new BackupJob
        {
            Name = "DUP", Source = Src, Destination = Dst, Versioned = true, Mirror = true,
            MultiThread = 1, Retries = 0, Wait = 0,
        };
        job.ForceCopyFiles = new() { "*.pst", "posta.*" };
        var svc = new VersionedBackupService(RunnerWithForceCopy());
        Assert.True((await svc.RunAsync(job)).Result.Success);
        await Task.Delay(1100);

        RobocopyRunResult run;
        using (new FileStream(Path.Combine(Current, "posta.pst"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            run = await svc.RunAsync(job);

        var note = Assert.Single(run.Result.VersionNotes);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(note, "posta\\.pst"));
    }

    [Fact]
    public async Task SmartForceCopy_AFileInUse_IsRecopiedOnceItIsFree()
    {
        // Il file escluso dalla passata perche' in uso non e' stato ricopiato: salvarne il nuovo hash
        // lo farebbe sembrare gia' a posto, e non verrebbe piu' ricopiato finche' non cambia di nuovo.
        File.WriteAllText(Path.Combine(Src, "db.dat"), "AAAA");
        var job = ForcedJob(smart: true);
        var svc = new VersionedBackupService(RunnerWithForceCopy());
        Assert.True((await svc.RunAsync(job)).Result.Success);

        await Task.Delay(1100);
        RewriteKeepingSizeAndTime(Path.Combine(Src, "db.dat"), "BBBB");
        using (new FileStream(Path.Combine(Current, "db.dat"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            await svc.RunAsync(job);
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Current, "db.dat")));

        await Task.Delay(1100);
        Assert.True((await svc.RunAsync(job)).Result.Success);
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(Current, "db.dat")));
    }

    [Fact]
    public async Task TooManyForcedFilesInUse_SkipTheForcedPass_AndKeepTheSavedHashes()
    {
        // Lo stesso limite vale per la passata forzata: se i file che non si possono mettere da
        // parte sono troppi per escluderli tutti, la passata non parte (riscriverebbe sul posto i
        // rimanenti) e gli hash restano quelli di prima, cosi' al prossimo run si riprova.
        File.WriteAllText(Path.Combine(Src, "x.dat"), "XXXX");
        File.WriteAllText(Path.Combine(Src, "y.dat"), "YYYY");
        var hashes = Path.Combine(_root, "hashes-limit.json");
        var job = new BackupJob
        {
            Name = "LIM", Source = Src, Destination = Dst, Versioned = true, Mirror = true,
            MultiThread = 1, ForceCopySmart = true, Retries = 0, Wait = 0,
        };
        job.ForceCopyFiles = new() { "*.dat" };
        var svc = new VersionedBackupService(
            new RobocopyRunner(forceCopyPlanner: new ForceCopyPlanner(new ForceCopyHashStore(hashes))), maxSkipped: 1);
        Assert.True((await svc.RunAsync(job)).Result.Success);
        var savedHashes = File.ReadAllText(hashes);

        await Task.Delay(1100);
        RewriteKeepingSizeAndTime(Path.Combine(Src, "x.dat"), "xxxx");
        RewriteKeepingSizeAndTime(Path.Combine(Src, "y.dat"), "yyyy");
        RobocopyRunResult run;
        using (new FileStream(Path.Combine(Current, "x.dat"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (new FileStream(Path.Combine(Current, "y.dat"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            run = await svc.RunAsync(job);

        Assert.False(run.Result.Success);
        Assert.Equal("XXXX", File.ReadAllText(Path.Combine(Current, "x.dat")));
        Assert.Equal("YYYY", File.ReadAllText(Path.Combine(Current, "y.dat")));
        Assert.Equal(savedHashes, File.ReadAllText(hashes));
    }

    /// <summary>Finto robocopy che risponde a <c>/?</c> troppo tardi (PC sotto carico) e per tutto il
    /// resto passa la mano a quello vero. Ogni domanda <c>/?</c> lascia una riga in probes.txt.</summary>
    private string SlowHelpRobocopy()
    {
        var path = Path.Combine(_root, "fake-slow-help.cmd");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path,
            "@echo off\r\n" +
            "if \"%~1\"==\"/?\" (\r\n" +
            "  >>\"%~dp0probes.txt\" echo probe\r\n" +
            "  ping -n 4 127.0.0.1 >nul\r\n" +
            "  exit /b 16\r\n" +
            ")\r\n" +
            "\"%SystemRoot%\\System32\\Robocopy.exe\" %*\r\n" +
            "exit /b %errorlevel%\r\n");
        return path;
    }

    [Fact]
    public async Task ProbeWithoutAnswer_IsNotRemembered_AndTheHashesAreNotSaved()
    {
        // Una sola risposta lenta di "robocopy /?" non deve spegnere /IM per tutta la vita del
        // programma: senza /IM i file riscritti con stessa data e dimensione vengono saltati, e
        // salvandone gli hash non verrebbero piu' ricopiati finche' non cambiano di nuovo.
        File.WriteAllText(Path.Combine(Src, "db.dat"), "AAAA");
        var hashes = Path.Combine(_root, "hashes-probe.json");
        var job = new BackupJob
        {
            Name = "PR", Source = Src, Destination = Dst, Mirror = true, MultiThread = 1,
            ForceCopySmart = true, Retries = 0, Wait = 0,
        };
        job.ForceCopyFiles = new() { "db.dat" };
        var runner = new RobocopyRunner(SlowHelpRobocopy(), new ForceCopyPlanner(new ForceCopyHashStore(hashes)),
            _ => DiskMedia.Unknown, probeTimeout: TimeSpan.FromSeconds(1));

        var first = await runner.RunAsync(job);
        var second = await runner.RunAsync(job);

        Assert.NotEmpty(first.Result.VersionNotes);
        Assert.NotEmpty(second.Result.VersionNotes);
        Assert.False(File.Exists(hashes) && File.ReadAllText(hashes).Contains("db.dat", StringComparison.OrdinalIgnoreCase),
            "hash salvati senza sapere se /IM funziona");
        Assert.Equal(2, File.ReadAllLines(Path.Combine(_root, "probes.txt")).Length);
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
            new VersionedBackupService(FakeRunner(FakeRobocopyForcedPass(hang: true)))
                .RunAsync(ForcedJob(), progress, cts.Token));
        Assert.Contains(Directory.GetDirectories(Versions), d => SnapshotName.IsInProgress(Path.GetFileName(d)));

        // Il run dopo (robocopy vero) promuove la cartella interrotta: la copia vecchia c'e'.
        Assert.True((await new VersionedBackupService(RunnerWithForceCopy()).RunAsync(ForcedJob())).Result.Success);
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

        var run = await new VersionedBackupService(FakeRunner(FakeRobocopyForcedPass(hang: false)))
            .RunAsync(ForcedJob(keep: 1));

        Assert.False(run.Result.Success);
        // La passata forzata non ha riscritto niente: la copia messa da parte torna in «current»
        // (fuori dal backup la ritenzione la cancellerebbe), e la cartella rimasta vuota non diventa
        // una versione.
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Current, "db.dat")));
        var names = SnapshotName.ListValid(Versions);
        Assert.Equal(new[] { "2026-01-01_000000", "2026-01-02_000000" }, names.OrderBy(n => n).ToArray()); // nessuna ritenzione dopo un fallimento
    }

    private sealed class SyncProgress : IProgress<string>
    {
        private readonly Action<string> _action;
        public SyncProgress(Action<string> action) => _action = action;
        public void Report(string value) => _action(value);
    }
}
