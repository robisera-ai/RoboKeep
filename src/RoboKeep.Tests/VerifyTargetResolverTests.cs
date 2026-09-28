using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VerifyTargetResolverTests : IDisposable
{
    private readonly string _dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public VerifyTargetResolverTests() => Directory.CreateDirectory(_dest);
    public void Dispose() { if (Directory.Exists(_dest)) Directory.Delete(_dest, true); }

    [Fact]
    public void PlainJob_ReturnsDestination()
    {
        var job = new BackupJob { Name = "j", Destination = _dest };
        Assert.Equal(_dest, VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void VersionedJob_ReturnsLatestSnapshot()
    {
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-01_100000"));
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-03_100000"));
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-02_100000.inprogress")); // ignorata
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Path.Combine(_dest, "2026-07-03_100000"), VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void VersionedJob_NoSnapshots_ReturnsNull()
    {
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Null(VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void DifferentialJob_ReturnsCurrent()
    {
        // Modello per differenza: il mirror vero sta in «current». Le cartelle di «versions»
        // contengono stati passati, che con la sorgente di oggi non coincidono per definizione.
        Directory.CreateDirectory(Path.Combine(_dest, VersioningLayout.CurrentFolderName));
        Directory.CreateDirectory(Path.Combine(_dest, VersioningLayout.VersionsFolderName, "2026-07-01_100000"));
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Path.Combine(_dest, VersioningLayout.CurrentFolderName), VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void DifferentialJob_WithVersionsButNoCurrentYet_StillReturnsCurrent()
    {
        // «current» cancellata a mano, o primo mirror mai riuscito: il bersaglio resta quello.
        // Rispondere null manderebbe l'anteprima contro la radice della destinazione, che
        // elencherebbe tutto l'archivio di versions\ come roba da cancellare.
        Directory.CreateDirectory(Path.Combine(_dest, VersioningLayout.VersionsFolderName, "2026-07-01_100000"));
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Path.Combine(_dest, VersioningLayout.CurrentFolderName), VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void BothLayouts_DatedFoldersWin_AsInVersioningLayout()
    {
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-03_100000"));
        Directory.CreateDirectory(Path.Combine(_dest, VersioningLayout.CurrentFolderName));
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Path.Combine(_dest, "2026-07-03_100000"), VerifyTargetResolver.Resolve(job));
    }
}
