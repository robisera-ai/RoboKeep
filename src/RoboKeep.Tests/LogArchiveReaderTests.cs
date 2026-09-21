using System.IO.Compression;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class LogArchiveReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public LogArchiveReaderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void PlainLogFile_IsReadDirectly()
    {
        var path = Path.Combine(_dir, "run.log");
        File.WriteAllText(path, "riga 1\nriga 2");
        Assert.Equal("riga 1\nriga 2", LogArchiveReader.ReadLogText(path));
    }

    [Fact]
    public void ZippedLog_IsReadFromFirstEntry()
    {
        var inner = Path.Combine(_dir, "20260704-100000-Job.log");
        File.WriteAllText(inner, "contenuto zippato");
        var zip = inner + ".zip";
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            z.CreateEntryFromFile(inner, Path.GetFileName(inner));
        File.Delete(inner);

        Assert.Equal("contenuto zippato", LogArchiveReader.ReadLogText(zip));
    }

    [Fact]
    public void MissingFile_ReturnsNull()
        => Assert.Null(LogArchiveReader.ReadLogText(Path.Combine(_dir, "no.log.zip")));

    [Fact]
    public void CorruptZip_ReturnsNull()
    {
        var zip = Path.Combine(_dir, "bad.log.zip");
        File.WriteAllText(zip, "non sono uno zip");
        Assert.Null(LogArchiveReader.ReadLogText(zip));
    }

    [Fact]
    public void ExtractForViewing_UnzipsToAPlainLog_ReadableByNotepad()
    {
        var inner = Path.Combine(_dir, "20260920-164615-Progetti.log");
        File.WriteAllText(inner, "Più recente: è andato tutto bene");
        var zip = inner + ".zip";
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            z.CreateEntryFromFile(inner, Path.GetFileName(inner));
        var viewer = Path.Combine(_dir, "viewer");

        var copy = LogArchiveReader.ExtractForViewing(zip, viewer);

        Assert.Equal(Path.Combine(viewer, "20260920-164615-Progetti.log"), copy);
        Assert.Equal("Più recente: è andato tutto bene", File.ReadAllText(copy!));
        // BOM UTF-8: senza, il Blocco note puo' sbagliare le accentate.
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(copy!).Take(3).ToArray());
        Assert.True(File.Exists(zip)); // l'originale in archivio non si tocca
    }

    [Fact]
    public void ExtractForViewing_RemovesYesterdaysCopies_AndReturnsNullWhenLogIsGone()
    {
        var viewer = Path.Combine(_dir, "viewer");
        Directory.CreateDirectory(viewer);
        var stale = Path.Combine(viewer, "vecchio.log");
        File.WriteAllText(stale, "x");
        File.SetLastWriteTime(stale, DateTime.Now.AddDays(-3));
        var log = Path.Combine(_dir, "run.log");
        File.WriteAllText(log, "oggi");

        Assert.NotNull(LogArchiveReader.ExtractForViewing(log, viewer));
        Assert.False(File.Exists(stale));
        Assert.Null(LogArchiveReader.ExtractForViewing(Path.Combine(_dir, "sparito.log.zip"), viewer));
    }
}
