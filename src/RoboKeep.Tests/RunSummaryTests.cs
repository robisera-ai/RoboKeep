using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep.Tests;

/// <summary>Il riassunto di un gruppo di job: una notifica sola al posto di una per job, che
/// Windows accodava a cinque secondi l'una.</summary>
public class RunSummaryTests
{
    private static readonly string[] None = Array.Empty<string>();

    [Theory]
    [InlineData(0, 0, false)]   // tutti saltati: non e' successo niente da notificare
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    public void Worth_OnlyWhenSomethingRan(int ok, int failed, bool expected)
        => Assert.Equal(expected, RunSummary.Worth(ok, failed));

    [Fact]
    public void TitleKey_ChangesWhenSomethingFailed()
    {
        Assert.Equal("Toast_BatchTitleOk", RunSummary.TitleKey(0));
        Assert.Equal("Toast_BatchTitleErr", RunSummary.TitleKey(1));
        // I due testi esistono davvero: Loc restituisce la chiave quando non li trova.
        Assert.NotEqual("Toast_BatchTitleOk", Loc.Instance["Toast_BatchTitleOk"]);
        Assert.NotEqual("Toast_BatchTitleErr", Loc.Instance["Toast_BatchTitleErr"]);
    }

    [Fact]
    public void Body_ListsTheNames_OnePerLine()
    {
        var body = RunSummary.Body(new[] { "Documenti", "Foto" }, None, 0);

        Assert.Equal(new[] { "Documenti", "Foto" }, Lines(body));
    }

    [Fact]
    public void Body_StopsAtThreeNames_ThenSaysHowManyAreLeft()
    {
        var body = RunSummary.Body(new[] { "A", "B", "C", "D", "E" }, None, 0);
        var lines = Lines(body);

        Assert.Equal(4, lines.Length);
        Assert.Equal(new[] { "A", "B", "C" }, lines.Take(3));
        Assert.Contains("2", lines[3]);          // «e altri 2», non «e altri 5»
        Assert.DoesNotContain("D", lines[3]);
    }

    [Fact]
    public void Body_WithFailures_NamesTheFailedOnesAndCountsTheRest()
    {
        var body = RunSummary.Body(new[] { "A", "B", "C", "D", "E" }, new[] { "Foto" }, 0);
        var lines = Lines(body);

        Assert.Equal(2, lines.Length);
        Assert.Contains("Foto", lines[0]);       // il nome che interessa
        Assert.Contains("5", lines[1]);          // i riusciti restano un numero
        Assert.DoesNotContain("A", lines[1]);
    }

    [Fact]
    public void Body_AddsTheSkippedOnly_WhenThereAreSome()
    {
        Assert.Single(Lines(RunSummary.Body(new[] { "A" }, None, 0)));

        var withSkipped = Lines(RunSummary.Body(new[] { "A" }, None, 2));
        Assert.Equal(2, withSkipped.Length);
        Assert.Contains("2", withSkipped[1]);
    }

    [Fact]
    public void Body_FallsBackToCounts_WhenTheNamesAreTooLongForWindows()
    {
        // Oltre il limite di NOTIFYICONDATA.szInfo Windows taglia dove capita: meglio un
        // conteggio onesto che una frase mozzata.
        var huge = new[] { new string('x', 200), new string('y', 200), new string('z', 200) };

        var body = RunSummary.Body(huge, None, 0);

        Assert.True(body.Length <= RunSummary.MaxLength, $"corpo di {body.Length} caratteri");
        Assert.DoesNotContain("xxx", body);
        Assert.Contains("3", body);
    }

    private static string[] Lines(string body) =>
        body.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
