using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class IntegrityVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");

    public IntegrityVerifierTests()
    {
        Directory.CreateDirectory(Src);
        Directory.CreateDirectory(Dst);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private void Pair(string relative, string content, string? dstContent = null)
    {
        var s = Path.Combine(Src, relative);
        var d = Path.Combine(Dst, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(s)!);
        Directory.CreateDirectory(Path.GetDirectoryName(d)!);
        File.WriteAllText(s, content);
        File.WriteAllText(d, dstContent ?? content);
        // stessa data: solo il contenuto decide l'esito
        var t = new DateTime(2026, 7, 1, 12, 0, 0);
        File.SetLastWriteTime(s, t);
        File.SetLastWriteTime(d, t);
    }

    [Fact]
    public async Task Identical_NoMismatch()
    {
        Pair("a.txt", "same");
        Pair(@"sub\b.txt", "same2");
        var r = await IntegrityVerifier.VerifyAsync(Src, Dst, null, CancellationToken.None);
        Assert.Equal(2, r.Checked);
        Assert.Equal(0, r.Mismatched);
        Assert.Equal(0, r.ChangedSinceBackup);
        Assert.Equal(0, r.Missing);
    }

    [Fact]
    public async Task DifferentContent_SameDate_IsMismatch()
    {
        Pair("a.txt", "good", "CORRUPT");
        var r = await IntegrityVerifier.VerifyAsync(Src, Dst, null, CancellationToken.None);
        Assert.Equal(1, r.Mismatched);
        Assert.Contains(@"a.txt", r.MismatchedPaths[0]);
    }

    [Fact]
    public async Task DifferentContent_NewerSource_IsChangedSinceBackup()
    {
        Pair("a.txt", "nuovo contenuto", "vecchio contenuto");
        File.SetLastWriteTime(Path.Combine(Src, "a.txt"), new DateTime(2026, 7, 2, 12, 0, 0));
        var r = await IntegrityVerifier.VerifyAsync(Src, Dst, null, CancellationToken.None);
        Assert.Equal(0, r.Mismatched);
        Assert.Equal(1, r.ChangedSinceBackup);
    }

    [Fact]
    public async Task MissingInDestination_IsMissing()
    {
        Pair("a.txt", "x");
        File.WriteAllText(Path.Combine(Src, "only-src.txt"), "y");
        var r = await IntegrityVerifier.VerifyAsync(Src, Dst, null, CancellationToken.None);
        Assert.Equal(1, r.Missing);
    }

    [Fact]
    public async Task LockedFile_IsSkipped()
    {
        Pair("a.txt", "x");
        using var lockStream = new FileStream(Path.Combine(Src, "a.txt"),
            FileMode.Open, FileAccess.Read, FileShare.None);
        var r = await IntegrityVerifier.VerifyAsync(Src, Dst, null, CancellationToken.None);
        Assert.Equal(1, r.Skipped);
        Assert.Equal(0, r.Mismatched);
    }

    [Fact]
    public async Task Cancellation_Throws()
    {
        for (var i = 0; i < 20; i++) Pair($"f{i}.txt", $"c{i}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => IntegrityVerifier.VerifyAsync(Src, Dst, null, cts.Token));
    }
}
