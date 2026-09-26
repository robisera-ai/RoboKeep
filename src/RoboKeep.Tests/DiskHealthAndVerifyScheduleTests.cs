using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>Controllo salute dal registro eventi di Windows e cadenza della verifica integrità.</summary>
public sealed class DiskHealthAndVerifyScheduleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcHealth_" + Guid.NewGuid().ToString("N"));
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");

    public DiskHealthAndVerifyScheduleTests()
    {
        Directory.CreateDirectory(Src);
        Directory.CreateDirectory(Dst);
        File.WriteAllText(Path.Combine(Src, "a.txt"), "contenuto");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    // ---- registro eventi ----

    [Fact]
    public void DiskEvents_AreMatchedByDiskNumber()
    {
        const string msg = @"Rilevato blocco danneggiato sul dispositivo \Device\Harddisk1\DR7.";
        Assert.True(DiskEventLog.Concerns("disk", msg, diskNumber: 1, driveLetter: 'E'));
        Assert.False(DiskEventLog.Concerns("disk", msg, diskNumber: 0, driveLetter: 'E'));
        Assert.False(DiskEventLog.Concerns("disk", msg, diskNumber: null, driveLetter: 'E'));
        // Harddisk1 non deve combaciare con Harddisk10 e viceversa.
        Assert.False(DiskEventLog.Concerns("disk", @"errore su \Device\Harddisk10\DR2.", 1, 'E'));
    }

    [Fact]
    public void NtfsEvents_AreMatchedByDriveLetter()
    {
        const string msg = @"{Scrittura rimandata non riuscita} Impossibile salvare tutti i dati del file E:\$Mft.";
        Assert.True(DiskEventLog.Concerns("Ntfs", msg, diskNumber: 1, driveLetter: 'e'));
        Assert.False(DiskEventLog.Concerns("Ntfs", msg, diskNumber: 1, driveLetter: 'D'));
        // "VOLUME:" finisce per E: ma non e' l'unita' E.
        Assert.False(DiskEventLog.Concerns("Ntfs", "errore nel VOLUME: C:", 1, 'E'));
    }

    [Fact]
    public void Summarize_GroupsByKind_AndIgnoresUnrelatedIds()
    {
        var t = new DateTime(2026, 7, 19, 16, 32, 0);
        var s = DiskEventLog.Summarize(new[]
        {
            ("disk", 7, t), ("disk", 7, t.AddHours(1)), ("disk", 52, t),      // blocchi danneggiati / guasto previsto
            ("disk", 51, t), ("disk", 153, t), ("disk", 11, t),                // I/O
            ("Ntfs", 50, t.AddHours(5)), ("Microsoft-Windows-Ntfs", 140, t),   // file system
            ("disk", 158, t), ("volmgr", 162, t),                              // non pertinenti
        });

        Assert.Equal(3, s.BadBlocks);
        Assert.Equal(3, s.IoErrors);
        Assert.Equal(2, s.FileSystemErrors);
        Assert.Equal(8, s.Total);
        Assert.Equal(t.AddHours(5), s.Latest);
    }

    [Fact]
    public void Collect_IsBestEffort_NeverThrows()
    {
        Assert.Equal(0, DiskEventLog.Collect(null).Total);
        Assert.Equal(0, DiskEventLog.Collect(@"\\server\share\x").Total);
        _ = DiskEventLog.Collect(_root); // registro reale della macchina: qualunque esito, nessuna eccezione
    }

    [Fact]
    public void Preflight_WarnsAboutRecentDiskErrors_EvenWhenDestinationIsUnreachable()
    {
        var events = new DiskEventSummary(329, 19, 5, new DateTime(2026, 7, 19));
        var w = PreflightChecker.Evaluate(new PreflightInputs(
            DestinationReachable: false, FreeBytes: 0, MinFreeBytes: 0, SourceSizeBytes: null,
            DestinationDiskEvents: events, SourceDiskEvents: DiskEventSummary.None));

        Assert.Contains(w, x => x.MessageKey == "Preflight_DiskEventsDest" && x.Detail.Contains("329"));
        Assert.DoesNotContain(w, x => x.MessageKey == "Preflight_DiskEventsSource");
    }

    [Fact]
    public async Task ScheduledRun_PutsTheDiskWarningInTheRecap()
    {
        // I backup pianificati non passano dal pre-avvio: l'avviso deve stare nel loro log,
        // dopo il riepilogo, senza far fallire il job.
        var lines = new List<string>();
        var result = await NewRunner(history: null,
                diskEvents: _ => new DiskEventSummary(7, 0, 1, new DateTime(2026, 7, 19)))
            .RunJobAsync(new BackupJob { Name = "T", Source = Src, Destination = Dst }, progress: new Collect(lines));

        Assert.True(result.Success);
        Assert.NotEmpty(result.HealthWarnings);
        var recap = lines.FindIndex(l => l.StartsWith("======"));
        Assert.True(lines.FindLastIndex(l => l.Contains("7 ") && l.Contains("SMART")) > recap);
    }

    [Fact]
    public async Task HealthWarnings_GoToEmailOnlyWhenNewerThanLastRun_ButAlwaysToTheLog()
    {
        var results = new LastResultStore(Path.Combine(_root, "lastresults.json"));
        var eventTime = new DateTime(2026, 9, 1, 12, 0, 0); // vecchio evento, sempre lo stesso
        var runner = NewRunner(history: null, diskEvents: _ => new DiskEventSummary(3, 0, 0, eventTime), results);
        var job = new BackupJob { Name = "T", Source = Src, Destination = Dst };

        var lines1 = new List<string>();
        var r1 = await runner.RunJobAsync(job, progress: new Collect(lines1));
        Assert.NotEmpty(r1.HealthWarnings);                          // prima volta: in email
        Assert.Contains(lines1, l => l.Contains("SMART"));

        var lines2 = new List<string>();
        var r2 = await runner.RunJobAsync(job, progress: new Collect(lines2));
        Assert.Empty(r2.HealthWarnings);                             // stesso evento gia' segnalato: niente email
        Assert.Contains(lines2, l => l.Contains("SMART"));           // ma nel log resta
    }

    // ---- verifica periodica ----

    [Theory]
    [InlineData(0, 1, true)]    // 0 = a ogni backup
    [InlineData(7, 6, false)]
    [InlineData(7, 7, true)]
    [InlineData(7, 30, true)]
    public void VerifyIsDue_ByCalendarDays(int everyDays, int daysAgo, bool expected)
    {
        var now = new DateTime(2026, 9, 20, 21, 0, 0);
        // Verifica precedente finita qualche minuto DOPO l'ora di adesso: non deve slittare all'8° giorno.
        var last = now.AddDays(-daysAgo).AddMinutes(40);
        Assert.Equal(expected, VerifySchedule.IsDue(everyDays, last, now));
    }

    [Fact]
    public void VerifyIsDue_WhenNeverVerified() =>
        Assert.True(VerifySchedule.IsDue(7, null, DateTime.Now));

    [Fact]
    public async Task VerifyAfterRun_RunsOnce_ThenWaitsForTheInterval()
    {
        var history = new RunHistoryStore(Path.Combine(_root, "history.json"));
        var job = new BackupJob { Name = "T", Source = Src, Destination = Dst, VerifyAfterRun = true, VerifyEveryDays = 7 };
        var runner = NewRunner(history, _ => DiskEventSummary.None);

        await runner.RunJobAsync(job);
        Assert.Single(history.List("T"), e => e.Kind == RunHistoryEntry.KindVerify);

        var lines = new List<string>();
        await runner.RunJobAsync(job, progress: new Collect(lines));
        Assert.Single(history.List("T"), e => e.Kind == RunHistoryEntry.KindVerify); // nessuna seconda verifica
        Assert.Contains(lines, l => l.Contains('7') && l.StartsWith("[")); // e il log dice quando sara' la prossima

        job.VerifyEveryDays = 0; // a ogni backup: verifica di nuovo
        await runner.RunJobAsync(job);
        Assert.Equal(2, history.List("T").Count(e => e.Kind == RunHistoryEntry.KindVerify));
    }

    [Fact]
    public async Task Verification_GetsItsOwnLog_LinkedToItsHistoryEntry()
    {
        // 60 file: abbastanza da far scattare la riga di avanzamento ("verificati 50/61"), che
        // deve restare solo a video.
        for (var i = 0; i < 60; i++) File.WriteAllText(Path.Combine(Src, $"f{i}.txt"), "x");
        var history = new RunHistoryStore(Path.Combine(_root, "history.json"));
        var job = new BackupJob { Name = "T", Source = Src, Destination = Dst, VerifyAfterRun = true, VerifyEveryDays = 0 };
        var lines = new List<string>();

        await NewRunner(history, _ => DiskEventSummary.None).RunJobAsync(job, progress: new Collect(lines));

        var entries = history.List("T");
        var verify = Assert.Single(entries, e => e.Kind == RunHistoryEntry.KindVerify);
        var backup = Assert.Single(entries, e => e.Kind == RunHistoryEntry.KindBackup);
        Assert.NotNull(verify.LogPath);
        Assert.NotEqual(backup.LogPath, verify.LogPath); // due voci, due log
        // Il suffisso e' localizzato (verifica/verify/...) e altri test, in parallelo, cambiano la
        // lingua del processo: si controlla la FORMA del nome, non la parola.
        Assert.Matches(@"-T-[a-z]+\.log", Path.GetFileName(verify.LogPath!));

        var text = LogArchiveReader.ReadLogText(verify.LogPath)!;
        Assert.Contains("61", text);                     // l'esito: 61 file identici
        Assert.Contains(lines, l => l.Contains("50/61")); // l'avanzamento si vede a video...
        Assert.DoesNotContain("50/61", text);             // ...ma non intasa il file
    }

    [Fact]
    public async Task Verifier_SendsProgressTicksThroughTheTransientChannel()
    {
        // Regressione della release 1.7.0 fallita in CI: le righe di avanzamento venivano
        // riconosciute dal TESTO, che e' localizzato; su un thread con un'altra lingua non
        // combaciavano e finivano nel log. Ora chi le produce le dichiara transitorie.
        for (var i = 0; i < 120; i++) File.WriteAllText(Path.Combine(Src, $"f{i}.txt"), "x");
        var split = new SplitProgress();

        await IntegrityVerifier.VerifyAsync(Src, Dst, null, null, split, CancellationToken.None);

        Assert.Equal(2, split.Transient.Count);                    // 50/121 e 100/121
        Assert.All(split.Transient, l => Assert.Contains("/121", l));
        Assert.DoesNotContain(split.Kept, l => l.Contains("50/121") || l.Contains("100/121"));
    }

    [Fact]
    public void Recorder_KeepsReportedLines_ButOnlyForwardsTransientOnes()
    {
        var shown = new List<string>();
        var rec = new VerifyLogRecorder(new Collect(shown));
        rec.Report("esito");
        rec.ReportTransient("avanzamento");

        var settings = new AppConfig().Settings;
        settings.LogRoot = Path.Combine(_root, "logs");
        settings.TempRoot = Path.Combine(_root, "temp");
        var text = LogArchiveReader.ReadLogText(rec.Save(new LogService(settings), "T", DateTime.Now, null))!;

        Assert.Equal(new[] { "esito", "avanzamento" }, shown); // a video arrivano entrambe
        Assert.Contains("esito", text);
        Assert.DoesNotContain("avanzamento", text);            // nel file solo quella che conta
    }

    private sealed class SplitProgress : ITransientProgress
    {
        public List<string> Kept { get; } = new();
        public List<string> Transient { get; } = new();
        public void Report(string value) { lock (Kept) Kept.Add(value); }
        public void ReportTransient(string value) { lock (Transient) Transient.Add(value); }
    }

    [Fact]
    public async Task NotDueNote_IsWrittenInTheBackupLog()
    {
        var history = new RunHistoryStore(Path.Combine(_root, "history.json"));
        var job = new BackupJob { Name = "T", Source = Src, Destination = Dst, VerifyAfterRun = true, VerifyEveryDays = 7 };
        var runner = NewRunner(history, _ => DiskEventSummary.None);

        await runner.RunJobAsync(job);              // verifica eseguita
        var second = await runner.RunJobAsync(job); // verifica rimandata

        var text = LogArchiveReader.ReadLogText(second.LogPath)!;
        Assert.Contains(DateTime.Now.ToString("d"), text); // "l'ultima è del <oggi>": ora sta anche nel file
    }

    [Fact]
    public void InterruptedVerify_DoesNotCountAsTheLastVerification()
    {
        var interrupted = RunHistoryEntry.ForVerifyInterrupted("T", DateTime.Now, null);
        var completed = RunHistoryEntry.ForVerify("T", DateTime.Now, new VerifyResult(10, 2, 0, 0, 0, new[] { "a", "b" }));

        Assert.False(interrupted.Success);
        Assert.False(interrupted.IsCompletedVerify); // interrotta: la prossima verifica resta dovuta
        Assert.True(completed.IsCompletedVerify);    // completata, anche se ha trovato differenze
    }

    [Fact]
    public void NewJobs_GetSafeDefaults_ButSavedJobsKeepTheirRetention()
    {
        var fresh = new BackupJob();
        Assert.Equal(BackupJob.DefaultSnapshotKeepCount, fresh.SnapshotKeepCount);
        Assert.Equal(BackupJob.DefaultVerifyEveryDays, fresh.VerifyEveryDays);

        // Un job salvato con ritenzione illimitata (0 scritto nel file) resta illimitato: stringerla
        // di nascosto cancellerebbe versioni che l'utente ha scelto di tenere.
        var saved = System.Text.Json.JsonSerializer.Deserialize<BackupJob>("{\"SnapshotKeepCount\":0}")!;
        Assert.Equal(0, saved.SnapshotKeepCount);
    }

    private BackupRunner NewRunner(RunHistoryStore? history, Func<string?, DiskEventSummary> diskEvents,
        LastResultStore? results = null)
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        // La destinazione e' una cartella temporanea: la radice del suo volume e' il disco di sistema
        // della macchina, e una prova su salute e verifiche non deve scrivere in C:\ (vedi ConfigMirror).
        config.Settings.ConfigCopyToDestination = false;
        var creds = new CredentialService(config.Settings.CredentialScope);
        return new BackupRunner(config, new RobocopyRunner(detectMedia: _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds, results,
            history: history, diskEvents: diskEvents);
    }

    private sealed class Collect : IProgress<string>
    {
        private readonly List<string> _lines;
        public Collect(List<string> lines) => _lines = lines;
        public void Report(string value) { lock (_lines) _lines.Add(value); }
    }
}
