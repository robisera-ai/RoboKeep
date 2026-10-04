using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class JobLockFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void Acquire_CreatesLockFile_DeletedOnDispose()
    {
        var lockPath = Path.Combine(_dir, "Desktop.lock");
        using (JobLockFile.TryAcquire(_dir, "Desktop"))
            Assert.True(File.Exists(lockPath));
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public void GetInterrupted_ReturnsJobsWithALeftoverLockFile()
    {
        // Il processo e' morto: il file resta, ma nessuno lo tiene aperto.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "Desktop.lock"), "");

        var interrupted = JobLockFile.GetInterrupted(_dir, ["Desktop", "Programmi"]);
        Assert.Contains("Desktop", interrupted);
        Assert.DoesNotContain("Programmi", interrupted);
    }

    [Fact]
    public void GetInterrupted_IgnoresAJobThatIsRunningRightNow()
    {
        // L'attivita' pianificata sta girando mentre si apre la finestra: non e' "interrotto".
        using (JobLockFile.TryAcquire(_dir, "Desktop"))
            Assert.Empty(JobLockFile.GetInterrupted(_dir, ["Desktop"]));
    }

    [Fact]
    public void TryAcquire_SecondTime_ReturnsNull_UntilTheFirstIsReleased()
    {
        using (var first = JobLockFile.TryAcquire(_dir, "Desktop"))
        {
            Assert.NotNull(first);
            Assert.Null(JobLockFile.TryAcquire(_dir, "desktop"));
        }
        using var again = JobLockFile.TryAcquire(_dir, "Desktop");
        Assert.NotNull(again);
    }

    [Fact]
    public void TryAcquire_TakesOverALeftoverLockFile()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "Desktop.lock"), "");
        using var handle = JobLockFile.TryAcquire(_dir, "Desktop");
        Assert.NotNull(handle);
    }

    [Fact]
    public void GetInterrupted_EmptyAfterDispose()
    {
        using (JobLockFile.TryAcquire(_dir, "Desktop")) { }
        var interrupted = JobLockFile.GetInterrupted(_dir, ["Desktop"]);
        Assert.Empty(interrupted);
    }

    [Fact]
    public void GetInterrupted_MissingFolder_ReturnsEmpty()
    {
        var interrupted = JobLockFile.GetInterrupted(_dir, ["Desktop"]);
        Assert.Empty(interrupted);
    }

    [Fact]
    public void DestinationLock_TheSameFolderWrittenDifferently_IsTheSameLock()
    {
        // Due job (o un job rinominato mentre gira) sulla stessa destinazione: il lock per nome non
        // li vede, quello per destinazione si'. Maiuscole, barra finale e spazi non contano.
        using var first = JobLockFile.TryAcquireDestination(_dir, @"Z:\Backup\Documenti");
        Assert.NotNull(first);
        Assert.Null(JobLockFile.TryAcquireDestination(_dir, @"z:\backup\documenti\"));
        Assert.Null(JobLockFile.TryAcquireDestination(_dir, @"Z:\Backup\Documenti "));
    }

    [Fact]
    public void DestinationLock_AnotherFolder_IsFree()
    {
        using var documenti = JobLockFile.TryAcquireDestination(_dir, @"Z:\Backup\Documenti");
        using var foto = JobLockFile.TryAcquireDestination(_dir, @"Z:\Backup\Foto");
        Assert.NotNull(documenti);
        Assert.NotNull(foto);
    }

    [Fact]
    public void IsDestinationBusy_OnlyWhileTheLockIsHeld()
    {
        Assert.False(JobLockFile.IsDestinationBusy(_dir, @"Z:\Backup\Documenti"));
        using (JobLockFile.TryAcquireDestination(_dir, @"Z:\Backup\Documenti"))
            Assert.True(JobLockFile.IsDestinationBusy(_dir, @"z:\backup\documenti"));
        Assert.False(JobLockFile.IsDestinationBusy(_dir, @"Z:\Backup\Documenti"));
    }

    [Fact]
    public void GetInterrupted_IgnoresDestinationLocks()
    {
        using (JobLockFile.TryAcquireDestination(_dir, @"Z:\Backup\Documenti"))
            Assert.Empty(JobLockFile.GetInterrupted(_dir, ["Documenti", @"Z:\Backup\Documenti"]));
    }

    [Fact]
    public void TryAcquire_WaitsBriefly_ForALockReleasedAMomentLater()
    {
        // Il controllo dei run interrotti apre per un istante i file di lock: un backup che parte
        // in quell'istante non deve credere che lo stesso job stia gia' girando.
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "Desktop.lock");
        var probe = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);
        // Un thread suo, non il pool: con tutta la suite in parallelo il pool e' saturo e il
        // rilascio arriverebbe in ritardo per motivi che con il lock non c'entrano.
        var release = new Thread(() => { Thread.Sleep(50); probe.Dispose(); }) { IsBackground = true };
        release.Start();

        using var handle = JobLockFile.TryAcquire(_dir, "Desktop");
        release.Join();
        Assert.NotNull(handle);
    }

    [Fact]
    public void Acquire_SanitizesInvalidChars_InFileName()
    {
        using (JobLockFile.TryAcquire(_dir, "My:Job/Name"))
        {
            var files = Directory.GetFiles(_dir, "*.lock");
            Assert.Single(files);
            Assert.DoesNotContain(":", Path.GetFileName(files[0]));
        }
    }
}
