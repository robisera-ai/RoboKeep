using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class UpdateStateStoreTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "RbcUpd_" + Guid.NewGuid().ToString("N"), "update-state.json");

    [Fact]
    public void SaveThenLoad_RoundTripsBothFields()
    {
        var path = TempFile();
        try
        {
            var store = new UpdateStateStore(path);
            var when = new DateTime(2026, 10, 12, 12, 30, 0);
            store.Save(new UpdateState(when, true));

            var back = new UpdateStateStore(path).Load();
            Assert.Equal(when, back.LastCheck);
            Assert.True(back.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenTheFileIsMissing()
    {
        var state = new UpdateStateStore(TempFile()).Load();
        Assert.Null(state.LastCheck);
        Assert.False(state.LastCheckFailed);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenTheFileIsCorrupt()
    {
        var path = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ questo non e' json");
            var state = new UpdateStateStore(path).Load();
            Assert.Null(state.LastCheck);
            Assert.False(state.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Load_IgnoresIgnoredVersion_FromAnOlderInstall()
    {
        // File scritto da una versione precedente: il campo non esiste piu' e va ignorato,
        // senza perdere la data e senza far fallire la lettura.
        var path = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Senza fuso orario nella data: con l'offset il valore letto dipenderebbe dal fuso
            // della macchina che esegue i test.
            File.WriteAllText(path, """
            {
              "lastCheck": "2026-10-11T16:26:00",
              "ignoredVersion": "1.10.0"
            }
            """);
            var state = new UpdateStateStore(path).Load();
            Assert.Equal(new DateTime(2026, 10, 11, 16, 26, 0), state.LastCheck);
            Assert.False(state.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Save_CreatesTheFolder_AndOverwritesThePreviousState()
    {
        var path = TempFile();
        try
        {
            var store = new UpdateStateStore(path);
            store.Save(new UpdateState(new DateTime(2026, 1, 1), true));
            store.Save(new UpdateState(new DateTime(2026, 2, 2), false));

            var back = store.Load();
            Assert.Equal(new DateTime(2026, 2, 2), back.LastCheck);
            Assert.False(back.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }

    private static void Cleanup(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
}
