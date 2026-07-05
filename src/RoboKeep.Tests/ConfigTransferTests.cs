using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class ConfigTransferTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ConfigTransferTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void ExportImport_RoundTrips()
    {
        var config = new AppConfig();
        config.Jobs.Add(new BackupJob { Name = "Documenti", Source = @"C:\a", Destination = @"D:\b", UseVss = true });
        config.Settings.LogRetentionDays = 42;

        var path = Path.Combine(_dir, "export.json");
        ConfigTransfer.Export(config, path);
        var back = ConfigTransfer.Import(path);

        Assert.Single(back.Jobs);
        Assert.Equal("Documenti", back.Jobs[0].Name);
        Assert.True(back.Jobs[0].UseVss);
        Assert.Equal(42, back.Settings.LogRetentionDays);
    }

    [Fact]
    public void Import_InvalidJson_Throws()
    {
        var path = Path.Combine(_dir, "bad.json");
        File.WriteAllText(path, "{ rotto");
        Assert.ThrowsAny<Exception>(() => ConfigTransfer.Import(path));
    }
}
