using RoboKeep.Core.Services.Smart;
using static RoboKeep.Tests.SmartAttributesTests;

namespace RoboKeep.Tests;

/// <summary>La fusione tra cio' che l'app legge da sola e cio' che l'helper elevato riporta.
/// Pura: nessun processo avviato, nessun disco toccato.</summary>
public class SmartSessionTests
{
    private static readonly byte[] Block = AtaBlock((0x05, 100, 100, 0), (0x09, 100, 100, 387));

    private static DiskReport Local(int number, string serial, DiskBus bus = DiskBus.Usb) =>
        new(number, "SAMSUNG HD103SI", serial, bus, new[] { "E:" }, null, null, null);

    private static DiskReport FromHelper(int number, string serial, byte[]? block = null, string? error = null) =>
        new(number, "SAMSUNG HD103SI", serial, DiskBus.Usb, Array.Empty<string>(), block, null, error);

    [Fact]
    public void Merge_SameNumberSameSerial_TakesTheAtaBlock()
    {
        var merged = SmartSession.Merge(new[] { Local(1, "S1") }, new[] { FromHelper(1, "S1", Block) });
        Assert.Equal(Block, merged.Single().AtaBlock);
        Assert.NotNull(merged.Single().Ata);
    }

    [Fact]
    public void Merge_SameNumberDifferentSerial_LeavesTheDiskAlone()
    {
        // Il numero di disco e' solo la posizione del momento: se un disco viene staccato mentre
        // l'helper gira, il numero puo' finire a un altro. Attribuire quello SMART sarebbe un
        // referto sul disco sbagliato — peggio di nessun referto.
        var merged = SmartSession.Merge(new[] { Local(1, "S1") }, new[] { FromHelper(1, "S2", Block) });
        Assert.Null(merged.Single().AtaBlock);
        Assert.Null(merged.Single().Error);
    }

    [Fact]
    public void Merge_WhenTheLocalSerialIsMissing_MatchesByNumberAlone()
    {
        // Alcuni box USB non dichiarano il seriale: resta solo il numero, ed e' meglio di niente.
        var merged = SmartSession.Merge(new[] { Local(1, "") }, new[] { FromHelper(1, "S2", Block) });
        Assert.Equal(Block, merged.Single().AtaBlock);
    }

    [Fact]
    public void Merge_KeepsTheHelperError_AndNeverTouchesNvme()
    {
        var nvme = new DiskReport(0, "ORICO", "S0", DiskBus.Nvme, new[] { "C:" }, null, null, null);
        var merged = SmartSession.Merge(
            new[] { nvme, Local(1, "S1") },
            new[] { FromHelper(0, "S0", Block), FromHelper(1, "S1", null, "ata") });

        Assert.Null(merged[0].AtaBlock); // NVMe: l'app l'ha gia' letto, l'helper non lo tocca
        Assert.Null(merged[0].Error);
        Assert.Null(merged[1].AtaBlock);
        Assert.Equal("ata", merged[1].Error);
    }

    [Fact]
    public void Merge_WithNothingFromTheHelper_ReturnsTheDisksUnchanged()
    {
        var merged = SmartSession.Merge(new[] { Local(1, "S1") }, Array.Empty<DiskReport>());
        Assert.Null(merged.Single().AtaBlock);
        Assert.Null(merged.Single().Error);
    }
}
