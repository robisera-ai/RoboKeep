using System.Diagnostics;
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
        Assert.Equal(new[] { "{ID-1}" }, ledger.List().Select(e => e.ShadowId));
    }

    [Fact]
    public void Remove_DeletesId_OthersSurvive()
    {
        var ledger = new VssLedger(LedgerPath);
        ledger.Add("{ID-1}");
        ledger.Add("{ID-2}");
        ledger.Remove("{ID-1}");
        Assert.Equal(new[] { "{ID-2}" }, ledger.List().Select(e => e.ShadowId));
    }

    [Fact]
    public void Add_Duplicate_NotDuplicated()
    {
        var ledger = new VssLedger(LedgerPath);
        ledger.Add("{ID-1}");
        ledger.Add("{ID-1}");
        Assert.Single(ledger.List());
    }

    [Fact]
    public void ListStale_OwnerAlive_NotReturned()
    {
        var ledger = new VssLedger(LedgerPath);
        ledger.Add("{ID-1}"); // stampato con Environment.ProcessId (il processo di test, vivo)
        Assert.Single(ledger.List());
        Assert.Empty(ledger.ListStale());
    }

    [Fact]
    public void ListStale_OwnerDead_Returned()
    {
        var deadPid = FindDeadPid();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(LedgerPath, $"[{{\"ShadowId\":\"{{ID-DEAD}}\",\"OwnerPid\":{deadPid}}}]");

        var ledger = new VssLedger(LedgerPath);
        Assert.Equal(new[] { "{ID-DEAD}" }, ledger.ListStale());
    }

    /// <summary>Trova un PID che non corrisponde a nessun processo vivo.</summary>
    private static int FindDeadPid()
    {
        for (var pid = 999_999; pid > 100_000; pid--)
        {
            try { Process.GetProcessById(pid); }
            catch { return pid; }
        }
        throw new InvalidOperationException("Nessun PID libero trovato per il test.");
    }
}
