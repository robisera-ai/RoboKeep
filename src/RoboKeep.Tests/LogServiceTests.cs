using System.IO.Compression;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class LogServiceTests : IDisposable
{
    private readonly string _root;

    public LogServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "RoboKeepLogTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private LogService NewService(bool compress) => new(new AppSettings
    {
        LogRoot = Path.Combine(_root, "logs"),
        TempRoot = Path.Combine(_root, "temp"),
        CompressLogs = compress,
        LogRetentionDays = 30,
    });

    [Fact]
    public void BuildLogFileName_HasTimestampAndJob()
    {
        var name = LogService.BuildLogFileName("Documenti", new DateTime(2026, 6, 19, 15, 4, 5));
        Assert.Equal("20260619-150405-Documenti.log", name);
    }

    [Fact]
    public void SanitizeName_ReplacesInvalidChars()
    {
        var name = LogService.SanitizeName("a/b:c*?");
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain(':', name);
        Assert.DoesNotContain('*', name);
    }

    [Fact]
    public void WriteAndArchive_Compressed_CreatesZipUnderDailyFolder()
    {
        var svc = NewService(compress: true);
        var ts = new DateTime(2026, 6, 19, 15, 4, 5);
        var path = svc.WriteAndArchive("Documenti", "contenuto log", ts);

        Assert.True(File.Exists(path));
        Assert.EndsWith(".log.zip", path);
        Assert.Contains(Path.Combine("logs", "20260619"), path);

        using var zip = ZipFile.OpenRead(path);
        Assert.Single(zip.Entries);
        Assert.Equal("20260619-150405-Documenti.log", zip.Entries[0].Name);
    }

    [Fact]
    public void WriteAndArchive_Uncompressed_WritesPlainLog()
    {
        var svc = NewService(compress: false);
        var ts = new DateTime(2026, 6, 19, 15, 4, 5);
        var path = svc.WriteAndArchive("Foto", "xyz", ts);

        Assert.True(File.Exists(path));
        Assert.EndsWith(".log", path);
        Assert.Equal("xyz", File.ReadAllText(path));
    }

    [Fact]
    public void CleanupOldLogs_DeletesFilesOlderThanRetention()
    {
        var svc = NewService(compress: true);
        var oldTs = new DateTime(2026, 1, 1, 10, 0, 0);
        var oldPath = svc.WriteAndArchive("Vecchio", "old", oldTs);
        // Forza la data di scrittura nel passato.
        File.SetLastWriteTime(oldPath, oldTs);

        var recentPath = svc.WriteAndArchive("Recente", "new", new DateTime(2026, 6, 19, 10, 0, 0));
        File.SetLastWriteTime(recentPath, new DateTime(2026, 6, 19, 10, 0, 0));

        var deleted = svc.CleanupOldLogs(new DateTime(2026, 6, 19, 12, 0, 0));

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(recentPath));
    }

    [Fact]
    public void LatestLogFolder_PicksTheMostRecentDay_IgnoringOtherFolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "20260719"));
        Directory.CreateDirectory(Path.Combine(_root, "20260920"));
        Directory.CreateDirectory(Path.Combine(_root, "zzz-non-un-giorno"));

        Assert.Equal(Path.Combine(_root, "20260920"), LogService.LatestLogFolder(_root));
    }

    [Fact]
    public void LatestLogFolder_FallsBackToTheRoot_WhenThereAreNoLogsYet()
    {
        Assert.Equal(_root, LogService.LatestLogFolder(_root));
        var missing = Path.Combine(_root, "non-esiste");
        Assert.Equal(missing, LogService.LatestLogFolder(missing)); // best-effort: mai un'eccezione
    }
}
