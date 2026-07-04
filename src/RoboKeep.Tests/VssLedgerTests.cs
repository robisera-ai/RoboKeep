using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VssLedgerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string LedgerPath => Path.Combine(_dir, "vss-ledger.json");

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void List_MissingFile_ReturnsEmpty()
        => Assert.Empty(new VssLedger(LedgerPath).List());

    [Fact]
    public void Add_ThenList_ContainsId()
    {
        var ledger = new VssLedger(LedgerPath);
        ledger.Add("{ID-1}");
        Assert.Equal(new[] { "{ID-1}" }, ledger.List());
    }

    [Fact]
    public void Remove_DeletesId_OthersSurvive()
    {
        var ledger = new VssLedger(LedgerPath);
        ledger.Add("{ID-1}");
        ledger.Add("{ID-2}");
        ledger.Remove("{ID-1}");
        Assert.Equal(new[] { "{ID-2}" }, ledger.List());
    }

    [Fact]
    public void Add_Duplicate_NotDuplicated()
    {
        var ledger = new VssLedger(LedgerPath);
        ledger.Add("{ID-1}");
        ledger.Add("{ID-1}");
        Assert.Single(ledger.List());
    }
}
