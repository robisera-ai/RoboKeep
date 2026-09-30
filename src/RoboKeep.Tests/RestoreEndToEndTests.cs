using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Il giro completo con robocopy VERO: backup con versioni su cartelle temporanee, poi il
/// ripristino «prima del backup del …» in una cartella nuova. L'albero ricostruito deve essere
/// identico — nomi, contenuti e niente in più — a quello che la sorgente aveva subito prima di quel
/// backup. È la prova che manifest, cartelle-versione e pianificatore raccontano la stessa storia:
/// i test in memoria descrivono le regole, questo verifica che il disco le rispetti.
/// </summary>
public class RestoreEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcRestE2E_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort */ }
    }

    private (string Source, string Dest, BackupJob Job, DifferentialSnapshotService Svc) Setup(string name)
    {
        var source = Path.Combine(_root, name + "-sorgente");
        var dest = Path.Combine(_root, name + "-backup");
        Directory.CreateDirectory(source);
        var job = new BackupJob
        {
            Name = name, Source = source, Destination = dest, Versioned = true, Mirror = true,
            MultiThread = 0, Retries = 0, Wait = 0,
        };
        return (source, dest, job, new DifferentialSnapshotService(new RobocopyRunner()));
    }

    /// <summary>
    /// Lo scenario trovato sul campo: backup 1 di sole aggiunte (il suo manifest viene potato),
    /// backup 2 con modifica + cancellazione + aggiunta, backup 3 senza cambiamenti. Resta UNA sola
    /// cartella-versione, ed è lì l'unica copia del file cancellato: scegliendo il suo punto
    /// («prima del backup 2») deve tornare fuori, senza il file che il backup 2 ha aggiunto.
    /// </summary>
    [Fact]
    public async Task FieldScenario_TheOnlyVersionFolder_IsReachable_AndGivesBackTheDeletedFile()
    {
        var (source, dest, job, svc) = Setup("campo");

        Write(source, "lettera.docx", "originale");
        Write(source, "prezioso.txt", "unica copia dopo la cancellazione");
        Assert.True((await svc.RunAsync(job)).Result.Success);          // 1: sole aggiunte
        var beforeSecond = Snapshot(source);

        await Task.Delay(1100); // il nome delle versioni ha la risoluzione del secondo
        Write(source, "lettera.docx", "modificata");
        File.Delete(Path.Combine(source, "prezioso.txt"));
        Write(source, "nuovo.txt", "aggiunto dal backup 2");
        Assert.True((await svc.RunAsync(job)).Result.Success);          // 2: modifica + cancellazione + aggiunta

        await Task.Delay(1100);
        Assert.True((await svc.RunAsync(job)).Result.Success);          // 3: niente da fare

        var point = Assert.Single(VersionCatalog.List(VersioningLayout.VersionsDir(dest)));
        Assert.True(point.HasFolder);

        var before = await RestoreInto(dest, source, point.Date, "prima-del-2");
        Assert.Equal(beforeSecond, before);
        Assert.Equal("unica copia dopo la cancellazione", before["prezioso.txt"]);
        Assert.Equal("originale", before["lettera.docx"]);
        Assert.False(before.ContainsKey("nuovo.txt"));

        var now = await RestoreInto(dest, source, null, "adesso");
        Assert.Equal(Snapshot(source), now);
        Assert.False(now.ContainsKey("prezioso.txt"));
    }

    [Fact]
    public async Task Differential_EachPoint_RebuildsTheTreeFromBeforeItsBackupExactly()
    {
        var (source, dest, job, svc) = Setup("tre-run");

        // --- backup 1: l'albero di partenza ---
        Write(source, "lettera.docx", "versione di settembre");
        Write(source, @"sotto\nota.txt", "una nota");
        Write(source, @"sotto\accentata èàù.txt", "con accenti");
        Write(source, @"archivio\vecchio.txt", "roba d'archivio");
        Assert.True((await svc.RunAsync(job)).Result.Success);
        var afterFirst = Snapshot(source);

        // --- backup 2: sostituzione, cancellazione, cartella sparita, aggiunta ---
        await Task.Delay(1100);
        Write(source, "lettera.docx", "riscritta a ottobre");
        File.Delete(Path.Combine(source, "sotto", "nota.txt"));
        Directory.Delete(Path.Combine(source, "archivio"), recursive: true);
        Write(source, "nuovo.txt", "aggiunto dal secondo backup");
        Assert.True((await svc.RunAsync(job)).Result.Success);
        var afterSecond = Snapshot(source);

        // --- backup 3: altre modifiche ---
        await Task.Delay(1100);
        Write(source, "lettera.docx", "riscritta a novembre");
        File.Delete(Path.Combine(source, "nuovo.txt"));
        Write(source, "altro.txt", "aggiunto dal terzo backup");
        Assert.True((await svc.RunAsync(job)).Result.Success);

        // Due punti nel tempo: il manifest del primo run (sole aggiunte) è più vecchio della
        // cartella-versione più vecchia e la ritenzione lo pota (VersionCatalog.ManifestsToPrune).
        var points = VersionCatalog.List(VersioningLayout.VersionsDir(dest));
        Assert.Equal(2, points.Count);

        // Prima del backup 2 = com'era dopo il backup 1; prima del backup 3 = dopo il backup 2.
        Assert.Equal(afterFirst, await RestoreInto(dest, source, points[0].Date, "prima-del-2"));
        Assert.Equal(afterSecond, await RestoreInto(dest, source, points[1].Date, "prima-del-3"));
    }

    [Fact]
    public async Task Differential_AFileChangedTwice_EachPointGivesTheContentBeforeItsBackup()
    {
        var (source, dest, job, svc) = Setup("due-modifiche");

        Write(source, "a.txt", "v1");
        Assert.True((await svc.RunAsync(job)).Result.Success);

        await Task.Delay(1100);
        Write(source, "a.txt", "v2");
        Assert.True((await svc.RunAsync(job)).Result.Success);

        await Task.Delay(1100);
        Write(source, "a.txt", "v3");
        Assert.True((await svc.RunAsync(job)).Result.Success);

        var points = VersionCatalog.List(VersioningLayout.VersionsDir(dest));
        Assert.Equal(2, points.Count); // le cartelle-versione dei run 2 e 3

        Assert.Equal("v1", (await RestoreInto(dest, source, points[0].Date, "prima-del-2"))["a.txt"]);
        Assert.Equal("v2", (await RestoreInto(dest, source, points[1].Date, "prima-del-3"))["a.txt"]);
        Assert.Equal("v3", (await RestoreInto(dest, source, null, "adesso"))["a.txt"]);
    }

    [Fact]
    public async Task Differential_RestoringNow_GivesTheCurrentMirror()
    {
        var (source, dest, job, svc) = Setup("adesso");
        Write(source, "a.txt", "uno");
        Write(source, @"sotto\b.txt", "due");
        Assert.True((await svc.RunAsync(job)).Result.Success);

        Assert.Equal(Snapshot(source), await RestoreInto(dest, source, null, "copia-di-adesso"));
    }

    /// <summary>Ripristina l'intero piano di quel punto nel tempo in una cartella nuova e
    /// restituisce l'albero ottenuto.</summary>
    private async Task<Dictionary<string, string>> RestoreInto(string dest, string source, DateTime? when, string folder)
    {
        var plan = RestorePlanner.Resolve(dest, when);
        var target = Path.Combine(_root, folder);
        var outcome = await RestoreCopier.CopyAsync(plan, target, jobSource: source);
        Assert.Empty(outcome.Failures);
        Assert.False(outcome.Cancelled);
        return Directory.Exists(target) ? Snapshot(target) : new Dictionary<string, string>();
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Percorso relativo → contenuto, per tutto l'albero: il modo più diretto di dire
    /// «questi due alberi sono lo stesso albero».</summary>
    private static Dictionary<string, string> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(root, f), File.ReadAllText,
                StringComparer.OrdinalIgnoreCase);
}
