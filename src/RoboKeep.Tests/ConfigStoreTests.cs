using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ConfigStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RoboKeepTests_" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static AppConfig SampleConfig() => new()
    {
        Settings = new AppSettings
        {
            LogRoot = @"D:\logs",
            TempRoot = @"D:\temp",
            CompressLogs = true,
            LogRetentionDays = 45,
            Email = new EmailSettings { Enabled = true, SmtpHost = "smtp.test", SmtpPort = 587, To = "a@b.c", OnlyOnError = false },
        },
        Credentials = { new CredentialEntry { Id = "srv", Host = @"\\srv\share", User = "dom\\bk", PasswordProtected = "ENC" } },
        Jobs =
        {
            new BackupJob { Name = "Documenti", Source = @"D:\doc", Destination = @"E:\bak\doc", Mirror = true, MultiThread = 8, ExcludeFiles = { "*.tmp" }, ExcludeDirs = { "cache" } },
            new BackupJob { Name = "Foto", Source = @"D:\foto", Destination = @"E:\bak\foto", Mirror = false, CredentialId = "srv" },
        },
    };

    [Fact]
    public void Save_CreatesFileAndDirectory()
    {
        var store = new ConfigStore(_path);
        store.Save(SampleConfig());
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var store = new ConfigStore(_path);
        store.Save(SampleConfig());

        var loaded = store.Load();

        Assert.Equal(45, loaded.Settings.LogRetentionDays);
        Assert.True(loaded.Settings.CompressLogs);
        Assert.Equal("smtp.test", loaded.Settings.Email.SmtpHost);
        Assert.Equal(587, loaded.Settings.Email.SmtpPort);
        Assert.False(loaded.Settings.Email.OnlyOnError);

        Assert.Single(loaded.Credentials);
        Assert.Equal("srv", loaded.Credentials[0].Id);
        Assert.Equal("ENC", loaded.Credentials[0].PasswordProtected);

        Assert.Equal(2, loaded.Jobs.Count);
        var doc = loaded.Jobs[0];
        Assert.Equal("Documenti", doc.Name);
        Assert.True(doc.Mirror);
        Assert.Equal(8, doc.MultiThread);
        Assert.Equal("*.tmp", Assert.Single(doc.ExcludeFiles));
        Assert.Equal("cache", Assert.Single(doc.ExcludeDirs));

        var foto = loaded.Jobs[1];
        Assert.False(foto.Mirror);
        Assert.Equal("srv", foto.CredentialId);
    }

    [Fact]
    public void Load_ReturnsDefault_WhenFileMissing()
    {
        var store = new ConfigStore(_path);
        var cfg = store.Load();
        Assert.NotNull(cfg);
        Assert.Empty(cfg.Jobs);
        Assert.Empty(cfg.Credentials);
        // Una configurazione nuova nasce già "migrata": non c'è nessun valore vecchio da alzare, e
        // il segno impedisce che la migrazione torni a guardarla in futuro.
        Assert.Equal(10240, cfg.Settings.MinFreeSpaceMb);
        Assert.True(cfg.Settings.MinFreeSpaceMigrated);
    }

    // ---- Migrazione del default di MinFreeSpaceMb (1 GB -> 10 GB) ----

    private void WriteSettings(string settingsJson) =>
        WriteRaw($"{{ \"settings\": {settingsJson}, \"credentials\": [], \"jobs\": [] }}");

    private void WriteRaw(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, json);
    }

    [Fact]
    public void Load_RaisesTheOldDefaultMinFreeSpace_OnceAndOnlyForTheDefaultValue()
    {
        // Configurazione scritta da una versione precedente: 1024 = il default di allora, nessun
        // segno di migrazione. Il nuovo default non la raggiungerebbe mai da solo.
        WriteSettings("{ \"minFreeSpaceMb\": 1024 }");
        var migrated = new ConfigStore(_path).Load().Settings;
        Assert.Equal(10240, migrated.MinFreeSpaceMb);
        Assert.True(migrated.MinFreeSpaceMigrated);

        // Un valore scelto dall'utente non si tocca.
        WriteSettings("{ \"minFreeSpaceMb\": 2048 }");
        Assert.Equal(2048, new ConfigStore(_path).Load().Settings.MinFreeSpaceMb);

        // Migrazione già avvenuta: 1024 è ormai una scelta dell'utente e resta 1024.
        WriteSettings("{ \"minFreeSpaceMb\": 1024, \"minFreeSpaceMigrated\": true }");
        Assert.Equal(1024, new ConfigStore(_path).Load().Settings.MinFreeSpaceMb);
    }
}
