using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Dove stanno le versioni e quando una destinazione ne ha gia' da rispettare: «current» o
/// versioni datate in «versions». Solo lettura, nessun file scritto.
/// </summary>
public class VersioningLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcLayout_" + Guid.NewGuid().ToString("N"));

    public VersioningLayoutTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private string Dest(string name)
    {
        var d = Path.Combine(_root, name);
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void CurrentFolder_MeansVersionsToRespect()
    {
        var dest = Dest("diff");
        Directory.CreateDirectory(VersioningLayout.CurrentDir(dest));
        Assert.True(VersioningLayout.HasVersions(dest));
    }

    [Fact]
    public void VersionsFolderWithDatedEntries_IsEnough()
    {
        // «current» cancellata a mano (o mai creata perche' il primo mirror non e' riuscito): le
        // versioni restano, e i file che contengono sono spesso l'unica copia rimasta.
        var dest = Dest("solo-versions");
        Directory.CreateDirectory(Path.Combine(VersioningLayout.VersionsDir(dest), "2026-09-25_210000"));
        Assert.True(VersioningLayout.HasVersions(dest));
    }

    [Fact]
    public void AManifestAlone_IsEnough()
    {
        // Un backup di sole aggiunte lascia soltanto il manifest: e' un punto nel tempo, e dice che
        // quella «versions» e' di RoboKeep.
        var dest = Dest("solo-manifest");
        Directory.CreateDirectory(VersioningLayout.VersionsDir(dest));
        File.WriteAllText(Path.Combine(VersioningLayout.VersionsDir(dest),
            "2026-09-25_210000" + VersionManifest.FileSuffix), "{}");
        Assert.True(VersioningLayout.HasVersions(dest));
        Assert.True(VersioningLayout.HasArchivedVersions(dest));
    }

    [Fact]
    public void CurrentAlone_IsNotAnArchive()
    {
        var dest = Dest("solo-current");
        Directory.CreateDirectory(VersioningLayout.CurrentDir(dest));
        Assert.True(VersioningLayout.HasVersions(dest));
        Assert.False(VersioningLayout.HasArchivedVersions(dest));
    }

    [Fact]
    public void InterruptedVersion_IsEnough()
    {
        // Una versione interrotta contiene gli originali spostati via da «current».
        var dest = Dest("solo-inprogress");
        Directory.CreateDirectory(Path.Combine(VersioningLayout.VersionsDir(dest),
            "2026-09-25_210000" + SnapshotName.InProgressSuffix));
        Assert.True(VersioningLayout.HasVersions(dest));
    }

    [Fact]
    public void HasVersions_IsFalse_OnAVirginOrMissingDestination()
    {
        Assert.False(VersioningLayout.HasVersions(Dest("nuda")));
        Assert.False(VersioningLayout.HasVersions(Path.Combine(_root, "non-esiste")));
        Assert.False(VersioningLayout.HasVersions(""));
        // Una cartella «versions» vuota non conta: non c'e' niente da proteggere.
        var dest = Dest("versions-vuota");
        Directory.CreateDirectory(VersioningLayout.VersionsDir(dest));
        Assert.False(VersioningLayout.HasVersions(dest));
    }

    [Fact]
    public void DatedFoldersInTheRoot_AreNotVersions()
    {
        // Cartelle con nome-data nella radice non sono versioni di RoboKeep: sono contenuto come un
        // altro, e l'unico posto dove stanno le versioni e' «versions».
        var dest = Dest("radice-datata");
        Directory.CreateDirectory(Path.Combine(dest, "2026-09-25_210000"));
        File.WriteAllText(Path.Combine(dest, "documento.txt"), "x");
        Assert.False(VersioningLayout.HasVersions(dest));
    }

    [Fact]
    public void HasVersions_WritesNothing()
    {
        // E' la domanda che fanno anche «Versioni...» e il ripristino: deve poter rispondere su un
        // disco in sola lettura, senza scriverci sopra un byte.
        var dest = Dest("ro");
        Assert.False(VersioningLayout.HasVersions(dest));
        Assert.Empty(Directory.GetFileSystemEntries(dest));
    }

    [Fact]
    public void FolderNames_AreTheOnesOnDisk()
    {
        Assert.Equal("current", VersioningLayout.CurrentFolderName);
        Assert.Equal("versions", VersioningLayout.VersionsFolderName);
        Assert.Equal(Path.Combine(@"E:\B", "current"), VersioningLayout.CurrentDir(@"E:\B"));
        Assert.Equal(Path.Combine(@"E:\B", "versions"), VersioningLayout.VersionsDir(@"E:\B"));
    }
}
