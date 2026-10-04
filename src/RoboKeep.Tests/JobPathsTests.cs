using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class JobPathsTests
{
    [Theory]
    [InlineData(@"D:\ProvaDiff", @"D:\ProvaDiff", true)]          // stessa cartella
    [InlineData(@"D:\ProvaDiff", @"d:\provadiff\", true)]         // maiuscole e barra finale
    [InlineData(@"D:\ProvaDiff", @"D:\ProvaDiff\backup", true)]   // destinazione dentro la sorgente
    [InlineData(@"D:\ProvaDiff\sotto", @"D:\ProvaDiff", true)]    // sorgente dentro la destinazione
    [InlineData(@"D:\", @"D:\Backup", true)]                      // disco intero come sorgente
    [InlineData(@"D:\dati", @"D:\dati-vecchi", false)]            // nome che inizia uguale
    [InlineData(@"D:\ProvaDiff", @"E:\Backup\ProvaDiff", false)]
    [InlineData("", @"E:\Backup", false)]
    public void Overlap_IsTrue_OnlyForTheSameFolderOrOneInsideTheOther(string source, string dest, bool expected)
        => Assert.Equal(expected, JobPaths.Overlap(source, dest));

    [Fact]
    public void DestinationOverlaps_FindsEachClashingPairOnce()
    {
        // Una configurazione importata non passa dall'editor: chi la importa deve sapere quali job
        // scrivono nella stessa cartella (o uno dentro l'altro), perche' non partiranno.
        var jobs = new[]
        {
            new RoboKeep.Core.Models.BackupJob { Name = "Tutto", Destination = @"E:\Backup" },
            new RoboKeep.Core.Models.BackupJob { Name = "Foto", Destination = @"E:\Backup\Foto" },
            new RoboKeep.Core.Models.BackupJob { Name = "Altro", Destination = @"E:\Altro" },
        };

        var pair = Assert.Single(JobPaths.DestinationOverlaps(jobs));

        Assert.Equal(("Tutto", "Foto"), (pair.First.Name, pair.Second.Name));
    }

    [Fact]
    public void DestinationOverlaps_TheSameFolderOnTwoDifferentDisks_IsNotAClash()
    {
        // Rotazione dei dischi: stessa cartella, ma ogni job legato al suo disco. Non finiscono mai
        // sullo stesso disco: il job del disco che non c'e' viene saltato («disco non collegato»).
        var jobs = new[]
        {
            new RoboKeep.Core.Models.BackupJob { Name = "Samsung", Destination = @"E:\Backup\Provadiff", DestinationVolumeId = @"\\?\Volume{a}\" },
            new RoboKeep.Core.Models.BackupJob { Name = "WD", Destination = @"E:\Backup\Provadiff", DestinationVolumeId = @"\\?\Volume{b}\" },
        };

        Assert.Empty(JobPaths.DestinationOverlaps(jobs));
    }

    [Fact]
    public void DestinationOverlaps_AJobNotTiedToADisk_StillClashes()
    {
        // Senza disco associato (cartella di rete, job di una versione vecchia) il job scrive su
        // qualunque disco abbia quella lettera: puo' finire sullo stesso dell'altro.
        var jobs = new[]
        {
            new RoboKeep.Core.Models.BackupJob { Name = "Legato", Destination = @"E:\Backup\Provadiff", DestinationVolumeId = @"\\?\Volume{a}\" },
            new RoboKeep.Core.Models.BackupJob { Name = "Libero", Destination = @"E:\Backup\Provadiff" },
        };

        Assert.Single(JobPaths.DestinationOverlaps(jobs));
    }
}

public class RestoreTargetRulesTests
{
    [Theory]
    [InlineData(@"E:\Backup\ProvaDiff", true)]            // il backup stesso
    [InlineData(@"E:\Backup\ProvaDiff\current", true)]    // riscontro della prova a mano
    [InlineData(@"E:\Backup\ProvaDiff\versions\x", true)] // una versione
    [InlineData(@"E:\Backup", true)]                      // una cartella che contiene il backup
    [InlineData(@"E:\Recupero", false)]
    [InlineData(@"D:\Recupero", false)]
    public void TouchesBackup_RefusesTheBackupAndAnythingOverlappingIt(string target, bool expected)
        => Assert.Equal(expected, RestoreCopier.TouchesBackup(target, @"E:\Backup\ProvaDiff"));

    [Fact]
    public async Task CopyAsync_RefusesTheBackupAsTarget_EvenIfTheWindowWereBypassed()
    {
        var plan = new RestorePlan(
            new Dictionary<string, RestoreEntry>(), Array.Empty<string>(), 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RestoreCopier.CopyAsync(plan, @"E:\Backup\ProvaDiff\current",
                jobDestination: @"E:\Backup\ProvaDiff"));
    }
}

public class RestoreTreeSelectAllTests
{
    [Fact]
    public void SetAll_TicksEverything_AndClearsTheExceptions()
    {
        var files = new[] { @"a\x.txt", @"a\y.txt", "z.txt" }.ToDictionary(
            r => r, r => new RoboKeep.Core.Services.RestoreEntry(r, @"D:\s\" + r, 1, null),
            StringComparer.OrdinalIgnoreCase);
        var plan = new RoboKeep.Core.Services.RestorePlan(files, new[] { "a" }, 3);
        var tree = new RoboKeep.ViewModels.RestoreTree(plan);

        tree.Set(@"a\x.txt", false);
        tree.SetAll(true);
        Assert.True(tree.StateOf("a", isFolder: true));
        Assert.Equal(3, RestorePlanner.Select(plan, tree.Selection()).Files.Count);

        tree.SetAll(false);
        Assert.False(tree.AnySelected);
        Assert.Empty(tree.Selection());
    }
}

public class RestoreOnlyChangedTests
{
    private static RestoreEntry E(string rel, string from) => new(rel, from + @"\" + rel, 1, null);

    [Fact]
    public void OnlyChangedBy_KeepsWhatThatBackupReplacedOrDeleted_AndNothingElse()
    {
        var files = new Dictionary<string, RestoreEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["lettera.docx"] = E("lettera.docx", @"E:\B\versions\v1"),       // sostituito da quel backup
            [@"vecchi\a.txt"] = E(@"vecchi\a.txt", @"E:\B\versions\v1"),     // cartella cancellata
            [@"vecchi\sotto\b.txt"] = E(@"vecchi\sotto\b.txt", @"E:\B\versions\v1"),
            ["intatto.txt"] = E("intatto.txt", @"E:\B\current"),             // mai toccato
        };
        var plan = new RestorePlan(files, new[] { "vecchi", @"vecchi\sotto" }, 4);
        var manifest = new VersionManifest
        {
            Changed = new() { "lettera.docx" },
            Deleted = new() { "vecchi" },
            Added = new() { "nuovo.txt" },
        };

        var only = RestorePlanner.OnlyChangedBy(plan, manifest);

        Assert.Equal(new[] { "lettera.docx", @"vecchi\a.txt", @"vecchi\sotto\b.txt" },
            only.Files.Keys.OrderBy(k => k).ToArray());
        Assert.False(only.Files.ContainsKey("intatto.txt"));
    }
}
