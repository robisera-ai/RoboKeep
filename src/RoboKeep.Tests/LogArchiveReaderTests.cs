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
}
