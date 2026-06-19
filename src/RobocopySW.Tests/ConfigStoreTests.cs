using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ConfigStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RobocopySWTests_" + Guid.NewGuid().ToString("N"));
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
    }
}
