using RoboKeep.Core;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Un job che va storto non deve portarsi dietro gli altri, e due run dello stesso job non devono
/// mai lavorare insieme sulla stessa destinazione.
/// </summary>
public class BackupRunnerRobustnessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcRobust_" + Guid.NewGuid().ToString("N"));
    private string Locks => Path.Combine(_root, "locks");

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private (BackupRunner Runner, RunHistoryStore History) NewRunner(AppConfig config)
    {
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.ConfigCopyToDestination = false;
        var creds = new CredentialService(config.Settings.CredentialScope);
        var history = new RunHistoryStore(Path.Combine(_root, "history.json"));
        var runner = new BackupRunner(config, new RobocopyRunner(), new LogService(config.Settings),
            new EmailService(creds), creds,
            results: new LastResultStore(Path.Combine(_root, "results.json")),
            lockFolder: Locks, history: history);
        return (runner, history);
    }

    private BackupJob NewJob(string name)
    {
        var src = Path.Combine(_root, name + "-src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "contenuto");
        return new BackupJob
        {
            Name = name, Source = src, Destination = Path.Combine(_root, name + "-dst"),
            Retries = 0, Wait = 0, MultiThread = 0,
        };
    }

    [Fact]
    public async Task RunAll_AJobThatThrows_IsRecorded_AndTheNextOneStillRuns()
    {
        var config = new AppConfig();
        config.Credentials.Add(new CredentialEntry
        {
            Id = "rotta", Host = @"\\nessuno\share", User = "u", PasswordProtected = "non e' una password cifrata",
        });
        var broken = NewJob("Rotto");
        broken.CredentialId = "rotta";
        var good = NewJob("Buono");
        config.Jobs.Add(broken);
        config.Jobs.Add(good);
        var (runner, history) = NewRunner(config);

        var results = await runner.RunAllAsync();

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Success);
        Assert.Equal(CoreLoc.S("Run_UnexpectedStatus"), results[0].Status);
        Assert.True(results[1].Success);
        Assert.True(File.Exists(Path.Combine(good.Destination, "a.txt")));

        var entry = Assert.Single(history.List("Rotto"));
        Assert.False(entry.Success);
        Assert.NotNull(entry.LogPath);
        Assert.True(File.Exists(entry.LogPath));
    }

    [Fact]
    public async Task SameJobAlreadyRunning_DoesNotTouchTheDestination()
    {
        var config = new AppConfig();
        var job = NewJob("T");
        config.Jobs.Add(job);
        var (runner, history) = NewRunner(config);

        using (JobLockFile.TryAcquire(Locks, "T"))
        {
            var result = await runner.RunJobAsync(job);

            Assert.True(result.AlreadyRunning);
            Assert.True(result.Skipped);
            Assert.False(Directory.Exists(job.Destination));
            Assert.Empty(history.List("T"));
        }

        Assert.True((await runner.RunJobAsync(job)).Success);
    }

    [Fact]
    public async Task TwoJobsWritingIntoTheSameDestination_TheOtherIsRefused_AndWritesNothing()
    {
        // Un job duplicato (per avere un secondo orario) con un'altra sorgente: il suo mirror
        // cancellerebbe i file dell'altro, o li spingerebbe nelle versioni.
        var config = new AppConfig();
        var first = NewJob("Primo");
        var second = NewJob("Secondo");
        second.Destination = first.Destination;
        config.Jobs.Add(first);
        config.Jobs.Add(second);
        var (runner, _) = NewRunner(config);

        var result = await runner.RunJobAsync(second);

        Assert.False(result.Success);
        Assert.True(result.NotStarted);
        Assert.False(Directory.Exists(first.Destination));
    }

    [Fact]
    public async Task TheSameFolderOnAnotherDisk_IsNotRefusedAsAClash()
    {
        // Rotazione dei dischi: stessa cartella, dischi diversi. Il job non va rifiutato come
        // «destinazione in comune»: se il suo disco non c'e', lo salta il controllo del disco.
        var config = new AppConfig();
        var samsung = NewJob("Samsung");
        samsung.DestinationVolumeId = @"\\?\Volume{aaaaaaaa-0000-0000-0000-000000000000}\";
        var wd = NewJob("WD");
        wd.Destination = samsung.Destination;
        wd.DestinationVolumeId = @"\\?\Volume{bbbbbbbb-0000-0000-0000-000000000000}\";
        config.Jobs.Add(samsung);
        config.Jobs.Add(wd);
        var (runner, _) = NewRunner(config);

        var result = await runner.RunJobAsync(wd);

        Assert.True(result.Skipped, "rifiutato come destinazione in comune invece di essere saltato per il disco");
    }

    [Fact]
    public async Task ADestinationInsideAnotherJobsDestination_IsRefused()
    {
        // Un mirror su E:\Backup cancellerebbe come "extra" la cartella E:\Backup\Foto dell'altro job.
        var config = new AppConfig();
        var outer = NewJob("Fuori");
        var inner = NewJob("Dentro");
        inner.Destination = Path.Combine(outer.Destination, "sotto");
        config.Jobs.Add(outer);
        config.Jobs.Add(inner);
        var (runner, _) = NewRunner(config);

        var result = await runner.RunJobAsync(inner);

        Assert.False(result.Success);
        Assert.True(result.NotStarted);
        Assert.False(Directory.Exists(inner.Destination));
    }

    [Fact]
    public async Task DestinationBusy_SkipsTheRun_AndTheNextRunIsNotBlocked()
    {
        // Un altro run lavora sulla stessa cartella (un job rinominato mentre la sua attivita' gira).
        // Il run si salta, e il lock per nome preso nel frattempo va rilasciato: altrimenti il job
        // risulterebbe «gia' in esecuzione» per sempre.
        var config = new AppConfig();
        var job = NewJob("D");
        config.Jobs.Add(job);
        var (runner, _) = NewRunner(config);

        using (JobLockFile.TryAcquireDestination(Locks, job.Destination))
        {
            var result = await runner.RunJobAsync(job);

            Assert.True(result.Skipped);
            Assert.True(result.AlreadyRunning);
            Assert.False(Directory.Exists(job.Destination));
        }

        Assert.True((await runner.RunJobAsync(job)).Success);
    }
}
