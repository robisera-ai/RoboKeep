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
        using (JobLockFile.Acquire(_dir, "Desktop"))
            Assert.True(File.Exists(lockPath));
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public void GetInterrupted_ReturnsJobsWithLockFile()
    {
        using (JobLockFile.Acquire(_dir, "Desktop"))
        {
            var interrupted = JobLockFile.GetInterrupted(_dir, ["Desktop", "Programmi"]);
            Assert.Contains("Desktop", interrupted);
            Assert.DoesNotContain("Programmi", interrupted);
        }
    }

    [Fact]
    public void GetInterrupted_EmptyAfterDispose()
    {
        using (JobLockFile.Acquire(_dir, "Desktop")) { }
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
        using (JobLockFile.Acquire(_dir, "My:Job/Name"))
        {
            var files = Directory.GetFiles(_dir, "*.lock");
            Assert.Single(files);
            Assert.DoesNotContain(":", Path.GetFileName(files[0]));
        }
    }
}
