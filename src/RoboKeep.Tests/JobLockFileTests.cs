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
