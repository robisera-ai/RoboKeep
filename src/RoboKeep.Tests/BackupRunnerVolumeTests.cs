using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class BackupRunnerVolumeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");

    public BackupRunnerVolumeTests()
    {
        Directory.CreateDirectory(Src);
        Directory.CreateDirectory(Dst);
        File.WriteAllText(Path.Combine(Src, "a.txt"), "contenuto");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private BackupRunner NewRunner(AppConfig config)
    {
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        var creds = new CredentialService(config.Settings.CredentialScope);
        return new BackupRunner(config, new RobocopyRunner(), new LogService(config.Settings),
            new EmailService(creds), creds);
    }

    [Fact]
    public async Task WrongDisk_SkipsWithoutRunningRobocopy()
    {
        var config = new AppConfig();
        var job = new BackupJob
        {
            Name = "T", Source = Src, Destination = Dst,
            // Identificativo palesemente diverso da quello del volume vero: disco sbagliato.
            DestinationVolumeId = @"\\?\Volume{deadbeef-0000-0000-0000-000000000000}\",
            DestinationVolumeLabel = "ALTRO-DISCO",
        };
        config.Jobs.Add(job);

        var result = await NewRunner(config).RunJobAsync(job);

        Assert.True(result.Skipped);
        Assert.False(result.Success);
        // La prova che robocopy non e' mai partito: la destinazione e' rimasta intatta.
        Assert.Empty(Directory.GetFiles(Dst));
    }

    [Fact]
    public async Task RightDisk_RunsNormally()
    {
        var config = new AppConfig();
        var expected = VolumeIdentity.ForPath(Dst);
        Assert.NotNull(expected); // la temp e' su un volume locale identificabile
        var job = new BackupJob
        {
            Name = "T", Source = Src, Destination = Dst,
            DestinationVolumeId = expected!.VolumeId,
            DestinationVolumeLabel = expected.Label,
        };
        config.Jobs.Add(job);

        var result = await NewRunner(config).RunJobAsync(job);

        Assert.False(result.Skipped);
        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(Dst, "a.txt")));
    }

    /// <summary>Prima lettera di unita' non montata sulla macchina, per simulare il disco
    /// staccato senza dipendere da come e' partizionato il PC che esegue i test.</summary>
    private static string? FreeDriveLetter()
    {
        var inUso = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        foreach (var c in "XYWVUT")
            if (!inUso.Contains(c)) return $"{c}:\\";
        return null;
    }

    [Fact]
    public async Task DiskAbsent_SkipsWithoutRunningRobocopy()
    {
        var libera = FreeDriveLetter();
        // Macchina con tutte le lettere occupate: non c'e' modo di simulare il disco staccato,
        // il caso resta coperto dagli unit test puri di VolumeGuard.
        if (libera is null) return;

        var config = new AppConfig();
        var destAssente = Path.Combine(libera!, "backup");
        // La destinazione non e' identificabile: il disco non e' collegato affatto.
        Assert.Null(VolumeIdentity.ForPath(destAssente));
        var job = new BackupJob
        {
            Name = "T", Source = Src, Destination = destAssente,
            DestinationVolumeId = @"\\?\Volume{deadbeef-0000-0000-0000-000000000000}\",
            DestinationVolumeLabel = "DISCO-STACCATO",
        };
        config.Jobs.Add(job);

        var result = await NewRunner(config).RunJobAsync(job);

        Assert.True(result.Skipped);
        Assert.False(result.Success);
        // La prova che robocopy non e' mai partito: non ha creato la cartella di destinazione.
        Assert.False(Directory.Exists(destAssente));
    }

    [Fact]
    public async Task NoVolumeExpectation_RunsNormally()
    {
        var config = new AppConfig();
        var job = new BackupJob { Name = "T", Source = Src, Destination = Dst }; // nessun volume atteso
        config.Jobs.Add(job);

        var result = await NewRunner(config).RunJobAsync(job);

        Assert.False(result.Skipped);
        Assert.True(File.Exists(Path.Combine(Dst, "a.txt")));
    }
}
