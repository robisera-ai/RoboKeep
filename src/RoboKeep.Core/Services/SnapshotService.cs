using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Esegue un job versionato producendo uno snapshot datato con hard-link:
/// clona lo snapshot precedente, rompe gli hard-link dei file cambiati, robocopy nella cartella
/// .inprogress, rinomina a esito riuscito, applica la ritenzione. Assume destinazione idonea agli
/// hard-link (verificata a monte).
/// </summary>
public sealed class SnapshotService
{
    private readonly RobocopyRunner _runner;
    private readonly AppSettings? _settings;
    private readonly Func<string, long?> _freeSpace;

    /// <param name="settings">Impostazioni globali: servono alla ritenzione per spazio
    /// (<see cref="AppSettings.FreeSpaceCleanup"/> e <see cref="AppSettings.MinFreeSpaceMb"/>).
    /// null = nessuna pulizia per spazio (il comportamento di sempre).</param>
    /// <param name="freeSpace">Lettore dello spazio libero, iniettabile nei test; default
    /// <see cref="FreeSpaceReader.Read"/>. null dal lettore = non si sa, e la pulizia si salta.</param>
    public SnapshotService(RobocopyRunner runner, AppSettings? settings = null,
        Func<string, long?>? freeSpace = null)
    {
        _runner = runner;
        _settings = settings;
        _freeSpace = freeSpace ?? FreeSpaceReader.Read;
    }

    /// <param name="confirmDeletions">Guardia sulle cancellazioni: chiesto solo quando l'anteprima
    /// contro l'ultimo snapshot dice che il mirror rimuoverebbe piu' della soglia del job. null =
    /// nessuno da interpellare, il job si ferma. Vedi <see cref="MirrorDeleteGuard"/>.</param>
    public async Task<RobocopyRunResult> RunVersionedAsync(
        BackupJob job, IProgress<string>? progress = null, CancellationToken ct = default,
        string? sourceOverride = null, Func<MirrorDeleteEstimate, Task<bool>>? confirmDeletions = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        var dest = (job.Destination ?? "").Trim();
        Directory.CreateDirectory(dest);

        var now = DateTime.Now;
        var prevName = SnapshotName.Latest(dest) ?? AdoptPlainMirror(dest, sourceOverride ?? job.Source, now, progress);

        var newName = SnapshotName.For(now);
        var curr = Path.Combine(dest, newName + SnapshotName.InProgressSuffix);

        // Ripulisci OGNI .inprogress residua, non solo quella con lo stesso nome: un run precedente
        // interrotto (crash, chiusura dell'app, caduta di corrente) lascia una .inprogress con un
        // timestamp diverso che i run successivi non toccherebbero mai (la ritenzione ignora le
        // .inprogress), accumulandole all'infinito. Stesso discorso per i residui ".deleting-…":
        // FileSystemDelete rinomina prima di cancellare, e una cancellazione interrotta a meta'
        // lascia una cartella che nessuna regola tocca piu' - occupa disco e non e' una versione.
        // Best-effort: un residuo bloccato non deve far fallire il job. Uso la cancellazione
        // POSIX-safe per non intaccare il read-only degli inode ancora condivisi col precedente.
        foreach (var stale in Directory.GetDirectories(dest)
                     .Where(d => Path.GetFileName(d) is { } n
                         && (SnapshotName.IsInProgress(n) || n.Contains(".deleting-", StringComparison.Ordinal))))
        {
            try
            {
                // Su thread di background: cancellare migliaia di hard-link non deve congelare la UI.
                await Task.Run(() => FileSystemDelete.DeleteDirectory(stale), ct).ConfigureAwait(false);
                progress?.Report($"[versioning] rimosso snapshot incompleto di un run interrotto: {Path.GetFileName(stale)}");
            }
            // L'annullamento dell'utente non e' un residuo che resiste: ferma il job, e va riproposto
            // a chi chiama invece di finire in una riga di log.
            catch (OperationCanceledException) { throw; }
            // Best-effort vale per lock e permessi, NON per un errore hardware: quello ferma il job.
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                progress?.Report($"[versioning] residuo {Path.GetFileName(stale)} non rimosso: {ex.Message}");
            }
        }

        // Ritenzione per SPAZIO, prima di scrivere qualunque cosa (anteprima, clone, copia): se il
        // disco e' sotto la soglia di spazio libero e l'utente ha acceso la pulizia, si fa posto
        // cancellando le versioni piu' vecchie di QUESTO job, una alla volta, ricontrollando lo
        // spazio dopo ciascuna. Mai la piu' recente: quella E' il backup. Se non basta, il run
        // prosegue e fallira' come prima - con un messaggio chiaro, non con un exit code.
        await FreeUpSpaceAsync(dest, progress, ct).ConfigureAwait(false);

        // Niente di cambiato = niente snapshot nuovo. Clonare lo snapshot precedente costa una
        // scrittura di metadati (MFT, indice, journal) per OGNI file, e altrettante cancellazioni
        // quando la ritenzione lo eliminera': su un disco meccanico e' il carico piu' pesante
        // dell'intero programma, e pagarlo per ottenere una copia identica alla precedente non ha
        // senso. Un'anteprima (/L) contro l'ultimo snapshot e' di sola lettura e dice se serve.
        if (prevName is not null)
        {
            progress?.Report(CoreLoc.S("Versioning_Checking"));
            // Anteprima SENZA multi-thread: con /MT robocopy conta come "copiata" ogni cartella
            // anche quando non c'e' nulla da fare, e il suo exit code resta 0 perfino con una
            // cartella vuota nuova. Solo i conteggi a thread singolo dicono il vero; e per una
            // semplice enumerazione il parallelismo non serve.
            var previewJob = job.Clone();
            previewJob.MultiThread = 0;
            var preview = await _runner.RunAsync(previewJob, dryRun: true, progress: null, ct,
                destinationOverride: Path.Combine(dest, prevName), sourceOverride: sourceOverride)
                .ConfigureAwait(false);

            // Guardia sulle cancellazioni, sui conteggi dell'anteprima appena fatta: nessuna
            // seconda enumerazione. Una sorgente svuotata per errore si vede proprio qui — quasi
            // tutti i file dell'ultimo snapshot risultano "extra". Prima di decidere "niente da
            // fare": un blocco deve arrivare all'utente in ogni caso. Solo in mirror, e solo con
            // uno snapshot precedente: al primo run (o all'adozione) non c'e' nulla da confrontare.
            if (job.Mirror && preview.Result.Success && !preview.Result.HardwareError)
            {
                var estimate = MirrorDeleteGuard.Estimate(job, preview.Result, previousSnapshot: prevName);
                if (MirrorDeleteGuard.ShouldBlock(estimate)
                    && !(confirmDeletions is not null && await confirmDeletions(estimate).ConfigureAwait(false)))
                {
                    // Esito "fermato" come quello dei mirror senza versioni: BackupRunner lo
                    // riconosce e lo chiude allo stesso modo (log, cronologia, email).
                    return new RobocopyRunResult
                    {
                        Result = MirrorDeleteGuard.BlockedResult(estimate, now),
                        Output = preview.Output,
                    };
                }
            }

            if (NothingToDo(preview.Result))
            {
                var note = string.Format(CoreLoc.S("Versioning_NoChanges"), prevName);
                progress?.Report(note);
                preview.Result.DryRun = false; // e' l'esito reale del job: "gia' allineato"
                preview.Result.ThreadCapNote = null; // non si e' copiato nulla: l'avviso sui thread sarebbe rumore
                return new RobocopyRunResult { Result = preview.Result, Output = note };
            }
            // Un errore hardware gia' in anteprima: inutile (e dannoso) proseguire.
            if (preview.Result.HardwareError)
            {
                preview.Result.DryRun = false;
                return preview;
            }
        }

        Directory.CreateDirectory(curr);

        if (prevName is not null)
        {
            // Clonazione e rottura-hard-link sono lavoro IO pesante e sincrono: su thread di background,
            // altrimenti su cartelle grandi (migliaia di file) la finestra si congela.
            var prevPath = Path.Combine(dest, prevName);
            // Il confronto per l'unlink va fatto contro l'origine effettivamente copiata
            // (lo snapshot VSS quando presente), non contro job.Source: origini diverse
            // tra unlink e robocopy romperebbero la garanzia di sovrainsieme sicuro.
            var source = sourceOverride ?? job.Source;
            progress?.Report($"[versioning] clono lo snapshot precedente ({prevName}) via hard-link...");
            // Un file non collegabile nel vecchio snapshot (lock, permessi) non deve far fallire il
            // backup: viene saltato qui e ricopiato fresco da robocopy poco dopo. Un errore hardware
            // del disco invece interrompe il clone con DiskHardwareException (gestita da BackupRunner).
            var skipped = await Task.Run(() => HardLinkCloner.Clone(prevPath, curr,
                path => progress?.Report(string.Format(CoreLoc.S("Versioning_SkipFile"), path))),
                ct).ConfigureAwait(false);
            if (skipped > 0)
                progress?.Report(string.Format(CoreLoc.S("Versioning_SkipSummary"), skipped));
            // Pre-passata: rompe l'hard-link dei file cambiati, cosi robocopy li ricrea nuovi
            // senza modificare sul posto i file ancora condivisi col precedente.
            progress?.Report("[versioning] preparo lo snapshot (rompo gli hard-link dei file cambiati)...");
            await Task.Run(() => SnapshotChangedUnlinker.UnlinkChanged(source, curr), ct).ConfigureAwait(false);
        }

        // La passata "forza copia" sovrascrive SUL POSTO: sui file ancora hard-linkati al vecchio
        // snapshot riscriverebbe anche le versioni precedenti. Prima di lasciarla partire si
        // scollegano dal nuovo snapshot i file che ricopiera', cosi' robocopy li crea ex novo.
        var run = await _runner.RunAsync(job, dryRun: false, progress, ct, destinationOverride: curr,
            sourceOverride: sourceOverride,
            beforeForceCopyPass: filters => Task.Run(() => SnapshotChangedUnlinker.UnlinkMatching(curr, filters), ct))
            .ConfigureAwait(false);

        if (run.Result.Success)
        {
            // Nome gia' occupato (due run nello stesso secondo): si avanza di un secondo invece di
            // appiccicare un suffisso casuale, che renderebbe la cartella invisibile alla
            // ritenzione e all'elenco delle versioni.
            var final = Path.Combine(dest, SnapshotName.FreeName(newName,
                n => Directory.Exists(Path.Combine(dest, n))));
            try
            {
                Directory.Move(curr, final);
                ClearReadOnly(final); // evita che Esplora mostri la cartella-data col nome di un desktop.ini interno
                progress?.Report($"[versioning] snapshot creato: {Path.GetFileName(final)}");

                var after = Directory.GetDirectories(dest).Select(Path.GetFileName).OfType<string>();
                foreach (var name in SnapshotPlanner.SnapshotsToDelete(after, job.SnapshotKeepCount, job.SnapshotMaxAgeDays, now))
                {
                    try
                    {
                        FileSystemDelete.DeleteDirectory(Path.Combine(dest, name));
                        progress?.Report($"[versioning] rimosso snapshot vecchio: {name}");
                    }
                    catch (Exception ex) when (!DiskError.IsUnreadable(ex))
                    {
                        progress?.Report($"[versioning] impossibile rimuovere {name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                progress?.Report($"[versioning] backup riuscito ma rinomina snapshot fallita ({ex.Message}); resta {Path.GetFileName(curr)}.");
            }
        }
        else
        {
            progress?.Report($"[versioning] backup non riuscito: snapshot incompleto lasciato come {Path.GetFileName(curr)}.");
        }

        return run;
    }

    /// <summary>Ritenzione per spazio sulle cartelle-data della destinazione: la regola e' condivisa
    /// con il modello per differenza (vedi <see cref="VersionSpaceCleanup"/>), qui le versioni
    /// stanno nella radice della destinazione.</summary>
    private Task FreeUpSpaceAsync(string dest, IProgress<string>? progress, CancellationToken ct)
        => VersionSpaceCleanup.FreeUpSpaceAsync(dest, _settings, _freeSpace, progress, ct);

    /// <summary>
    /// Un job che passa da "copia semplice" a "con versioni" ha gia' un backup completo: i file
    /// stanno sciolti nella destinazione. Ignorarli vorrebbe dire ricopiare tutto in una cartella
    /// datata e lasciare il doppione sciolto a occupare il disco. Qui invece la copia esistente
    /// viene ADOTTATA come prima versione: si spostano le voci sciolte in una cartella datata
    /// (rinomina sullo stesso volume: istantanea, zero copia), e il run che segue clona da li' e
    /// copia solo cio' che e' cambiato. Scatta solo quando NON esiste nessuno snapshot, la
    /// destinazione non e' vuota E il contenuto sciolto e' riconoscibile come copia della sorgente:
    /// ogni voce di primo livello deve avere un omonimo dello stesso tipo nella sorgente (una copia
    /// puo' avere MENO voci della sorgente, per le esclusioni, mai una in piu'). Basta una voce
    /// estranea e non si adotta niente: meglio una prima versione "da zero" che spostare roba
    /// altrui. Le voci nascoste o di sistema (es. "System Volume Information" se la destinazione
    /// e' la radice di un disco) non si toccano. Restituisce il nome dello snapshot adottato, o
    /// null se non c'era nulla da adottare.
    /// </summary>
    private static string? AdoptPlainMirror(string dest, string source, DateTime now, IProgress<string>? progress)
    {
        // Le cartelle di RoboKeep non sono contenuto da adottare: i residui dei run interrotti, la
        // copia della configurazione (che finita in una cartella-data sparirebbe dalla radice del
        // disco, dove serve) e le due cartelle del modello per differenza, se una destinazione ha
        // avuto entrambi i layout. Elenco condiviso con l'adozione per differenza.
        var entries = new DirectoryInfo(dest).EnumerateFileSystemInfos()
            .Where(e => (e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            .Where(e => !MirrorAdoption.IsLayoutEntry(e.Name))
            .ToList();
        if (entries.Count == 0) return null;

        // Riconoscimento: e' davvero una copia della sorgente? Una copia (anche con esclusioni) e'
        // un SOTTOINSIEME della sorgente: ogni file e cartella della destinazione deve esistere
        // nella sorgente allo stesso percorso relativo. Contenuto diverso va bene (e' la versione
        // precedente, proprio quella da adottare); un percorso che nella sorgente non c'e' no.
        // Costa un'enumerazione della destinazione, una volta sola nella vita del job. Se la
        // sorgente non e' leggibile non si puo' dire, quindi non si adotta.
        if (!Directory.Exists(source)) return null;
        var foreign = MirrorAdoption.ForeignPaths(dest, source, entries, max: 3, out var foreignCount);
        if (foreignCount > 0)
        {
            progress?.Report(string.Format(CoreLoc.S("Versioning_NotAdopted"),
                string.Join(", ", foreign), foreignCount));
            return null;
        }

        // Datata con l'ultima scrittura DENTRO la copia (= quando l'ultimo backup semplice l'ha
        // scritta), non con quella della cartella di destinazione, che chiunque puo' toccare
        // (Esplora risorse, un run interrotto). Mai uguale al nome che sta per nascere: la versione
        // adottata deve risultare piu' vecchia della nuova.
        var stamp = entries.Max(e => e.LastWriteTime);
        // I nomi hanno la risoluzione del secondo: se cade nello stesso secondo del nuovo snapshot
        // (o dopo), si arretra di un secondo, altrimenti i due nomi coinciderebbero.
        if (stamp >= now || SnapshotName.For(stamp) == SnapshotName.For(now)) stamp = now.AddSeconds(-1);
        var adoptedName = SnapshotName.For(stamp);
        var adoptedDir = Path.Combine(dest, adoptedName);
        Directory.CreateDirectory(adoptedDir);

        var moved = 0;
        foreach (var e in entries)
        {
            try
            {
                var target = Path.Combine(adoptedDir, e.Name);
                if (e is DirectoryInfo d) d.MoveTo(target); else ((FileInfo)e).MoveTo(target);
                moved++;
            }
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                // Una voce bloccata resta sciolta: la ritenzione la ignora e robocopy /MIR nella
                // nuova versione la ricopia dalla sorgente. Non vale un fallimento.
                progress?.Report($"[versioning] {e.Name} non spostato nella versione adottata: {ex.Message}");
            }
        }
        // Lo spostamento ha appena toccato la cartella: si ripristina la data che documenta l'adozione.
        try { Directory.SetLastWriteTime(adoptedDir, stamp); } catch { /* solo cosmetico */ }

        progress?.Report(string.Format(CoreLoc.S("Versioning_Adopted"), adoptedName, moved));
        return adoptedName;
    }

    /// <summary>true se l'anteprima dice che sorgente e ultimo snapshot sono gia' allineati: nessun
    /// file o cartella da copiare, nessun extra da rimuovere, nessun errore. Si pretendono sia
    /// l'exit code 0 sia i conteggi a zero: due letture indipendenti dello stesso fatto.</summary>
    private static bool NothingToDo(JobResult r) =>
        !r.HardwareError && r.ExitCode == 0
        && r.FilesCopied == 0 && r.DirsCopied == 0
        && r.FilesExtra == 0 && r.DirsExtra == 0
        && r.FilesFailed == 0 && r.DirsFailed == 0;

    // Toglie l'attributo sola-lettura dalla cartella-snapshot. robocopy copia gli attributi della
    // cartella sorgente: se la sorgente e' una cartella "speciale" (es. Desktop) read-only con un
    // desktop.ini, Esplora risorse mostrerebbe la cartella-data col nome/icona del desktop.ini invece
    // del timestamp. Togliendo il read-only sulla cartella-data Esplora ne mostra il nome reale.
    // Il desktop.ini resta tra i file dello snapshot, intatto. Best-effort.
    private static void ClearReadOnly(string dir)
    {
        try
        {
            var di = new DirectoryInfo(dir);
            if ((di.Attributes & FileAttributes.ReadOnly) != 0)
                di.Attributes &= ~FileAttributes.ReadOnly;
        }
        catch { /* best-effort: un attributo non deve far fallire il backup */ }
    }
}
