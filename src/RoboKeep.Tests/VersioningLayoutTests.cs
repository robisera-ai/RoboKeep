using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Scelta del modello di versioni: il layout che c'e' in destinazione vince sempre (un backup
/// avviato non cambia modello sotto i piedi dell'utente), e solo su una destinazione vergine decide
/// la prova sul disco, qui iniettata.
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
    public void DatedFolders_WinOverTheProbe()
    {
        var dest = Dest("hl");
        Directory.CreateDirectory(Path.Combine(dest, "2026-09-25_210000"));
        // La prova dice "niente hard-link", ma qui ci sono gia' cartelle datate: cambiare modello
        // renderebbe irraggiungibili le versioni esistenti.
        Assert.Equal(VersioningMode.HardLinks, VersioningLayout.Detect(dest, _ => false));
    }

    [Fact]
    public void StaleInProgressDatedFolder_IsStillTheHardLinkLayout()
    {
        var dest = Dest("hl-stale");
        Directory.CreateDirectory(Path.Combine(dest, "2026-09-25_210000" + SnapshotName.InProgressSuffix));
        Assert.Equal(VersioningMode.HardLinks, VersioningLayout.Detect(dest, _ => false));
    }

    [Fact]
    public void CurrentFolder_WinsOverTheProbe()
    {
        var dest = Dest("diff");
        Directory.CreateDirectory(VersioningLayout.CurrentDir(dest));
        // La prova dice "hard-link disponibili" (il disco e' NTFS), ma il job gira gia' per
        // differenza: passare all'altro modello lascerebbe «current» e «versions» orfane.
        Assert.Equal(VersioningMode.Differential, VersioningLayout.Detect(dest, _ => true));
    }

    [Fact]
    public void VirginDestination_AsksTheProbe()
    {
        var dest = Dest("vuota");
        Assert.Equal(VersioningMode.HardLinks, VersioningLayout.Detect(dest, _ => true));
        Assert.Equal(VersioningMode.Differential, VersioningLayout.Detect(dest, _ => false));
    }

    [Fact]
    public void MissingDestination_AsksTheProbe()
    {
        var dest = Path.Combine(_root, "non-esiste-ancora");
        Assert.Equal(VersioningMode.Differential, VersioningLayout.Detect(dest, _ => false));
        Assert.Equal(VersioningMode.HardLinks, VersioningLayout.Detect(dest, _ => true));
    }

    [Fact]
    public void DestinationWithUnrelatedContent_AsksTheProbe()
    {
        // Contenuto qualsiasi non e' un layout: decide la prova.
        var dest = Dest("piatta");
        File.WriteAllText(Path.Combine(dest, "documento.txt"), "x");
        Directory.CreateDirectory(Path.Combine(dest, "cartella"));
        Assert.Equal(VersioningMode.Differential, VersioningLayout.Detect(dest, _ => false));
    }

    [Fact]
    public void VersionsFolderWithDatedEntries_IsEnoughToWinOverTheProbe()
    {
        // «current» cancellata a mano (o mai creata perche' il primo mirror non e' riuscito): le
        // versioni restano, e i file che contengono sono spesso l'unica copia rimasta. Il layout
        // c'e' ancora e va rispettato.
        var dest = Dest("solo-versions");
        Directory.CreateDirectory(Path.Combine(VersioningLayout.VersionsDir(dest), "2026-09-25_210000"));
        Assert.Equal(VersioningMode.Differential, VersioningLayout.Detect(dest, _ => true));
        Assert.True(VersioningLayout.HasLayout(dest));
    }

    [Fact]
    public void HasLayout_IsFalse_OnAVirginOrMissingDestination()
    {
        Assert.False(VersioningLayout.HasLayout(Dest("nuda")));
        Assert.False(VersioningLayout.HasLayout(Path.Combine(_root, "non-esiste")));
        Assert.False(VersioningLayout.HasLayout(""));
        // Una cartella «versions» vuota non e' un layout: non c'e' niente da proteggere.
        var dest = Dest("versions-vuota");
        Directory.CreateDirectory(VersioningLayout.VersionsDir(dest));
        Assert.False(VersioningLayout.HasLayout(dest));
    }

    [Fact]
    public void HasLayout_IsTrue_ForBothModels()
    {
        var hl = Dest("hl-layout");
        Directory.CreateDirectory(Path.Combine(hl, "2026-09-25_210000"));
        Assert.True(VersioningLayout.HasLayout(hl));

        var diff = Dest("diff-layout");
        Directory.CreateDirectory(VersioningLayout.CurrentDir(diff));
        Assert.True(VersioningLayout.HasLayout(diff));
    }

    [Fact]
    public void DefaultProbe_OnNtfsTempFolder_SaysHardLinks()
    {
        var dest = Dest("ntfs");
        // Su una macchina la cui cartella temporanea NON fosse NTFS (ramdisk exFAT, cartella
        // reindirizzata su una share) la risposta giusta sarebbe l'altra: la prova non ha senso.
        if (!HardLinkSupport.IsSupported(dest)) return;
        Assert.Equal(VersioningMode.HardLinks, VersioningLayout.Detect(dest));
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
