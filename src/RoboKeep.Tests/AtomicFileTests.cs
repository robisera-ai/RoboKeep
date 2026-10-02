using System.Text;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcAtomic_" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Writes_Overwrites_AndLeavesNoTemporaryFile()
    {
        var path = Path.Combine(_dir, "sotto", "config.json");

        AtomicFile.WriteAllText(path, "{\"a\":1}");
        AtomicFile.WriteAllText(path, "{\"a\":2}");

        Assert.Equal("{\"a\":2}", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "sotto")));
    }

    [Fact]
    public void DefaultIsUtf8WithoutBom_ButAnEncodingWithBomIsHonoured()
    {
        var plain = Path.Combine(_dir, "plain.txt");
        var withBom = Path.Combine(_dir, "bom.txt");

        AtomicFile.WriteAllText(plain, "è");
        AtomicFile.WriteAllText(withBom, "è", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal(new byte[] { 0xC3, 0xA8 }, File.ReadAllBytes(plain));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xA8 }, File.ReadAllBytes(withBom));
    }
}
