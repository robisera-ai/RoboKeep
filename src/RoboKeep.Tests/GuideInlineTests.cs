using RoboKeep.Guide;

namespace RoboKeep.Tests;

public class GuideInlineTests
{
    [Fact]
    public void PlainText_SingleNormalSpan()
    {
        var s = GuideInline.Parse("solo testo semplice");
        Assert.Single(s);
        Assert.Equal("solo testo semplice", s[0].Text);
        Assert.Equal(InlineStyle.Normal, s[0].Style);
    }

    [Fact]
    public void Bold_Italic_Code_AreRecognized()
    {
        var s = GuideInline.Parse("a **b** c *d* e `f`");
        // "a ", "b"(bold), " c ", "d"(italic), " e ", "f"(code)
        Assert.Collection(s,
            x => { Assert.Equal("a ", x.Text); Assert.Equal(InlineStyle.Normal, x.Style); },
            x => { Assert.Equal("b", x.Text); Assert.Equal(InlineStyle.Bold, x.Style); },
            x => { Assert.Equal(" c ", x.Text); Assert.Equal(InlineStyle.Normal, x.Style); },
            x => { Assert.Equal("d", x.Text); Assert.Equal(InlineStyle.Italic, x.Style); },
            x => { Assert.Equal(" e ", x.Text); Assert.Equal(InlineStyle.Normal, x.Style); },
            x => { Assert.Equal("f", x.Text); Assert.Equal(InlineStyle.Code, x.Style); });
    }

    [Fact]
    public void Link_HasLabelAndUrl()
    {
        var s = GuideInline.Parse("vedi [il sito](https://esempio.it) qui");
        var link = Assert.Single(s, x => x.Link is not null);
        Assert.Equal("il sito", link.Text);
        Assert.Equal("https://esempio.it", link.Link);
    }

    [Fact]
    public void StarsInsideCode_StayLiteral()
    {
        // Gli asterischi dentro il codice non devono diventare corsivo/grassetto.
        var s = GuideInline.Parse("usa `a*b*c` così");
        var code = Assert.Single(s, x => x.Style == InlineStyle.Code);
        Assert.Equal("a*b*c", code.Text);
    }

    [Fact]
    public void UnclosedMarker_StaysLiteral()
    {
        var s = GuideInline.Parse("un * asterisco solo");
        Assert.Single(s);
        Assert.Equal("un * asterisco solo", s[0].Text);
    }

    [Fact]
    public void BoldNotSplitAsTwoItalics()
    {
        var s = GuideInline.Parse("**forte**");
        var span = Assert.Single(s);
        Assert.Equal("forte", span.Text);
        Assert.Equal(InlineStyle.Bold, span.Style);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyOrNull_NoSpans(string? line)
        => Assert.Empty(GuideInline.Parse(line));
}
