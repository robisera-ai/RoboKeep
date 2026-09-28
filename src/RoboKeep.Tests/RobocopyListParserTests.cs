using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

/// <summary>
/// Lettura dell'elenco che robocopy stampa in anteprima (<c>/L /FP /BYTES</c>). I due blocchi di
/// righe qui sotto sono il formato VERO, colonna per colonna: quello italiano e' stato catturato su
/// questa macchina (Windows in italiano: robocopy TRADUCE le etichette, «Nuovo file»,
/// «Modificato», «*File supplementare», «Nuova directory», «*directory supplementare»), quello
/// inglese e' lo stesso tracciato con le etichette originali. Il lettore deve cavarsela con
/// entrambi, e con una terza lingua che non conosce.
/// </summary>
public class RobocopyListParserTests
{
    private const string ItSource = @"D:\o";
    private const string ItDest = @"D:\d";

    // Catturato da: robocopy D:\o D:\d /MIR /L /FP /BYTES (Windows 11 in italiano).
    // Intestazione, riga delle opzioni e riepilogo sono quelli veri: il lettore li deve scartare.
    private static readonly string[] ItalianOutput =
    {
        "",
        "-------------------------------------------------------------------------------",
        "   ROBOCOPY     ::     Copia di file efficace per Windows                              ",
        "-------------------------------------------------------------------------------",
        "",
        "  Avviato: domenica 27 settembre 2026 15:19:41",
        @"      Origine : D:\o\",
        @" Destinazione : D:\d\",
        "",
        "         File: *.*",
        "\t    ",
        "      Opzioni: *.* /FP /BYTES /L /S /E /DCOPY:DA /COPY:DAT /PURGE /MIR /R:0 /W:0 ",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "\t                   3\t" + @"D:\o\",
        "\t*directory supplementare      -1\t" + @"D:\d\cartella extra\",
        "\t  *File supplementare\t\t       1\t" + @"D:\d\cartella extra\dentro.txt",
        "\t  *File supplementare\t\t       1\t" + @"D:\d\extra però.txt",
        "\t    Modificato\t\t      36\t" + @"D:\o\modificato.txt",
        "\t    Nuovo file\t\t       3\t" + @"D:\o\nuovo.txt",
        "\tNuova directory       1\t" + @"D:\o\nuova cartella\",
        "\t    Nuovo file\t\t       1\t" + @"D:\o\nuova cartella\file.txt",
        "\t                   1\t" + @"D:\o\sub con spazi\",
        "\t    Nuovo file\t\t       1\t" + @"D:\o\sub con spazi\città però.txt",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "              Totale   Copiato  IgnorateNon corrispondentiNon riuscitaSupplementari",
        "Directory:         3         1         2         0         0         1",
        "     File:         5         4         1         0         0         2",
        "     Byte:        47        41         6         0         0         2",
        "   Durata:   0:00:00   0:00:00                       0:00:00   0:00:00",
        "   Terminato: domenica 27 settembre 2026 15:19:41",
    };

    // Stesso tracciato di colonne, etichette inglesi.
    private static readonly string[] EnglishOutput =
    {
        "-------------------------------------------------------------------------------",
        "   ROBOCOPY     ::     Robust File Copy for Windows                              ",
        "-------------------------------------------------------------------------------",
        @"      Source : D:\o\",
        @"        Dest : D:\d\",
        "      Options: *.* /FP /BYTES /L /S /E /DCOPY:DA /COPY:DAT /PURGE /MIR /R:0 /W:0 ",
        "\t                   3\t" + @"D:\o\",
        "\t*EXTRA Dir        -1\t" + @"D:\d\extra folder\",
        "\t  *EXTRA File \t\t       1\t" + @"D:\d\extra folder\inside.txt",
        "\t    Newer  \t\t      36\t" + @"D:\o\changed.txt",
        "\t    Older  \t\t      12\t" + @"D:\o\older.txt",
        "\t    Changed\t\t      12\t" + @"D:\o\touched.txt",
        "\t    Tweaked\t\t      12\t" + @"D:\o\tweaked.txt",
        "\t    New File  \t\t       3\t" + @"D:\o\new.txt",
        "\tNew Dir          1\t" + @"D:\o\new folder\",
        "\t    New File  \t\t       1\t" + @"D:\o\new folder\file.txt",
        "\t    same   \t\t       6\t" + @"D:\o\identical.txt",
        "\t   *Lonely File\t\t       6\t" + @"D:\d\lonely.txt",
        "               Total    Copied   Skipped  Mismatch    FAILED    Extras",
        "    Dirs :         3         1         2         0         0         1",
        "   Files :         5         4         1         0         0         2",
    };

    private static string[] Rel(IEnumerable<ListedChange> c, ChangeKind kind) =>
        c.Where(x => x.Kind == kind).Select(x => x.RelativePath).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    [Fact]
    public void ItalianOutput_EveryLineClassified_DespiteTranslatedTags()
    {
        var changes = RobocopyListParser.Parse(ItalianOutput, ItSource, ItDest);

        Assert.Equal(new[] { "nuova cartella\\file.txt", "nuovo.txt", "sub con spazi\\città però.txt" },
            Rel(changes, ChangeKind.NewFile));
        Assert.Equal(new[] { "modificato.txt" }, Rel(changes, ChangeKind.Overwrite));
        // Il file dentro la cartella extra e' elencato anche lui: il percorso viene dalla radice di
        // DESTINAZIONE, non da quella di sorgente.
        Assert.Equal(new[] { "cartella extra\\dentro.txt", "extra però.txt" }, Rel(changes, ChangeKind.ExtraFile));
        Assert.Equal(new[] { "cartella extra" }, Rel(changes, ChangeKind.ExtraDir));
        Assert.Equal(new[] { "nuova cartella" }, Rel(changes, ChangeKind.NewDir));
        // «sub con spazi» esiste da entrambe le parti: nessuna etichetta, solo il conteggio -> niente.
        Assert.DoesNotContain(changes, c => c.RelativePath == "sub con spazi");
        // La radice stessa non e' un cambiamento, e nessuna riga di intestazione o riepilogo passa.
        Assert.DoesNotContain(changes, c => c.RelativePath.Length == 0);
        Assert.Equal(8, changes.Count);
    }

    [Fact]
    public void EnglishOutput_AllOverwriteFlavours_AreOverwrite()
    {
        var changes = RobocopyListParser.Parse(EnglishOutput, ItSource, ItDest);

        Assert.Equal(new[] { "changed.txt", "older.txt", "touched.txt", "tweaked.txt" },
            Rel(changes, ChangeKind.Overwrite));
        Assert.Equal(new[] { "new folder\\file.txt", "new.txt" }, Rel(changes, ChangeKind.NewFile));
        Assert.Equal(new[] { "extra folder\\inside.txt" }, Rel(changes, ChangeKind.ExtraFile));
        Assert.Equal(new[] { "extra folder" }, Rel(changes, ChangeKind.ExtraDir));
        Assert.Equal(new[] { "new folder" }, Rel(changes, ChangeKind.NewDir));
    }

    [Fact]
    public void SameAndLonelyLines_AreIgnored()
    {
        var changes = RobocopyListParser.Parse(EnglishOutput, ItSource, ItDest);
        Assert.DoesNotContain(changes, c => c.RelativePath == "identical.txt");
        Assert.DoesNotContain(changes, c => c.RelativePath == "lonely.txt");
    }

    [Fact]
    public void UnknownLanguageTag_FallsBackToOverwrite()
    {
        // Una lingua che il lettore non conosce (giapponese): "nuovo" non si riconosce, e mettere da
        // parte un file che era nuovo non fa danno (non esiste in destinazione, quindi non c'e'
        // nulla da spostare); prenderlo per nuovo quando andava sovrascritto perderebbe la versione.
        var lines = new[] { "\t    新しいファイル\t\t       3\t" + @"D:\o\a.txt" };
        var only = Assert.Single(RobocopyListParser.Parse(lines, ItSource, ItDest));
        Assert.Equal(ChangeKind.Overwrite, only.Kind);
    }

    [Fact]
    public void LinesFromOtherRoots_AreIgnored()
    {
        // "D:\organizzazione" non e' sotto "D:\o": il confronto rispetta il confine di cartella.
        var lines = new[] { "\t    Nuovo file\t\t       3\t" + @"D:\organizzazione\a.txt" };
        Assert.Empty(RobocopyListParser.Parse(lines, ItSource, ItDest));
    }

    [Fact]
    public void EmptyInput_NoChanges()
        => Assert.Empty(RobocopyListParser.Parse(Array.Empty<string>(), ItSource, ItDest));
}

/// <summary>
/// La stessa lettura, ma su robocopy VERO: cartelle temporanee, un'anteprima <c>/L /FP /BYTES</c>
/// eseguita dal runner dell'applicazione e l'elenco che ne esce. E' la prova che il formato
/// ipotizzato nei test di sopra e' quello che la macchina stampa davvero.
/// </summary>
public class RobocopyListParserRealRunTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RbcList_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task RealRobocopyPreview_IsParsedIntoTheFourKinds()
    {
        var src = Path.Combine(_root, "src");
        var dst = Path.Combine(_root, "dst");
        Directory.CreateDirectory(Path.Combine(src, "sotto cartella"));
        Directory.CreateDirectory(Path.Combine(dst, "sotto cartella"));
        Directory.CreateDirectory(Path.Combine(dst, "cartella extra"));

        File.WriteAllText(Path.Combine(src, "nuovo.txt"), "uno");
        File.WriteAllText(Path.Combine(src, "modificato.txt"), "contenuto nuovo, piu' lungo di prima");
        File.WriteAllText(Path.Combine(dst, "modificato.txt"), "vecchio");
        File.WriteAllText(Path.Combine(src, "sotto cartella", "città però.txt"), "x");
        File.WriteAllText(Path.Combine(dst, "extra però.txt"), "y");
        File.WriteAllText(Path.Combine(dst, "cartella extra", "dentro.txt"), "z");
        // Identico da entrambe le parti (stesso contenuto e stessa data): non deve comparire.
        File.WriteAllText(Path.Combine(src, "uguale.txt"), "identico");
        File.Copy(Path.Combine(src, "uguale.txt"), Path.Combine(dst, "uguale.txt"));

        var job = new BackupJob
        {
            Name = "L", Source = src, Destination = dst, Mirror = true,
            MultiThread = 0, Retries = 0, Wait = 0,
        };
        var preview = await new RobocopyRunner().RunAsync(job, dryRun: true, listDetails: true);
        var changes = RobocopyListParser.Parse(preview.Output.Split('\n'), src, dst);

        Assert.Contains(new ListedChange(ChangeKind.NewFile, "nuovo.txt"), changes);
        Assert.Contains(new ListedChange(ChangeKind.NewFile, @"sotto cartella\città però.txt"), changes);
        Assert.Contains(new ListedChange(ChangeKind.Overwrite, "modificato.txt"), changes);
        Assert.Contains(new ListedChange(ChangeKind.ExtraFile, "extra però.txt"), changes);
        Assert.Contains(new ListedChange(ChangeKind.ExtraDir, "cartella extra"), changes);
        Assert.Contains(new ListedChange(ChangeKind.ExtraFile, @"cartella extra\dentro.txt"), changes);
        // Il file identico non e' un cambiamento, e nemmeno la cartella che c'e' da entrambe le parti.
        Assert.DoesNotContain(changes, c => c.RelativePath == "uguale.txt");
        Assert.DoesNotContain(changes, c => c.RelativePath == "sotto cartella");
    }

    [Fact]
    public void ArgsBuilder_ListDetails_AddsFullPathAndBytes_OnlyWhenAsked()
    {
        var job = new BackupJob { Name = "L", Source = @"C:\a", Destination = @"C:\b" };
        Assert.DoesNotContain("/FP", RobocopyArgsBuilder.Build(job, dryRun: true));
        var args = RobocopyArgsBuilder.Build(job, dryRun: true, listDetails: true);
        Assert.Contains("/FP", args);
        Assert.Contains("/BYTES", args);
    }
}
