using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

/// <summary>
/// Test di integrazione che eseguono il robocopy REALE di sistema su cartelle temporanee,
/// verificando il comportamento descritto dall'utente: skip degli uguali, sovrascrittura dei
/// più recenti, cancellazione in mirror e nessuna modifica in dry-run.
/// </summary>
public sealed class RobocopyRunnerIntegrationTests : IDisposable
{
    private readonly string _base;
    private readonly string _src;
    private readonly string _dst;

    public RobocopyRunnerIntegrationTests()
    {
        _base = Path.Combine(Path.GetTempPath(), "RbcInt_" + Guid.NewGuid().ToString("N"));
        _src = Path.Combine(_base, "src");
        _dst = Path.Combine(_base, "dst");
        Directory.CreateDirectory(Path.Combine(_src, "sub"));
        Directory.CreateDirectory(_dst);
        File.WriteAllText(Path.Combine(_src, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(_src, "b.txt"), "bravo");
        File.WriteAllText(Path.Combine(_src, "sub", "c.txt"), "charlie");
    }

    public void Dispose()
    {
        if (Directory.Exists(_base))
            Directory.Delete(_base, recursive: true);
    }

    private BackupJob Job(bool mirror) => new()
    {
        Name = "Int",
        Source = _src,
        Destination = _dst,
        Mirror = mirror,
        MultiThread = 1,
        Retries = 0,
        Wait = 0,
    };

    private int DstFileCount() => Directory.GetFiles(_dst, "*", SearchOption.AllDirectories).Length;

    [Fact]
    public async Task DryRun_DoesNotModifyDestination()
    {
        var runner = new RobocopyRunner();
        var run = await runner.RunAsync(Job(mirror: true), dryRun: true);

        Assert.True(run.Result.DryRun);
        Assert.True(run.Result.Success); // exit < 8
        Assert.Equal(0, DstFileCount()); // nessun file copiato realmente
    }

    [Fact]
    public async Task Mirror_CopiesAll_ThenSkipsUnchanged()
    {
        var runner = new RobocopyRunner();

        var first = await runner.RunAsync(Job(mirror: true));
        Assert.True(first.Result.Success);
        Assert.Equal(3, DstFileCount());
        Assert.Equal("alpha", File.ReadAllText(Path.Combine(_dst, "a.txt")));
        Assert.True(first.Result.FilesCopied >= 3);

        // Seconda esecuzione senza modifiche: tutto saltato, niente copiato.
        var second = await runner.RunAsync(Job(mirror: true));
        Assert.True(second.Result.Success);
        Assert.Equal(0, second.Result.FilesCopied);
        Assert.True(second.Result.FilesSkipped >= 3);
    }

    [Fact]
    public async Task Mirror_OverwritesNewerSource()
    {
        var runner = new RobocopyRunner();
        await runner.RunAsync(Job(mirror: true));

        // Modifica la sorgente e rendila più recente.
        var srcA = Path.Combine(_src, "a.txt");
        File.WriteAllText(srcA, "alpha-modificato");
        File.SetLastWriteTime(srcA, DateTime.Now.AddMinutes(5));

        var run = await runner.RunAsync(Job(mirror: true));
        Assert.True(run.Result.Success);
        Assert.Equal("alpha-modificato", File.ReadAllText(Path.Combine(_dst, "a.txt")));
    }

    [Fact]
    public async Task Mirror_RemovesFilesDeletedInSource()
    {
        var runner = new RobocopyRunner();
        await runner.RunAsync(Job(mirror: true));
        Assert.True(File.Exists(Path.Combine(_dst, "b.txt")));

        // Elimina un file in sorgente: in mirror deve sparire anche in destinazione.
        File.Delete(Path.Combine(_src, "b.txt"));
        var run = await runner.RunAsync(Job(mirror: true));

        Assert.True(run.Result.Success);
        Assert.False(File.Exists(Path.Combine(_dst, "b.txt")));
        Assert.Equal(2, DstFileCount());
    }

    [Fact]
    public async Task BackupRunner_AppendsItalianRecapToLog()
    {
        var prevDefault = System.Globalization.CultureInfo.DefaultThreadCurrentUICulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("it");
        try
        {
            var config = new RobocopySW.Core.Models.AppConfig
            {
                Settings = new RobocopySW.Core.Models.AppSettings
                {
                    LogRoot = Path.Combine(_base, "logs"),
                    TempRoot = Path.Combine(_base, "temp"),
                    CompressLogs = false,
                },
            };
            var creds = new CredentialService();
            var runner = new BackupRunner(config, new RobocopyRunner(), new LogService(config.Settings), new EmailService(creds), creds);

            var result = await runner.RunJobAsync(Job(mirror: true));

            Assert.NotNull(result.LogPath);
            var logText = File.ReadAllText(result.LogPath!);
            Assert.Contains("RIEPILOGO", logText);
            Assert.Contains("File invariati", logText);
        }
        finally
        {
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = prevDefault;
        }
    }

    [Fact]
    public async Task RunAsync_StreamsOutputLines_ViaProgress()
    {
        var runner = new RobocopyRunner();
        var lines = new List<string>();
        var progress = new Progress<string>(l => { lock (lines) lines.Add(l); });

        var run = await runner.RunAsync(Job(mirror: true), dryRun: false, progress);

        Assert.True(run.Result.Success);
        // L'output deve essere stato trasmesso riga per riga (non solo a fine processo come blocco unico).
        lock (lines)
            Assert.NotEmpty(lines);
    }

    [Fact]
    public async Task NonMirror_DoesNotRemoveFilesDeletedInSource()
    {
        var runner = new RobocopyRunner();
        await runner.RunAsync(Job(mirror: false));
        Assert.True(File.Exists(Path.Combine(_dst, "b.txt")));

        // Senza mirror, l'eliminazione in sorgente NON cancella in destinazione.
        File.Delete(Path.Combine(_src, "b.txt"));
        var run = await runner.RunAsync(Job(mirror: false));

        Assert.True(run.Result.Success);
        Assert.True(File.Exists(Path.Combine(_dst, "b.txt")));
    }

    [Fact]
    public async Task ForceCopy_Simple_ReCopiesOtherwiseSkippedFile()
    {
        var hashPath = Path.Combine(_base, "hashes.json");
        var planner = new ForceCopyPlanner(new ForceCopyHashStore(hashPath));
        var runner = new RobocopyRunner(null, planner);

        var job = Job(mirror: true);
        job.ForceCopyFiles = new() { "a.txt" }; // modalità semplice (ForceCopySmart = false)

        // Primo backup: copia tutto.
        var first = await runner.RunAsync(job);
        Assert.True(first.Result.Success);

        // Secondo backup: la passata normale salterebbe a.txt (identico),
        // ma la passata forza copia lo ricopia comunque.
        var second = await runner.RunAsync(job);
        Assert.True(second.Result.Success);
        Assert.True(second.Result.FilesCopied >= 1);
    }
}
