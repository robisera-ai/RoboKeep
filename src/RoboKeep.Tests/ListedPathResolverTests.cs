using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class ListedPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcResolve_" + Guid.NewGuid().ToString("N"));
    private readonly string _src;
    private readonly string _cur;

    public ListedPathResolverTests()
    {
        _src = Path.Combine(_root, "src");
        _cur = Path.Combine(_root, "current");
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_cur);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    [Fact]
    public void ExistingPaths_PassThroughUnchanged()
    {
        Touch(Path.Combine(_src, "a.txt"));
        Touch(Path.Combine(_cur, "old.txt"));
        var unresolved = new List<string>();

        var r = ListedPathResolver.Resolve(new[]
        {
            new ListedChange(ChangeKind.NewFile, "a.txt"),
            new ListedChange(ChangeKind.ExtraFile, "old.txt"),
        }, _src, _cur, unresolved);

        Assert.Equal(new[] { "a.txt", "old.txt" }, r.Select(c => c.RelativePath));
        Assert.Equal(ChangeKind.NewFile, r[0].Kind);
        Assert.Empty(unresolved);
    }

    [Theory]
    [InlineData("foto?.txt")]   // un '?' per carattere
    [InlineData("foto??.txt")]  // un '?' per unita' UTF-16
    public void MangledName_IsResolvedToTheRealOne(string listed)
    {
        Touch(Path.Combine(_src, "foto\U0001F600.txt"));
        var unresolved = new List<string>();

        var r = ListedPathResolver.Resolve(new[] { new ListedChange(ChangeKind.Overwrite, listed) },
            _src, _cur, unresolved);

        Assert.Equal("foto\U0001F600.txt", Assert.Single(r).RelativePath);
        Assert.Empty(unresolved);
    }

    [Fact]
    public void AmbiguousName_TakesEveryCandidate()
    {
        Touch(Path.Combine(_cur, "Łodz.txt"));
        Touch(Path.Combine(_cur, "Đodz.txt"));
        Touch(Path.Combine(_src, "Đodz.txt"));   // c'e' ancora in sorgente: non e' extra
        var unresolved = new List<string>();

        var r = ListedPathResolver.Resolve(new[] { new ListedChange(ChangeKind.ExtraFile, "?odz.txt") },
            _src, _cur, unresolved);

        Assert.Equal("Łodz.txt", Assert.Single(r).RelativePath);
        Assert.Empty(unresolved);
    }

    [Fact]
    public void MangledFolderSegment_IsResolvedToo()
    {
        Touch(Path.Combine(_cur, "cartellaŁ", "dentro.txt"));
        var unresolved = new List<string>();

        var r = ListedPathResolver.Resolve(
            new[] { new ListedChange(ChangeKind.ExtraFile, Path.Combine("cartella?", "dentro.txt")) },
            _src, _cur, unresolved);

        Assert.Equal(Path.Combine("cartellaŁ", "dentro.txt"), Assert.Single(r).RelativePath);
    }

    [Fact]
    public void NothingMatches_GoesToUnresolved()
    {
        var unresolved = new List<string>();

        var r = ListedPathResolver.Resolve(new[]
        {
            new ListedChange(ChangeKind.Overwrite, "sparito?.txt"),
            new ListedChange(ChangeKind.ExtraFile, "Lodz.txt"),       // best-fit: niente '?', non esiste
        }, _src, _cur, unresolved);

        Assert.Empty(r);
        Assert.Equal(new[] { "sparito?.txt", "Lodz.txt" }, unresolved);
    }
}
