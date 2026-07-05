using System.Text.RegularExpressions;
using RoboKeep.Localization;

namespace RoboKeep.Tests;

public class LocParityTests
{
    [Fact]
    public void AllLanguages_HaveTheSameKeySet()
    {
        var reference = Loc.Langs["it"].Keys.OrderBy(k => k).ToList();
        foreach (var lang in Loc.Supported)
        {
            var keys = Loc.Langs[lang].Keys.OrderBy(k => k).ToList();
            Assert.True(reference.SequenceEqual(keys),
                $"Lingua {lang}: chiavi diverse da it. Mancanti: [{string.Join(", ", reference.Except(keys))}] Extra: [{string.Join(", ", keys.Except(reference))}]");
        }
    }

    [Fact]
    public void EveryKeyUsedInSource_IsDefined()
    {
        // Risale alla cartella src del repo dal percorso dell'assembly di test.
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        if (dir is null) return; // fuori dal repo (CI con layout diverso): salta senza fallire

        var defined = Loc.Langs["en"].Keys.ToHashSet();
        var missing = new List<string>();
        var rxCs = new Regex(@"Loc\.Instance\[""(\w+)""\]");
        var rxXaml = new Regex(@"\{l:Tr (\w+)\}");
        foreach (var file in Directory.EnumerateFiles(Path.Combine(dir, "src", "RoboKeep"), "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
                     .Where(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\")))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in rxCs.Matches(text))
                if (!defined.Contains(m.Groups[1].Value)) missing.Add($"{m.Groups[1].Value} ({Path.GetFileName(file)})");
            foreach (Match m in rxXaml.Matches(text))
                if (!defined.Contains(m.Groups[1].Value)) missing.Add($"{m.Groups[1].Value} ({Path.GetFileName(file)})");
        }
        Assert.True(missing.Count == 0, "Chiavi usate ma non definite: " + string.Join("; ", missing.Distinct()));
    }
}
