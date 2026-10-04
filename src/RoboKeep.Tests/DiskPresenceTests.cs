using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// A quale disco fisico appartiene un errore del registro eventi. Windows nomina il disco per numero
/// (\Device\Harddisk1) o per lettera (E:): con la rotazione dei dischi lo stesso numero e la stessa
/// lettera passano da un disco all'altro. Il registro «Partition/Diagnostic» (evento 1006) dice quale
/// disco aveva quel numero in ogni momento, con la firma scritta sul disco stesso.
/// </summary>
public class DiskPresenceTests
{
    // Tabella delle partizioni come la scrive Windows: stile (0 = MBR), numero di voci, firma del
    // disco. Questa e' quella vera del WD da 500 GB: firma 1AA6F3E1.
    private const string WdTable = "0000000004000000E1F3A61A2A7A3F090000000000000000";
    private const string SamsungTable = "0000000004000000E131AE5C00000000";

    private static readonly DateTime SamsungIn = new(2026, 10, 1, 2, 7, 40);
    private static readonly DateTime SamsungOut = new(2026, 10, 4, 19, 9, 59);
    private static readonly DateTime WdIn = new(2026, 10, 4, 19, 10, 26);

    private static DiskPresence Samsung(DateTime when) =>
        DiskPresence.From(when, 1, capacity: 1_000_204_886_016, SamsungTable, "SAMSUNG", "HD103SI", "1234567890123");

    private static DiskPresence Wd(DateTime when) =>
        DiskPresence.From(when, 1, capacity: 500_107_862_016, WdTable, "WDC WD50", "00BPVT-60HXZT3", "621098765432");

    private static DiskPresence Removed(DateTime when) =>
        DiskPresence.From(when, 1, capacity: 0, "", "SAMSUNG", "HD103SI", "1234567890123");

    [Fact]
    public void TheSignatureWrittenOnTheDisk_IsItsIdentity()
    {
        Assert.Equal("MBR:1AA6F3E1", Wd(WdIn).OnDiskId);
        Assert.Equal("MBR:5CAE31E1", Samsung(SamsungIn).OnDiskId);
        Assert.False(Removed(SamsungOut).Present);
    }

    [Fact]
    public void AnErrorFromWhenAnotherDiskHadThatNumber_DoesNotBelongToTheDiskPluggedInNow()
    {
        // Il caso vero: errori del 01/10 alle 02:07 sul Samsung, oggi su E: c'e' il WD.
        var timeline = new[] { Samsung(SamsungIn), Removed(SamsungOut), Wd(WdIn) };

        Assert.False(DiskPresence.BelongsToCurrentDisk(new DateTime(2026, 10, 1, 2, 7, 47), 1, timeline));
        Assert.True(DiskPresence.BelongsToCurrentDisk(new DateTime(2026, 10, 4, 20, 0, 0), 1, timeline));
    }

    [Fact]
    public void WhenTheFirstDiskComesBack_ItsOwnErrorsAreShownAgain()
    {
        var timeline = new[] { Samsung(SamsungIn), Removed(SamsungOut), Wd(WdIn), Samsung(new DateTime(2026, 10, 5, 9, 0, 0)) };

        Assert.True(DiskPresence.BelongsToCurrentDisk(new DateTime(2026, 10, 1, 2, 7, 47), 1, timeline));
        Assert.False(DiskPresence.BelongsToCurrentDisk(new DateTime(2026, 10, 4, 20, 0, 0), 1, timeline));
    }

    [Fact]
    public void WithoutInformation_TheErrorIsCounted()
    {
        // Registro vuoto o disattivato, o errore piu' vecchio del primo collegamento registrato:
        // meglio un avviso in piu' che un avviso perso.
        Assert.True(DiskPresence.BelongsToCurrentDisk(new DateTime(2026, 10, 1), 1, Array.Empty<DiskPresence>()));
        Assert.True(DiskPresence.BelongsToCurrentDisk(new DateTime(2026, 9, 1), 1, new[] { Wd(WdIn) }));
    }

    [Fact]
    public void OtherDiskNumbers_DoNotMatter()
    {
        var timeline = new[] { Samsung(SamsungIn), Wd(WdIn) with { DiskNumber = 2 } };

        Assert.True(DiskPresence.BelongsToCurrentDisk(new DateTime(2026, 10, 4, 20, 0, 0), 1, timeline));
    }
}
