using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class PreflightCheckerTests
{
    private const long Mb = 1024L * 1024L;

    [Fact]
    public void AllGood_NoWarnings()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(
            DestinationReachable: true, FreeBytes: 10_000 * Mb, MinFreeBytes: 1024 * Mb, SourceSizeBytes: 100 * Mb));
        Assert.Empty(w);
    }

    [Fact]
    public void Unreachable_OnlyUnreachableWarning()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(
            DestinationReachable: false, FreeBytes: 0, MinFreeBytes: 1024 * Mb, SourceSizeBytes: 100 * Mb));
        var only = Assert.Single(w);
        Assert.Equal("Preflight_DestUnreachable", only.MessageKey);
    }

    [Fact]
    public void LowFreeSpace_Warns()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(
            DestinationReachable: true, FreeBytes: 500 * Mb, MinFreeBytes: 1024 * Mb, SourceSizeBytes: null));
        Assert.Contains(w, x => x.MessageKey == "Preflight_LowSpace");
    }

    [Fact]
    public void SourceBiggerThanFree_Warns()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(
            DestinationReachable: true, FreeBytes: 2000 * Mb, MinFreeBytes: 1024 * Mb, SourceSizeBytes: 5000 * Mb));
        Assert.Contains(w, x => x.MessageKey == "Preflight_SourceBigger");
    }

    [Fact]
    public void NullSourceSize_NoSizeWarning()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(
            DestinationReachable: true, FreeBytes: 2000 * Mb, MinFreeBytes: 1024 * Mb, SourceSizeBytes: null));
        Assert.DoesNotContain(w, x => x.MessageKey == "Preflight_SourceBigger");
    }
}
