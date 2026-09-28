using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// L'archivio delle versioni per differenza: quali punti nel tempo esistono, e quali manifest si
/// possono buttare. La regola che conta: «tieni N versioni» deve contare le versioni VERE (quelle
/// con una cartella), non i backup di sole aggiunte, che non hanno niente da conservare.
/// </summary>
public class VersionCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcCat_" + Guid.NewGuid().ToString("N"));

    public VersionCatalogTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    // ---- ManifestsToPrune (funzione pura) ----

    [Fact]
    public void ManifestsToPrune_DropsOnlyTheOnesOlderThanTheOldestFolder()
    {
        var dirs = new[] { "2026-09-20_210000", "2026-09-25_210000" };
        var manifests = new[]
        {
            "2026-09-10_210000", // piu' vecchio della cartella piu' vecchia -> inutile
            "2026-09-19_235959", // idem, per un secondo
            "2026-09-20_210000", // gemello di una cartella: mai toccato
            "2026-09-22_210000", // in mezzo: serve, dice che cosa non esisteva ancora
            "2026-09-25_210000", // gemello di una cartella
            "2026-09-27_210000", // piu' recente: serve
        };

        Assert.Equal(new[] { "2026-09-10_210000", "2026-09-19_235959" },
            VersionCatalog.ManifestsToPrune(dirs, manifests));
    }

    [Fact]
    public void ManifestsToPrune_WithNoFolderAtAll_KeepsEverything()
    {
        // Senza cartelle i manifest sono l'unica memoria rimasta, e pesano pochi byte.
        Assert.Empty(VersionCatalog.ManifestsToPrune(
            Array.Empty<string>(), new[] { "2026-09-10_210000", "2026-09-27_210000" }));
    }

    [Fact]
    public void ManifestsToPrune_SameDateAsTheOldestFolder_IsKept()
    {
        // "Piu' vecchio" e' stretto: la stessa data non e' piu' vecchia.
        Assert.Empty(VersionCatalog.ManifestsToPrune(
            new[] { "2026-09-20_210000" }, new[] { "2026-09-20_210000" }));
    }

    [Fact]
    public void ManifestsToPrune_IgnoresInProgressAndUnparsableNames()
    {
        var dirs = new[] { "2026-09-20_210000", "2026-09-19_210000.inprogress", "roba a caso" };
        var manifests = new[] { "2026-09-01_210000", "non-una-data", "2026-09-02_210000.inprogress" };
        Assert.Equal(new[] { "2026-09-01_210000" }, VersionCatalog.ManifestsToPrune(dirs, manifests));
    }

    // ---- List (sul disco) ----

    [Fact]
    public void List_ReturnsFoldersAndManifestOnlyPoints_InChronologicalOrder()
    {
        var versions = Path.Combine(_root, "versions");
        Directory.CreateDirectory(Path.Combine(versions, "2026-09-25_210000"));
        Directory.CreateDirectory(Path.Combine(versions, "2026-09-20_210000"));
        Directory.CreateDirectory(Path.Combine(versions, "2026-09-26_210000.inprogress")); // ignorata
        new VersionManifest().WriteTo(Path.Combine(versions, "2026-09-25_210000")); // gemello
        new VersionManifest().WriteTo(Path.Combine(versions, "2026-09-22_210000")); // solo manifest
        new VersionManifest().WriteTo(Path.Combine(versions, "2026-09-27_210000")); // solo manifest

        var points = VersionCatalog.List(versions);

        Assert.Equal(
            new[] { "2026-09-20_210000", "2026-09-22_210000", "2026-09-25_210000", "2026-09-27_210000" },
            points.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { true, false, true, false }, points.Select(p => p.HasFolder).ToArray());
        Assert.Equal(new DateTime(2026, 9, 20, 21, 0, 0), points[0].Date);
    }

    [Fact]
    public void List_MissingFolder_IsEmpty()
        => Assert.Empty(VersionCatalog.List(Path.Combine(_root, "non-esiste")));

    [Fact]
    public void ManifestNames_ReadsTheDatePartOnly()
    {
        var versions = Path.Combine(_root, "v2");
        Directory.CreateDirectory(versions);
        new VersionManifest().WriteTo(Path.Combine(versions, "2026-09-22_210000"));
        File.WriteAllText(Path.Combine(versions, "appunti.manifest.json"), "{}"); // non e' una data
        Assert.Equal(new[] { "2026-09-22_210000" }, VersionCatalog.ManifestNames(versions));
    }
}

/// <summary>Nomi liberi per le cartelle-versione: mai un suffisso casuale, che renderebbe la
/// cartella invisibile alla ritenzione e all'elenco delle versioni.</summary>
public class SnapshotFreeNameTests
{
    [Fact]
    public void FreeName_WhenTheNameIsFree_KeepsIt()
        => Assert.Equal("2026-09-27_213000",
            SnapshotName.FreeName("2026-09-27_213000", _ => false));

    [Fact]
    public void FreeName_OnCollision_AdvancesBySeconds_AndStaysAParsableDate()
    {
        var taken = new HashSet<string> { "2026-09-27_213000", "2026-09-27_213001" };
        var name = SnapshotName.FreeName("2026-09-27_213000", taken.Contains);

        Assert.Equal("2026-09-27_213002", name);
        Assert.True(SnapshotName.TryParse(name, out var date));
        Assert.Equal(new DateTime(2026, 9, 27, 21, 30, 2), date);
    }

    [Fact]
    public void FreeName_CrossesTheMinute_AndTheDay()
    {
        Assert.Equal("2026-09-28_000000",
            SnapshotName.FreeName("2026-09-27_235959", n => n == "2026-09-27_235959"));
    }

    [Fact]
    public void FreeName_EverythingTaken_FallsBackWithoutOverwriting()
    {
        var name = SnapshotName.FreeName("2026-09-27_213000", _ => true, maxTries: 3);
        Assert.StartsWith("2026-09-27_213000_", name);
    }
}
