using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VerifyTargetResolverTests : IDisposable
{
    private readonly string _dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public VerifyTargetResolverTests() => Directory.CreateDirectory(_dest);
    public void Dispose() { if (Directory.Exists(_dest)) Directory.Delete(_dest, true); }

    private string Current => Path.Combine(_dest, VersioningLayout.CurrentFolderName);

    [Fact]
    public void PlainJob_ReturnsDestination()
    {
        var job = new BackupJob { Name = "j", Destination = _dest };
        Assert.Equal(_dest, VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void VersionedJob_NothingWrittenYet_ReturnsNull()
    {
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Null(VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void VersionedJob_ReturnsCurrent()
    {
        // Il mirror vero sta in «current». Le cartelle di «versions» contengono stati passati,
        // che con la sorgente di oggi non coincidono per definizione.
        Directory.CreateDirectory(Current);
        Directory.CreateDirectory(Path.Combine(_dest, VersioningLayout.VersionsFolderName, "2026-07-01_100000"));
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Current, VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void VersionedJob_WithVersionsButNoCurrentYet_StillReturnsCurrent()
    {
        // «current» cancellata a mano, o primo mirror mai riuscito: il bersaglio resta quello.
        // Rispondere null manderebbe l'anteprima contro la radice della destinazione, che
        // elencherebbe tutto l'archivio di versions\ come roba da cancellare.
        Directory.CreateDirectory(Path.Combine(_dest, VersioningLayout.VersionsFolderName, "2026-07-01_100000"));
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Current, VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void DatedFoldersInTheRoot_AreNeverTheTarget()
    {
        // Cartelle con nome-data nella radice non sono versioni: il bersaglio e' sempre «current».
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-03_100000"));
        Directory.CreateDirectory(Current);
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Current, VerifyTargetResolver.Resolve(job));

        // ...e da sole non bastano a dire che il job abbia gia' scritto qualcosa.
        Directory.Delete(Current);
        Assert.Null(VerifyTargetResolver.Resolve(job));
    }
}
