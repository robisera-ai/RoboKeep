using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class RunHistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string StorePath => Path.Combine(_dir, "runhistory.json");

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static RunHistoryEntry Entry(string job, DateTime start, string kind = "backup") =>
        new(job, kind, start, start.AddMinutes(1), true, 1, 10, 5, 0, 0, 0, null);

    [Fact]
    public void List_MissingFile_ReturnsEmpty()
        => Assert.Empty(new RunHistoryStore(StorePath).List());

    [Fact]
    public void Append_ThenList_NewestFirst()
    {
        var s = new RunHistoryStore(StorePath);
        s.Append(Entry("A", new DateTime(2026, 7, 1, 10, 0, 0)));
        s.Append(Entry("B", new DateTime(2026, 7, 2, 10, 0, 0)));
        var list = s.List();
        Assert.Equal(2, list.Count);
        Assert.Equal("B", list[0].JobName);
    }

    [Fact]
    public void List_FilterByJob()
    {
        var s = new RunHistoryStore(StorePath);
        s.Append(Entry("A", DateTime.Now));
        s.Append(Entry("B", DateTime.Now));
        Assert.Single(s.List("A"));
    }

    [Fact]
    public void Append_Over500_OldestDropped()
    {
        var s = new RunHistoryStore(StorePath);
        for (var i = 0; i < 505; i++)
            s.Append(Entry("J", new DateTime(2026, 1, 1).AddMinutes(i)));
        var list = s.List();
        Assert.Equal(500, list.Count);
        Assert.Equal(new DateTime(2026, 1, 1).AddMinutes(5), list[^1].StartedAt);
    }

    [Fact]
    public void List_CorruptFile_ReturnsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(StorePath, "{ not json !");
        Assert.Empty(new RunHistoryStore(StorePath).List());
    }
}
