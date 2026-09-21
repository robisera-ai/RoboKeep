using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Due comportamenti legati tra loro: (1) il versioning non crea uno snapshot identico al
/// precedente; (2) la "forza copia" ricopia davvero un file cambiato a parita' di data e
/// dimensione — anche in un job versionato, e senza riscrivere gli snapshot precedenti.
/// Tutto su robocopy reale.
/// </summary>
public sealed class VersioningLoadAndForceCopyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcVfc_" + Guid.NewGuid().ToString("N"));
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");

    public VersioningLoadAndForceCopyTests() => Directory.CreateDirectory(Src);

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;
        Directory.Delete(_root, recursive: true);
    }

    private string[] Snapshots() =>
        Directory.GetDirectories(Dst).Select(Path.GetFileName)
            .Where(n => n is not null && !SnapshotName.IsInProgress(n!) && SnapshotName.TryParse(n!, out _))
            .Cast<string>().OrderBy(n => n).ToArray();

    /// <summary>Riscrive il contenuto lasciando identiche dimensione e data di modifica: il caso
    /// dei file (PST, database) che la forza copia esiste per coprire.</summary>
    private static void RewriteKeepingSizeAndTime(string path, string sameLengthContent)
    {
        var mtime = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, sameLengthContent);
        File.SetLastWriteTimeUtc(path, mtime);
    }

    private RobocopyRunner RunnerWithForceCopy() =>
        new(forceCopyPlanner: new ForceCopyPlanner(new ForceCopyHashStore(Path.Combine(_root, "hashes.json"))));

    // ---- niente snapshot se nulla e' cambiato ----

    [Fact]
    public async Task UnchangedSource_DoesNotCreateASecondSnapshot()
    {
        File.WriteAllText(Path.Combine(Src, "f.txt"), "v1");
        var job = new BackupJob { Name = "V", Source = Src, Destination = Dst, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());

        await svc.RunVersionedAsync(job);
        await Task.Delay(1100);
        var lines = new List<string>();
        var second = await svc.RunVersionedAsync(job, new Collect(lines));

        Assert.Single(Snapshots());
        Assert.True(second.Result.Success);
        Assert.False(second.Result.DryRun); // e' l'esito reale del job, non un'anteprima
        Assert.Equal(0, second.Result.ExitCode);
        Assert.Contains(lines, l => l.Contains(Snapshots()[0])); // il log dice quale versione resta valida
        Assert.Empty(Directory.GetDirectories(Dst).Where(d => SnapshotName.IsInProgress(Path.GetFileName(d))));
    }

    [Theory]
    [InlineData("nuovo")]
    [InlineData("cancellato")]
    [InlineData("cartella")]
    public async Task AnyRealChange_StillCreatesASnapshot(string change)
    {
        File.WriteAllText(Path.Combine(Src, "f.txt"), "v1");
        File.WriteAllText(Path.Combine(Src, "g.txt"), "v1");
        var job = new BackupJob { Name = "V", Source = Src, Destination = Dst, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());
        await svc.RunVersionedAsync(job);
        await Task.Delay(1100);

        switch (change)
        {
            case "nuovo": File.WriteAllText(Path.Combine(Src, "h.txt"), "x"); break;
            case "cancellato": File.Delete(Path.Combine(Src, "g.txt")); break;
            case "cartella": Directory.CreateDirectory(Path.Combine(Src, "vuota")); break;
        }
        await svc.RunVersionedAsync(job);

        Assert.Equal(2, Snapshots().Length);
    }

    // ---- forza copia ----

    [Fact]
    public async Task ForceCopy_RecopiesContentChangedWithSameSizeAndTime()
    {
        var file = Path.Combine(Src, "db.dat");
        File.WriteAllText(file, "AAAA");
        var job = new BackupJob { Name = "F", Source = Src, Destination = Dst, Mirror = true, MultiThread = 1 };
        job.ForceCopyFiles = new() { "db.dat" };
        var runner = RunnerWithForceCopy();

        await runner.RunAsync(job);
        RewriteKeepingSizeAndTime(file, "BBBB");
        var second = await runner.RunAsync(job);

        Assert.True(second.Result.Success);
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(Dst, "db.dat")));
    }

    [Fact]
    public async Task ForceCopy_InVersionedJob_CopiesNewContent_WithoutRewritingHistory()
    {
        var file = Path.Combine(Src, "db.dat");
        File.WriteAllText(file, "AAAA");
        File.WriteAllText(Path.Combine(Src, "altro.txt"), "invariato");
        var job = new BackupJob { Name = "VF", Source = Src, Destination = Dst, Versioned = true, MultiThread = 1 };
        job.ForceCopyFiles = new() { "db.dat" };
        var svc = new SnapshotService(RunnerWithForceCopy());

        await svc.RunVersionedAsync(job);
        await Task.Delay(1100);
        RewriteKeepingSizeAndTime(file, "BBBB");
        await svc.RunVersionedAsync(job);

        var snaps = Snapshots();
        Assert.Equal(2, snaps.Length);
        Assert.Equal("BBBB", File.ReadAllText(Path.Combine(Dst, snaps[1], "db.dat"))); // la versione nuova c'e'
        Assert.Equal("AAAA", File.ReadAllText(Path.Combine(Dst, snaps[0], "db.dat"))); // e la vecchia non e' stata riscritta
    }

    [Fact]
    public void UnlinkMatching_RemovesOnlyTheNamedFiles_AnywhereInTheTree()
    {
        var snap = Path.Combine(_root, "snap");
        Directory.CreateDirectory(Path.Combine(snap, "sub"));
        File.WriteAllText(Path.Combine(snap, "posta.pst"), "x");
        File.WriteAllText(Path.Combine(snap, "sub", "archivio.PST"), "x");
        File.WriteAllText(Path.Combine(snap, "sub", "note.txt"), "x");

        var n = SnapshotChangedUnlinker.UnlinkMatching(snap, new[] { "*.pst" });

        Assert.Equal(2, n);
        Assert.False(File.Exists(Path.Combine(snap, "posta.pst")));
        Assert.False(File.Exists(Path.Combine(snap, "sub", "archivio.PST")));
        Assert.True(File.Exists(Path.Combine(snap, "sub", "note.txt")));
    }

    private sealed class Collect : IProgress<string>
    {
        private readonly List<string> _lines;
        public Collect(List<string> lines) => _lines = lines;
        public void Report(string value) { lock (_lines) _lines.Add(value); }
    }
}
