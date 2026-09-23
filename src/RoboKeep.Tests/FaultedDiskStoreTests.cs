using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public sealed class FaultedDiskStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcFault_" + Guid.NewGuid().ToString("N"));
    private string PathOf => Path.Combine(_dir, "faulted-disks.json");
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static FaultedDisk Disk(string id, DateTime since) =>
        new(id, "BACKUP1", @"E:\", since, "Win32 23: CRC");

    [Fact]
    public void Mark_ThenFind_AcrossInstances()
    {
        var now = new DateTime(2026, 9, 22, 21, 0, 0);
        new FaultedDiskStore(PathOf).Mark(Disk(@"\\?\Volume{a}\", now));

        var found = new FaultedDiskStore(PathOf).Find(@"\\?\Volume{a}\", now.AddDays(3));
        Assert.NotNull(found);
        Assert.Equal("BACKUP1", found!.Label);
        Assert.False(found.Notified);
        Assert.Null(new FaultedDiskStore(PathOf).Find(@"\\?\Volume{b}\", now));
        Assert.Null(new FaultedDiskStore(PathOf).Find(null, now));
    }

    [Fact]
    public void Expires_AfterExpiryDays()
    {
        var now = new DateTime(2026, 9, 22);
        var store = new FaultedDiskStore(PathOf);
        store.Mark(Disk(@"\\?\Volume{a}\", now));
        Assert.NotNull(store.Find(@"\\?\Volume{a}\", now.AddDays(FaultedDiskStore.ExpiryDays - 1)));
        Assert.Null(store.Find(@"\\?\Volume{a}\", now.AddDays(FaultedDiskStore.ExpiryDays + 1)));
    }

    [Fact]
    public void Mark_ReplacesEntryForSameVolume_CaseInsensitive()
    {
        var now = new DateTime(2026, 9, 22);
        var store = new FaultedDiskStore(PathOf);
        store.Mark(Disk(@"\\?\Volume{ABC}\", now));
        store.Mark(Disk(@"\\?\Volume{abc}\", now.AddDays(1)));
        Assert.Single(store.Load(now.AddDays(1)));
    }

    [Fact]
    public void MarkNotified_IsRemembered()
    {
        var now = DateTime.Now;
        var store = new FaultedDiskStore(PathOf);
        store.Mark(Disk(@"\\?\Volume{a}\", now));
        store.MarkNotified(@"\\?\Volume{a}\");
        Assert.True(new FaultedDiskStore(PathOf).Find(@"\\?\Volume{a}\", now)!.Notified);
        store.MarkNotified(@"\\?\Volume{missing}\"); // nessuna eccezione
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var now = DateTime.Now;
        var store = new FaultedDiskStore(PathOf);
        store.Mark(Disk(@"\\?\Volume{a}\", now));
        store.Clear();
        Assert.Empty(store.Load(now));
    }

    [Fact]
    public void CorruptFile_IsTreatedAsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathOf, "{ non json");
        Assert.Empty(new FaultedDiskStore(PathOf).Load(DateTime.Now));
    }
}
