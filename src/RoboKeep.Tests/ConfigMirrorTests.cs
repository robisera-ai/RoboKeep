using RoboKeep.Core;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Copia della configurazione nella radice del disco di backup: dove va, cosa contiene e che non
/// possa mai far fallire un backup riuscito. La radice vera di un disco non si tocca: le prove di
/// scrittura usano una cartella temporanea passata a <c>WriteTo</c>, che e' lo stesso punto in cui
/// entra <c>BackupRunner</c>.
/// </summary>
[Collection(CultureCollection.Name)]
public sealed class ConfigMirrorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcCfg_" + Guid.NewGuid().ToString("N"));
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");

    public ConfigMirrorTests()
    {
        Directory.CreateDirectory(Src);
        Directory.CreateDirectory(Dst);
        File.WriteAllText(Path.Combine(Src, "a.txt"), "contenuto");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    // ---- dove va la copia ----

    [Fact]
    public void TargetFolder_IsTheRootOfTheDestinationVolume()
    {
        // La copia sta nella radice, non dentro la cartella del job: nessun mirror la raggiunge.
        Assert.Equal(@"E:\RoboKeep-config", ConfigMirror.TargetFolder(@"E:\Backup\Foto"));
        Assert.Equal(@"E:\RoboKeep-config", ConfigMirror.TargetFolder(@"E:\"));
        // Uno spazio davanti e' uno sbaglio di battitura, non una destinazione diversa.
        Assert.Equal(@"E:\RoboKeep-config", ConfigMirror.TargetFolder("  E:\\  "));
        Assert.Equal(ConfigMirror.FolderName, Path.GetFileName(ConfigMirror.TargetFolder(@"E:\Backup")!));
    }

    [Theory]
    [InlineData("backup")]
    [InlineData(@".\backup")]
    [InlineData(@"\")]
    public void TargetFolder_IsNullForPathsThatAreNotComplete(string destination)
        // Un percorso relativo verrebbe risolto sulla cartella di lavoro del processo, e la copia
        // finirebbe nella radice del disco di SISTEMA: l'unico posto in cui non deve mai andare.
        => Assert.Null(ConfigMirror.TargetFolder(destination));

    [Theory]
    [InlineData(@"\\nas01\backup$\Foto")]
    [InlineData(@"\\nas01\backup$")]
    public void TargetFolder_IsNullForNetworkShares(string destination)
        // Una share non e' il disco che si stacca e si mette nel cassetto: la copia li' non
        // salverebbe nessuno e sporcherebbe la radice di un server.
        => Assert.Null(ConfigMirror.TargetFolder(destination));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TargetFolder_IsNullWhenThereIsNoDestination(string? destination)
        => Assert.Null(ConfigMirror.TargetFolder(destination));

    // ---- cosa scrive ----

    private static AppConfig SampleConfig() => new()
    {
        Settings = { StaleAfterDays = 3, Email = { To = "chi@esempio.it" } },
        Jobs =
        {
            new BackupJob { Name = "Documenti", Source = @"D:\documenti", Destination = @"E:\Backup\Documenti" },
        },
    };

    [Fact]
    public void Write_LeavesTheConfigurationAndAReadme()
    {
        var folder = Path.Combine(_root, "copia");

        var lines = new List<string>();
        ConfigMirror.WriteTo(SampleConfig(), folder, new Collect(lines), "PC-DI-CASA",
            new DateTime(2026, 9, 26, 21, 30, 0));

        var configFile = Path.Combine(folder, ConfigMirror.ConfigFileName);
        var readmeFile = Path.Combine(folder, ConfigMirror.ReadmeFileName);
        Assert.True(File.Exists(configFile));
        Assert.True(File.Exists(readmeFile));

        // Il file e' una configurazione valida per l'importazione delle Impostazioni: e' lo stesso
        // formato, letto dallo stesso codice.
        var reloaded = ConfigTransfer.Import(configFile);
        Assert.Equal("Documenti", Assert.Single(reloaded.Jobs).Name);
        Assert.Equal(3, reloaded.Settings.StaleAfterDays);
        Assert.Equal("chi@esempio.it", reloaded.Settings.Email.To);

        // Il LEGGIMI dice da quale PC e quando, e lo dice in due lingue: chi trova il disco nel
        // cassetto potrebbe non essere chi ha fatto il backup.
        var readme = File.ReadAllText(readmeFile);
        Assert.Contains("PC-DI-CASA", readme);
        Assert.Contains("26/09/2026", readme);
        Assert.Contains("Importa configurazione", readme);
        Assert.Contains("Import configuration", readme);
        Assert.Contains("DPAPI", readme);
        Assert.Contains(ConfigMirror.ConfigFileName, readme);

        Assert.Contains(lines, l => l.Contains(folder));
    }

    [Fact]
    public void Write_OverwritesThePreviousCopy()
    {
        // Una copia per disco, riscritta a ogni run: vince l'ultimo job, senza accumulare file.
        var folder = Path.Combine(_root, "copia");
        var first = SampleConfig();
        ConfigMirror.WriteTo(first, folder, null, "PC-VECCHIO", new DateTime(2026, 1, 1));

        var second = SampleConfig();
        second.Jobs.Add(new BackupJob { Name = "Foto", Source = @"D:\foto", Destination = @"E:\Backup\Foto" });
        ConfigMirror.WriteTo(second, folder, null, "PC-NUOVO", new DateTime(2026, 9, 26, 21, 30, 0));

        Assert.Equal(2, Directory.GetFiles(folder).Length);
        Assert.Equal(2, ConfigTransfer.Import(Path.Combine(folder, ConfigMirror.ConfigFileName)).Jobs.Count);
        var readme = File.ReadAllText(Path.Combine(folder, ConfigMirror.ReadmeFileName));
        Assert.Contains("PC-NUOVO", readme);
        Assert.DoesNotContain("PC-VECCHIO", readme);
    }

    [Fact]
    public void Write_IsAtomic_AndLeavesNoLeftovers()
    {
        // La scrittura passa da un .tmp e poi ci sposta sopra: chi apre la cartella trova la
        // versione vecchia o quella nuova, mai una a meta'. Finito, di .tmp non resta traccia.
        var folder = Path.Combine(_root, "copia");
        ConfigMirror.WriteTo(SampleConfig(), folder, null, "PC-DI-CASA", new DateTime(2026, 9, 26, 21, 30, 0));

        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        Assert.Equal(2, Directory.GetFiles(folder).Length);
    }

    [Fact]
    public void Write_SkipsTheDiskWhenNothingChanged()
    {
        // Questa copia viene riscritta a ogni backup riuscito, e di solito e' identica: riscriverla
        // consumerebbe la flash del disco e cambierebbe la data per niente.
        var folder = Path.Combine(_root, "copia");
        var when = new DateTime(2026, 9, 26, 21, 30, 0);
        ConfigMirror.WriteTo(SampleConfig(), folder, null, "PC-DI-CASA", when);

        // Date spostate indietro: un'eventuale riscrittura le riporterebbe a oggi, e si vedrebbe.
        var stamp = new DateTime(2020, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var files = Directory.GetFiles(folder);
        foreach (var f in files) File.SetLastWriteTimeUtc(f, stamp);

        ConfigMirror.WriteTo(SampleConfig(), folder, null, "PC-DI-CASA", when);
        Assert.All(files, f => Assert.Equal(stamp, File.GetLastWriteTimeUtc(f)));

        // Cambia qualcosa (un job in piu') e il file viene riscritto davvero.
        var changed = SampleConfig();
        changed.Jobs.Add(new BackupJob { Name = "Foto", Source = @"D:\foto", Destination = @"E:\Backup\Foto" });
        ConfigMirror.WriteTo(changed, folder, null, "PC-DI-CASA", when);
        Assert.NotEqual(stamp, File.GetLastWriteTimeUtc(Path.Combine(folder, ConfigMirror.ConfigFileName)));
    }

    [Fact]
    public void Write_NeverThrows_AndSaysSoInTheLog()
    {
        // Disco in sola lettura, spazio finito, permessi: qui il caso si riproduce con un FILE al
        // posto della cartella. Il backup e' gia' riuscito e non puo' diventare un fallimento.
        var blocked = Path.Combine(_root, "occupato");
        File.WriteAllText(blocked, "non sono una cartella");

        var lines = new List<string>();
        ConfigMirror.WriteTo(SampleConfig(), blocked, new Collect(lines), "PC-DI-CASA", DateTime.Now);

        var failure = CoreLoc.S("ConfigCopy_Failed").Split('{')[0].Trim();
        Assert.Contains(lines, l => l.StartsWith(failure));
        Assert.True(File.Exists(blocked)); // il file di prima e' intatto
    }

    [Fact]
    public void Write_DoesNothingForANetworkDestination()
    {
        // Nessuna radice di volume: la scorciatoia pubblica non deve inventarsi una cartella.
        var lines = new List<string>();
        ConfigMirror.Write(SampleConfig(), @"\\nas01\backup$\Foto", new Collect(lines));
        Assert.Empty(lines);
    }

    // ---- il mirror non la cancella ----

    [Fact]
    public void ARootDestination_ExcludesTheCopyFromTheMirror()
    {
        var atRoot = new BackupJob { Name = "T", Source = @"D:\documenti", Destination = @"E:\", Mirror = true };
        var args = RobocopyArgsBuilder.Build(atRoot).ToList();
        var i = args.IndexOf("/XD");
        Assert.True(i >= 0);
        // Il percorso preciso della copia, non il nome nudo: un "RoboKeep-config" in sorgente si
        // copia come qualunque altra cartella.
        Assert.Contains(ConfigMirror.TargetFolder(@"E:\"), args);

        // In sottocartella la copia sta piu' in alto, fuori dalla portata del job: niente da escludere.
        var inFolder = new BackupJob { Name = "T", Source = @"D:\documenti", Destination = @"E:\Backup", Mirror = true };
        Assert.DoesNotContain(RobocopyArgsBuilder.Build(inFolder), a => a.Contains(ConfigMirror.FolderName));
    }

    // ---- il run riuscito la scrive ----

    [Fact]
    public async Task ASuccessfulRun_WritesTheCopy_AndTellsItInTheLog()
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.CompressLogs = false;
        var creds = new CredentialService(config.Settings.CredentialScope);
        // La destinazione del job e' una cartella temporanea, e la radice del suo volume e' il disco
        // di sistema di chi esegue le prove: la cartella di destinazione della copia si reindirizza,
        // altrimenti il test scriverebbe in C:\RoboKeep-config.
        var folder = Path.Combine(_root, "copia");
        var runner = new BackupRunner(config, new RobocopyRunner(detectMedia: _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds,
            configMirrorTarget: _ => folder);

        var lines = new List<string>();
        var result = await runner.RunJobAsync(
            new BackupJob { Name = "T", Source = Src, Destination = Dst, Retries = 0, Wait = 0 },
            progress: new Collect(lines));

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(folder, ConfigMirror.ConfigFileName)));
        Assert.True(File.Exists(Path.Combine(folder, ConfigMirror.ReadmeFileName)));

        // La riga sta nel riepilogo, quindi la vede chi guarda la finestra E chi riapre il log.
        var written = CoreLoc.S("ConfigCopy_Written").Split('{')[0].Trim();
        Assert.Contains(lines, l => l.StartsWith(written));
        Assert.Contains(written, File.ReadAllText(result.LogPath!));
    }

    [Fact]
    public async Task APreviewWritesNothing_AndNeitherDoesASwitchedOffSetting()
    {
        var folder = Path.Combine(_root, "copia");
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.CompressLogs = false;
        var creds = new CredentialService(config.Settings.CredentialScope);
        BackupRunner NewRunner() => new(config, new RobocopyRunner(detectMedia: _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds,
            configMirrorTarget: _ => folder);
        var job = new BackupJob { Name = "T", Source = Src, Destination = Dst, Retries = 0, Wait = 0 };

        // Anteprima: non ha copiato niente, non deve scrivere niente.
        await NewRunner().RunJobAsync(job, dryRun: true);
        Assert.False(Directory.Exists(folder));

        // Casella spenta: nessuna copia, nemmeno dopo un run riuscito.
        config.Settings.ConfigCopyToDestination = false;
        var result = await NewRunner().RunJobAsync(job);
        Assert.True(result.Success);
        Assert.False(Directory.Exists(folder));
    }

    private sealed class Collect : IProgress<string>
    {
        private readonly List<string> _lines;
        public Collect(List<string> lines) => _lines = lines;
        public void Report(string value) { lock (_lines) _lines.Add(value); }
    }
}
