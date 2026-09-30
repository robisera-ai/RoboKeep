using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Ricostruzione di «com'era alla data X» su archivi costruiti in memoria: nessun disco, nessun
/// robocopy, solo le regole. È il cuore del ripristino, e le sue risposte sbagliate sarebbero
/// invisibili — un file in meno o una versione più vecchia del dovuto non fanno rumore.
/// </summary>
public class RestorePlannerTests
{
    private const string Dest = @"D:\bk\Documenti";
    private static string Current => VersioningLayout.CurrentDir(Dest);
    private static string Versions => VersioningLayout.VersionsDir(Dest);

    private static readonly DateTime D1 = new(2026, 9, 26, 21, 30, 0);
    private static readonly DateTime D2 = new(2026, 9, 27, 21, 30, 0);
    private static readonly DateTime D3 = new(2026, 9, 28, 21, 30, 0);

    private const string V1 = "2026-09-26_213000";
    private const string V2 = "2026-09-27_213000";
    private const string V3 = "2026-09-28_213000";

    /// <summary>Archivio finto: file, cartelle, punti nel tempo e manifest descritti a mano.</summary>
    private sealed class Fake
    {
        private static readonly StringComparer C = StringComparer.OrdinalIgnoreCase;
        private readonly Dictionary<string, RestoreFile> _files = new(C);
        private readonly HashSet<string> _dirs = new(C);
        private readonly Dictionary<string, VersionManifest> _manifests = new(C);
        private readonly List<VersionPoint> _points = new();

        public Fake File(string path, long size = 10, DateTime? when = null)
        {
            _files[path] = new RestoreFile(path, size, when ?? new DateTime(2026, 1, 1));
            Ancestors(path);
            return this;
        }

        public Fake Dir(string path) { _dirs.Add(path); Ancestors(path); return this; }

        public Fake Point(string name, DateTime date, bool hasFolder, VersionManifest? manifest)
        {
            _points.Add(new VersionPoint(name, date, hasFolder));
            if (hasFolder) Dir(System.IO.Path.Combine(Versions, name));
            if (manifest is not null) _manifests[System.IO.Path.Combine(Versions, name)] = manifest;
            return this;
        }

        private void Ancestors(string path)
        {
            var d = System.IO.Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(d)) { _dirs.Add(d); d = System.IO.Path.GetDirectoryName(d); }
        }

        public RestoreReader Reader => new()
        {
            Files = dir => Under(_files.Keys, dir).Select(p => _files[p]).ToList(),
            Directories = dir => Under(_dirs, dir).ToList(),
            Stat = p => _files.TryGetValue(p, out var f) ? f : null,
            DirectoryExists = _dirs.Contains,
            Versions = _ => _points.OrderBy(p => p.Date).ToList(),
            Manifest = d => _manifests.TryGetValue(d, out var m) ? m : null,
        };

        private static IEnumerable<string> Under(IEnumerable<string> all, string dir) =>
            all.Where(p => p.StartsWith(dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
    }

    private static VersionManifest Manifest(DateTime when, string[]? changed = null,
        string[]? deleted = null, string[]? added = null) => new()
    {
        CreatedAt = when,
        Changed = (changed ?? Array.Empty<string>()).ToList(),
        Deleted = (deleted ?? Array.Empty<string>()).ToList(),
        Added = (added ?? Array.Empty<string>()).ToList(),
    };

    // ---------------- ricostruzione ----------------

    [Fact]
    public void Now_TakesEverythingFromCurrent_AndNoVersionIsRead()
    {
        var fake = new Fake()
            .File(Path.Combine(Current, "a.txt"))
            .File(Path.Combine(Current, "sotto", "b.txt"))
            .Dir(Path.Combine(Current, "vuota"))
            .Point("2026-09-27_213000", D2, hasFolder: true,
                Manifest(D2, changed: new[] { "a.txt" }))
            .File(Path.Combine(Versions, "2026-09-27_213000", "a.txt"));

        var plan = RestorePlanner.Resolve(Dest, null, fake.Reader);

        Assert.Equal(2, plan.Files.Count);
        Assert.Equal(Path.Combine(Current, "a.txt"), plan.Files["a.txt"].SourcePath);
        Assert.Equal(Path.Combine(Current, "sotto", "b.txt"), plan.Files[@"sotto\b.txt"].SourcePath);
        // «Adesso» conserva anche le cartelle vuote di oggi: esistono, e un recupero che le
        // lasciasse indietro restituirebbe un albero diverso da quello che l'utente vede.
        Assert.Contains("vuota", plan.Directories);
    }

    [Fact]
    public void ChoosingAPoint_MeansBeforeThatBackup_SoEvenTheNewestDiffersFromNow()
    {
        // Un punto vuol dire «l'albero PRIMA di quel backup»: la versione scelta si applica
        // anch'essa, perché la sua cartella contiene proprio lo stato di prima. Anche il punto più
        // recente è quindi uno stato diverso da «Adesso», e la finestra lo elenca.
        var fake = new Fake()
            .File(Path.Combine(Current, "a.txt"), size: 300)
            .Point(V2, D2, hasFolder: true, Manifest(D2, changed: new[] { "a.txt" }))
            .File(Path.Combine(Versions, V2, "a.txt"), size: 100);

        var beforeNewest = RestorePlanner.Resolve(Dest, D2, fake.Reader);
        var now = RestorePlanner.Resolve(Dest, null, fake.Reader);

        Assert.Equal(100, beforeNewest.Files["a.txt"].Size);
        Assert.Equal(300, now.Files["a.txt"].Size);
    }

    [Fact]
    public void UntouchedFile_ComesFromCurrent_EvenGoingBackInTime()
    {
        var fake = new Fake()
            .File(Path.Combine(Current, "mai-toccato.txt"), size: 7)
            .File(Path.Combine(Current, "a.txt"))
            .Point(V2, D2, hasFolder: false, Manifest(D2))
            .Point(V3, D3, hasFolder: true, Manifest(D3, changed: new[] { "a.txt" }))
            .File(Path.Combine(Versions, V3, "a.txt"), size: 99);

        var plan = RestorePlanner.Resolve(Dest, D2, fake.Reader);

        Assert.Equal(Path.Combine(Current, "mai-toccato.txt"), plan.Files["mai-toccato.txt"].SourcePath);
        Assert.Equal(Path.Combine(Versions, V3, "a.txt"), plan.Files["a.txt"].SourcePath);
        Assert.Equal(7 + 99, plan.TotalBytes);
    }

    [Fact]
    public void FileAddedByALaterBackup_IsNotInThePlan()
    {
        var fake = new Fake()
            .File(Path.Combine(Current, "vecchio.txt"))
            .File(Path.Combine(Current, "nuovo.txt"))
            .Point(V2, D2, hasFolder: false, Manifest(D2))
            .Point(V3, D3, hasFolder: false, Manifest(D3, added: new[] { "nuovo.txt" }));

        var plan = RestorePlanner.Resolve(Dest, D2, fake.Reader);

        Assert.True(plan.Files.ContainsKey("vecchio.txt"));
        Assert.False(plan.Files.ContainsKey("nuovo.txt")); // il backup del 27 non lo aveva ancora
    }

    [Fact]
    public void AFileAddedByOneBackupAndChangedByTheNext_ExistsOnlyBeforeTheSecond()
    {
        // x.txt nasce con il backup del 27 e viene riscritto da quello del 28.
        // «Prima del backup del 27» → non esisteva ancora; «prima del backup del 28» → il
        // contenuto che il 27 gli aveva dato, messo da parte dal backup del 28.
        var fake = new Fake()
            .File(Path.Combine(Current, "x.txt"), size: 300)
            .Point(V1, D1, hasFolder: false, Manifest(D1))
            .Point(V2, D2, hasFolder: false, Manifest(D2, added: new[] { "x.txt" }))
            .Point(V3, D3, hasFolder: true, Manifest(D3, changed: new[] { "x.txt" }))
            .File(Path.Combine(Versions, V3, "x.txt"), size: 200);

        Assert.False(RestorePlanner.Resolve(Dest, D1, fake.Reader)
            .Files.ContainsKey("x.txt"));
        Assert.False(RestorePlanner.Resolve(Dest, D2, fake.Reader)
            .Files.ContainsKey("x.txt"));
        Assert.Equal(200, RestorePlanner.Resolve(Dest, D3, fake.Reader)
            .Files["x.txt"].Size);
    }

    [Fact]
    public void ChangedThreeTimes_EachPointGivesTheStateBeforeItsBackup()
    {
        // lettera.docx riscritta dai backup del 26, del 27 e del 28. Ogni cartella-versione
        // contiene lo stato di PRIMA del proprio backup, e scegliere quel punto dà proprio quello:
        //   prima del 26 → 50,  prima del 27 → 100,  prima del 28 → 200,  adesso → 300.
        // Vince sempre la versione più vicina: prima del 26 NON prende la copia del 27.
        var fake = new Fake()
            .File(Path.Combine(Current, "lettera.docx"), size: 300)
            .Point(V1, D1, hasFolder: true, Manifest(D1, changed: new[] { "lettera.docx" }))
            .File(Path.Combine(Versions, V1, "lettera.docx"), size: 50)
            .Point(V2, D2, hasFolder: true, Manifest(D2, changed: new[] { "lettera.docx" }))
            .File(Path.Combine(Versions, V2, "lettera.docx"), size: 100)
            .Point(V3, D3, hasFolder: true, Manifest(D3, changed: new[] { "lettera.docx" }))
            .File(Path.Combine(Versions, V3, "lettera.docx"), size: 200);

        long SizeAt(DateTime? when) => RestorePlanner
            .Resolve(Dest, when, fake.Reader).Files["lettera.docx"].Size;

        Assert.Equal(50, SizeAt(D1));
        Assert.Equal(100, SizeAt(D2));
        Assert.Equal(200, SizeAt(D3));
        Assert.Equal(300, SizeAt(null));
    }

    [Fact]
    public void DeletedFile_ComesBackFromItsVersion()
    {
        var fake = new Fake()
            .File(Path.Combine(Current, "resta.txt"))
            .Point(V2, D2, hasFolder: false, Manifest(D2))
            .Point(V3, D3, hasFolder: true, Manifest(D3, deleted: new[] { "prezioso.txt" }))
            .File(Path.Combine(Versions, V3, "prezioso.txt"), size: 42);

        // «Prima del backup che l'ha cancellato»: il punto stesso di quel backup basta, anche se
        // quello precedente (qui D2) non esistesse più. È il caso reale in cui il manifest del
        // backup precedente, di sole aggiunte, è già stato potato dalla ritenzione.
        foreach (var when in new[] { D2, D3 })
        {
            var plan = RestorePlanner.Resolve(Dest, when, fake.Reader);
            Assert.Equal(Path.Combine(Versions, V3, "prezioso.txt"), plan.Files["prezioso.txt"].SourcePath);
            Assert.Equal(2, plan.Files.Count);
        }
    }

    [Fact]
    public void DeletedFolder_BringsBackEverythingInside()
    {
        // Nel manifest una cartella sparita è UNA voce; sul disco è un albero intero, e di quella
        // roba la cartella-versione è l'unica copia rimasta.
        var fake = new Fake()
            .File(Path.Combine(Current, "resta.txt"))
            .Point(V2, D2, hasFolder: false, Manifest(D2))
            .Point(V3, D3, hasFolder: true, Manifest(D3, deleted: new[] { "archivio" }))
            .File(Path.Combine(Versions, V3, "archivio", "1.txt"))
            .File(Path.Combine(Versions, V3, "archivio", "dentro", "2.txt"))
            .Dir(Path.Combine(Versions, V3, "archivio", "vuota"));

        var plan = RestorePlanner.Resolve(Dest, D2, fake.Reader);

        Assert.True(plan.Files.ContainsKey(@"archivio\1.txt"));
        Assert.True(plan.Files.ContainsKey(@"archivio\dentro\2.txt"));
        Assert.Contains("archivio", plan.Directories);
        Assert.Contains(@"archivio\vuota", plan.Directories);
    }

    [Fact]
    public void ManifestOnlyPoint_OnlyRemovesWhatThatBackupAdded()
    {
        // Un backup di sole aggiunte non ha cartella: resta il manifest, e serve esattamente a
        // sapere che prima di lui quei file non c'erano ancora.
        var fake = new Fake()
            .File(Path.Combine(Current, "vecchio.txt"))
            .File(Path.Combine(Current, "foto", "nuova.jpg"))
            .Point(V1, D1, hasFolder: false, Manifest(D1))
            .Point(V2, D2, hasFolder: false, Manifest(D2, added: new[] { @"foto\nuova.jpg" }));

        var plan = RestorePlanner.Resolve(Dest, D1, fake.Reader);

        Assert.True(plan.Files.ContainsKey("vecchio.txt"));
        Assert.False(plan.Files.ContainsKey(@"foto\nuova.jpg"));
    }

    [Fact]
    public void AVersionOlderThanTheChosenBackup_IsIgnored()
    {
        var fake = new Fake()
            .File(Path.Combine(Current, "a.txt"), size: 300)
            .Point(V1, D1, hasFolder: true, Manifest(D1, changed: new[] { "a.txt" }))
            .File(Path.Combine(Versions, V1, "a.txt"), size: 10)
            .Point(V3, D3, hasFolder: false, Manifest(D3));

        var plan = RestorePlanner.Resolve(Dest, D3, fake.Reader);

        // Il backup del 26 è successo PRIMA di quello chiesto: quello che ha messo da parte è
        // roba più vecchia ancora, e non descrive l'albero lasciato dal backup del 28.
        Assert.Equal(300, plan.Files["a.txt"].Size);
    }

    [Fact]
    public void AFileMissingFromItsVersionFolder_FallsBackToTheNextVersion()
    {
        // File in uso al momento del backup del 27: non si è potuto mettere da parte, e nella sua
        // cartella non c'è. La copia più vicina resta allora quella del 28.
        var fake = new Fake()
            .File(Path.Combine(Current, "db.mdb"), size: 300)
            .Point(V1, D1, hasFolder: false, Manifest(D1))
            .Point(V2, D2, hasFolder: true, Manifest(D2, changed: new[] { "db.mdb" }))
            .Point(V3, D3, hasFolder: true, Manifest(D3, changed: new[] { "db.mdb" }))
            .File(Path.Combine(Versions, V3, "db.mdb"), size: 200);

        var plan = RestorePlanner.Resolve(Dest, D1, fake.Reader);

        Assert.Equal(200, plan.Files["db.mdb"].Size);
    }

    [Fact]
    public void AVersionWithoutManifest_StillGivesBackWhatItHolds()
    {
        var fake = new Fake()
            .File(Path.Combine(Current, "a.txt"), size: 300)
            .Point(V2, D2, hasFolder: false, Manifest(D2))
            .Point(V3, D3, hasFolder: true, manifest: null)
            .File(Path.Combine(Versions, V3, "a.txt"), size: 10);

        var plan = RestorePlanner.Resolve(Dest, D2, fake.Reader);

        Assert.Equal(10, plan.Files["a.txt"].Size);
    }

    // ---------------- nessuna versione ancora ----------------

    [Fact]
    public void NothingOnDiskYet_IsAnEmptyPlan()
    {
        // Job con le versioni appena accese e nessun backup fatto: niente «current», niente
        // versioni. Il ripristino si apre lo stesso e dice che non c'è niente da recuperare.
        var fake = new Fake();

        Assert.Empty(RestorePlanner.Resolve(Dest, null, fake.Reader).Files);
        Assert.Empty(RestorePlanner.Resolve(Dest, D2, fake.Reader).Files);
    }

    [Fact]
    public void CurrentWithoutVersions_IsTheAnswerForAnyDate()
    {
        // Primo backup fatto, nessun file ancora sostituito: ogni data dà l'albero di «current».
        var fake = new Fake().File(Path.Combine(Current, "a.txt"), size: 5);

        Assert.Equal(Path.Combine(Current, "a.txt"),
            RestorePlanner.Resolve(Dest, null, fake.Reader).Files["a.txt"].SourcePath);
        Assert.Equal(Path.Combine(Current, "a.txt"),
            RestorePlanner.Resolve(Dest, D2, fake.Reader).Files["a.txt"].SourcePath);
    }

    [Fact]
    public void DatedFoldersInTheRoot_AreNeverRead()
    {
        // Cartelle con nome-data nella radice della destinazione non sono versioni: il piano
        // legge solo «current» e «versions».
        var fake = new Fake()
            .File(Path.Combine(Current, "a.txt"), size: 1)
            .File(Path.Combine(Dest, V2, "a.txt"), size: 99)
            .File(Path.Combine(Dest, V2, "solo-qui.txt"), size: 3);

        var plan = RestorePlanner.Resolve(Dest, D1, fake.Reader);

        Assert.Single(plan.Files);
        Assert.Equal(1, plan.Files["a.txt"].Size);
    }

    [Fact]
    public void EmptyDestination_IsAnEmptyPlan()
        => Assert.Empty(RestorePlanner.Resolve("", null, new Fake().Reader).Files);

    // ---------------- selezione ----------------

    [Fact]
    public void Select_AFolderTakesEverythingBeneathIt()
    {
        var fake = new Fake()
            .File(Path.Combine(Current, "fuori.txt"), size: 5)
            .File(Path.Combine(Current, "foto", "a.jpg"), size: 10)
            .File(Path.Combine(Current, "foto", "2026", "b.jpg"), size: 20)
            .File(Path.Combine(Current, "fotografie", "c.jpg"), size: 40)
            .Dir(Path.Combine(Current, "foto", "vuota"));

        var plan = RestorePlanner.Resolve(Dest, null, fake.Reader);
        var sub = RestorePlanner.Select(plan, new[] { "foto" });

        Assert.Equal(2, sub.Files.Count);
        Assert.Equal(30, sub.TotalBytes);
        Assert.Contains(@"foto\vuota", sub.Directories);
        // «fotografie» comincia per «foto» ma NON è dentro «foto»: il confronto è per segmenti.
        Assert.False(sub.Files.ContainsKey(@"fotografie\c.jpg"));
    }

    [Fact]
    public void Select_ASingleFileBringsItsFoldersAlong()
    {
        var fake = new Fake().File(Path.Combine(Current, "foto", "2026", "b.jpg"), size: 20);

        var plan = RestorePlanner.Resolve(Dest, null, fake.Reader);
        var sub = RestorePlanner.Select(plan, new[] { @"foto\2026\b.jpg" });

        Assert.Single(sub.Files);
        Assert.Contains("foto", sub.Directories);
        Assert.Contains(@"foto\2026", sub.Directories);
    }

    [Fact]
    public void Select_NothingChosen_IsAnEmptyPlan()
    {
        var fake = new Fake().File(Path.Combine(Current, "a.txt"));
        var plan = RestorePlanner.Resolve(Dest, null, fake.Reader);

        Assert.Empty(RestorePlanner.Select(plan, Array.Empty<string>()).Files);
    }
}
