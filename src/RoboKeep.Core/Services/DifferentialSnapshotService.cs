using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Esegue un job versionato, su qualunque disco (NTFS, exFAT, FAT32, share di rete): il mirror
/// vive in <c>current\</c> e ogni backup, PRIMA di sovrascrivere, sposta in
/// <c>versions\&lt;data&gt;\</c> i file che sta per sostituire o rimuovere. E' il modello di
/// Cronologia file di Windows e di <c>rsync --backup-dir</c>: niente formati speciali, niente
/// estrazioni, solo file normali leggibili con Esplora risorse.
/// <para>Costa una scrittura sola per ogni file cambiato: lo spostamento e' una rinomina sullo
/// stesso volume, istantanea. Una versione non e' l'albero completo di quel giorno: e' la
/// differenza. Ricostruire "com'era il {giorno}" e' il lavoro del ripristino guidato, che legge i
/// manifest (<see cref="VersionManifest"/>).</para>
/// <para>Sicurezza: se il mirror fallisce a meta', <c>current</c> resta incompleta ma NESSUN dato e'
/// perso — i file spostati stanno nella <c>.inprogress</c>, che viene ripulita solo dopo un mirror
/// riuscito, e il run successivo li ricopia dalla sorgente.</para>
/// </summary>
public sealed class DifferentialSnapshotService
{
    private readonly RobocopyRunner _runner;
    private readonly AppSettings? _settings;
    private readonly Func<string, long?> _freeSpace;

    /// <summary>Quante voci "non spostate" si possono escludere dal mirror: oltre questo numero la
    /// riga di comando di robocopy diventerebbe troppo lunga per essere avviata.</summary>
    private const int MaxExclusions = 100;

    /// <param name="settings">Impostazioni globali: servono alla ritenzione per spazio
    /// (<see cref="AppSettings.FreeSpaceCleanup"/> e <see cref="AppSettings.MinFreeSpaceMb"/>).
    /// null = nessuna pulizia per spazio.</param>
    /// <param name="freeSpace">Lettore dello spazio libero, iniettabile nei test; default
    /// <see cref="FreeSpaceReader.Read"/>.</param>
    public DifferentialSnapshotService(RobocopyRunner runner, AppSettings? settings = null,
        Func<string, long?>? freeSpace = null)
    {
        _runner = runner;
        _settings = settings;
        _freeSpace = freeSpace ?? FreeSpaceReader.Read;
    }

    /// <param name="confirmDeletions">Guardia sulle cancellazioni, sui conteggi della STESSA
    /// anteprima che serve a comporre la versione: nessuna seconda enumerazione. null = nessuno da
    /// interpellare, il job si ferma. Vedi <see cref="MirrorDeleteGuard"/>.</param>
    public async Task<RobocopyRunResult> RunAsync(
        BackupJob job, IProgress<string>? progress = null, CancellationToken ct = default,
        string? sourceOverride = null, Func<MirrorDeleteEstimate, Task<bool>>? confirmDeletions = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        var dest = (job.Destination ?? "").Trim();
        Directory.CreateDirectory(dest);
        var current = VersioningLayout.CurrentDir(dest);
        var versions = VersioningLayout.VersionsDir(dest);
        var now = DateTime.Now;

        AdoptPlainMirror(dest, current, versions, sourceOverride ?? job.Source, progress);
        Directory.CreateDirectory(current);
        // Subito, non solo dopo il mirror: anche un run che non ha niente da fare deve rimettere a
        // posto una «current» rimasta in sola lettura da un backup precedente.
        ClearReadOnly(current);
        Directory.CreateDirectory(versions);

        await SweepLeftoversAsync(versions, progress, ct).ConfigureAwait(false);

        // Ritenzione per SPAZIO prima di scrivere qualunque cosa: se il disco e' sotto la soglia e
        // l'utente ha acceso la pulizia, si fa posto cancellando le versioni piu' vecchie di QUESTO
        // job. Mai la piu' recente. Se non basta, il run prosegue e fallira' con un messaggio chiaro.
        await VersionSpaceCleanup.FreeUpSpaceAsync(versions, _settings, _freeSpace, progress, ct)
            .ConfigureAwait(false);

        progress?.Report(CoreLoc.S("Versioning_Checking"));
        var preview = await PreviewAsync(job, current, sourceOverride, ct).ConfigureAwait(false);

        // Guardia sulle cancellazioni, sui conteggi dell'anteprima appena fatta. Una sorgente
        // svuotata per errore si vede proprio qui: quasi tutti i file di current risultano "extra".
        // Prima di decidere "niente da fare": un blocco deve arrivare all'utente in ogni caso.
        if (job.Mirror && preview.Result.Success && !preview.Result.HardwareError)
        {
            var estimate = MirrorDeleteGuard.Estimate(job, preview.Result);
            if (MirrorDeleteGuard.ShouldBlock(estimate)
                && !(confirmDeletions is not null && await confirmDeletions(estimate).ConfigureAwait(false)))
            {
                // Esito "fermato" identico a quello dei mirror senza versioni: BackupRunner lo
                // riconosce e lo chiude allo stesso modo (log, cronologia, email).
                return new RobocopyRunResult
                {
                    Result = MirrorDeleteGuard.BlockedResult(estimate, now),
                    Output = preview.Output,
                };
            }
        }

        // Un errore hardware gia' in anteprima: inutile (e dannoso) proseguire.
        if (preview.Result.HardwareError)
        {
            preview.Result.DryRun = false;
            return preview;
        }

        var changes = RobocopyListParser.Parse(preview.Output.Split('\n'),
            sourceOverride ?? job.Source, current);

        // Niente da fare = nessuna cartella-versione. Creare una versione vuota a ogni backup
        // riempirebbe il disco di cartelle che non raccontano niente. Ma la passata "forza copia"
        // esiste proprio per i file che l'anteprima vede invariati (stessa data e dimensione, e
        // l'anteprima gira senza di lei): se il job ne ha, dall'anteprima non si puo' decidere.
        var forceCopy = job.ForceCopyFiles is { Count: > 0 };
        if (changes.Count == 0 && NothingToDo(preview.Result) && !forceCopy)
        {
            var note = CoreLoc.S("Diff_NoChanges");
            progress?.Report(note);
            preview.Result.DryRun = false;        // e' l'esito reale del job: "gia' allineato"
            preview.Result.ThreadCapNote = null;  // non si e' copiato nulla: l'avviso sui thread sarebbe rumore
            return new RobocopyRunResult { Result = preview.Result, Output = note };
        }

        var newName = SnapshotName.For(now);
        var versionDir = Path.Combine(versions, newName + SnapshotName.InProgressSuffix);
        Directory.CreateDirectory(versionDir);

        // Il manifest si scrive PRIMA di spostare, con gli elenchi previsti, e si riscrive dopo con
        // quelli veri. Un annullamento (o una caduta di corrente) nel mezzo degli spostamenti
        // lascia cosi' una cartella che ha i file E il suo manifest: il run successivo la promuove
        // a versione invece di trovarsi una cartella muta.
        var manifest = Plan(current, changes, now);
        manifest.WriteTo(versionDir);

        var skipped = new List<string>();
        // I file messi da parte dal gancio della forza-copia: dopo la passata si confrontano con
        // quelli appena riscritti (vedi DropUnchangedForced).
        var forcedMoved = new List<string>();
        var (movedFiles, movedDirs) = MoveAsideChanges(current, versionDir, changes, manifest, skipped, progress, ct);
        manifest.WriteTo(versionDir);
        progress?.Report(string.Format(CoreLoc.S("Diff_Moved"), movedFiles, movedDirs, newName));

        // Un file che non si e' potuto mettere da parte (aperto da un altro programma) va TOLTO da
        // questo mirror: se lo si lasciasse fare, robocopy lo sovrascriverebbe e la copia
        // precedente non esisterebbe piu' da nessuna parte. Escluso, «current» resta com'era e il
        // backup successivo riprovera'. Vale anche per le cartelle "extra" non spostate: senza
        // l'esclusione il mirror le cancellerebbe.
        // Si lavora SEMPRE su una copia del job: il gancio della forza-copia ci aggiungera' le
        // esclusioni scoperte a meta' strada, e il job del chiamante non si tocca.
        var mirrorJob = job.Clone();
        ApplyExclusions(mirrorJob, job, changes, skipped, current, sourceOverride);

        // Mirror vero su current, con tutte le opzioni del job (/MT compreso). La passata
        // "forza copia" sovrascrive SUL POSTO: il gancio mette da parte quei file prima che
        // vengano riscritti, altrimenti la versione non li conterrebbe.
        var run = await _runner.RunAsync(mirrorJob, dryRun: false, progress, ct, destinationOverride: current,
            sourceOverride: sourceOverride,
            beforeForceCopyPass: filters => Task.Run(() =>
            {
                var before = skipped.Count;
                var moved = MoveMatching(current, versionDir, filters, manifest, skipped, forcedMoved, progress);
                if (moved > 0) manifest.WriteTo(versionDir);
                // Anche i file della forza-copia che non si sono potuti spostare vanno esclusi: gli
                // argomenti di quella passata vengono costruiti DOPO questo gancio, quindi si
                // arriva in tempo. Senza, /IS /IT li riscriverebbe sul posto e la copia precedente
                // sparirebbe senza lasciare traccia.
                if (skipped.Count > before)
                    ApplyExclusions(mirrorJob, job, changes, skipped, current, sourceOverride);
            }, ct))
            .ConfigureAwait(false);

        // La passata "forza copia" semplice ricopia i suoi file a OGNI run, cambiati o no, e il
        // gancio li ha messi tutti da parte. Quelli identici alla copia appena scritta non sono
        // uno stato precedente: tenerli farebbe nascere una versione a ogni backup, e a furia di
        // versioni-doppione la ritenzione («tieni N versioni») finirebbe per cancellare quella che
        // conteneva l'unica copia di un file sparito dalla sorgente. Costa una rilettura dei soli
        // file forzati (di solito pochi, anche se grandi): un prezzo giusto per non perdere quella
        // copia. Solo a run riuscito: dopo un fallimento il contenuto di «current» non e' affidabile.
        if (run.Result.Success && forcedMoved.Count > 0
            && DropUnchangedForced(current, versionDir, forcedMoved, manifest) > 0)
        {
            manifest.WriteTo(versionDir);
        }

        // Si e' corso solo per la "forza copia" e non c'era niente da conservare: nessun punto nel
        // tempo da annotare, come quando l'anteprima dice che non c'e' niente da fare. Mai se un
        // file e' rimasto indietro perche' in uso: quella notizia deve arrivare all'utente (piu'
        // sotto), altrimenti il backup direbbe "tutto a posto" con «current» ferma per sempre.
        if (run.Result.Success && changes.Count == 0 && skipped.Count == 0 && !HasContent(versionDir)
            && manifest.Changed.Count == 0 && manifest.Deleted.Count == 0 && manifest.Added.Count == 0)
        {
            // Non ricorsiva: la cartella e' vuota per costruzione, e se non lo fosse non si butta.
            try { Directory.Delete(versionDir, recursive: false); } catch { /* best-effort */ }
            VersionManifest.DeleteFor(versionDir);
            ClearReadOnly(current);
            progress?.Report(CoreLoc.S("Diff_NoChanges"));
            return run;
        }

        // Riuscito o no, la cartella va promossa a versione vera se contiene qualcosa: i file che
        // ci sono stati messi da parte NON sono in nessun altro posto — quelli cancellati dalla
        // sorgente hanno li' l'unica copia rimasta — e lasciarla ".inprogress" significherebbe
        // vederla cancellata dalla pulizia dei residui al run successivo. Una versione parziale e'
        // una versione vera: contiene esattamente gli stati precedenti di cio' che ha spostato.
        Promote(versions, versionDir, newName, progress,
            run.Result.Success ? null : CoreLoc.S("Diff_PromotedPartial"));

        ClearReadOnly(current);

        // La ritenzione si applica solo dopo un backup RIUSCITO: dopo un fallimento il conto delle
        // versioni non e' quello che l'utente crede, e cancellare la piu' vecchia per far posto a
        // una parziale sarebbe uno scambio in perdita. Gira anche quando non e' nata nessuna
        // cartella (backup di sole aggiunte): ci sono comunque i manifest da potare.
        if (run.Result.Success)
            ApplyRetention(versions, job, now, progress);

        // I file che non si sono potuti mettere da parte sono rimasti com'erano (esclusi da questo
        // mirror): e' una notizia che riguarda dei dati e deve arrivare all'utente anche a backup
        // riuscito, quindi viaggia nell'esito fino al riepilogo, al log e all'email.
        if (skipped.Count > 0)
            run.Result.VersionNotes.Add(string.Format(CoreLoc.S("Diff_SkippedLocked"),
                skipped.Count, string.Join(", ", skipped.Take(10))));

        return run;
    }

    /// <summary>
    /// Riscrive le esclusioni del job-copia: quelle dell'utente piu' i percorsi che non si sono
    /// potuti mettere da parte, cosi' la loro copia precedente resta intatta in <c>current</c>
    /// invece di essere sovrascritta o cancellata senza che ne esista un'altra. Idempotente: si
    /// ricostruisce sempre da <paramref name="original"/>, quindi la si puo' richiamare quando
    /// l'elenco cresce a meta' del run senza accumulare doppioni.
    /// <para>Verificato con robocopy vero su questa macchina: <c>/XF</c> e <c>/XD</c> accettano un
    /// percorso completo, ma con due semantiche diverse — il percorso di SORGENTE impedisce la
    /// COPIA (quindi la sovrascrittura), quello di DESTINAZIONE impedisce la RIMOZIONE del file o
    /// della cartella "extra". Un percorso di destinazione da solo NON ferma una sovrascrittura.
    /// Si passano quindi entrambi: quello che non serve semplicemente non corrisponde a nulla.</para>
    /// </summary>
    private static void ApplyExclusions(BackupJob clone, BackupJob original,
        IReadOnlyList<ListedChange> changes, IReadOnlyList<string> skipped, string current, string? sourceOverride)
    {
        var source = sourceOverride ?? original.Source ?? "";
        var dirs = changes.Where(c => c.Kind is ChangeKind.ExtraDir or ChangeKind.NewDir)
            .Select(c => c.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        clone.ExcludeFiles = new List<string>(original.ExcludeFiles ?? new List<string>());
        clone.ExcludeDirs = new List<string>(original.ExcludeDirs ?? new List<string>());
        // La riga di comando di Windows ha un tetto (circa 32 000 caratteri) e ogni voce ne costa
        // due percorsi completi: oltre un centinaio si rischia di non poter avviare robocopy
        // affatto, che sarebbe peggio. Se tanti file sono in uso insieme c'e' un problema piu'
        // grande (un programma che tiene aperto un albero intero), e il conto completo finisce
        // comunque nell'avviso dell'esito.
        foreach (var rel in skipped.Take(MaxExclusions))
        {
            var target = dirs.Contains(rel) ? clone.ExcludeDirs : clone.ExcludeFiles;
            target.Add(Path.Combine(current, rel));
            if (source.Length > 0) target.Add(Path.Combine(source, rel));
        }
    }

    /// <summary>Elenchi PREVISTI, decisi dal disco (il file esiste gia' in <c>current</c>?) e non
    /// dalle etichette tradotte di robocopy. Serve a poter scrivere il manifest prima di muovere
    /// qualunque cosa.</summary>
    private static VersionManifest Plan(string current, IReadOnlyList<ListedChange> changes, DateTime now)
    {
        var manifest = new VersionManifest { CreatedAt = now };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            var rel = change.RelativePath;
            if (!seen.Add(rel)) continue;
            switch (change.Kind)
            {
                case ChangeKind.NewDir:
                    seen.Remove(rel); // non e' una voce del manifest: non occupa il posto di un file
                    break;
                case ChangeKind.ExtraDir:
                case ChangeKind.ExtraFile:
                    manifest.Deleted.Add(rel);
                    break;
                default:
                    if (File.Exists(Path.Combine(current, rel))) manifest.Changed.Add(rel);
                    else manifest.Added.Add(rel);
                    break;
            }
        }
        return manifest;
    }

    /// <summary>Anteprima <c>/L /FP /BYTES</c> della sorgente contro <c>current</c>: dice sia che
    /// cosa cambierebbe (riga per riga) sia quanto (i conteggi del riepilogo, che servono alla
    /// guardia). Una sola enumerazione per due domande.</summary>
    private Task<RobocopyRunResult> PreviewAsync(BackupJob job, string current, string? sourceOverride, CancellationToken ct)
    {
        var previewJob = job.Clone();
        // Senza multi-thread: con /MT robocopy conta come "copiata" ogni cartella anche quando non
        // c'e' niente da fare, e i conteggi mentirebbero. Per una enumerazione il parallelismo non serve.
        previewJob.MultiThread = 0;
        // Senza /V: le righe dei file identici non descrivono nessun cambiamento e, con le etichette
        // tradotte, sarebbero l'unica riga che il lettore non saprebbe scartare con certezza.
        previewJob.LogAllFiles = false;
        // Niente passata "forza copia" nell'anteprima: gira senza /MIR (quindi non dice nulla alla
        // guardia), i suoi conteggi gonfierebbero il totale e le sue righe duplicherebbero l'elenco.
        // I file di quella lista li mette da parte il gancio beforeForceCopyPass, al momento giusto.
        previewJob.ForceCopyFiles = new();
        return _runner.RunAsync(previewJob, dryRun: true, progress: null, ct,
            destinationOverride: current, sourceOverride: sourceOverride, listDetails: true);
    }

    /// <summary>
    /// Sposta in <paramref name="versionDir"/> tutto cio' che il mirror sta per sostituire o
    /// rimuovere, riempiendo il manifest. L'etichetta di robocopy non dice in modo affidabile se un
    /// file e' nuovo o da sovrascrivere (e' tradotta): lo decide il disco, cioe' se il file esiste
    /// gia' in <c>current</c>. Restituisce quanti file e quante cartelle sono stati messi da parte.
    /// </summary>
    private static (int Files, int Dirs) MoveAsideChanges(string current, string versionDir,
        IReadOnlyList<ListedChange> changes, VersionManifest manifest, List<string> skipped,
        IProgress<string>? progress, CancellationToken ct)
    {
        int files = 0, dirs = 0;
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Gli elenchi arrivano pieni di cio' che era PREVISTO (vedi Plan): si riscrivono con cio'
        // che e' davvero finito nella cartella della versione.
        manifest.Changed.Clear();
        manifest.Deleted.Clear();
        manifest.Added.Clear();

        foreach (var change in changes)
        {
            ct.ThrowIfCancellationRequested();
            var rel = change.RelativePath;

            switch (change.Kind)
            {
                // La cartella nuova la crea robocopy: non c'e' niente da mettere da parte.
                case ChangeKind.NewDir:
                    break;

                case ChangeKind.ExtraDir:
                {
                    var from = Path.Combine(current, rel);
                    // Gia' portata via insieme alla cartella che la conteneva: robocopy elenca il
                    // padre prima dei figli, e lo spostamento si porta dietro tutto l'albero. Nel
                    // manifest ci va lo stesso: il ripristino deve sapere che quella
                    // sottocartella e' finita in questa versione, non nel nulla.
                    if (!Directory.Exists(from))
                    {
                        if (Directory.Exists(Path.Combine(versionDir, rel))) Add(manifest.Deleted, done, rel);
                        break;
                    }
                    try
                    {
                        var to = Path.Combine(versionDir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                        Directory.Move(from, to);
                        dirs++;
                        Add(manifest.Deleted, done, rel);
                    }
                    catch (Exception ex) when (!DiskError.IsUnreadable(ex))
                    {
                        // Cartella non spostata: va esclusa dal mirror, altrimenti la cancellerebbe
                        // come "extra" e il suo contenuto non esisterebbe piu' da nessuna parte.
                        progress?.Report(string.Format(CoreLoc.S("Diff_MoveFailed"), rel, ex.Message));
                        skipped.Add(rel);
                    }
                    break;
                }

                case ChangeKind.ExtraFile:
                    if (File.Exists(Path.Combine(current, rel)))
                    {
                        if (MoveAside(current, versionDir, rel, progress)) { files++; Add(manifest.Deleted, done, rel); }
                        else skipped.Add(rel);
                    }
                    // Arrivata qui dentro con la cartella che la contiene: nel manifest ci va lo
                    // stesso, il ripristino deve sapere che quel file e' finito in questa versione.
                    else if (File.Exists(Path.Combine(versionDir, rel)))
                    {
                        Add(manifest.Deleted, done, rel);
                    }
                    break;

                // NewFile / Overwrite: decide il disco, non l'etichetta tradotta.
                default:
                    if (File.Exists(Path.Combine(current, rel)))
                    {
                        if (MoveAside(current, versionDir, rel, progress)) { files++; Add(manifest.Changed, done, rel); }
                        else skipped.Add(rel);
                    }
                    else
                    {
                        Add(manifest.Added, done, rel);
                    }
                    break;
            }
        }

        return (files, dirs);
    }

    /// <summary>Mette da parte i file della lista "Forza copia" prima che la passata con
    /// <c>/IS /IT</c> li riscriva sul posto. Stessa semantica di filtro di robocopy (nome o pattern,
    /// cercato in tutto l'albero); i file gia' spostati non si toccano due volte. Quelli spostati
    /// finiscono anche in <paramref name="movedList"/>; quelli che non si sono potuti spostare in
    /// <paramref name="skipped"/>, e chi chiama li esclude dalla passata prima che parta.</summary>
    private static int MoveMatching(string current, string versionDir, IReadOnlyList<string> filters,
        VersionManifest manifest, List<string> skipped, List<string> movedList, IProgress<string>? progress)
    {
        if (!Directory.Exists(current)) return 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive,
        };
        var done = new HashSet<string>(manifest.Changed.Concat(manifest.Deleted).Concat(manifest.Added),
            StringComparer.OrdinalIgnoreCase);
        var moved = 0;
        foreach (var filter in filters.Where(f => !string.IsNullOrWhiteSpace(f)))
            foreach (var file in Directory.EnumerateFiles(current, filter.Trim(), options).ToList())
            {
                var rel = Path.GetRelativePath(current, file);
                if (done.Contains(rel)) continue;
                if (!MoveAside(current, versionDir, rel, progress)) { skipped.Add(rel); continue; }
                Add(manifest.Changed, done, rel);
                movedList.Add(rel);
                moved++;
            }
        return moved;
    }

    /// <summary>Toglie dalla versione le copie dei file forzati che la passata ha riscritto
    /// identici (stessa lunghezza e stesso SHA-256), e le toglie dal manifest: non erano uno stato
    /// precedente. Restituisce quante ne ha tolte. Nel dubbio (un file che non si legge) la copia
    /// resta: meglio una versione in piu' che una copia in meno.</summary>
    private static int DropUnchangedForced(string current, string versionDir, IReadOnlyList<string> forced,
        VersionManifest manifest)
    {
        var dropped = 0;
        foreach (var rel in forced)
        {
            var kept = Path.Combine(versionDir, rel);
            if (!SameContent(kept, Path.Combine(current, rel))) continue;
            try
            {
                FileSystemDelete.DeleteFile(kept);
            }
            catch (Exception ex) when (!DiskError.IsUnreadable(ex)) { continue; }
            manifest.Changed.RemoveAll(c => string.Equals(c, rel, StringComparison.OrdinalIgnoreCase));
            RemoveEmptyParents(versionDir, Path.GetDirectoryName(kept), manifest);
            dropped++;
        }
        return dropped;
    }

    /// <summary>true se i due file hanno la stessa lunghezza e lo stesso SHA-256. Qualunque
    /// imprevisto (file sparito, in uso) vale "diversi".</summary>
    private static bool SameContent(string a, string b)
    {
        try
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (!fa.Exists || !fb.Exists || fa.Length != fb.Length) return false;
            using var sa = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sb = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read);
            return System.Security.Cryptography.SHA256.HashData(sa)
                .AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(sb));
        }
        catch (Exception ex) when (!DiskError.IsUnreadable(ex)) { return false; }
    }

    /// <summary>Dopo aver tolto un file dalla versione, toglie le cartelle rimaste vuote risalendo
    /// fino alla cartella della versione (esclusa). Una cartella che il manifest elenca come
    /// cancellata e' un dato della versione, e resta anche se vuota.</summary>
    private static void RemoveEmptyParents(string versionDir, string? dir, VersionManifest manifest)
    {
        var root = Path.TrimEndingDirectorySeparator(versionDir);
        while (!string.IsNullOrEmpty(dir)
               && !string.Equals(Path.TrimEndingDirectorySeparator(dir), root, StringComparison.OrdinalIgnoreCase)
               && dir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var rel = Path.GetRelativePath(versionDir, dir);
            if (manifest.Deleted.Contains(rel, StringComparer.OrdinalIgnoreCase)) return;
            try
            {
                if (Directory.EnumerateFileSystemEntries(dir).Any()) return;
                Directory.Delete(dir, recursive: false);
            }
            catch { return; }
            dir = Path.GetDirectoryName(dir);
        }
    }

    /// <summary>Sposta un file da <c>current</c> alla cartella della versione (rinomina sullo stesso
    /// volume: istantanea). Un file che non si riesce a spostare (in uso, permessi) resta dov'e': chi
    /// chiama lo esclude dal mirror, e il backup successivo riprovera'.</summary>
    private static bool MoveAside(string current, string versionDir, string rel, IProgress<string>? progress)
    {
        try
        {
            var to = Path.Combine(versionDir, rel);
            // Mai sovrascrivere qualcosa che sta gia' nella cartella della versione: quel file
            // potrebbe essere l'unica copia rimasta di un file cancellato dalla sorgente.
            // File.Move senza overwrite lancerebbe comunque; il controllo esplicito rende il
            // motivo leggibile nel log invece di un codice di errore di Windows.
            if (File.Exists(to) || Directory.Exists(to))
            {
                progress?.Report(string.Format(CoreLoc.S("Diff_MoveFailed"), rel,
                    CoreLoc.S("Diff_TargetExists")));
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Move(Path.Combine(current, rel), to);
            return true;
        }
        catch (Exception ex) when (!DiskError.IsUnreadable(ex))
        {
            progress?.Report(string.Format(CoreLoc.S("Diff_MoveFailed"), rel, ex.Message));
            return false;
        }
    }

    private static void Add(List<string> list, HashSet<string> done, string rel)
    {
        if (done.Add(rel)) list.Add(rel);
    }

    /// <summary>
    /// La <c>.inprogress</c> diventa una versione vera: rinomina al nome definitivo (avanzando di
    /// un secondo se quel nome e' gia' preso) portandosi dietro il manifest gemello. Restituisce il
    /// percorso della CARTELLA finale, o null se cartella non ne e' rimasta nessuna (backup di sole
    /// aggiunte, o rinomina non riuscita).
    /// <para>Si promuove anche dopo un mirror FALLITO (<paramref name="partialNote"/> valorizzato):
    /// i file messi da parte non stanno in nessun altro posto, e per quelli cancellati dalla
    /// sorgente quella e' l'unica copia rimasta.</para>
    /// <para>Una cartella VUOTA invece non si tiene: se il run e' riuscito resta il solo manifest
    /// (il backup ha solo aggiunto file), se e' fallito non resta niente.</para>
    /// </summary>
    private static string? Promote(string versions, string versionDir, string newName,
        IProgress<string>? progress, string? partialNote)
    {
        // Nessuno stato precedente da conservare: il backup ha solo AGGIUNTO file (il caso normale
        // di un archivio che cresce) oppure e' fallito prima di toccare qualcosa. Una cartella
        // vuota non va tenuta: occuperebbe uno dei posti di «tieni N versioni» senza contenere
        // niente, e a furia di backup di sole aggiunte la ritenzione finirebbe per cancellare
        // l'unica versione che conteneva davvero qualcosa. Del run riuscito resta il manifest, che
        // pesa un nulla e dice al ripristino quali file a quella data non esistevano ancora.
        if (!HasContent(versionDir))
        {
            // Non ricorsiva: vuota per costruzione, e se non lo fosse non si butta.
            try { Directory.Delete(versionDir, recursive: false); } catch { /* best-effort */ }

            if (partialNote is not null)
            {
                // Run fallito e niente messo da parte: non c'e' nemmeno un punto nel tempo
                // coerente da annotare. Via anche il manifest.
                VersionManifest.DeleteFor(versionDir);
                return null;
            }

            var onlyManifest = Path.Combine(versions, SnapshotName.FreeName(newName,
                n => IsTaken(versions, n)));
            VersionManifest.MoveWith(versionDir, onlyManifest);
            progress?.Report(string.Format(CoreLoc.S("Diff_AddedOnly"), Path.GetFileName(onlyManifest)));
            return null; // nessuna cartella: non c'e' niente su cui applicare la ritenzione
        }

        // Nome gia' occupato: si avanza di un secondo, cosi' resta una data interpretabile (un
        // suffisso casuale la renderebbe invisibile alla ritenzione e all'elenco delle versioni).
        var final = Path.Combine(versions, SnapshotName.FreeName(newName, n => IsTaken(versions, n)));
        try
        {
            Directory.Move(versionDir, final);
            VersionManifest.MoveWith(versionDir, final);
        }
        catch (Exception ex) when (!DiskError.IsUnreadable(ex))
        {
            // La cartella resta ".inprogress" con i suoi file dentro: non e' perduta, la pulizia
            // dei residui del prossimo run la promuove invece di cancellarla (vedi SweepLeftoversAsync).
            progress?.Report(string.Format(CoreLoc.S("Diff_RenameFailed"),
                Path.GetFileName(versionDir), ex.Message));
            return null;
        }

        progress?.Report(partialNote is null
            ? string.Format(CoreLoc.S("Diff_Created"), Path.GetFileName(final))
            : string.Format(partialNote, Path.GetFileName(final)));
        return final;
    }

    /// <summary>true se quel nome-data e' gia' usato da una cartella-versione O da un manifest
    /// orfano: due punti nel tempo non possono condividere il nome, altrimenti il manifest di uno
    /// diventerebbe il gemello dell'altro.</summary>
    private static bool IsTaken(string versions, string name)
    {
        var path = Path.Combine(versions, name);
        return Directory.Exists(path) || File.Exists(VersionManifest.PathFor(path));
    }

    /// <summary>Ritenzione sulle cartelle di <c>versions\</c>: «tieni N versioni» / «non piu'
    /// vecchie di N giorni» (<see cref="SnapshotPlanner"/>). Ogni versione se ne porta via il
    /// manifest gemello.</summary>
    private static void ApplyRetention(string versions, BackupJob job, DateTime now, IProgress<string>? progress)
    {
        // «Tieni N versioni» conta le CARTELLE, cioe' le versioni che contengono davvero qualcosa.
        // I backup di sole aggiunte non ne creano (vedi VersionCatalog): se contassero anche loro,
        // dieci giorni di sole aggiunte basterebbero a far cancellare l'unica versione che teneva
        // la copia di un file sparito dalla sorgente.
        var after = Directory.GetDirectories(versions).Select(Path.GetFileName).OfType<string>().ToList();
        foreach (var name in SnapshotPlanner.SnapshotsToDelete(after, job.SnapshotKeepCount, job.SnapshotMaxAgeDays, now))
        {
            var dir = Path.Combine(versions, name);
            try
            {
                FileSystemDelete.DeleteDirectory(dir);
                VersionManifest.DeleteFor(dir);
                after.Remove(name);
                progress?.Report(string.Format(CoreLoc.S("Diff_Removed"), name));
            }
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                progress?.Report(string.Format(CoreLoc.S("Diff_RemoveFailed"), name, ex.Message));
            }
        }

        // Manifest senza cartella piu' vecchi della cartella piu' vecchia rimasta: non possono
        // aiutare nessun ripristino (gli stati precedenti di quel periodo non ci sono piu').
        foreach (var name in VersionCatalog.ManifestsToPrune(after, VersionCatalog.ManifestNames(versions)))
            VersionManifest.DeleteFor(Path.Combine(versions, name));
    }

    /// <summary>true se la cartella contiene qualcosa (il manifest sta FUORI, come file gemello,
    /// quindi non falsa il conto). Best-effort: se non si riesce a guardarci dentro si risponde
    /// "c'e' qualcosa", cosi' nel dubbio la cartella si conserva invece di sparire.</summary>
    private static bool HasContent(string dir)
    {
        try { return Directory.EnumerateFileSystemEntries(dir).Any(); }
        catch { return true; }
    }

    /// <summary>
    /// Un job che passa da "copia semplice" a "con versioni" ha gia' un backup
    /// completo sciolto nella destinazione. Qui quella copia diventa <c>current</c> con una rinomina
    /// per voce - stesso volume, istantanea, zero ricopia - invece di essere ricopiata da zero
    /// lasciando il doppione a occupare il disco. Le cartelle del layout (<c>current</c>,
    /// <c>versions</c>), la copia della configurazione e i residui dei run interrotti non sono
    /// contenuto da adottare; le voci nascoste o di sistema (es. "System Volume Information" se la
    /// destinazione e' la radice di un disco) non si toccano.
    /// <para>Si adotta solo se il contenuto sciolto e' riconoscibile come copia della sorgente
    /// (<see cref="MirrorAdoption"/>): qui conta doppio, perche' <c>current</c> e' il bersaglio del
    /// mirror e quel che ci finisce dentro senza essere in sorgente verrebbe cancellato al primo run.</para>
    /// </summary>
    private static void AdoptPlainMirror(string dest, string current, string versions, string source,
        IProgress<string>? progress)
    {
        if (!Directory.Exists(dest)) return;

        var entries = new DirectoryInfo(dest).EnumerateFileSystemInfos()
            .Where(e => (e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            .Where(e => !MirrorAdoption.IsLayoutEntry(e.Name))
            .ToList();

        if (Directory.Exists(current))
        {
            // «current» c'e' gia'. Di solito vuol dire che il layout e' in piedi e non c'e' niente
            // da adottare. Ma se accanto a lei e' rimasto anche del contenuto sciolto, la
            // spiegazione puo' essere un'altra: la sorgente conteneva una cartella di primo livello
            // chiamata «current», un mirror piatto precedente l'ha copiata li', e quella e' roba
            // dell'utente — non il nostro layout. Nel dubbio non si adotta e non si sposta niente,
            // ma lo si DICE: in silenzio l'utente si ritroverebbe il backup dentro una cartella sua
            // senza capire perche'. Lo si dice finche' non esiste nessuna versione: dopo, il
            // layout e' evidentemente il nostro, e ripeterlo a ogni run (una destinazione che e' la
            // radice di un disco con altre cartelle dell'utente) sarebbe solo rumore.
            if (entries.Count > 0 && VersionCatalog.List(versions).Count == 0)
                progress?.Report(string.Format(CoreLoc.S("Diff_AdoptAmbiguous"),
                    VersioningLayout.CurrentFolderName, entries.Count));
            return;
        }

        if (entries.Count == 0) return;

        if (!Directory.Exists(source)) return; // sorgente illeggibile: non si puo' dire se e' una copia
        var foreign = MirrorAdoption.ForeignPaths(dest, source, entries, max: 3, out var foreignCount);
        if (foreignCount > 0)
        {
            progress?.Report(string.Format(CoreLoc.S("Versioning_NotAdopted"),
                string.Join(", ", foreign), foreignCount));
            return;
        }

        Directory.CreateDirectory(current);
        var moved = 0;
        foreach (var e in entries)
        {
            try
            {
                var target = Path.Combine(current, e.Name);
                if (e is DirectoryInfo d) d.MoveTo(target); else ((FileInfo)e).MoveTo(target);
                moved++;
            }
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                // Una voce bloccata resta sciolta nella destinazione: non e' dentro current, quindi
                // nessun mirror la tocchera', e robocopy la ricopia dalla sorgente. Non e' un fallimento.
                progress?.Report(string.Format(CoreLoc.S("Diff_AdoptSkipped"), e.Name, ex.Message));
            }
        }

        progress?.Report(string.Format(CoreLoc.S("Diff_Adopted"), moved));
    }

    /// <summary>
    /// Ripulisce <c>versions\</c> dai resti di un run interrotto (crash, chiusura dell'app, caduta
    /// di corrente) e di una cancellazione interrotta (<c>.deleting-…</c>): nessuna regola li
    /// toccherebbe mai piu' e occuperebbero disco per sempre.
    /// <para><b>Ma una <c>.inprogress</c> che contiene qualcosa NON si cancella</b>: contiene gli
    /// originali SPOSTATI via da <c>current</c>, e per quelli cancellati dalla sorgente e' l'unica
    /// copia rimasta. Quindi si promuove a versione vera (era esattamente questo: gli stati
    /// precedenti di cio' che era stato spostato) e solo le cartelle vuote si cancellano.</para>
    /// Best-effort — un residuo bloccato non deve far fallire il job — ma un errore hardware no:
    /// quello ferma il job come ovunque.
    /// </summary>
    private static async Task SweepLeftoversAsync(string versions, IProgress<string>? progress, CancellationToken ct)
    {
        foreach (var stale in Directory.GetDirectories(versions)
                     .Where(d => Path.GetFileName(d) is { } n
                         && (SnapshotName.IsInProgress(n) || n.Contains(".deleting-", StringComparison.Ordinal))))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(stale);

            // Una versione interrotta con dei file dentro e' un recupero, non un residuo. I
            // ".deleting-…" invece erano gia' stati condannati da una cancellazione voluta.
            if (SnapshotName.IsInProgress(name) && HasContent(stale))
            {
                // Promozione non riuscita: la cartella resta com'e' e si riprovera' al run dopo.
                // Mai cancellata.
                Promote(versions, stale, name[..^SnapshotName.InProgressSuffix.Length], progress,
                    CoreLoc.S("Diff_Recovered"));
                continue;
            }

            try
            {
                await Task.Run(() => FileSystemDelete.DeleteDirectory(stale), ct).ConfigureAwait(false);
                VersionManifest.DeleteFor(stale);
                progress?.Report(string.Format(CoreLoc.S("Diff_StaleRemoved"), name));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                progress?.Report(string.Format(CoreLoc.S("Diff_LeftoverKept"), name, ex.Message));
            }
        }
    }

    /// <summary>Toglie gli attributi sola-lettura e sistema dalla cartella <c>current</c>. robocopy
    /// copia gli attributi della cartella sorgente: se la sorgente e' una cartella "speciale" (es.
    /// Desktop) con un desktop.ini, Esplora risorse — che lo onora con l'uno o con l'altro
    /// attributo — mostrerebbe <c>current</c> col nome e l'icona del desktop.ini invece del suo
    /// nome vero. Il desktop.ini resta tra i file del backup, intatto. Best-effort: un attributo
    /// non deve far fallire il backup.</summary>
    private static void ClearReadOnly(string dir)
    {
        const FileAttributes marks = FileAttributes.ReadOnly | FileAttributes.System;
        try
        {
            var di = new DirectoryInfo(dir);
            if (di.Exists && (di.Attributes & marks) != 0)
                di.Attributes &= ~marks;
        }
        catch { /* best-effort */ }
    }

    /// <summary>true se l'anteprima dice che sorgente e <c>current</c> sono gia' allineate: nessun
    /// file o cartella da copiare, nessun extra da rimuovere, nessun errore. Si pretendono sia
    /// l'exit code 0 sia i conteggi a zero: due letture indipendenti dello stesso fatto.</summary>
    private static bool NothingToDo(JobResult r) =>
        !r.HardwareError && r.ExitCode == 0
        && r.FilesCopied == 0 && r.DirsCopied == 0
        && r.FilesExtra == 0 && r.DirsExtra == 0
        && r.FilesFailed == 0 && r.DirsFailed == 0;
}
