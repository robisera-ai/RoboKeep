using System.ComponentModel;
using System.Diagnostics;
using RoboKeep.Core;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Sicurezza del supporto fisico: arresto al primo errore hardware, disco "a riposo" per i job
/// successivi, tetto ai thread sui dischi meccanici, PC tenuto sveglio durante il lavoro.
/// Un disco rotto non si puo' simulare: al posto di robocopy gira uno script che ne imita la
/// riga di errore e poi resta appeso, per provare che viene fermato subito.
/// </summary>
public sealed class HardwareSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcHw_" + Guid.NewGuid().ToString("N"));
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");

    public HardwareSafetyTests()
    {
        Directory.CreateDirectory(Src);
        Directory.CreateDirectory(Dst);
        File.WriteAllText(Path.Combine(Src, "a.txt"), "contenuto");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    /// <summary>Finto robocopy: stampa una riga di errore CRC come quella vera, poi "lavora" per 30 s.</summary>
    private string FakeRobocopyWithCrcError()
    {
        var path = Path.Combine(_root, "fake-robocopy.cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            "echo 2026/09/20 13:00:00 ERRORE 23 (0x00000017) Copia del file in corso C:\\dati\\f.dat\r\n" +
            "ping -n 30 127.0.0.1 >nul\r\n" +
            "echo FINE-NON-RAGGIUNTA\r\n");
        return path;
    }

    /// <summary>Finto robocopy che lavora un po' (stampa righe) e poi resta appeso: serve a provare
    /// l'annullamento a meta'.</summary>
    private string FakeRobocopyThatHangs()
    {
        var path = Path.Combine(_root, "fake-robocopy-hang.cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            "echo     Nuovo file  		      12	primo.txt\r\n" +
            "echo     Nuovo file  		      34	secondo.txt\r\n" +
            "ping -n 30 127.0.0.1 >nul\r\n" +
            "echo FINE-NON-RAGGIUNTA\r\n");
        return path;
    }

    [Fact]
    public async Task Cancelled_Job_StillWritesALog_AndAHistoryEntry()
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.CompressLogs = false;
        var creds = new CredentialService(config.Settings.CredentialScope);
        var history = new RunHistoryStore(Path.Combine(_root, "history.json"));
        var runner = new BackupRunner(config,
            new RobocopyRunner(FakeRobocopyThatHangs(), detectMedia: _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds, history: history);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // annulla a lavoro iniziato
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunJobAsync(Job(), ct: cts.Token));

        // Il log c'e', contiene quel che era stato fatto e dice che e' stato annullato.
        var log = Assert.Single(Directory.GetFiles(Path.Combine(_root, "logs"), "*.log", SearchOption.AllDirectories));
        var text = File.ReadAllText(log);
        Assert.Contains("primo.txt", text);
        Assert.Contains("secondo.txt", text);
        Assert.DoesNotContain("FINE-NON-RAGGIUNTA", text);
        Assert.Contains(CoreLoc.S("Run_CancelledRecap").Split('{')[0].Trim(), text);

        // E la cronologia ha una voce "annullato" che lo apre.
        var entry = Assert.Single(history.List("T"));
        Assert.True(entry.IsCancelled);
        Assert.False(entry.Success);
        Assert.Equal(log, entry.LogPath);
    }

    private BackupJob Job(string name = "T") => new()
    {
        Name = name, Source = Src, Destination = Dst, MultiThread = 8, Retries = 0, Wait = 0,
    };

    // ---- DiskError ----

    [Fact]
    public void InnerWin32Exception_IsUnreadable()
    {
        // FileSystemDelete incapsula cosi' i suoi errori: la causa non deve andare persa.
        var ex = new IOException("Cancellazione fallita", new Win32Exception(1117));
        Assert.True(DiskError.IsUnreadable(ex));
        Assert.False(DiskError.IsUnreadable(new IOException("x", new Win32Exception(5)))); // accesso negato
    }

    [Theory]
    [InlineData("2026/09/20 13:00:00 ERRORE 23 (0x00000017) Copia del file in corso D:\\x\\f.dat")]
    [InlineData("2026/09/20 13:00:00 ERROR 1117 (0x0000045D) Copying File D:\\x\\f.dat")]
    [InlineData("2026/09/20 13:00:00 FEHLER 27 (0x0000001B) Datei wird kopiert D:\\x\\f.dat")]
    public void RobocopyHardwareErrorLine_IsRecognized_InAnyLanguage(string line)
    {
        Assert.True(DiskError.IsRobocopyHardwareErrorLine(line, out var detail));
        Assert.Contains(@"D:\x\f.dat", detail);
    }

    [Theory]
    [InlineData("2026/09/20 13:00:00 ERRORE 5 (0x00000005) Copia del file in corso D:\\x\\f.dat")]  // accesso negato
    [InlineData("2026/09/20 13:00:00 ERRORE 32 (0x00000020) Copia del file in corso D:\\x\\f.dat")] // file in uso
    [InlineData("2026/09/20 13:00:00 ERRORE 23 (0x00000005) codice decimale ed esadecimale discordi")]
    [InlineData("	    Nuovo file  		      23	report (0x00000017).txt")] // file di 23 byte dal nome bizzarro
    [InlineData("	    Nuovo file  		      23	report.txt")]
    [InlineData("")]
    public void OtherLines_AreNotHardwareErrors(string line)
        => Assert.False(DiskError.IsRobocopyHardwareErrorLine(line, out _));

    // ---- Arresto di robocopy ----

    [Fact]
    public async Task HardwareErrorLine_KillsRobocopyImmediately()
    {
        var runner = new RobocopyRunner(FakeRobocopyWithCrcError(), detectMedia: _ => DiskMedia.Unknown);

        var sw = Stopwatch.StartNew();
        var run = await runner.RunAsync(Job());
        sw.Stop();

        Assert.True(run.Result.HardwareError);
        Assert.False(run.Result.Success);
        Assert.Equal(RobocopyRunner.HardwareFailureExitCode, run.Result.ExitCode);
        Assert.Contains("Win32 23", run.Result.HardwareErrorDetail);
        Assert.DoesNotContain("FINE-NON-RAGGIUNTA", run.Output);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"robocopy non fermato subito: {sw.Elapsed}");
    }

    [Fact]
    public async Task AfterHardwareError_NextJobOnSameDisk_IsNotRun()
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        var creds = new CredentialService(config.Settings.CredentialScope);
        var runner = new BackupRunner(config,
            new RobocopyRunner(FakeRobocopyWithCrcError(), detectMedia: _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds);

        var lines = new List<string>();
        var progress = new SyncProgress(lines.Add);

        var first = await runner.RunJobAsync(Job("Primo"), progress: progress);
        Assert.True(first.HardwareError);
        Assert.False(first.Skipped); // e' un fallimento, non un salto neutro
        Assert.Contains(lines, l => l.Contains("CrystalDiskInfo")); // il consiglio azionabile e' nel log

        var sw = Stopwatch.StartNew();
        var second = await runner.RunJobAsync(Job("Secondo"), progress: progress);
        sw.Stop();

        Assert.True(second.HardwareError);
        Assert.False(second.Success);
        Assert.True(second.NotStarted);
        Assert.Null(second.LogPath); // niente log: il disco non e' stato toccato
        // Il finto robocopy impiegherebbe secondi anche solo per partire ed essere ucciso: un
        // ritorno immediato prova che il disco non e' stato piu' toccato.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"il secondo job e' partito: {sw.Elapsed}");

        // E non solo il secondo: un --run-all notturno con cinque job sullo stesso disco li salta
        // tutti, non e' il primo salto a "consumare" il riposo del disco.
        var third = await runner.RunJobAsync(Job("Terzo"), progress: progress);
        Assert.True(third.NotStarted);
        Assert.Null(third.LogPath);
    }

    [Fact]
    public async Task HardwareError_RestsTheDisk_AcrossRunners_AndIsPersistedInLastResult()
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        var creds = new CredentialService(config.Settings.CredentialScope);
        var results = new LastResultStore(Path.Combine(_root, "lastresults.json"));
        var faulted = new FaultedDiskStore(Path.Combine(_root, "faulted-disks.json"));
        BackupRunner NewRunner() => new(config,
            new RobocopyRunner(FakeRobocopyWithCrcError(), detectMedia: _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds, results, faultedDisks: faulted);

        var first = await NewRunner().RunJobAsync(Job("Primo"));
        Assert.True(first.HardwareError);
        var saved = results.Load()["Primo"];
        Assert.True(saved.HardwareError);                       // la UI lo sapra' anche domani
        Assert.Contains("Win32 23", saved.HardwareErrorDetail);
        var entry = Assert.Single(faulted.Load(DateTime.Now));  // il volume della temp e' a riposo
        // Email disabilitata: nessuno e' stato avvisato, e la voce non deve sostenere il contrario.
        Assert.False(entry.Notified);

        // Un runner NUOVO (= un altro processo, es. l'attivita' pianificata) non tocca il disco.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var second = await NewRunner().RunJobAsync(Job("Secondo"));
        Assert.True(second.HardwareError);
        Assert.True(second.NotStarted);
        Assert.Equal(CoreLoc.S("Hw_NotStartedStatus"), second.Status); // non "INTERROTTO": non e' partito
        Assert.True(results.Load()["Secondo"].HardwareError);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Null(second.LogPath);                            // niente log: il disco non e' stato toccato
        // Email ancora disabilitata: il tentativo di avvisare non e' andato a buon fine, quindi
        // l'episodio resta da raccontare (la prossima volta che l'email funzionera').
        Assert.False(Assert.Single(faulted.Load(DateTime.Now)).Notified);

        faulted.Clear();                                        // "ho controllato il disco"
        var third = await NewRunner().RunJobAsync(Job("Terzo")); // riparte (e rifallisce sul finto robocopy)
        Assert.True(third.HardwareError);
        Assert.False(third.NotStarted);
        Assert.NotNull(third.LogPath);                          // e' partito davvero: ha il suo log
        Assert.Single(faulted.Load(DateTime.Now));              // e il disco e' tornato a riposo
    }

    // ---- Tetto ai thread ----

    [Theory]
    [InlineData(true, false, null, DiskMedia.Hdd)]    // il disco dichiara la penalita': meccanico
    [InlineData(false, false, null, DiskMedia.Ssd)]
    [InlineData(null, true, true, DiskMedia.Ssd)]     // bridge USB muto, ma TRIM: SSD
    [InlineData(null, true, false, DiskMedia.Hdd)]    // bridge USB muto, niente TRIM: prudenza
    [InlineData(null, true, null, DiskMedia.Hdd)]
    [InlineData(null, false, null, DiskMedia.Unknown)]
    public void Classify_DecidesFromWhatTheDeviceDeclares(bool? seek, bool usb, bool? trim, DiskMedia expected)
        => Assert.Equal(expected, StorageProbe.Classify(seek, usb, trim));

    [Fact]
    public void ThreadCap_AppliesWhenEitherSideIsMechanical()
    {
        Assert.Equal(StorageProbe.HddMaxThreads, StorageProbe.ThreadCap(DiskMedia.Ssd, DiskMedia.Hdd));
        Assert.Equal(StorageProbe.HddMaxThreads, StorageProbe.ThreadCap(DiskMedia.Hdd, DiskMedia.Unknown));
        Assert.Null(StorageProbe.ThreadCap(DiskMedia.Ssd, DiskMedia.Unknown));
    }

    [Fact]
    public void Detect_IsBestEffort_NeverThrows()
    {
        Assert.Equal(DiskMedia.Unknown, StorageProbe.Detect(null));
        Assert.Equal(DiskMedia.Unknown, StorageProbe.Detect(@"\\server\share\cartella"));
        _ = StorageProbe.Detect(_root); // disco reale della macchina: qualunque esito, nessuna eccezione
    }

    [Fact]
    public void ArgsBuilder_CapsThreads_ButNeverRaisesThem()
    {
        var job = Job();
        Assert.Contains("/MT:2", RobocopyArgsBuilder.Build(job, maxThreads: 2));
        Assert.Contains("/MT:8", RobocopyArgsBuilder.Build(job));
        job.MultiThread = 1;
        Assert.Contains("/MT:1", RobocopyArgsBuilder.Build(job, maxThreads: 2));
        job.MultiThread = 8;
        job.ForceCopyFiles = new() { "*.pst" };
        Assert.Contains("/MT:2", RobocopyArgsBuilder.BuildForceCopyPass(job, job.ForceCopyFiles, maxThreads: 2));
    }

    [Fact]
    public async Task MechanicalDisk_CapsRealRobocopyThreads()
    {
        var lines = new List<string>();
        var runner = new RobocopyRunner(detectMedia: _ => DiskMedia.Hdd);

        var run = await runner.RunAsync(Job(), progress: new SyncProgress(lines.Add));

        Assert.True(run.Result.Success);
        Assert.Contains("/MT:2", run.Output);      // l'intestazione di robocopy elenca le opzioni ricevute
        Assert.DoesNotContain("/MT:8", run.Output);
        Assert.Contains(lines, l => l.Contains(" 8 ") && l.Contains(" 2 ")); // avviso "da 8 a 2" a video...
        Assert.StartsWith(lines.First(l => l.Contains(" 8 ") && l.Contains(" 2 ")), run.Output); // ...e nel log salvato
    }

    [Fact]
    public async Task ThreadCapNote_IsRepeatedAfterTheRecap_WhereTheUserActuallyLooks()
    {
        // In cima al log l'avviso finisce sepolto sotto migliaia di righe di robocopy, e la
        // finestra scorre sempre alla fine: va ripetuto dopo il riepilogo.
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        var creds = new CredentialService(config.Settings.CredentialScope);
        var runner = new BackupRunner(config, new RobocopyRunner(detectMedia: _ => DiskMedia.Hdd),
            new LogService(config.Settings), new EmailService(creds), creds);

        var lines = new List<string>();
        var result = await runner.RunJobAsync(Job(), progress: new SyncProgress(lines.Add));

        Assert.True(result.Success);
        Assert.NotNull(result.ThreadCapNote);
        var recapStart = lines.FindIndex(l => l.StartsWith("======"));
        Assert.True(recapStart >= 0);
        Assert.True(lines.LastIndexOf(result.ThreadCapNote!) > recapStart);
    }

    // ---- PC sveglio ----

    [Fact]
    public void SleepBlocker_IsActiveUntilDisposed()
    {
        var blocker = SleepBlocker.Acquire("RoboKeep: test");
        Assert.True(blocker.IsActive);
        blocker.Dispose();
        Assert.False(blocker.IsActive);
        blocker.Dispose(); // idempotente
    }

    private sealed class SyncProgress : IProgress<string>
    {
        private readonly Action<string> _action;
        public SyncProgress(Action<string> action) => _action = action;
        public void Report(string value) { lock (_action) _action(value); }
    }
}
