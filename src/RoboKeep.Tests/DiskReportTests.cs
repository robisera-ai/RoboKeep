using RoboKeep.Core.Services.Smart;
using static RoboKeep.Tests.SmartAttributesTests;

namespace RoboKeep.Tests;

public class DiskReportTests
{
    private static byte[] NvmeLog()
    {
        var log = new byte[512];
        log[1] = 0x51; log[2] = 0x01;          // 337 K = 64 C
        log[3] = 100; log[4] = 10; log[5] = 1; // spare, soglia, usata
        BitConverter.GetBytes(738UL).CopyTo(log, 128);
        return log;
    }

    [Fact]
    public void JsonRoundTrip_KeepsFieldsAndDecodesBothKindsOfSmart()
    {
        var ata = AtaBlock((0x05, 100, 100, 0), (0x09, 100, 100, 387), (0xC7, 100, 100, 9));
        var source = new List<DiskReport>
        {
            new(0, "ORICO", "SN-NVME", DiskBus.Nvme, new[] { "C:", "D:" }, null, NvmeLog(), null),
            new(1, "SAMSUNG HD103SI", "SN-USB", DiskBus.Usb, new[] { "E:" }, ata, null, null),
        };

        var back = DiskReport.FromJson(DiskReport.ToJson(source));

        Assert.Equal(2, back.Count);
        var nvme = back[0];
        Assert.Equal(0, nvme.Number);
        Assert.Equal("ORICO", nvme.Model);
        Assert.Equal("SN-NVME", nvme.Serial);
        Assert.Equal(DiskBus.Nvme, nvme.Bus);
        Assert.Equal(new[] { "C:", "D:" }, nvme.Letters);
        Assert.Null(nvme.Error);
        Assert.Null(nvme.Ata);
        Assert.NotNull(nvme.Nvme);
        Assert.Equal(64, nvme.Nvme!.TemperatureC);
        Assert.Equal(738UL, nvme.Nvme.PowerOnHours);
        Assert.True(nvme.IsReadable);

        var usb = back[1];
        Assert.Equal(1, usb.Number);
        Assert.Equal("SAMSUNG HD103SI", usb.Model);
        Assert.Equal(DiskBus.Usb, usb.Bus);
        Assert.Equal(new[] { "E:" }, usb.Letters);
        Assert.Equal(ata, usb.AtaBlock);
        Assert.Null(usb.Nvme);
        Assert.NotNull(usb.Ata);
        Assert.Equal(387, usb.Ata!.PowerOnHours);
        Assert.Equal(9, usb.Ata[0xC7].Raw);
        Assert.True(usb.IsReadable);
    }

    [Fact]
    public void IsReadable_IsFalse_WhenBothBlocksAreNull()
    {
        var d = new DiskReport(2, "?", "", DiskBus.Unknown, Array.Empty<string>(), null, null, "ata");
        Assert.False(d.IsReadable);
        Assert.Null(d.Ata);
        Assert.Null(d.Nvme);
        var back = DiskReport.FromJson(DiskReport.ToJson(new[] { d })).Single();
        Assert.False(back.IsReadable);
        Assert.Equal("ata", back.Error);
        Assert.Empty(back.Letters);
    }

    [Fact]
    public void FromJson_OnGarbage_ReturnsEmptyOrThrowsNothingUseful()
        => Assert.Empty(DiskReport.FromJson("[]"));
}
