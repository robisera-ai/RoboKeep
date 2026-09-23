using RoboKeep.Core.Services.Smart;
using static RoboKeep.Tests.SmartAttributesTests;

namespace RoboKeep.Tests;

public class DiskVerdictTests
{
    private static SmartAttributes Ata(params (byte, byte, byte, long)[] a) => SmartAttributes.ParseAta(AtaBlock(a))!;

    [Fact]
    public void HealthyHdd_IsGood()
    {
        var v = DiskVerdict.Evaluate(Ata((0x05, 100, 100, 0), (0xC5, 100, 100, 0), (0xC6, 100, 100, 0), (0xC7, 100, 100, 0), (0xC2, 100, 100, 30)));
        Assert.Equal(DiskHealthLevel.Good, v.Level);
        Assert.DoesNotContain(v.Findings, f => f.Key == "Smart_LinkErrors");
    }

    [Fact]
    public void ReallocatedOrUncorrectable_IsDanger()
    {
        Assert.Equal(DiskHealthLevel.Danger, DiskVerdict.Evaluate(Ata((0x05, 90, 90, 12))).Level);
        Assert.Equal(DiskHealthLevel.Danger, DiskVerdict.Evaluate(Ata((0xC6, 100, 100, 3))).Level);
    }

    [Fact]
    public void PendingSectors_IsWarning_WithTheRewriteAdvice()
    {
        // Il Samsung prima della formattazione completa: C5 = 17, 05 = 0.
        var v = DiskVerdict.Evaluate(Ata((0x05, 100, 100, 0), (0xC5, 100, 100, 17)));
        Assert.Equal(DiskHealthLevel.Warning, v.Level);
        Assert.Contains(v.Findings, f => f.Key == "Smart_Pending" && f.Level == DiskHealthLevel.Warning);
    }

    [Fact]
    public void CrcErrors_AreANote_NotAVerdict()
    {
        var v = DiskVerdict.Evaluate(Ata((0xC7, 100, 100, 9)));
        Assert.Equal(DiskHealthLevel.Good, v.Level);
        Assert.Contains(v.Findings, f => f.Key == "Smart_LinkErrors" && f.Level == DiskHealthLevel.Warning);
    }

    [Fact]
    public void HotHdd_IsWarning()
        => Assert.Equal(DiskHealthLevel.Warning, DiskVerdict.Evaluate(Ata((0xC2, 45, 40, 56))).Level);

    [Fact]
    public void Danger_BeatsWarning()
        => Assert.Equal(DiskHealthLevel.Danger, DiskVerdict.Evaluate(Ata((0x05, 100, 100, 1), (0xC5, 100, 100, 5))).Level);

    [Theory]
    [InlineData(0, 64, 100, 10, 1, 0UL, DiskHealthLevel.Good)]
    [InlineData(1, 64, 100, 10, 1, 0UL, DiskHealthLevel.Danger)]   // avviso critico
    [InlineData(0, 64, 5, 10, 1, 0UL, DiskHealthLevel.Danger)]     // riserva sotto soglia
    [InlineData(0, 64, 100, 10, 1, 3UL, DiskHealthLevel.Danger)]   // errori del supporto
    [InlineData(0, 64, 100, 10, 95, 0UL, DiskHealthLevel.Warning)] // usura
    [InlineData(0, 75, 100, 10, 1, 0UL, DiskHealthLevel.Warning)]  // temperatura
    public void Nvme_Rules(int crit, int temp, int spare, int thr, int used, ulong mediaErr, DiskHealthLevel expected)
    {
        var h = new NvmeHealth((byte)crit, temp, (byte)spare, (byte)thr, (byte)used, 100, 0, mediaErr);
        Assert.Equal(expected, DiskVerdict.Evaluate(h).Level);
    }

    [Fact]
    public void NvmeSpare_ShowsAvailableOverThreshold_WithNoWordsToTranslate()
    {
        // Il valore e' neutro: il senso — disponibile su soglia minima — lo dà la spiegazione
        // tradotta, non il testo della riga, che altrimenti resterebbe in italiano ovunque.
        var v = DiskVerdict.Evaluate(new NvmeHealth(0, 40, 100, 10, 1, 738, 0, 0));
        Assert.Equal("100 % / 10 %", v.Findings.Single(f => f.Key == "Smart_NvmeSpare").Value);
    }

    [Fact]
    public void Findings_ListTheValuesThatMatter_InAStableOrder()
    {
        var v = DiskVerdict.Evaluate(Ata((0x05, 100, 100, 0), (0x09, 100, 100, 387), (0xBB, 100, 100, 1138), (0xC2, 71, 62, 29), (0xC5, 100, 100, 0), (0xC6, 100, 100, 0), (0xC7, 100, 100, 9)));
        var keys = v.Findings.Select(f => f.Key).ToList();
        Assert.Equal(new[] { "Smart_Reallocated", "Smart_Pending", "Smart_Uncorrectable", "Smart_LinkErrors", "Smart_ReportedUncorrectable", "Smart_Temperature", "Smart_PowerOnHours" }, keys);
        Assert.Equal("1138", v.Findings.Single(f => f.Key == "Smart_ReportedUncorrectable").Value);
        Assert.Equal("29 °C", v.Findings.Single(f => f.Key == "Smart_Temperature").Value);
    }
}
