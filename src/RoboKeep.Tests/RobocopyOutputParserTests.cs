using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class RobocopyOutputParserTests
{
    // Riepilogo robocopy in inglese.
    private const string EnglishSummary = """
        ------------------------------------------------------------------------------

                       Total    Copied   Skipped  Mismatch    FAILED    Extras
            Dirs :        10         2         8         0         0         1
           Files :        50         5        44         0         1         3
           Bytes :   1.5 m     500 k     1.0 m         0         0     200 k
           Times :   0:00:01   0:00:00                       0:00:00   0:00:00

           Speed :            1234567 Bytes/sec.
        """;

    // Riepilogo robocopy in italiano (etichette localizzate, stesse colonne).
    private const string ItalianSummary = """
        ------------------------------------------------------------------------------

                      Totale   Copiati  Ignorati  Mancata   FAILED     Extra
            Dirs :        10         2         8         0         0         1
            File :        50         5        44         0         1         3
            Byte :   1.5 m     500 k     1.0 m         0         0     200 k
           Tempi :   0:00:01   0:00:00                       0:00:00   0:00:00
        """;

    [Fact]
    public void Parse_English_ExtractsFileCounts()
    {
        var c = RobocopyOutputParser.ParseCounts(EnglishSummary.Split('\n'));
        Assert.Equal(2, c.DirsCopied);
        Assert.Equal(5, c.FilesCopied);
        Assert.Equal(44, c.FilesSkipped);
        Assert.Equal(1, c.FilesFailed);
        Assert.Equal(3, c.FilesExtra);
    }

    [Fact]
    public void Parse_Italian_ExtractsSameCounts_LanguageIndependent()
    {
        var c = RobocopyOutputParser.ParseCounts(ItalianSummary.Split('\n'));
        Assert.Equal(2, c.DirsCopied);
        Assert.Equal(5, c.FilesCopied);
        Assert.Equal(44, c.FilesSkipped);
        Assert.Equal(1, c.FilesFailed);
        Assert.Equal(3, c.FilesExtra);
    }

    [Fact]
    public void Parse_DirsFailed_IsExtracted()
    {
        // Riga Dirs con FAILED = 1 (colonna 5): una cartella non copiata (es. accesso negato).
        var summary = """
            ------------------------------------------------------------------------------

                           Total    Copied   Skipped  Mismatch    FAILED    Extras
                Dirs :      2729      2728         0         0         1         0
               Files :     20486         0     20486         0         0         0
            """;
        var c = RobocopyOutputParser.ParseCounts(summary.Split('\n'));
        Assert.Equal(1, c.DirsFailed);
        Assert.Equal(0, c.FilesFailed);
        Assert.Equal(2728, c.DirsCopied);
    }

    [Fact]
    public void Parse_EmptyOrNoise_ReturnsZeroes()
    {
        var c = RobocopyOutputParser.ParseCounts(new[] { "", "qualcosa", "Inizio: ..." });
        Assert.Equal(0, c.FilesCopied);
        Assert.Equal(0, c.DirsCopied);
    }
}
