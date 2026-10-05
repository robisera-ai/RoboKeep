using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class PreflightCheckerTests
{
    private const long Mb = 1024L * 1024L;

    [Fact]
    public void DiskErrors_CanBeMarkedAsChecked_UpToTheLatestOneShown()
    {
        // «Ho controllato» vale per gli errori mostrati, non per quelli che arriveranno: la data
        // salvata e' quella dell'ultimo errore del riepilogo, non l'ora del clic.
        var job = new BackupJob { Source = @"D:\Dati", Destination = @"E:\Backup" };
        var latest = new DateTime(2026, 10, 1, 2, 7, 49);
        var inputs = new PreflightInputs(true, 10_000 * Mb, 1024 * Mb, 100 * Mb, null,
            new DiskEventSummary(0, 295, 7, latest), DiskEventSummary.None);

        var disk = Assert.Single(PreflightChecker.AcknowledgeableDisks(job, inputs));

        Assert.Equal((@"E:\Backup", latest), (disk.Path, disk.Until));
    }

    [Fact]
    public void WithoutDiskErrors_ThereIsNothingToMarkAsChecked()
    {
        var job = new BackupJob { Source = @"D:\Dati", Destination = @"E:\Backup" };
        var inputs = new PreflightInputs(true, 10 * Mb, 1024 * Mb, 100 * Mb);   // solo spazio scarso

        Assert.Empty(PreflightChecker.AcknowledgeableDisks(job, inputs));
    }

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

    [Fact]
    public void ReachableDestination_WithRoomToSpare_HasNothingToSay()
    {
        // Il tipo di disco non conta: le versioni funzionano ovunque, e il pre-avvio non ha
        // niente da dire su una destinazione raggiungibile e con spazio.
        var w = PreflightChecker.Evaluate(new PreflightInputs(
            DestinationReachable: true, FreeBytes: 10_000 * Mb, MinFreeBytes: 1024 * Mb, SourceSizeBytes: 100 * Mb));
        Assert.Empty(w);
    }

    [Fact]
    public void Evaluate_VssSourceNotEligible_Warns()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(true, long.MaxValue, 0, null,
            VssSourceEligible: false));
        Assert.Contains(w, x => x.MessageKey == "Preflight_VssNotEligible");
    }

    [Fact]
    public void Evaluate_VssSourceEligible_NoWarning()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(true, long.MaxValue, 0, null,
            VssSourceEligible: true));
        Assert.DoesNotContain(w, x => x.MessageKey == "Preflight_VssNotEligible");
    }

    [Fact]
    public void Evaluate_VssNotRelevant_NoWarning()
    {
        var w = PreflightChecker.Evaluate(new PreflightInputs(true, long.MaxValue, 0, null));
        Assert.DoesNotContain(w, x => x.MessageKey == "Preflight_VssNotEligible");
    }
}
