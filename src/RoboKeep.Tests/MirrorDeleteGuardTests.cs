using RoboKeep.Core;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Soglia sulle cancellazioni in mirror: la regola pura, il blocco del run (con e senza versioni)
/// e l'email del caso bloccato. Il mirror che cancella tutto non si puo' provare su robocopy vero
/// senza dati veri da perdere: al suo posto gira un finto robocopy che dichiara i conteggi che
/// servono e REGISTRA ogni chiamata, cosi' il test puo' dimostrare che il mirror vero non e' mai
/// partito. La strada con le versioni invece usa robocopy vero: li' l'anteprima esiste gia'.
/// </summary>
[Collection(CultureCollection.Name)]
public sealed class MirrorDeleteGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcGuard_" + Guid.NewGuid().ToString("N"));
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");
    private string CallsFile => Path.Combine(_root, "calls.txt");

    public MirrorDeleteGuardTests()
    {
        Directory.CreateDirectory(Src);
        Directory.CreateDirectory(Dst);
        File.WriteAllText(Path.Combine(Src, "a.txt"), "contenuto");
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;
        Directory.Delete(_root, recursive: true);
    }

    // ---- la regola pura ----

    private static BackupJob Job(int limit = BackupJob.DefaultMirrorDeleteLimitPercent) => new()
    {
        Name = "T", Source = @"D:\documenti", Destination = @"E:\Backup\Documenti",
        MirrorDeleteLimitPercent = limit,
    };

    private static RobocopyCounts Counts(long extra, long skipped = 0, long copied = 0) =>
        new(DirsCopied: 0, FilesCopied: copied, FilesSkipped: skipped, FilesFailed: 0, FilesExtra: extra);

    /// <summary>I numeri nei messaggi hanno il separatore delle migliaia (1.812), e il separatore
    /// dipende dalla cultura del processo: si confronta con la stessa formattazione, non col numero
    /// nudo, altrimenti il test dice cose diverse su un PC inglese.</summary>
    private static string N(long n) => n.ToString("N0");

    [Fact]
    public void UnderTheThreshold_DoesNotBlock()
    {
        var e = MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 30, skipped: 970));
        Assert.Equal(3, e.Percent);
        Assert.False(MirrorDeleteGuard.ShouldBlock(e));
    }

    [Fact]
    public void OverTheThreshold_Blocks_AndRoundsThePercentage()
    {
        // 1812 su 2014 sono il 89,97 %: chi legge deve vedere 90, non 89.
        var e = MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 1812, skipped: 202));
        Assert.Equal(1812, e.Extra);
        Assert.Equal(2014, e.Total);
        Assert.Equal(90, e.Percent);
        Assert.Equal(20, e.LimitPercent);
        Assert.Equal("T", e.JobName);
        Assert.Equal(@"E:\Backup\Documenti", e.Destination);
        Assert.True(MirrorDeleteGuard.ShouldBlock(e));
    }

    [Fact]
    public void ExactlyAtTheThreshold_Blocks()
        => Assert.True(MirrorDeleteGuard.ShouldBlock(MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 200, skipped: 800))));

    [Fact]
    public void HalfPercent_RoundsUp()
    {
        // 25 su 200 sono il 12,5 %: si arrotonda per eccesso (13), mai a caso.
        var e = MirrorDeleteGuard.Estimate(Job(13), Counts(extra: 25, skipped: 175));
        Assert.Equal(13, e.Percent);
        Assert.True(MirrorDeleteGuard.ShouldBlock(e));
        // Con la soglia a 14 lo stesso 12,5 % non basta.
        Assert.False(MirrorDeleteGuard.ShouldBlock(MirrorDeleteGuard.Estimate(Job(14), Counts(extra: 25, skipped: 175))));
    }

    [Fact]
    public void FewFiles_NeverBlock_NotEvenAtOneHundredPercent()
    {
        // Una cartella con quattro file cambia di natura in un pomeriggio: fermare un backup per
        // 19 file cancellati insegnerebbe solo a ignorare gli avvisi.
        var e = MirrorDeleteGuard.Estimate(Job(20), Counts(extra: MirrorDeleteGuard.MinFiles - 1));
        Assert.Equal(100, e.Percent);
        Assert.False(MirrorDeleteGuard.ShouldBlock(e));
        // Con un file in piu' invece scatta: il confine e' esattamente MinFiles.
        Assert.True(MirrorDeleteGuard.ShouldBlock(
            MirrorDeleteGuard.Estimate(Job(20), Counts(extra: MirrorDeleteGuard.MinFiles))));
    }

    [Fact]
    public void ZeroLimit_NeverBlocks()
        => Assert.False(MirrorDeleteGuard.ShouldBlock(MirrorDeleteGuard.Estimate(Job(0), Counts(extra: 5000))));

    [Fact]
    public void EmptyDestination_HasNoPercentage_AndDoesNotBlock()
    {
        // Primo run: niente in destinazione, niente da cancellare, nessuna divisione per zero.
        var e = MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 0));
        Assert.Equal(0, e.Total);
        Assert.Equal(0, e.Percent);
        Assert.False(MirrorDeleteGuard.ShouldBlock(e));
    }

    [Fact]
    public void BlockedResult_LooksLikeAFailureToWhoeverOnlyReadsTheExitCode()
    {
        var e = MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 1812, skipped: 202));
        var r = MirrorDeleteGuard.BlockedResult(e, new DateTime(2026, 9, 26, 21, 0, 0));

        Assert.False(r.Success);
        Assert.True(r.NotStarted);
        Assert.True(r.DeletionsBlocked);
        Assert.Equal(8, r.ExitCode);
        Assert.Equal(CoreLoc.S("Guard_Status"), r.Status);
        Assert.Equal(1812, r.FilesExtra);
        Assert.Contains(N(1812), r.DeletionsBlockedDetail);   // col separatore delle migliaia
        Assert.Contains(N(2014), r.DeletionsBlockedDetail);
        Assert.Contains("90", r.DeletionsBlockedDetail);
        Assert.Contains(@"E:\Backup\Documenti", r.DeletionsBlockedDetail);
    }

    [Fact]
    public void BlockedResult_ForAVersionedJob_TalksAboutTheNewVersion_NotAboutDeleting()
    {
        // Con le versioni non si cancella niente: l'ultima versione resta intatta e la nuova
        // avrebbe meno file. Dirlo come una cancellazione sarebbe falso, e spaventerebbe.
        var e = MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 1812, skipped: 202),
            previousSnapshot: "2026-09-25_210000");
        var r = MirrorDeleteGuard.BlockedResult(e, DateTime.Now);

        Assert.Equal("2026-09-25_210000", e.PreviousSnapshot);
        Assert.Contains("2026-09-25_210000", r.DeletionsBlockedDetail);
        Assert.Contains(N(1812), r.DeletionsBlockedDetail);
        Assert.DoesNotContain(@"E:\Backup\Documenti", r.DeletionsBlockedDetail);
        // E non e' la frase del mirror piatto.
        var plain = MirrorDeleteGuard.BlockedResult(
            MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 1812, skipped: 202)), DateTime.Now);
        Assert.NotEqual(plain.DeletionsBlockedDetail, r.DeletionsBlockedDetail);
    }

    [Fact]
    public void TheThreshold_IsNotARobocopyOption()
    {
        // La soglia la applica RoboKeep, non robocopy: la riga di comando non cambia di una virgola.
        var withGuard = RobocopyArgsBuilder.Build(Job(50));
        var withoutGuard = RobocopyArgsBuilder.Build(Job(0));
        Assert.Equal(withoutGuard, withGuard);
    }

    // ---- mirror senza versioni: il run non parte ----

    /// <summary>Finto robocopy: registra gli argomenti ricevuti (una riga per chiamata) e dichiara
    /// i conteggi chiesti nel formato del riepilogo vero (6 colonne intere).</summary>
    private string FakeRobocopy(long extra, long skipped)
    {
        var path = Path.Combine(_root, "fake-robocopy.cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            // Redirezione PRIMA di echo: gli argomenti finiscono con una cifra (/W:0) e in coda
            // ">>" la trasformerebbe in un numero di flusso.
            $">>\"{CallsFile}\" echo %*\r\n" +
            "echo    Dirs :         1         0         1         0         0         0\r\n" +
            $"echo   File :   {extra + skipped + 1}         0         {skipped}         0         0         {extra}\r\n");
        return path;
    }

    /// <summary>Finto robocopy che esce con un errore grave: l'anteprima non puo' stimare nulla.</summary>
    private string FakeRobocopyThatFails()
    {
        var path = Path.Combine(_root, "fake-robocopy-fails.cmd");
        File.WriteAllText(path,
            "@echo off\r\n" +
            $">>\"{CallsFile}\" echo %*\r\n" +
            "echo ERRORE : la cartella sorgente non esiste\r\n" +
            "exit /b 16\r\n");
        return path;
    }

    private string[] Calls() => File.Exists(CallsFile) ? File.ReadAllLines(CallsFile) : Array.Empty<string>();

    private BackupRunner FakeRunner(string robocopy, RunHistoryStore? history = null,
        LastResultStore? results = null, bool withForceCopy = false)
    {
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.CompressLogs = false;
        // Destinazione in una cartella temporanea: la radice del suo volume e' il disco di sistema
        // della macchina, e la copia della configurazione (ConfigMirror) non c'entra con la guardia.
        config.Settings.ConfigCopyToDestination = false;
        var creds = new CredentialService(config.Settings.CredentialScope);
        var planner = withForceCopy
            ? new ForceCopyPlanner(new ForceCopyHashStore(Path.Combine(_root, "hashes.json")))
            : null;
        return new BackupRunner(config, new RobocopyRunner(robocopy, planner, _ => DiskMedia.Unknown),
            new LogService(config.Settings), new EmailService(creds), creds, results, history: history);
    }

    private BackupJob LocalJob(bool mirror = true, int limit = BackupJob.DefaultMirrorDeleteLimitPercent) => new()
    {
        Name = "T", Source = Src, Destination = Dst, Mirror = mirror, Retries = 0, Wait = 0,
        MirrorDeleteLimitPercent = limit,
    };

    [Fact]
    public async Task PlainMirror_OverThreshold_WithNoOneToAsk_IsBlocked_AndRobocopyNeverRuns()
    {
        var history = new RunHistoryStore(Path.Combine(_root, "history.json"));
        var results = new LastResultStore(Path.Combine(_root, "lastresults.json"));
        var lines = new List<string>();

        // Callback null = attivita' pianificata o riga di comando: nessuno da interpellare.
        var result = await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202), history, results)
            .RunJobAsync(LocalJob(), progress: new Collect(lines));

        Assert.False(result.Success);
        Assert.True(result.DeletionsBlocked);
        Assert.True(result.NotStarted);
        Assert.Equal(MirrorDeleteGuard.BlockedExitCode, result.ExitCode);
        Assert.Contains(N(1812), result.DeletionsBlockedDetail);

        // Una sola chiamata a robocopy, e in anteprima: il mirror vero non e' mai partito.
        var calls = Calls();
        Assert.Single(calls);
        Assert.Contains("/L", calls[0]);
        Assert.Contains(Src, calls[0]);   // l'anteprima guarda la sorgente del job, non altro
        Assert.Contains(lines, l => l == CoreLoc.S("Guard_Preview"));

        // La cronologia ha una voce fallita che apre il log, e l'ultimo esito lo ricorda anche domani.
        var entry = Assert.Single(history.List("T"));
        Assert.Equal(RunHistoryEntry.KindBackup, entry.Kind);
        Assert.False(entry.Success);
        Assert.Equal(MirrorDeleteGuard.BlockedExitCode, entry.ExitCode);
        Assert.NotNull(result.LogPath);
        Assert.Equal(result.LogPath, entry.LogPath);
        var saved = results.Load()["T"];
        Assert.True(saved.DeletionsBlocked);
        Assert.False(saved.Success);
        Assert.Contains(N(1812), saved.DeletionsBlockedDetail);
    }

    [Fact]
    public async Task PlainMirror_WithAConfirmedYes_RunsForReal()
    {
        var asked = new List<MirrorDeleteEstimate>();
        var result = await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202)).RunJobAsync(
            LocalJob(), confirmDeletions: e => { asked.Add(e); return Task.FromResult(true); });

        Assert.False(result.DeletionsBlocked);
        Assert.True(result.Success);
        var estimate = Assert.Single(asked);
        Assert.Equal(90, estimate.Percent);

        // Anteprima e poi il mirror vero: due chiamate, la seconda senza /L.
        var calls = Calls();
        Assert.Equal(2, calls.Length);
        Assert.Contains("/L", calls[0]);
        Assert.DoesNotContain("/L", calls[1]);
    }

    [Fact]
    public async Task PlainMirror_WithAConfirmedNo_IsBlocked()
    {
        var result = await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202)).RunJobAsync(
            LocalJob(), confirmDeletions: _ => Task.FromResult(false));

        Assert.True(result.DeletionsBlocked);
        Assert.Single(Calls());
    }

    [Fact]
    public async Task Accumulate_HasNothingToGuard_AndPaysNoExtraPass()
    {
        // L'accumulo non cancella mai: l'enumerazione in piu' sarebbe costo puro.
        var result = await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202))
            .RunJobAsync(LocalJob(mirror: false));

        Assert.False(result.DeletionsBlocked);
        var calls = Assert.Single(Calls());
        Assert.DoesNotContain("/L", calls);
    }

    [Fact]
    public async Task ZeroThreshold_SkipsThePreviewAltogether()
    {
        var result = await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202))
            .RunJobAsync(LocalJob(limit: 0));

        Assert.False(result.DeletionsBlocked);
        Assert.Single(Calls());
    }

    [Fact]
    public async Task ForceCopyList_StaysOutOfThePreview()
    {
        // La passata "forza copia" gira senza /MIR: non cancella niente, quindi alla guardia non
        // dice nulla — e i suoi conteggi, sommati a quelli della prima passata, gonfierebbero il
        // totale (in modalita' smart farebbe anche l'hash di file grandi per una stima).
        var job = LocalJob();
        job.ForceCopyFiles = new List<string> { "*.txt" };
        var asked = new List<MirrorDeleteEstimate>();

        var result = await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202), withForceCopy: true)
            .RunJobAsync(job, confirmDeletions: e => { asked.Add(e); return Task.FromResult(false); });

        Assert.True(result.DeletionsBlocked);
        var calls = Assert.Single(Calls());       // una sola passata nell'anteprima
        Assert.Contains("/L", calls);
        Assert.DoesNotContain("/IS", calls);     // non e' la passata forzata
        var estimate = Assert.Single(asked);
        Assert.Equal(1812, estimate.Extra);      // conteggi della prima passata, non raddoppiati
        Assert.Equal(2014, estimate.Total);
    }

    [Fact]
    public async Task WhenThePreviewFails_ItSaysSo_AndTheMirrorRunsAnyway()
    {
        // Sorgente illeggibile: senza stima non si puo' bloccare niente (e un mirror con la
        // sorgente assente non cancella comunque), ma il log deve dire che la rete non c'era.
        var lines = new List<string>();
        var result = await FakeRunner(FakeRobocopyThatFails())
            .RunJobAsync(LocalJob(), progress: new Collect(lines));

        Assert.False(result.DeletionsBlocked);
        Assert.Contains(lines, l => l == CoreLoc.S("Guard_PreviewFailed"));
        Assert.Equal(2, Calls().Length);   // anteprima + mirror vero: la guardia non ha fermato nulla
    }

    [Fact]
    public async Task Preview_OfAVersionedJob_DoesNotCryWolf()
    {
        // In anteprima il /L di un job versionato gira contro la RADICE della destinazione, dove
        // stanno le cartelle-data di tutti gli snapshot: le conterebbe come file "extra" e
        // annuncerebbe una cancellazione in massa che il run vero non farebbe mai (lui confronta
        // con l'ultima versione).
        var job = LocalJob();
        job.Versioned = true;
        var lines = new List<string>();

        await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202))
            .RunJobAsync(job, dryRun: true, progress: new Collect(lines));

        var note = CoreLoc.S("Guard_DryRunNote").Split('{')[0].Trim();
        Assert.DoesNotContain(lines, l => l.StartsWith(note));
    }

    [Fact]
    public async Task Preview_NeverBlocks_ButSaysItInTheRecap()
    {
        var lines = new List<string>();
        var result = await FakeRunner(FakeRobocopy(extra: 1812, skipped: 202))
            .RunJobAsync(LocalJob(), dryRun: true, progress: new Collect(lines));

        Assert.False(result.DeletionsBlocked);
        Assert.Single(Calls());   // l'anteprima E' il run: nessuna passata in piu'
        var note = CoreLoc.S("Guard_DryRunNote").Split('{')[0].Trim();
        var recapStart = lines.FindIndex(l => l.StartsWith("======"));
        Assert.True(recapStart >= 0);
        Assert.True(lines.FindLastIndex(l => l.StartsWith(note)) > recapStart,
            "l'avviso sulla soglia deve stare nel riepilogo, dove l'utente guarda");
    }

    // ---- mirror con versioni: si riusa l'anteprima del versioning (robocopy vero) ----

    private const int ManyFiles = 25; // sopra MirrorDeleteGuard.MinFiles

    private void FillSource(int files)
    {
        for (var i = 0; i < files; i++)
            File.WriteAllText(Path.Combine(Src, $"f{i}.txt"), "x");
    }

    private void EmptySource()
    {
        foreach (var f in Directory.GetFiles(Src)) File.Delete(f);
    }

    private string[] Snapshots() =>
        Directory.GetDirectories(Dst).Select(Path.GetFileName)
            .Where(n => n is not null && !SnapshotName.IsInProgress(n!) && SnapshotName.TryParse(n!, out _))
            .Cast<string>().OrderBy(n => n).ToArray();

    [Fact]
    public async Task VersionedMirror_IsBlockedByTheExistingPreview_BeforeCloningAnything()
    {
        FillSource(ManyFiles);
        var job = new BackupJob { Name = "V", Source = Src, Destination = Dst, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());
        await svc.RunVersionedAsync(job);
        var first = Assert.Single(Snapshots());
        var copied = Directory.GetFiles(Path.Combine(Dst, first)).Length;
        Assert.True(copied > MirrorDeleteGuard.MinFiles);

        EmptySource(); // la cartella sorgente si e' svuotata (spostata, unita' non montata, ...)
        var run = await svc.RunVersionedAsync(job);

        Assert.True(run.Result.DeletionsBlocked);
        Assert.False(run.Result.Success);
        Assert.Equal(copied, run.Result.FilesExtra);
        // Il messaggio nomina l'ultima versione e non promette cancellazioni: quella versione resta.
        Assert.Contains(first, run.Result.DeletionsBlockedDetail);
        // Niente clone: la versione precedente e' intatta e non e' rimasta nessuna .inprogress.
        Assert.Equal(new[] { first }, Snapshots());
        Assert.DoesNotContain(Directory.GetDirectories(Dst).Select(Path.GetFileName),
            n => n is not null && SnapshotName.IsInProgress(n!));
        Assert.Equal(copied, Directory.GetFiles(Path.Combine(Dst, first)).Length);

        // Con la conferma, invece, il run procede: nasce la versione nuova (vuota) e la vecchia resta.
        await Task.Delay(1100); // nome dello snapshot al secondo
        var confirmed = await svc.RunVersionedAsync(job, confirmDeletions: _ => Task.FromResult(true));
        Assert.False(confirmed.Result.DeletionsBlocked);
        Assert.Equal(2, Snapshots().Length);
        Assert.Equal(copied, Directory.GetFiles(Path.Combine(Dst, first)).Length);
    }

    [Fact]
    public async Task TheGuard_JudgesTheSourceItWasGiven_NotTheLiveOne()
    {
        // Con VSS il run copia da uno snapshot congelato: la stima deve venire da QUELLA origine,
        // altrimenti direbbe una cosa e il mirror ne farebbe un'altra. Qui la sorgente viva e'
        // piena e l'origine passata al run e' vuota: se la guardia guardasse quella viva non
        // bloccherebbe nulla.
        FillSource(ManyFiles);
        var job = new BackupJob { Name = "V", Source = Src, Destination = Dst, Versioned = true };
        var svc = new SnapshotService(new RobocopyRunner());
        await svc.RunVersionedAsync(job);
        var first = Assert.Single(Snapshots());

        var frozen = Path.Combine(_root, "frozen");
        Directory.CreateDirectory(frozen);
        var run = await svc.RunVersionedAsync(job, sourceOverride: frozen);

        Assert.True(run.Result.DeletionsBlocked);
        Assert.True(Directory.GetFiles(Src).Length > MirrorDeleteGuard.MinFiles); // la viva e' intatta
        Assert.Equal(new[] { first }, Snapshots());
    }

    [Fact]
    public async Task VersionedMirror_BlockedRun_IsClosedLikeAnyOtherBlock()
    {
        // Il blocco che arriva da dentro il versioning deve finire nella stessa strada: log,
        // cronologia e ultimo esito, cosi' la finestra e l'attivita' pianificata lo vedono.
        FillSource(ManyFiles);
        var history = new RunHistoryStore(Path.Combine(_root, "history.json"));
        var results = new LastResultStore(Path.Combine(_root, "lastresults.json"));
        var config = new AppConfig();
        config.Settings.LogRoot = Path.Combine(_root, "logs");
        config.Settings.TempRoot = Path.Combine(_root, "temp");
        config.Settings.CompressLogs = false;
        config.Settings.ConfigCopyToDestination = false; // come sopra: niente scritture in C:\
        var creds = new CredentialService(config.Settings.CredentialScope);
        var robocopy = new RobocopyRunner();
        var runner = new BackupRunner(config, robocopy, new LogService(config.Settings),
            new EmailService(creds), creds, results, new SnapshotService(robocopy), history: history);
        var job = new BackupJob { Name = "V", Source = Src, Destination = Dst, Versioned = true };

        var ok = await runner.RunJobAsync(job);
        Assert.True(ok.Success);
        Assert.False(ok.DeletionsBlocked);

        EmptySource();
        var blocked = await runner.RunJobAsync(job);

        Assert.True(blocked.DeletionsBlocked);
        Assert.False(blocked.Success);
        Assert.Equal(MirrorDeleteGuard.BlockedExitCode, blocked.ExitCode);
        Assert.NotNull(blocked.LogPath);
        Assert.True(results.Load()["V"].DeletionsBlocked);
        Assert.Equal(2, history.List("V").Count(e => e.Kind == RunHistoryEntry.KindBackup));
        var entry = history.List("V").First(e => e.Kind == RunHistoryEntry.KindBackup && !e.Success);
        Assert.Equal(blocked.LogPath, entry.LogPath);
    }

    // ---- email ----

    [Fact]
    public void Email_AnnouncesTheBlock_AndLeadsWithTheReason()
    {
        var result = MirrorDeleteGuard.BlockedResult(
            MirrorDeleteGuard.Estimate(Job(20), Counts(extra: 1812, skipped: 202)), DateTime.Now);

        var subject = EmailService.BuildSubject(result);
        Assert.Contains(CoreLoc.S("Guard_EmailSubject"), subject);
        Assert.Contains("T", subject);
        Assert.DoesNotContain(CoreLoc.S("Email_HardwareError"), subject); // non e' un guasto del disco

        var body = EmailService.BuildBody(result);
        Assert.StartsWith(result.DeletionsBlockedDetail, body);
        Assert.DoesNotContain(CoreLoc.S("Hw_Advice"), body); // niente consigli sul cavo USB
        Assert.Contains(CoreLoc.S("Lbl_FilesExtra"), body);

        // Email "solo in caso di errore": un blocco e' una notizia, va mandata.
        Assert.True(EmailService.ShouldSend(new EmailSettings { Enabled = true, OnlyOnError = true }, result));
    }

    private sealed class Collect : IProgress<string>
    {
        private readonly List<string> _lines;
        public Collect(List<string> lines) => _lines = lines;
        public void Report(string value) { lock (_lines) _lines.Add(value); }
    }
}
