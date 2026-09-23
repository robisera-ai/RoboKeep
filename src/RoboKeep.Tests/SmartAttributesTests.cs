using RoboKeep.Core.Services.Smart;

namespace RoboKeep.Tests;

public class SmartAttributesTests
{
    /// <summary>Costruisce un blocco SMART ATA (512 byte) con gli attributi dati: (id, value, worst, raw).</summary>
    internal static byte[] AtaBlock(params (byte Id, byte Value, byte Worst, long Raw)[] attrs)
    {
        var b = new byte[512];
        b[0] = 0x10; b[1] = 0; // versione 16, come sui dischi reali
        for (var i = 0; i < attrs.Length; i++)
        {
            var o = 2 + i * 12;
            b[o] = attrs[i].Id; b[o + 1] = 0x33; b[o + 2] = 0; b[o + 3] = attrs[i].Value; b[o + 4] = attrs[i].Worst;
            var raw = attrs[i].Raw;
            for (var k = 0; k < 6; k++) { b[o + 5 + k] = (byte)(raw & 0xFF); raw >>= 8; }
        }
        return b;
    }

    [Fact]
    public void ParseAta_ReadsIdsValuesAndSixByteRaw()
    {
        // La fotografia del Samsung HD103SI del 23/09/2026: 05=0, 09=387 ore, BB=1138, C5=0, C6=0, C7=9.
        var block = AtaBlock((0x05, 100, 100, 0), (0x09, 100, 100, 387), (0xBB, 100, 100, 1138),
            (0xC2, 71, 62, 488439837), (0xC5, 100, 100, 0), (0xC6, 100, 100, 0), (0xC7, 100, 100, 9));
        var s = SmartAttributes.ParseAta(block)!;
        Assert.Equal(7, s.Count);
        Assert.Equal(387, s[0x09].Raw);
        Assert.Equal(1138, s[0xBB].Raw);
        Assert.Equal(9, s[0xC7].Raw);
        Assert.Equal(71, s[0xC2].Value);
        // La temperatura ATA sta nei due byte bassi del raw (il resto sono min/max del produttore).
        Assert.Equal(29, s[0xC2].Raw & 0xFFFF);
        Assert.Equal(29, s.TemperatureC);
        Assert.Equal(387, s.PowerOnHours);
    }

    [Fact]
    public void ParseAta_RejectsWrongSizeAndEmptyBlock()
    {
        Assert.Null(SmartAttributes.ParseAta(new byte[100]));
        Assert.Null(SmartAttributes.ParseAta(new byte[512])); // nessun attributo: non e' SMART
    }

    [Fact]
    public void TemperatureC_TakesTheLowByte_AndRejectsWhatIsNotATemperature()
    {
        // 0x00001E001E: 30 °C correnti, con minimo e massimo del produttore nei byte alti. Leggendo
        // due byte verrebbe 0x001E... cioe' un numero senza senso.
        Assert.Equal(30, SmartAttributes.ParseAta(AtaBlock((0xC2, 100, 100, 0x00001E001E)))!.TemperatureC);
        // 0xC8 = 200: su quel disco C2 non e' una temperatura. Meglio niente che un valore falso.
        Assert.Null(SmartAttributes.ParseAta(AtaBlock((0xC2, 100, 100, 0xC8)))!.TemperatureC);
        // BE (190) vale come ripiego quando C2 manca.
        Assert.Equal(37, SmartAttributes.ParseAta(AtaBlock((0xBE, 100, 100, 0x25)))!.TemperatureC);
    }

    [Fact]
    public void ParseAta_RawGreaterThan32Bits()
    {
        var s = SmartAttributes.ParseAta(AtaBlock((0xF1, 100, 100, 0x0123456789AB)))!;
        Assert.Equal(0x0123456789ABL, s[0xF1].Raw);
    }

    [Fact]
    public void ParseNvme_ReadsHealthLog()
    {
        var log = new byte[512];
        log[0] = 0x00;                                      // critical warning
        log[1] = 0x51; log[2] = 0x01;                       // temperatura 337 K = 64 C
        log[3] = 100; log[4] = 10; log[5] = 1;              // spare, soglia, usata
        BitConverter.GetBytes(4UL).CopyTo(log, 144);        // unsafe shutdowns
        BitConverter.GetBytes(0UL).CopyTo(log, 160);        // media errors
        BitConverter.GetBytes(738UL).CopyTo(log, 128);      // power on hours (16 byte, i primi 8 bastano)
        var h = SmartAttributes.ParseNvme(log)!;
        Assert.Equal(0, h.CriticalWarning);
        Assert.Equal(64, h.TemperatureC);
        Assert.Equal(100, h.AvailableSpare);
        Assert.Equal(10, h.SpareThreshold);
        Assert.Equal(1, h.PercentUsed);
        Assert.Equal(4UL, h.UnsafeShutdowns);
        Assert.Equal(0UL, h.MediaErrors);
        Assert.Equal(738UL, h.PowerOnHours);
        Assert.Null(SmartAttributes.ParseNvme(new byte[10]));
    }
}
