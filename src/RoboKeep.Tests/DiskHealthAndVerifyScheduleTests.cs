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
        var recap = lines.FindIndex(l => l.StartsWith("======"));
        Assert.True(lines.FindLastIndex(l => l.Contains("7 ") && l.Contains("SMART")) > recap);
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

    private BackupRunner NewRunner(RunHistoryStore? history, Func<string?, DiskEventSummary> diskEvents)
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        var creds = new CredentialService(config.Settings.CredentialScope);
        return new BackupRunner(config, new RobocopyRunner(detectMedia: _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds,
            history: history, diskEvents: diskEvents);
    }

    private sealed class Collect : IProgress<string>
    {
        private readonly List<string> _lines;
        public Collect(List<string> lines) => _lines = lines;
        public void Report(string value) { lock (_lines) _lines.Add(value); }
    }
}
