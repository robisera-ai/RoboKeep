using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class HardLinkClonerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcClone_" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    [Fact]
    public void Clone_RecreatesTree_WithHardLinkedFiles()
    {
        var src = Path.Combine(_root, "src");
        var dst = Path.Combine(_root, "dst");
        Directory.CreateDirectory(Path.Combine(src, "sub"));
        File.WriteAllText(Path.Combine(src, "a.txt"), "AAA");
        File.WriteAllText(Path.Combine(src, "sub", "b.txt"), "BBB");

        HardLinkCloner.Clone(src, dst);

        Assert.Equal("AAA", File.ReadAllText(Path.Combine(dst, "a.txt")));
        Assert.Equal("BBB", File.ReadAllText(Path.Combine(dst, "sub", "b.txt")));

        // sono hard-link: scrivere sul file sorgente IN-PLACE si riflette sul clone
        using (var fs = new FileStream(Path.Combine(src, "a.txt"), FileMode.Open, FileAccess.Write))
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes("ZZZ");
            fs.Write(bytes, 0, bytes.Length);
        }
        Assert.Equal("ZZZ", File.ReadAllText(Path.Combine(dst, "a.txt")));
    }

    [Fact]
    public void Clone_SkipsUnlinkableFile_AndContinues()
    {
        // Un file che non si riesce a collegare non deve far crollare il clone: viene saltato,
        // gli altri proseguono. Simuliamo il fallimento pre-creando la destinazione di "a.txt"
        // come CARTELLA, cosi' l'hard-link su quel percorso fallisce (come farebbe un settore rotto).
        var src = Path.Combine(_root, "src");
        var dst = Path.Combine(_root, "dst");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "AAA");
        File.WriteAllText(Path.Combine(src, "b.txt"), "BBB");
        Directory.CreateDirectory(Path.Combine(dst, "a.txt")); // ostacolo: a.txt gia' esiste come cartella

        var skippedPaths = new List<string>();
        var skipped = HardLinkCloner.Clone(src, dst, (path, _) => skippedPaths.Add(path));

        Assert.Equal(1, skipped);                                  // solo a.txt saltato
        Assert.Contains(skippedPaths, p => p.EndsWith("a.txt"));
        Assert.Equal("BBB", File.ReadAllText(Path.Combine(dst, "b.txt"))); // b.txt collegato lo stesso
    }
}
