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

    public SnapshotService(RobocopyRunner runner) => _runner = runner;

    public async Task<RobocopyRunResult> RunVersionedAsync(
        BackupJob job, IProgress<string>? progress = null, CancellationToken ct = default,
        string? sourceOverride = null)
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
        // .inprogress), accumulandole all'infinito. Best-effort: un residuo bloccato non deve far
        // fallire il job. Uso la cancellazione POSIX-safe per non intaccare il read-only degli inode
        // ancora condivisi con lo snapshot precedente.
        foreach (var stale in Directory.GetDirectories(dest)
                     .Where(d => SnapshotName.IsInProgress(Path.GetFileName(d) ?? "")))
        {
            try
            {
                // Su thread di background: cancellare migliaia di hard-link non deve congelare la UI.
                await Task.Run(() => FileSystemDelete.DeleteDirectory(stale), ct).ConfigureAwait(false);
                progress?.Report($"[versioning] rimosso snapshot incompleto di un run interrotto: {Path.GetFileName(stale)}");
            }
            // Best-effort vale per lock e permessi, NON per un errore hardware: quello ferma il job.
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                progress?.Report($"[versioning] residuo {Path.GetFileName(stale)} non rimosso: {ex.Message}");
            }
        }

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
            var final = Path.Combine(dest, newName);
            if (Directory.Exists(final))
                final = Path.Combine(dest, newName + "_" + Guid.NewGuid().ToString("N")[..8]);
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
        var entries = new DirectoryInfo(dest).EnumerateFileSystemInfos()
            .Where(e => (e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            .Where(e => !SnapshotName.IsInProgress(e.Name) && !e.Name.Contains(".deleting-", StringComparison.Ordinal))
            .ToList();
        if (entries.Count == 0) return null;

        // Riconoscimento: e' davvero una copia della sorgente? Una copia (anche con esclusioni) e'
        // un SOTTOINSIEME della sorgente: ogni file e cartella della destinazione deve esistere
        // nella sorgente allo stesso percorso relativo. Contenuto diverso va bene (e' la versione
        // precedente, proprio quella da adottare); un percorso che nella sorgente non c'e' no.
        // Costa un'enumerazione della destinazione, una volta sola nella vita del job. Se la
        // sorgente non e' leggibile non si puo' dire, quindi non si adotta.
        if (!Directory.Exists(source)) return null;
        var foreign = ForeignPaths(dest, source, entries, max: 3, out var foreignCount);
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

    /// <summary>Percorsi relativi presenti sotto <paramref name="entries"/> (in destinazione) ma
    /// assenti nella sorgente. Restituisce i primi <paramref name="max"/> per il messaggio e in
    /// <paramref name="count"/> il totale; si ferma presto se ne trova piu' di quanti servono per
    /// decidere (bastano pochi esempi per dire "non e' una copia").</summary>
    private static List<string> ForeignPaths(string dest, string source, IEnumerable<FileSystemInfo> entries, int max, out int count)
    {
        var examples = new List<string>();
        count = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var entry in entries)
        {
            var toCheck = entry is DirectoryInfo dir
                ? new[] { dir.FullName }.Concat(Directory.EnumerateFileSystemEntries(dir.FullName, "*", options))
                : new[] { entry.FullName };
            foreach (var path in toCheck)
            {
                var rel = Path.GetRelativePath(dest, path);
                var inSource = Path.Combine(source, rel);
                var isDir = Directory.Exists(path);
                if (isDir ? Directory.Exists(inSource) : File.Exists(inSource)) continue;
                count++;
                if (examples.Count < max) examples.Add(rel);
                if (count >= 1000) return examples; // abbastanza: non e' una copia, inutile contare oltre
            }
        }
        return examples;
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
