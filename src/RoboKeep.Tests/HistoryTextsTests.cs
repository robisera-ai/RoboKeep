using RoboKeep.Core.Models;
using RoboKeep.Localization;

namespace RoboKeep.Tests;

/// <summary>Nella cronologia ogni «saltato» mostrava «disco atteso non collegato», anche quando il
/// job era stato saltato perche' stava gia' girando o perche' la destinazione era occupata.</summary>
public class HistoryTextsTests
{
    [Theory]
    [InlineData(null, "Hist_CountsSkipped", "Hist_NoLogSkipped")]
    [InlineData(RunHistoryEntry.ReasonAlreadyRunning, "Hist_CountsAlreadyRunning", "Hist_NoLogAlreadyRunning")]
    [InlineData(RunHistoryEntry.ReasonDestinationBusy, "Hist_CountsDestinationBusy", "Hist_NoLogDestinationBusy")]
    public void EachReason_HasItsOwnTexts(string? reason, string counts, string noLog)
    {
        var keys = HistoryWindow.SkipTextKeys(reason);

        Assert.Equal((counts, noLog), keys);
        Assert.NotEqual(keys.Counts, Loc.Instance[keys.Counts]);   // il testo esiste davvero
        Assert.NotEqual(keys.NoLog, Loc.Instance[keys.NoLog]);
    }
}
