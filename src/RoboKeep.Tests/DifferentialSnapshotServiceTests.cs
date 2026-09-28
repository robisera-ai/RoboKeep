using RoboKeep.Core;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Versioni per differenza con robocopy VERO su cartelle temporanee: il mirror in <c>current</c>,
/// le cartelle-data in <c>versions</c> con i soli file sostituiti o cancellati, il manifest, la
/// ritenzione, i residui dei run interrotti e la guardia sulle cancellazioni.
/// </summary>
public class DifferentialSnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcDiff_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;
        Directory.Delete(_root, recursive: true);
    }

    private (string Source, string Dest, BackupJob Job, DifferentialSnapshotService Svc) Setup(
        string name, int keepCount = 0, int maxAgeDays = 0)
    {
        var source = Path.Combine(_root, name + "-src");
        var dest = Path.Combine(_root, name + "-dst");
        Directory.CreateDirectory(source);
        var job = new BackupJob
        {
            Name = "D", Source = source, Destination = dest, Versioned = true, Mirror = true,
            MultiThread = 0, Retries = 0, Wait = 0,
            SnapshotKeepCount = keepCount, SnapshotMaxAgeDays = maxAgeDays,
        };
        return (source, dest, job, new DifferentialSnapshotService(new RobocopyRunner()));
    }

    /// <summary>Progresso che esegue la richiamata sul thread di chi riporta, non sul pool: serve
    /// ai test che devono reagire a una riga di log mentre il lavoro è ancora a quel punto.</summary>
    private sealed class SyncProgress : IProgress<string>
    {
        private readonly Action<string> _action;
        public SyncProgress(Action<string> action) => _action = action;
        public void Report(string value) => _action(value);
    }

    private static string Current(string dest) => VersioningLayout.CurrentDir(dest);
    private static string Versions(string dest) => VersioningLayout.VersionsDir(dest);

    private static string[] VersionNames(string dest) =>
        Directory.Exists(Versions(dest))
            ? SnapshotName.ListValid(Versions(dest)).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    [Fact]
    public async Task FirstRun_OnEmptyDestination_FillsCurrent_AndCreatesTheFirstVersion()
    {
        var (source, dest, job, svc) = Setup("primo");
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");
        Directory.CreateDirectory(Path.Combine(source, "sotto"));
        File.WriteAllText(Path.Combine(source, "sotto", "b.txt"), "b1");

        var r = await svc.RunAsync(job);

        Assert.True(r.Result.Success);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(Current(dest), "a.txt")));
        Assert.Equal("b1", File.ReadAllText(Path.Combine(Current(dest), "sotto", "b.txt")));

        // Primo run: tutto era nuovo, non c'era nessuno stato precedente da mettere da parte.
        // NESSUNA cartella-versione (una vuota ruberebbe un posto a «tieni N versioni»), ma resta
        // il manifest: «added» dice che a quella data quei file non c'erano ancora.
        Assert.Empty(VersionNames(dest));
        Assert.Empty(Directory.GetDirectories(Versions(dest)));
        var point = Assert.Single(VersionCatalog.List(Versions(dest)));
        Assert.False(point.HasFolder);
        var manifest = VersionManifest.ReadFrom(Path.Combine(Versions(dest), point.Name));
        Assert.NotNull(manifest);
        Assert.Empty(manifest!.Changed);
        Assert.Empty(manifest.Deleted);
        Assert.Contains("a.txt", manifest.Added);
        Assert.Contains(Path.Combine("sotto", "b.txt"), manifest.Added);
    }

    [Fact]
    public async Task AddsOnlyRuns_DoNotEatRetentionSlots_SoTheOnlyCopyOfADeletedFileSurvives()
    {
        // Il caso normale di un archivio di foto o documenti: si aggiungono file, raramente se ne
        // sostituiscono o cancellano. Se ogni backup di sole aggiunte creasse una cartella (vuota),
        // con «tieni 3 versioni» basterebbero tre giorni per far cancellare l'unica versione che
        // conteneva davvero qualcosa.
        var (source, dest, job, svc) = Setup("posti-ritenzione", keepCount: 3);
        File.WriteAllText(Path.Combine(source, "base.txt"), "x");
        File.WriteAllText(Path.Combine(source, "prezioso.txt"), "unica copia dopo la cancellazione");
        await svc.RunAsync(job);                                   // run 1: sole aggiunte

        await Task.Delay(1100);
        File.Delete(Path.Combine(source, "prezioso.txt"));          // run 2: una cancellazione
        await svc.RunAsync(job);
        var withContent = Assert.Single(VersionNames(dest));
        var preziosoInVersione = Path.Combine(Versions(dest), withContent, "prezioso.txt");
        Assert.True(File.Exists(preziosoInVersione));

        // run 3, 4, 5: solo aggiunte. Con le cartelle vuote, a keepCount 3 la versione con il file
        // sarebbe stata cancellata al run 5.
        for (var i = 3; i <= 5; i++)
        {
            await Task.Delay(1100);
            File.WriteAllText(Path.Combine(source, $"nuovo{i}.txt"), "aggiunto al run " + i);
            var r = await svc.RunAsync(job);
            Assert.True(r.Result.Success);
        }

        Assert.Equal("unica copia dopo la cancellazione", File.ReadAllText(preziosoInVersione));
        Assert.Single(VersionNames(dest));                          // una sola versione VERA
        Assert.Equal(4, VersionCatalog.List(Versions(dest)).Count);  // ma quattro punti nel tempo
    }

    [Fact]
    public async Task Retention_PrunesManifestsOlderThanTheOldestSurvivingFolder()
    {
        var (source, dest, job, svc) = Setup("pota-manifest", keepCount: 1);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");
        await svc.RunAsync(job);                                    // punto 1: solo manifest
        var first = VersionCatalog.List(Versions(dest)).Single().Name;

        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v2 di lunghezza diversa");
        await svc.RunAsync(job);                                    // punto 2: cartella
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v3 di lunghezza ancora diversa");
        await svc.RunAsync(job);                                    // punto 3: cartella, keepCount 1

        // Resta una sola cartella (la piu' recente); il manifest del punto 1, piu' vecchio di lei,
        // non puo' piu' aiutare nessun ripristino e se ne va. Nessun file orfano in versions\.
        var points = VersionCatalog.List(Versions(dest));
        Assert.DoesNotContain(points, p => p.Name == first);
        Assert.Single(points.Where(p => p.HasFolder));
    }

    [Fact]
    public async Task TwoRunsInTheSameSecond_GetDistinctParsableNames()
    {
        var (source, dest, job, svc) = Setup("collisione");
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");
        await svc.RunAsync(job);

        // Una versione occupa gia' il nome che il prossimo run vorrebbe (stesso secondo).
        var now = DateTime.Now;
        var taken = Path.Combine(Versions(dest), SnapshotName.For(now));
        Directory.CreateDirectory(taken);
        File.WriteAllText(Path.Combine(taken, "occupato.txt"), "c'ero prima");

        File.WriteAllText(Path.Combine(source, "a.txt"), "v2 di lunghezza diversa");
        await svc.RunAsync(job);

        // La versione preesistente e' intatta, la nuova ha un nome DATA (niente suffisso casuale),
        // quindi resta visibile alla ritenzione e all'elenco delle versioni.
        Assert.Equal("c'ero prima", File.ReadAllText(Path.Combine(taken, "occupato.txt")));
        var names = VersionNames(dest);
        Assert.Equal(2, names.Length);
        Assert.All(names, n => Assert.True(SnapshotName.TryParse(n, out _)));
        var fresh = names.Single(n => n != SnapshotName.For(now));
        Assert.Equal("v1", File.ReadAllText(Path.Combine(Versions(dest), fresh, "a.txt")));
    }

    [Fact]
    public async Task AFileInUse_IsLeftAlone_NotOverwrittenWithoutAPreviousCopy()
    {
        var (source, dest, job, svc) = Setup("in-uso");
        File.WriteAllText(Path.Combine(source, "aperto.txt"), "v1");
        File.WriteAllText(Path.Combine(source, "normale.txt"), "v1");
        await svc.RunAsync(job);

        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "aperto.txt"), "v2 di lunghezza diversa");
        File.WriteAllText(Path.Combine(source, "normale.txt"), "v2 di lunghezza diversa");

        RobocopyRunResult r;
        // FileShare.ReadWrite: robocopy RIUSCIREBBE a sovrascriverlo, ma File.Move no. Senza
        // l'esclusione la copia precedente sparirebbe senza lasciare traccia.
        using (File.Open(Path.Combine(Current(dest), "aperto.txt"), FileMode.Open,
                   FileAccess.Read, FileShare.ReadWrite))
        {
            r = await svc.RunAsync(job);
        }

        // Il file in uso e' rimasto com'era: la sua versione precedente non e' andata persa.
        Assert.Equal("v1", File.ReadAllText(Path.Combine(Current(dest), "aperto.txt")));
        // Gli altri file sono stati aggiornati normalmente.
        Assert.Equal("v2 di lunghezza diversa", File.ReadAllText(Path.Combine(Current(dest), "normale.txt")));
        // E l'utente lo viene a sapere: nell'esito, quindi nel riepilogo, nel log e nell'email.
        var note = Assert.Single(r.Result.VersionNotes);
        Assert.Contains("aperto.txt", note);
    }

    [Fact]
    public async Task AForceCopyFileInUse_IsNeitherMovedNorOverwritten()
    {
        // La passata "forza copia" gira con /IS /IT: sovrascrive SUL POSTO anche un file identico.
        // Se non si e' potuto metterlo da parte, riscriverlo cancellerebbe l'unica copia precedente
        // che esisteva. L'esclusione del run deve valere anche per quella passata.
        var source = Path.Combine(_root, "forza-src");
        var dest = Path.Combine(_root, "forza-dst");
        Directory.CreateDirectory(source);
        var job = new BackupJob
        {
            Name = "D", Source = source, Destination = dest, Versioned = true, Mirror = true,
            MultiThread = 0, Retries = 0, Wait = 0,
            ForceCopyFiles = new List<string> { "*.pst" },
        };
        // Il runner ha bisogno di un pianificatore perche' la passata forzata parta davvero.
        var planner = new ForceCopyPlanner(new ForceCopyHashStore(Path.Combine(_root, "hashes.json")));
        var svc = new DifferentialSnapshotService(new RobocopyRunner(forceCopyPlanner: planner));

        // Un .pst la cui data e dimensione non cambiano mai: e' il caso per cui esiste la forza copia.
        var pst = Path.Combine(source, "posta.pst");
        File.WriteAllText(pst, "versione uno");
        var stamp = new DateTime(2026, 1, 1, 12, 0, 0);
        File.SetLastWriteTime(pst, stamp);
        File.WriteAllText(Path.Combine(source, "normale.txt"), "v1");
        await svc.RunAsync(job);
        Assert.Equal("versione uno", File.ReadAllText(Path.Combine(Current(dest), "posta.pst")));

        await Task.Delay(1100);
        // Contenuto cambiato ma data e dimensione identiche: solo la forza copia lo ricopierebbe.
        File.WriteAllText(pst, "versione due");
        File.SetLastWriteTime(pst, stamp);
        File.WriteAllText(Path.Combine(source, "normale.txt"), "v2 di lunghezza diversa");

        RobocopyRunResult r;
        // FileShare.ReadWrite: File.Move non ce la fa, robocopy invece ce la farebbe.
        using (File.Open(Path.Combine(Current(dest), "posta.pst"), FileMode.Open,
                   FileAccess.Read, FileShare.ReadWrite))
        {
            r = await svc.RunAsync(job, new SyncProgress(_ => { }));
        }

        // Ne' spostato ne' sovrascritto: in current c'e' ancora la versione di prima.
        Assert.Equal("versione uno", File.ReadAllText(Path.Combine(Current(dest), "posta.pst")));
        // Gli altri file sono stati aggiornati normalmente.
        Assert.Equal("v2 di lunghezza diversa", File.ReadAllText(Path.Combine(Current(dest), "normale.txt")));
        // E l'utente lo viene a sapere.
        Assert.Contains(r.Result.VersionNotes, n => n.Contains("posta.pst"));
    }

    [Fact]
    public async Task AnExtraFolderThatCannotBeMoved_IsNotDeletedByTheMirror()
    {
        var (source, dest, job, svc) = Setup("cartella-in-uso");
        Directory.CreateDirectory(Path.Combine(source, "da cancellare"));
        File.WriteAllText(Path.Combine(source, "da cancellare", "dentro.txt"), "unica copia");
        File.WriteAllText(Path.Combine(source, "base.txt"), "x");
        await svc.RunAsync(job);

        await Task.Delay(1100);
        Directory.Delete(Path.Combine(source, "da cancellare"), recursive: true);

        RobocopyRunResult r;
        using (File.Open(Path.Combine(Current(dest), "da cancellare", "dentro.txt"), FileMode.Open,
                   FileAccess.Read, FileShare.ReadWrite))
        {
            r = await svc.RunAsync(job);
        }

        // La cartella non si e' potuta spostare, quindi il mirror NON l'ha cancellata: il file c'e'
        // ancora e si riprovera' al prossimo backup.
        Assert.Equal("unica copia",
            File.ReadAllText(Path.Combine(Current(dest), "da cancellare", "dentro.txt")));
        Assert.Single(r.Result.VersionNotes);
    }

    [Fact]
    public async Task ASourceFolderNamedCurrent_IsNotMistakenForTheLayout()
    {
        // La sorgente ha una cartella di primo livello chiamata «current»: un mirror piatto
        // precedente l'ha copiata nella destinazione. Non si adotta niente, ma non in silenzio.
        var (source, dest, job, svc) = Setup("current-utente");
        Directory.CreateDirectory(Path.Combine(source, "current"));
        File.WriteAllText(Path.Combine(source, "current", "roba mia.txt"), "mia");
        File.WriteAllText(Path.Combine(source, "a.txt"), "x");
        Directory.CreateDirectory(Path.Combine(dest, "current"));
        File.WriteAllText(Path.Combine(dest, "current", "roba mia.txt"), "mia");
        File.WriteAllText(Path.Combine(dest, "a.txt"), "x");

        var lines = new List<string>();
        await svc.RunAsync(job, new SyncProgress(lines.Add));

        Assert.Contains(lines, l => l.Contains(VersioningLayout.CurrentFolderName)
            && l.Contains(CoreLoc.S("Diff_AdoptAmbiguous").Split('{')[0].Trim()[..20]));
        // Il contenuto sciolto e' rimasto dov'era: non si e' spostato niente al buio.
        Assert.True(File.Exists(Path.Combine(dest, "a.txt")));
    }

    [Fact]
    public async Task FirstRun_AdoptsAnExistingFlatMirrorIntoCurrent_WithoutRecopying()
    {
        var (source, dest, job, svc) = Setup("adozione");
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");
        Directory.CreateDirectory(Path.Combine(source, "sotto"));
        File.WriteAllText(Path.Combine(source, "sotto", "b.txt"), "b1");

        // Copia semplice gia' presente nella destinazione (il job faceva un mirror piatto finora).
        Directory.CreateDirectory(Path.Combine(dest, "sotto"));
        File.WriteAllText(Path.Combine(dest, "a.txt"), "vecchio");
        File.WriteAllText(Path.Combine(dest, "sotto", "b.txt"), "b1");

        var lines = new List<string>();
        await svc.RunAsync(job, new Progress<string>(lines.Add));

        // Il contenuto sciolto e' stato SPOSTATO in current (e li' aggiornato dal mirror), non
        // ricopiato lasciando il doppione fuori.
        Assert.False(File.Exists(Path.Combine(dest, "a.txt")));
        Assert.Equal("v1", File.ReadAllText(Path.Combine(Current(dest), "a.txt")));
        // La copia precedente di a.txt e' finita nella prima versione: era da sovrascrivere.
        var version = Assert.Single(VersionNames(dest));
        Assert.Equal("vecchio", File.ReadAllText(Path.Combine(Versions(dest), version, "a.txt")));
        var manifest = VersionManifest.ReadFrom(Path.Combine(Versions(dest), version))!;
        Assert.Contains("a.txt", manifest.Changed);
    }

    [Fact]
    public async Task ForeignContentInDestination_IsNotAdopted_AndStaysOutsideCurrent()
    {
        var (source, dest, job, svc) = Setup("estranei");
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, "documento-di-qualcun-altro.txt"), "non mio");

        var lines = new List<string>();
        await svc.RunAsync(job, new Progress<string>(lines.Add));

        // Il file estraneo resta dov'e': fuori da current, quindi nessun mirror lo cancellera'.
        Assert.True(File.Exists(Path.Combine(dest, "documento-di-qualcun-altro.txt")));
        Assert.False(File.Exists(Path.Combine(Current(dest), "documento-di-qualcun-altro.txt")));
        Assert.Contains(lines, l => l.Contains("documento-di-qualcun-altro.txt"));
    }

    [Fact]
    public async Task SecondRun_AfterChangeDeleteAdd_VersionHoldsThePreviousCopies_AndTheManifestIsRight()
    {
        var (source, dest, job, svc) = Setup("differenza");
        Directory.CreateDirectory(Path.Combine(source, "sotto cartella"));
        File.WriteAllText(Path.Combine(source, "modificato.txt"), "v1");
        File.WriteAllText(Path.Combine(source, "intatto.txt"), "sempre uguale");
        File.WriteAllText(Path.Combine(source, "sotto cartella", "città però.txt"), "prima");
        File.WriteAllText(Path.Combine(source, "cancellato.txt"), "spariro'");

        await svc.RunAsync(job);
        // Primo run di sole aggiunte: nessuna cartella-versione, solo il suo manifest.
        Assert.Empty(VersionNames(dest));
        var firstPoint = Assert.Single(VersionCatalog.List(Versions(dest))).Name;

        await Task.Delay(1100); // nome della versione al secondo: serve un timestamp distinto
        File.WriteAllText(Path.Combine(source, "modificato.txt"), "v2, piu' lungo di prima");
        File.WriteAllText(Path.Combine(source, "sotto cartella", "città però.txt"), "seconda versione");
        File.Delete(Path.Combine(source, "cancellato.txt"));
        File.WriteAllText(Path.Combine(source, "aggiunto.txt"), "nuovo di zecca");

        var r = await svc.RunAsync(job);
        Assert.True(r.Result.Success);

        // Il secondo run ha sostituito e cancellato: questa volta la cartella c'e'.
        var v2 = Path.Combine(Versions(dest), Assert.Single(VersionNames(dest)));

        // current e' il backup aggiornato.
        Assert.Equal("v2, piu' lungo di prima", File.ReadAllText(Path.Combine(Current(dest), "modificato.txt")));
        Assert.Equal("nuovo di zecca", File.ReadAllText(Path.Combine(Current(dest), "aggiunto.txt")));
        Assert.False(File.Exists(Path.Combine(Current(dest), "cancellato.txt")));

        // La versione contiene gli stati PRECEDENTI dei file toccati, e solo quelli.
        Assert.Equal("v1", File.ReadAllText(Path.Combine(v2, "modificato.txt")));
        Assert.Equal("prima", File.ReadAllText(Path.Combine(v2, "sotto cartella", "città però.txt")));
        Assert.Equal("spariro'", File.ReadAllText(Path.Combine(v2, "cancellato.txt")));
        Assert.False(File.Exists(Path.Combine(v2, "intatto.txt")));
        Assert.False(File.Exists(Path.Combine(v2, "aggiunto.txt")));

        var manifest = VersionManifest.ReadFrom(v2)!;
        Assert.Contains("modificato.txt", manifest.Changed);
        Assert.Contains(Path.Combine("sotto cartella", "città però.txt"), manifest.Changed);
        Assert.Contains("cancellato.txt", manifest.Deleted);
        Assert.Contains("aggiunto.txt", manifest.Added);
        Assert.DoesNotContain("intatto.txt", manifest.Changed);
        Assert.True(manifest.CreatedAt > DateTime.MinValue);

        // Il manifest del primo run e' stato potato: e' piu' vecchio della cartella piu' vecchia
        // rimasta, e per tornare alla data del primo run basta proprio questa cartella (contiene
        // gli stati com'erano PRIMA del secondo run). Piu' indietro non si puo' andare comunque.
        var points = VersionCatalog.List(Versions(dest));
        Assert.DoesNotContain(points, p => p.Name == firstPoint);
        Assert.Single(points);
    }

    [Fact]
    public async Task DeletedFolder_MovesWholeIntoTheVersion()
    {
        var (source, dest, job, svc) = Setup("cartella");
        Directory.CreateDirectory(Path.Combine(source, "da cancellare", "annidata"));
        File.WriteAllText(Path.Combine(source, "da cancellare", "dentro.txt"), "contenuto");
        File.WriteAllText(Path.Combine(source, "da cancellare", "annidata", "profondo.txt"), "in fondo");
        File.WriteAllText(Path.Combine(source, "resta.txt"), "x");

        await svc.RunAsync(job);
        await Task.Delay(1100);
        Directory.Delete(Path.Combine(source, "da cancellare"), recursive: true);

        await svc.RunAsync(job);

        var v2 = Path.Combine(Versions(dest), Assert.Single(VersionNames(dest)));
        Assert.False(Directory.Exists(Path.Combine(Current(dest), "da cancellare")));
        Assert.Equal("contenuto", File.ReadAllText(Path.Combine(v2, "da cancellare", "dentro.txt")));
        Assert.Equal("in fondo",
            File.ReadAllText(Path.Combine(v2, "da cancellare", "annidata", "profondo.txt")));
        var manifest = VersionManifest.ReadFrom(v2)!;
        Assert.Contains("da cancellare", manifest.Deleted);
        // Il file dentro la cartella e' elencato anche lui: il ripristino deve poterlo ritrovare.
        Assert.Contains(Path.Combine("da cancellare", "dentro.txt"), manifest.Deleted);
        // E la sottocartella annidata, portata via insieme al padre, non sparisce dal manifest.
        Assert.Contains(Path.Combine("da cancellare", "annidata"), manifest.Deleted);
        Assert.Contains(Path.Combine("da cancellare", "annidata", "profondo.txt"), manifest.Deleted);
    }

    [Fact]
    public async Task ThirdRun_WithoutChanges_CreatesNoNewVersion()
    {
        var (source, dest, job, svc) = Setup("nulla");
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");

        await svc.RunAsync(job);
        await Task.Delay(1100);
        var lines = new List<string>();
        var r = await svc.RunAsync(job, new Progress<string>(lines.Add));

        Assert.True(r.Result.Success);
        Assert.False(r.Result.DryRun);
        // Niente di cambiato: nessun punto nel tempo in piu', nemmeno un manifest (a differenza di
        // un backup di sole aggiunte, qui non e' successo proprio niente).
        Assert.Single(VersionCatalog.List(Versions(dest)));
        Assert.Empty(VersionNames(dest));
        Assert.Contains(lines, l => l.Contains("current"));  // lo dice nel log
        // E nemmeno una .inprogress abbandonata.
        Assert.DoesNotContain(Directory.GetDirectories(Versions(dest)).Select(Path.GetFileName),
            n => n is not null && SnapshotName.IsInProgress(n));
    }

    [Fact]
    public async Task Retention_KeepCount1_DeletesTheOlderVersion()
    {
        var (source, dest, job, svc) = Setup("ritenzione", keepCount: 1);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");

        await svc.RunAsync(job);
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v2 con lunghezza diversa");
        await svc.RunAsync(job);
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v3 di lunghezza ancora diversa");
        await svc.RunAsync(job);

        var versions = VersionNames(dest);
        Assert.Single(versions);
        // La versione superstite e' l'ultima: contiene la copia precedente (v2), non v1.
        Assert.Equal("v2 con lunghezza diversa",
            File.ReadAllText(Path.Combine(Versions(dest), versions[0], "a.txt")));
        Assert.Equal("v3 di lunghezza ancora diversa", File.ReadAllText(Path.Combine(Current(dest), "a.txt")));
    }

    [Fact]
    public async Task StaleInProgressVersion_WithFilesInIt_IsRecoveredAsARealVersion_NotDeleted()
    {
        var (source, dest, job, svc) = Setup("recupero");
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");

        // Run interrotto: una .inprogress residua con dentro un file messo da parte. Per il modello
        // per differenza quel file e' un ORIGINALE spostato via da current — cancellarlo, come fa
        // il modello a hard-link con i suoi cloni, lo perderebbe per sempre.
        var stale = Path.Combine(Versions(dest), "2026-01-01_000000" + SnapshotName.InProgressSuffix);
        Directory.CreateDirectory(Path.Combine(stale, "sotto"));
        File.WriteAllText(Path.Combine(stale, "sotto", "unica-copia.txt"), "non esiste altrove");
        new VersionManifest { CreatedAt = new DateTime(2026, 1, 1) }.WriteTo(stale);
        // ...e il residuo di una cancellazione interrotta a meta': quello era gia' condannato.
        var leftover = Path.Combine(Versions(dest), "2026-01-02_000000.deleting-abcd1234");
        Directory.CreateDirectory(leftover);
        File.WriteAllText(Path.Combine(leftover, "residuo.bin"), "spazzatura");
        // ...e una .inprogress VUOTA: quella non ha niente da conservare e se ne va.
        var empty = Path.Combine(Versions(dest), "2026-01-03_000000" + SnapshotName.InProgressSuffix);
        Directory.CreateDirectory(empty);

        var lines = new List<string>();
        await svc.RunAsync(job, new Progress<string>(lines.Add));

        Assert.False(Directory.Exists(stale));
        Assert.False(Directory.Exists(leftover));
        Assert.False(Directory.Exists(empty));
        // Promossa a versione vera, con il file ancora dentro e il manifest gemello al seguito.
        var recovered = Path.Combine(Versions(dest), "2026-01-01_000000");
        Assert.True(Directory.Exists(recovered));
        Assert.Equal("non esiste altrove",
            File.ReadAllText(Path.Combine(recovered, "sotto", "unica-copia.txt")));
        Assert.NotNull(VersionManifest.ReadFrom(recovered));
        Assert.Contains(VersionNames(dest), n => n == "2026-01-01_000000");
        Assert.Contains(lines, l => l.Contains("2026-01-01_000000"));
    }

    [Fact]
    public async Task FailedMirror_StillPromotesTheVersion_SoTheOnlyCopyOfADeletedFileSurvivesTheNextRuns()
    {
        var (source, dest, job, svc) = Setup("fallito");
        File.WriteAllText(Path.Combine(source, "solo-nel-backup.txt"), "unica copia dopo la cancellazione");
        File.WriteAllText(Path.Combine(source, "bloccato.txt"), "v1");
        await svc.RunAsync(job);
        Assert.Empty(VersionNames(dest)); // primo run di sole aggiunte: nessuna cartella

        await Task.Delay(1100);
        // La sorgente perde un file (in current diventa "extra": il backup ne ha l'unica copia) e
        // ne cambia un altro.
        File.Delete(Path.Combine(source, "solo-nel-backup.txt"));
        File.WriteAllText(Path.Combine(source, "bloccato.txt"), "v2 di lunghezza diversa");

        // Il file in SORGENTE viene tenuto aperto in esclusiva: gli spostamenti da current
        // riescono (li' le copie sono libere), ma robocopy non riesce a leggere l'originale e il
        // mirror fallisce dopo che la versione ha gia' raccolto dei file.
        var lines = new List<string>();
        RobocopyRunResult r;
        using (File.Open(Path.Combine(source, "bloccato.txt"), FileMode.Open,
                   FileAccess.Read, FileShare.None))
        {
            r = await svc.RunAsync(job, new Progress<string>(lines.Add));
        }

        Assert.False(r.Result.Success);
        // La versione NON resta .inprogress: verrebbe cancellata dalla pulizia dei residui.
        Assert.DoesNotContain(Directory.GetDirectories(Versions(dest)).Select(Path.GetFileName),
            n => n is not null && SnapshotName.IsInProgress(n));
        var partial = Path.Combine(Versions(dest), Assert.Single(VersionNames(dest)));
        Assert.Equal("unica copia dopo la cancellazione",
            File.ReadAllText(Path.Combine(partial, "solo-nel-backup.txt")));
        Assert.Contains("solo-nel-backup.txt", VersionManifest.ReadFrom(partial)!.Deleted);

        // Terzo run, tutto tornato normale: la versione parziale e il suo file sono ancora li'.
        await Task.Delay(1100);
        await svc.RunAsync(job);
        Assert.Equal("unica copia dopo la cancellazione",
            File.ReadAllText(Path.Combine(partial, "solo-nel-backup.txt")));
    }

    [Fact]
    public async Task CancellationDuringTheMoves_LeavesAFolderWithItsManifest()
    {
        var (source, dest, job, svc) = Setup("annullato");
        foreach (var name in new[] { "a-primo.txt", "b-bloccato.txt", "c-terzo.txt" })
            File.WriteAllText(Path.Combine(source, name), "v1");
        await svc.RunAsync(job);

        await Task.Delay(1100);
        foreach (var name in new[] { "a-primo.txt", "b-bloccato.txt", "c-terzo.txt" })
            File.WriteAllText(Path.Combine(source, name), "v2 di lunghezza diversa");

        // Si annulla appena il primo spostamento fallisce (il file e' tenuto aperto in esclusiva):
        // il ciclo degli spostamenti si ferma al giro successivo, a meta' del lavoro. Il progresso
        // deve essere SINCRONO: con Progress<string> la richiamata verrebbe accodata al pool e
        // l'annullamento arriverebbe a spostamenti gia' finiti.
        var cts = new CancellationTokenSource();
        var progress = new SyncProgress(l => { if (l.Contains("b-bloccato.txt")) cts.Cancel(); });

        using (File.Open(Path.Combine(Current(dest), "b-bloccato.txt"), FileMode.Open,
                   FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => svc.RunAsync(job, progress, cts.Token));
        }

        // La cartella interrotta esiste, ha gia' il suo manifest (scritto PRIMA degli spostamenti)
        // e il file che era riuscito a spostarsi. Il run successivo la promuove a versione vera.
        var inProgress = Directory.GetDirectories(Versions(dest))
            .Single(d => SnapshotName.IsInProgress(Path.GetFileName(d)!));
        var manifest = VersionManifest.ReadFrom(inProgress);
        Assert.NotNull(manifest);
        Assert.Contains("a-primo.txt", manifest!.Changed);
        Assert.True(File.Exists(Path.Combine(inProgress, "a-primo.txt")));
    }

    [Fact]
    public async Task AUserFileNamedLikeTheManifest_IsNotOverwritten()
    {
        var (source, dest, job, svc) = Setup("manifest-utente");
        // Un file dell'utente con lo stesso nome del manifest, proprio nella radice dell'albero.
        File.WriteAllText(Path.Combine(source, "_manifest.json"), "{ \"mio\": \"documento\" }");
        await svc.RunAsync(job);

        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "_manifest.json"), "{ \"mio\": \"documento aggiornato\" }");
        await svc.RunAsync(job);

        var v2 = Path.Combine(Versions(dest), Assert.Single(VersionNames(dest)));
        // Dentro la versione c'e' la copia PRECEDENTE del file dell'utente, intatta.
        Assert.Equal("{ \"mio\": \"documento\" }", File.ReadAllText(Path.Combine(v2, "_manifest.json")));
        // Il manifest di RoboKeep e' il file gemello, fuori dalla cartella.
        Assert.True(File.Exists(v2 + VersionManifest.FileSuffix));
        Assert.Contains("_manifest.json", VersionManifest.ReadFrom(v2)!.Changed);
        // E i file gemelli non compaiono mai nell'elenco delle versioni: sono file, non cartelle.
        Assert.DoesNotContain(VersionNames(dest), n => n.Contains(VersionManifest.FileSuffix));
    }

    [Fact]
    public async Task Retention_TakesTheManifestAwayWithItsVersion()
    {
        var (source, dest, job, svc) = Setup("manifest-ritenzione", keepCount: 1);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");
        await svc.RunAsync(job);

        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v2 di lunghezza diversa");
        await svc.RunAsync(job);
        var replaced = Assert.Single(VersionNames(dest)); // la cartella nata dal secondo run
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v3 di lunghezza ancora diversa");
        await svc.RunAsync(job);                          // keepCount 1: la cartella di sopra se ne va

        // La cartella cancellata dalla ritenzione si porta via il suo manifest gemello: niente
        // file orfani in versions\.
        Assert.False(Directory.Exists(Path.Combine(Versions(dest), replaced)));
        Assert.False(File.Exists(Path.Combine(Versions(dest), replaced) + VersionManifest.FileSuffix));
        Assert.Equal(Directory.GetDirectories(Versions(dest)).Length,
            Directory.GetFiles(Versions(dest), "*" + VersionManifest.FileSuffix).Length);
    }

    [Fact]
    public async Task SourceOverride_IsHonoured_ByBothThePreviewAndTheMirror()
    {
        var (source, dest, job, svc) = Setup("override");
        var frozen = Path.Combine(_root, "congelata");
        Directory.CreateDirectory(frozen);
        File.WriteAllText(Path.Combine(source, "a.txt"), "vivo");
        File.WriteAllText(Path.Combine(frozen, "a.txt"), "congelato");
        File.WriteAllText(Path.Combine(frozen, "b.txt"), "solo nello snapshot");

        await svc.RunAsync(job, sourceOverride: frozen);

        Assert.Equal("congelato", File.ReadAllText(Path.Combine(Current(dest), "a.txt")));
        Assert.True(File.Exists(Path.Combine(Current(dest), "b.txt")));
    }

    [Fact]
    public async Task Guard_OverTheThreshold_WithNoOneToAsk_BlocksAndTouchesNothing()
    {
        var (source, dest, job, svc) = Setup("guardia");
        // Abbastanza file perche' la guardia possa scattare (sotto i 20 non scatta mai).
        for (var i = 0; i < 40; i++)
            File.WriteAllText(Path.Combine(source, $"f{i}.txt"), "contenuto " + i);

        await svc.RunAsync(job);
        var before = VersionNames(dest);
        Assert.Empty(before); // primo run di sole aggiunte: nessuna cartella

        await Task.Delay(1100);
        // Sorgente svuotata per errore: il mirror porterebbe via tutto.
        foreach (var f in Directory.GetFiles(source)) File.Delete(f);

        // Callback null = attivita' pianificata o riga di comando: nessuno da interpellare.
        var r = await svc.RunAsync(job);

        Assert.True(r.Result.DeletionsBlocked);
        Assert.False(r.Result.Success);
        Assert.True(r.Result.NotStarted);
        Assert.Equal(MirrorDeleteGuard.BlockedExitCode, r.Result.ExitCode);
        // Niente e' stato toccato: current e' intatta, nessuna versione nuova, nessuna .inprogress.
        Assert.Equal(40, Directory.GetFiles(Current(dest)).Length);
        Assert.Equal(before, VersionNames(dest));
        Assert.DoesNotContain(Directory.GetDirectories(Versions(dest)).Select(Path.GetFileName),
            n => n is not null && SnapshotName.IsInProgress(n));
    }

    [Fact]
    public async Task Guard_OverTheThreshold_WithAYes_GoesAhead()
    {
        var (source, dest, job, svc) = Setup("guardia-si");
        for (var i = 0; i < 40; i++)
            File.WriteAllText(Path.Combine(source, $"f{i}.txt"), "contenuto " + i);

        await svc.RunAsync(job);
        await Task.Delay(1100);
        foreach (var f in Directory.GetFiles(source)) File.Delete(f);

        var asked = 0;
        var r = await svc.RunAsync(job, confirmDeletions: _ => { asked++; return Task.FromResult(true); });

        Assert.Equal(1, asked);
        Assert.False(r.Result.DeletionsBlocked);
        Assert.Empty(Directory.GetFiles(Current(dest)));
        // I 40 file non sono persi: stanno nella versione appena creata (piu' il manifest).
        var v2 = Path.Combine(Versions(dest), Assert.Single(VersionNames(dest)));
        Assert.Equal(40, Directory.GetFiles(v2, "f*.txt").Length);
        Assert.Equal(40, VersionManifest.ReadFrom(v2)!.Deleted.Count);
    }

    [Fact]
    public async Task FreeSpaceCleanup_DeletesTheOldestVersions_FromTheVersionsFolder()
    {
        var source = Path.Combine(_root, "spazio-src");
        var dest = Path.Combine(_root, "spazio-dst");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v1");

        var settings = new AppSettings { FreeSpaceCleanup = true, MinFreeSpaceMb = 10 };
        var job = new BackupJob
        {
            Name = "D", Source = source, Destination = dest, Versioned = true, Mirror = true,
            MultiThread = 0, Retries = 0, Wait = 0,
        };
        // Spazio libero sempre sotto la soglia: la pulizia cancella finche' resta la piu' recente.
        var svc = new DifferentialSnapshotService(new RobocopyRunner(), settings, _ => 1024L * 1024);

        await svc.RunAsync(job);
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v2 di lunghezza diversa");
        await svc.RunAsync(job);
        await Task.Delay(1100);
        File.WriteAllText(Path.Combine(source, "a.txt"), "v3 di lunghezza ancora diversa");
        await svc.RunAsync(job);

        // Mai la piu' recente: ne resta sempre almeno una (piu' quella creata da questo run).
        Assert.InRange(VersionNames(dest).Length, 1, 2);
        Assert.Equal("v3 di lunghezza ancora diversa", File.ReadAllText(Path.Combine(Current(dest), "a.txt")));
    }
}

/// <summary>
/// Il modello di versioni visto da BackupRunner: chi sceglie quale servizio, che cosa succede se
/// quello giusto non c'e', e contro che cosa gira l'anteprima di un job versionato.
/// </summary>
public class DifferentialBackupRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcDiffRun_" + Guid.NewGuid().ToString("N"));

    public DifferentialBackupRunnerTests() => Directory.CreateDirectory(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private sealed class Collect : IProgress<string>
    {
        public List<string> Lines { get; } = new();
        public void Report(string value) { lock (Lines) Lines.Add(value); }
    }

    private (AppConfig Config, CredentialService Creds) Wiring()
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.CompressLogs = false;
        config.Settings.ConfigCopyToDestination = false; // niente scritture nella radice di C:\
        return (config, new CredentialService(config.Settings.CredentialScope));
    }

    [Fact]
    public async Task ExistingVersionsLayout_WithoutTheServiceThatHandlesIt_StopsTheJob_AndDeletesNothing()
    {
        var source = Path.Combine(_root, "src");
        var dest = Path.Combine(_root, "dst");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "x");
        // Layout per differenza gia' in piedi, con una versione dentro.
        Directory.CreateDirectory(VersioningLayout.CurrentDir(dest));
        Directory.CreateDirectory(Path.Combine(VersioningLayout.VersionsDir(dest), "2026-09-25_210000"));
        File.WriteAllText(
            Path.Combine(VersioningLayout.VersionsDir(dest), "2026-09-25_210000", "unica-copia.txt"), "prezioso");

        var (config, creds) = Wiring();
        // BackupRunner senza DifferentialSnapshotService: un mirror piatto sulla radice
        // cancellerebbe current e versions come "extra".
        var runner = new BackupRunner(config, new RobocopyRunner(), new LogService(config.Settings),
            new EmailService(creds), creds);
        var progress = new Collect();
        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, Retries = 0, Wait = 0 };

        var result = await runner.RunJobAsync(job, progress: progress);

        Assert.False(result.Success);
        Assert.True(result.NotStarted);
        Assert.Equal(CoreLoc.S("Versioning_NoServiceStatus"), result.Status);
        // Niente e' stato toccato: le versioni ci sono ancora, tutte.
        Assert.True(Directory.Exists(VersioningLayout.CurrentDir(dest)));
        Assert.Equal("prezioso", File.ReadAllText(
            Path.Combine(VersioningLayout.VersionsDir(dest), "2026-09-25_210000", "unica-copia.txt")));
        Assert.Contains(progress.Lines, l => l.Contains(dest));
    }

    [Fact]
    public async Task VirginDestination_WithoutAnyVersioningService_StillRunsAPlainMirror()
    {
        // Nessun layout da proteggere: il degrado di sempre resta (mirror semplice con avviso).
        var source = Path.Combine(_root, "src2");
        var dest = Path.Combine(_root, "dst2");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "x");

        var (config, creds) = Wiring();
        var runner = new BackupRunner(config, new RobocopyRunner(), new LogService(config.Settings),
            new EmailService(creds), creds);
        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, Retries = 0, Wait = 0 };

        var result = await runner.RunJobAsync(job);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(dest, "a.txt")));
    }

    [Fact]
    public async Task DryRun_OfADifferentialJob_PreviewsAgainstCurrent_NotTheDestinationRoot()
    {
        var source = Path.Combine(_root, "src3");
        var dest = Path.Combine(_root, "dst3");
        Directory.CreateDirectory(source);
        var current = VersioningLayout.CurrentDir(dest);
        Directory.CreateDirectory(current);
        // Sorgente e current allineate: l'anteprima onesta deve dire "niente da fare".
        for (var i = 0; i < 30; i++)
        {
            File.WriteAllText(Path.Combine(source, $"f{i}.txt"), "contenuto " + i);
            File.Copy(Path.Combine(source, $"f{i}.txt"), Path.Combine(current, $"f{i}.txt"));
        }
        // ...e in versions\ c'e' una versione, che contro la RADICE risulterebbe tutta "extra".
        var old = Path.Combine(VersioningLayout.VersionsDir(dest), "2026-09-25_210000");
        Directory.CreateDirectory(old);
        File.WriteAllText(Path.Combine(old, "vecchio.txt"), "y");

        var (config, creds) = Wiring();
        var fake = new RobocopyRunner();
        var runner = new BackupRunner(config, fake, new LogService(config.Settings),
            new EmailService(creds), creds, snapshots: new SnapshotService(fake),
            differentialSnapshots: new DifferentialSnapshotService(fake));
        var progress = new Collect();
        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, Retries = 0, Wait = 0 };

        var result = await runner.RunJobAsync(job, dryRun: true, progress: progress);

        // L'anteprima dice quale cartella ha confrontato, e non conta le versioni come cancellazioni.
        Assert.Contains(progress.Lines, l => l.Contains(current));
        Assert.Equal(0, result.FilesExtra);
        Assert.Equal(0, result.FilesCopied);
    }

    [Fact]
    public async Task DryRun_OfAHardLinkJob_PreviewsAgainstTheLatestDatedFolder()
    {
        var source = Path.Combine(_root, "src4");
        var dest = Path.Combine(_root, "dst4");
        Directory.CreateDirectory(source);
        var latest = Path.Combine(dest, "2026-09-26_210000");
        Directory.CreateDirectory(Path.Combine(dest, "2026-09-25_210000"));
        Directory.CreateDirectory(latest);
        for (var i = 0; i < 30; i++)
        {
            File.WriteAllText(Path.Combine(source, $"f{i}.txt"), "contenuto " + i);
            File.Copy(Path.Combine(source, $"f{i}.txt"), Path.Combine(latest, $"f{i}.txt"));
        }

        var (config, creds) = Wiring();
        var fake = new RobocopyRunner();
        var runner = new BackupRunner(config, fake, new LogService(config.Settings),
            new EmailService(creds), creds, snapshots: new SnapshotService(fake));
        var progress = new Collect();
        var job = new BackupJob { Name = "V", Source = source, Destination = dest, Versioned = true, Retries = 0, Wait = 0 };

        var result = await runner.RunJobAsync(job, dryRun: true, progress: progress);

        Assert.Contains(progress.Lines, l => l.Contains(latest));
        Assert.Equal(0, result.FilesExtra);
    }
}
