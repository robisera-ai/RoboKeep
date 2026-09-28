using RoboKeep.Core;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Ritenzione per SPAZIO e racconto del disco pieno: il pianificatore puro, la pulizia dentro
/// SnapshotService (con lo spazio libero iniettato: un disco pieno non si puo' simulare) e il
/// riconoscimento del «spazio su disco insufficiente» nell'output di robocopy.
/// </summary>
public sealed class SpaceCleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcSpace_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;
        Directory.Delete(_root, recursive: true);
    }

    private const long Gb = 1024L * 1024 * 1024;

    // ---- SpaceCleanupPlanner (puro) ----

    [Fact]
    public void NextToDelete_PicksTheOldest_WhenSpaceIsShort()
    {
        var names = new[] { "2026-09-25_210000", "2026-09-20_210000", "2026-09-27_210000" };
        Assert.Equal("2026-09-20_210000", SpaceCleanupPlanner.NextToDelete(names, 1 * Gb, 10 * Gb));
    }

    [Fact]
    public void NextToDelete_NullWhenSpaceIsEnough()
        => Assert.Null(SpaceCleanupPlanner.NextToDelete(
            new[] { "2026-09-25_210000", "2026-09-20_210000" }, 50 * Gb, 10 * Gb));

    [Fact]
    public void NextToDelete_NullWhenThresholdIsOff()
        => Assert.Null(SpaceCleanupPlanner.NextToDelete(
            new[] { "2026-09-25_210000", "2026-09-20_210000" }, 0, 0));

    [Fact]
    public void NextToDelete_NeverTheOnlyVersion()
        => Assert.Null(SpaceCleanupPlanner.NextToDelete(new[] { "2026-09-25_210000" }, 0, 10 * Gb));

    [Fact]
    public void NextToDelete_IgnoresInProgressAndJunkNames()
    {
        // Due nomi validi piu' rumore: la .inprogress di un run interrotto e cartelle non datate
        // non sono versioni, e non devono nemmeno contare per la regola "mai l'ultima".
        var names = new[]
        {
            "2026-09-27_210000",
            "2026-09-26_210000" + SnapshotName.InProgressSuffix,
            "RoboKeep-config",
            "2026-09-25_210000",
        };
        Assert.Equal("2026-09-25_210000", SpaceCleanupPlanner.NextToDelete(names, 0, 10 * Gb));

        // Con UNA sola versione valida in mezzo al rumore non si cancella niente.
        var alone = new[] { "2026-09-27_210000", "2026-09-26_210000" + SnapshotName.InProgressSuffix };
        Assert.Null(SpaceCleanupPlanner.NextToDelete(alone, 0, 10 * Gb));
    }

    // ---- Pulizia dentro SnapshotService ----

    [Fact]
    public async Task Cleanup_DeletesTheOldestVersion_KeepsTheNewest_AndSaysSoInTheLog()
    {
        var source = Path.Combine(_root, "src");
        var dest = Path.Combine(_root, "dest");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");

        // Due versioni gia' in destinazione: la piu' vecchia e' quella che deve sparire.
        var old = Path.Combine(dest, "2026-09-20_210000");
        var recent = Path.Combine(dest, "2026-09-25_210000");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(recent);
        File.WriteAllText(Path.Combine(old, "f.txt"), "vecchia");
        File.WriteAllText(Path.Combine(recent, "f.txt"), "recente");

        // Spazio libero: sotto soglia alla prima lettura, sopra dopo la cancellazione. Un disco
        // pieno non si puo' simulare, ma la decisione dipende solo da questi numeri.
        var reads = 0;
        long? Space(string _) => ++reads == 1 ? 1 * Gb : 40 * Gb;

        var settings = new AppSettings { FreeSpaceCleanup = true, MinFreeSpaceMb = 10240 };
        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var lines = new List<string>();
        var svc = new SnapshotService(new RobocopyRunner(), settings, Space);

        await svc.RunVersionedAsync(job, new SyncProgress(lines.Add));

        Assert.False(Directory.Exists(old));      // la piu' vecchia e' stata cancellata
        Assert.True(Directory.Exists(recent));    // la piu' recente E' il backup: non si tocca
        // Una riga nel log per la cancellazione, con il nome della versione e quanto si e' liberato.
        var freed = Assert.Single(lines, l => l.Contains("2026-09-20_210000")
            && l.StartsWith(CoreLoc.S("Space_Freed").Split('{')[0], StringComparison.Ordinal));
        Assert.Contains("GB", freed);             // 40 GB - 1 GB: la cifra e' leggibile da un umano
        Assert.Equal(2, reads);                   // lo spazio si rilegge dopo la cancellazione
    }

    [Fact]
    public async Task Cleanup_SaysSoWhenAVersionFreesNothing_InsteadOfPrintingAnUnknownSize()
    {
        var source = Path.Combine(_root, "src4");
        var dest = Path.Combine(_root, "dest4");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");
        Directory.CreateDirectory(Path.Combine(dest, "2026-09-20_210000"));
        Directory.CreateDirectory(Path.Combine(dest, "2026-09-25_210000"));

        // Spazio libero sempre uguale: e' quel che succede quando la versione cancellata condivideva
        // tutti i suoi file con le altre (hard-link). Non e' una misura mancata, e' un fatto.
        var settings = new AppSettings { FreeSpaceCleanup = true, MinFreeSpaceMb = 10240 };
        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };
        var lines = new List<string>();

        await new SnapshotService(new RobocopyRunner(), settings, _ => 1 * Gb)
            .RunVersionedAsync(job, new SyncProgress(lines.Add));

        var note = Assert.Single(lines, l => l.Contains("2026-09-20_210000")
            && l.StartsWith(CoreLoc.S("Space_FreedNothing").Split('{')[0], StringComparison.Ordinal));
        Assert.DoesNotContain(CoreLoc.S("Space_Unknown"), note);
        // La versione piu' recente resta: il ciclo si e' fermato da solo, senza svuotare il disco.
        Assert.True(Directory.Exists(Path.Combine(dest, "2026-09-25_210000")));
    }

    [Fact]
    public async Task Cleanup_DoesNothing_WhenSettingIsOff_OrSpaceIsUnknown()
    {
        var source = Path.Combine(_root, "src2");
        var dest = Path.Combine(_root, "dest2");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "v1");
        var old = Path.Combine(dest, "2026-09-20_210000");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(Path.Combine(dest, "2026-09-25_210000"));

        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true };

        // Casella spenta (il default): nemmeno con il disco a zero si cancella qualcosa.
        await new SnapshotService(new RobocopyRunner(),
                new AppSettings { FreeSpaceCleanup = false, MinFreeSpaceMb = 10240 }, _ => 0L)
            .RunVersionedAsync(job);
        Assert.True(Directory.Exists(old));

        // Casella accesa ma spazio non determinabile: non si cancella al buio.
        await new SnapshotService(new RobocopyRunner(),
                new AppSettings { FreeSpaceCleanup = true, MinFreeSpaceMb = 10240 }, _ => null)
            .RunVersionedAsync(job);
        Assert.True(Directory.Exists(old));
    }

    // ---- Disco pieno ----

    [Theory]
    [InlineData("2026/09/27 21:30:00 ERRORE 112 (0x00000070) Copia del file in corso E:\\B\\f.dat")]
    [InlineData("2026/09/27 21:30:00 ERROR 112 (0x00000070) Copying File E:\\B\\f.dat")]
    [InlineData("2026/09/27 21:30:00 FEHLER 112 (0x00000070) Datei wird kopiert E:\\B\\f.dat")]
    [InlineData("2026/09/27 21:30:00 ERROR 112 Copying File E:\\B\\f.dat")]
    public void DiskFull_IsRecognized_InAnyLanguage(string line)
        => Assert.True(DiskFullDetector.Matches("riga prima\r\n" + line + "\r\nriga dopo"));

    [Theory]
    [InlineData("\t    Nuovo file  \t\t      112\treport.txt")]                                  // file di 112 byte
    [InlineData("2026/09/27 21:30:00 ERRORE 112 (0x00000005) decimale ed esadecimale discordi")]
    [InlineData("2026/09/27 21:30:00 ERRORE 5 (0x00000005) Accesso negato")]
    [InlineData("\t    Nuovo file  \t\t     2048\tD:\\posta\\error 112 risposta.docx")]           // nome di file, non un errore
    [InlineData("\t    Nuovo file  \t\t     2048\tD:\\posta\\ERROR 1120 vecchio.docx")]           // codice piu' lungo
    [InlineData("")]
    public void OtherLines_AreNotDiskFull(string text)
        => Assert.False(DiskFullDetector.Matches(text));

    [Fact]
    public async Task DiskFull_BecomesAClearStatus_WithDetailAndItsOwnEmailSubject()
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.CompressLogs = false;
        var creds = new CredentialService(config.Settings.CredentialScope);
        var results = new LastResultStore(Path.Combine(_root, "lastresults.json"));

        var source = Path.Combine(_root, "src3");
        var dest = Path.Combine(_root, "dest3");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "f.txt"), "x");
        // Due versioni in destinazione: il dettaglio deve saperle contare e misurare.
        foreach (var name in new[] { "2026-09-20_210000", "2026-09-25_210000" })
        {
            Directory.CreateDirectory(Path.Combine(dest, name));
            File.WriteAllText(Path.Combine(dest, name, "f.txt"), new string('x', 4096));
        }

        // Il job e' versionato e la destinazione HA gia' due cartelle-data: senza un servizio che
        // sappia gestirle BackupRunner si rifiuta di partire (un mirror piatto sulla radice le
        // cancellerebbe come file extra). Qui serve il run vero, quindi il servizio si passa.
        var fake = new RobocopyRunner(FakeRobocopyDiskFull(), detectMedia: _ => DiskMedia.Unknown);
        var runner = new BackupRunner(config, fake,
            new LogService(config.Settings), new EmailService(creds), creds, results,
            new SnapshotService(fake));

        var lines = new List<string>();
        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, Retries = 0, Wait = 0 };
        var result = await runner.RunJobAsync(job, progress: new SyncProgress(lines.Add));

        Assert.False(result.Success);
        Assert.True(result.DiskFull);
        Assert.False(result.HardwareError);                         // il disco sta bene: e' pieno
        Assert.Equal(CoreLoc.S("Space_Status"), result.Status);
        Assert.Contains("2", result.DiskFullDetail);                // «le 2 versioni del job occupano ...»
        Assert.Contains(result.DiskFullDetail!, lines);             // il dettaglio e' nel log del job
        Assert.Contains(result.DiskFullDetail!, File.ReadAllText(result.LogPath!));

        // Persistito: la finestra principale lo sapra' anche domani (riga del job e tooltip).
        var saved = results.Load()["V"];
        Assert.True(saved.DiskFull);
        Assert.Equal(result.DiskFullDetail, saved.DiskFullDetail);

        // Email: oggetto riconoscibile dall'anteprima sul telefono, corpo che parte dal dettaglio.
        Assert.Equal("[RoboKeep] " + string.Format(CoreLoc.S("Space_EmailSubject"), "V"),
            EmailService.BuildSubject(result));
        Assert.StartsWith(result.DiskFullDetail, EmailService.BuildBody(result));
    }

    [Fact]
    public void VersionsUsage_CountsEachPhysicalFileOnce_AndOnlyTheDatedFolders()
    {
        var dir = Path.Combine(_root, "usage");
        var v1 = Path.Combine(dir, "2026-09-20_210000");
        var v2 = Path.Combine(dir, "2026-09-25_210000");
        Directory.CreateDirectory(v1);
        Directory.CreateDirectory(v2);
        var first = Path.Combine(v1, "big.bin");
        File.WriteAllBytes(first, new byte[100_000]);
        // La seconda versione condivide il file con la prima: l'occupazione reale resta una sola copia.
        if (!HardLink.TryCreate(Path.Combine(v2, "big.bin"), first)) return; // niente hard-link qui: prova saltata

        // Roba che NON e' una versione: non va addebitata a chi decide quante versioni tenere.
        Directory.CreateDirectory(Path.Combine(dir, "RoboKeep-config"));
        File.WriteAllBytes(Path.Combine(dir, "RoboKeep-config", "config.json"), new byte[500_000]);
        var inProgress = Path.Combine(dir, "2026-09-26_210000" + SnapshotName.InProgressSuffix);
        Directory.CreateDirectory(inProgress);
        File.WriteAllBytes(Path.Combine(inProgress, "partial.bin"), new byte[500_000]);

        var measured = VersionsUsage.Measure(dir);
        Assert.NotNull(measured);
        Assert.InRange(measured!.Value, 100_000, 120_000); // non 200 000 (un solo file fisico), non 1 MB
        Assert.Contains("KB", VersionsUsage.Describe(measured));
        Assert.Equal(CoreLoc.S("Space_Unknown"), VersionsUsage.Describe(null));
        // Nessuna versione datata = niente da misurare: si dice «n/d», non «0».
        Assert.Null(VersionsUsage.Measure(Path.Combine(dir, "RoboKeep-config")));
    }

    [Fact]
    public void FreeSpaceReader_AnswersForAFolder_NotOnlyForTheRoot()
    {
        var dir = Path.Combine(_root, "libero");
        Directory.CreateDirectory(dir);
        var atFolder = FreeSpaceReader.Read(dir);
        Assert.NotNull(atFolder);
        Assert.True(atFolder > 0);

        // Destinazione non ancora creata: la chiamata sulla cartella fallisce e si ripiega sulla radice.
        Assert.NotNull(FreeSpaceReader.Read(Path.Combine(dir, "non", "ancora", "esistente")));
        Assert.Null(FreeSpaceReader.Read(""));
        Assert.Null(FreeSpaceReader.Read(null));
    }

    /// <summary>Finto robocopy che stampa la riga «spazio su disco insufficiente» e esce con
    /// l'errore grave: un disco pieno vero non si puo' fabbricare in una prova.</summary>
    private string FakeRobocopyDiskFull()
    {
        var path = Path.Combine(_root, "fake-robocopy-full.cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            "echo 2026/09/27 21:30:00 ERRORE 112 (0x00000070) Copia del file in corso E:\\B\\f.dat\r\n" +
            "exit /b 8\r\n");
        return path;
    }

    private sealed class SyncProgress : IProgress<string>
    {
        private readonly Action<string> _action;
        public SyncProgress(Action<string> action) => _action = action;
        public void Report(string value) { lock (_action) _action(value); }
    }
}
