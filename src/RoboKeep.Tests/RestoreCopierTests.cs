using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// La copia vera del ripristino su cartelle temporanee: le date conservate, le cartelle vuote
/// ricreate, un file che non si riesce a leggere elencato alla fine senza fermare gli altri, e il
/// rifiuto di ripristinare sopra la sorgente del job.
/// </summary>
public class RestoreCopierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcRestore_" + Guid.NewGuid().ToString("N"));

    public RestoreCopierTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* un handle ancora aperto non deve far fallire la suite */ }
    }

    private string Make(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static RestorePlan Plan(IEnumerable<RestoreEntry> files, params string[] dirs)
    {
        var map = files.ToDictionary(e => e.RelativePath, e => e, StringComparer.OrdinalIgnoreCase);
        return new RestorePlan(map, dirs, map.Values.Sum(e => e.Size));
    }

    private static RestoreEntry Entry(string rel, string source)
    {
        var fi = new FileInfo(source);
        return new RestoreEntry(rel, source, fi.Length, fi.LastWriteTime);
    }

    [Fact]
    public async Task CopiesTheFiles_KeepingTheirLastWriteTime()
    {
        var a = Make(@"src\a.txt", "contenuto a");
        var b = Make(@"src\sotto\b.txt", "contenuto b");
        var when = new DateTime(2019, 3, 14, 9, 26, 53);
        File.SetLastWriteTime(a, when);
        File.SetLastWriteTime(b, when);

        var target = Path.Combine(_root, "dentro");
        var outcome = await RestoreCopier.CopyAsync(
            Plan(new[] { Entry("a.txt", a), Entry(@"sotto\b.txt", b) }), target);

        Assert.Equal(2, outcome.Copied);
        Assert.Empty(outcome.Failures);
        Assert.False(outcome.Cancelled);
        Assert.Equal("contenuto a", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal("contenuto b", File.ReadAllText(Path.Combine(target, "sotto", "b.txt")));
        // La data è un dato: è quella che dice «questa è la lettera di marzo».
        Assert.Equal(when, File.GetLastWriteTime(Path.Combine(target, "a.txt")));
        Assert.Equal(when, File.GetLastWriteTime(Path.Combine(target, "sotto", "b.txt")));
    }

    [Fact]
    public async Task CreatesTheKnownEmptyFolders()
    {
        var a = Make(@"src\a.txt", "x");
        var target = Path.Combine(_root, "dentro");

        var outcome = await RestoreCopier.CopyAsync(
            Plan(new[] { Entry("a.txt", a) }, "vuota", @"vuota\ancora"), target);

        Assert.Equal(1, outcome.Copied);
        Assert.True(Directory.Exists(Path.Combine(target, "vuota", "ancora")));
    }

    [Fact]
    public async Task AnUnreadableFile_IsListed_AndTheOthersStillArrive()
    {
        var locked = Make(@"src\bloccato.txt", "in uso");
        var ok = Make(@"src\ok.txt", "libero");
        var target = Path.Combine(_root, "dentro");

        // Nessuna condivisione: File.Copy non riesce nemmeno ad aprirlo in lettura.
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var outcome = await RestoreCopier.CopyAsync(
                Plan(new[] { Entry("bloccato.txt", locked), Entry("ok.txt", ok) }), target);

            Assert.Equal(1, outcome.Copied);
            Assert.Equal(2, outcome.Total);
            var failure = Assert.Single(outcome.Failures);
            Assert.Equal("bloccato.txt", failure.RelativePath);
            Assert.NotEmpty(failure.Error);
        }

        Assert.Equal("libero", File.ReadAllText(Path.Combine(target, "ok.txt")));
    }

    [Fact]
    public async Task AFileAlreadyThere_IsNeverOverwritten()
    {
        var a = Make(@"src\a.txt", "quello di ieri");
        var target = Path.Combine(_root, "dentro");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.txt"), "quello di oggi");

        var outcome = await RestoreCopier.CopyAsync(Plan(new[] { Entry("a.txt", a) }), target);

        Assert.Equal(0, outcome.Copied);
        Assert.Single(outcome.Failures);
        Assert.Equal("quello di oggi", File.ReadAllText(Path.Combine(target, "a.txt")));
    }

    [Fact]
    public async Task ReportsProgress_FileByFile()
    {
        var a = Make(@"src\a.txt", "1");
        var b = Make(@"src\b.txt", "2");
        var seen = new List<(int Done, int Total)>();
        var target = Path.Combine(_root, "dentro");

        await RestoreCopier.CopyAsync(Plan(new[] { Entry("a.txt", a), Entry("b.txt", b) }), target,
            progress: new Progress<(int, int)>(p => { lock (seen) seen.Add(p); }));

        // Progress<T> consegna sul pool: si aspetta che le due notifiche siano arrivate.
        for (var i = 0; i < 50 && Count() < 2; i++) await Task.Delay(20);
        Assert.Equal(2, Count());
        int Count() { lock (seen) return seen.Count; }
    }

    [Fact]
    public async Task ACancelledRestore_StopsAndSaysSo()
    {
        var a = Make(@"src\a.txt", "1");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await RestoreCopier.CopyAsync(Plan(new[] { Entry("a.txt", a) }),
            Path.Combine(_root, "dentro"), ct: cts.Token);

        Assert.True(outcome.Cancelled);
        Assert.Equal(0, outcome.Copied);
    }

    [Fact]
    public async Task RefusesATargetThatIsTheJobSource()
    {
        var a = Make(@"src\a.txt", "1");
        var source = Path.Combine(_root, "src");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RestoreCopier.CopyAsync(Plan(new[] { Entry("a.txt", a) }), source, jobSource: source));
    }

    [Fact]
    public async Task RefusesATargetInsideTheJobSource()
    {
        var a = Make(@"src\a.txt", "1");
        var source = Path.Combine(_root, "src");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RestoreCopier.CopyAsync(Plan(new[] { Entry("a.txt", a) }),
                Path.Combine(source, "recuperati"), jobSource: source));
    }

    [Fact]
    public void ASisterFolderOfTheSource_IsNotInsideIt()
    {
        // «C:\dati» e «C:\dati-vecchi»: il confronto va fatto per segmenti, non per prefisso di
        // stringa, altrimenti una cartella legittima verrebbe rifiutata.
        Assert.False(RestoreCopier.IsSourceOrInside(@"C:\dati-vecchi", @"C:\dati"));
        Assert.True(RestoreCopier.IsSourceOrInside(@"C:\dati\sotto", @"C:\dati"));
        Assert.True(RestoreCopier.IsSourceOrInside(@"C:\DATI\", @"C:\dati"));
        Assert.False(RestoreCopier.IsSourceOrInside(@"C:\altrove", @"C:\dati"));
        Assert.False(RestoreCopier.IsSourceOrInside(@"C:\dati", null));
    }

    [Fact]
    public async Task APathThatWouldEscapeTheTarget_IsRefused()
    {
        var a = Make(@"src\a.txt", "1");
        var target = Path.Combine(_root, "dentro");

        var outcome = await RestoreCopier.CopyAsync(
            Plan(new[] { new RestoreEntry(@"..\fuori.txt", a, 1, null) }), target);

        Assert.Equal(0, outcome.Copied);
        Assert.Single(outcome.Failures);
        Assert.False(File.Exists(Path.Combine(_root, "fuori.txt")));
    }

    /// <summary>
    /// Percorsi ostili: un manifest modificato a mano, o un file d'archivio con un nome
    /// fabbricato, non deve poter scrivere fuori dalla cartella scelta. Ognuno di questi è un modo
    /// diverso di uscirne — percorso assoluto, condivisione di rete, sintassi lunga di Windows,
    /// percorso relativo al disco, nome di periferica riservato, risalita con «..» — e vanno
    /// rifiutati tutti allo stesso modo: il file finisce tra i non ripristinati, il resto arriva.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Windows\Temp\rubato.txt")]
    [InlineData(@"\\server\condivisa\rubato.txt")]
    [InlineData(@"\\?\C:\Windows\Temp\rubato.txt")]
    [InlineData(@"C:rubato.txt")]
    [InlineData("NUL")]
    [InlineData(@"sotto\..\..\rubato.txt")]
    [InlineData(@"..\..\rubato.txt")]
    public async Task AHostileRelativePath_IsRefused_AndTheOthersStillArrive(string hostile)
    {
        var a = Make(@"src\a.txt", "buono");
        var target = Path.Combine(_root, "dentro");

        var outcome = await RestoreCopier.CopyAsync(Plan(new[]
        {
            new RestoreEntry(hostile, a, 1, null),
            Entry("ok.txt", a),
        }), target);

        Assert.Equal(1, outcome.Copied);
        var failure = Assert.Single(outcome.Failures);
        Assert.Equal(hostile, failure.RelativePath);
        Assert.Equal("buono", File.ReadAllText(Path.Combine(target, "ok.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "rubato.txt")));
    }

    [Fact]
    public async Task AHostileFolderPath_IsRefusedToo()
    {
        var a = Make(@"src\a.txt", "buono");
        var target = Path.Combine(_root, "dentro");

        var outcome = await RestoreCopier.CopyAsync(
            Plan(new[] { Entry("ok.txt", a) }, @"..\fuori-creata"), target);

        Assert.Equal(1, outcome.Copied);
        Assert.Single(outcome.Failures);
        Assert.False(Directory.Exists(Path.Combine(_root, "fuori-creata")));
    }
}
