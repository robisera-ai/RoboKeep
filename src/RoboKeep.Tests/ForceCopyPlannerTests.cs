using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public sealed class ForceCopyPlannerTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "RbcPlan_" + Guid.NewGuid().ToString("N"));
    private readonly string _src;
    private readonly string _hashPath;

    public ForceCopyPlannerTests()
    {
        _src = Path.Combine(_base, "src");
        _hashPath = Path.Combine(_base, "hashes.json");
        Directory.CreateDirectory(_src);
    }

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
    }

    private BackupJob Job(bool smart, params string[] patterns) => new()
    {
        Name = "J",
        Source = _src,
        Destination = Path.Combine(_base, "dst"),
        ForceCopyFiles = patterns.ToList(),
        ForceCopySmart = smart,
    };

    private ForceCopyPlanner NewPlanner() => new(new ForceCopyHashStore(_hashPath));

    [Fact]
    public async Task EmptyList_ReturnsEmptyPlan()
    {
        var plan = await NewPlanner().PlanAsync(Job(smart: false), null, default);
        Assert.Empty(plan.Filters);
    }

    [Fact]
    public async Task SimpleMode_ReturnsPatternsAsFilters()
    {
        var plan = await NewPlanner().PlanAsync(Job(smart: false, "*.pst", "db.dat"), null, default);
        Assert.False(plan.Smart);
        Assert.Equal(new[] { "*.pst", "db.dat" }, plan.Filters);
    }

    [Fact]
    public async Task SmartMode_NewFile_IsTreatedAsChanged()
    {
        File.WriteAllText(Path.Combine(_src, "data.bin"), "AAAA");
        var plan = await NewPlanner().PlanAsync(Job(smart: true, "*.bin"), null, default);
        Assert.True(plan.Smart);
        Assert.Contains("data.bin", plan.Filters);
        Assert.NotEmpty(plan.NewHashes);
    }

    [Fact]
    public async Task SmartMode_UnchangedFile_IsNotInFilters()
    {
        File.WriteAllText(Path.Combine(_src, "data.bin"), "AAAA");
        var planner = NewPlanner();
        var first = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        planner.Commit("J", first.NewHashes);

        var second = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        Assert.Empty(second.Filters);
    }

    [Fact]
    public async Task SmartMode_ContentChangedSameSize_IsInFilters()
    {
        var file = Path.Combine(_src, "data.bin");
        File.WriteAllText(file, "AAAA");
        var planner = NewPlanner();
        var first = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        planner.Commit("J", first.NewHashes);

        File.WriteAllText(file, "BBBB"); // stessa lunghezza, contenuto diverso
        var second = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        Assert.Contains("data.bin", second.Filters);
    }
}
