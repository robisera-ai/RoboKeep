namespace RoboKeep.Guide;

/// <summary>Stile di un frammento di testo in linea.</summary>
public enum InlineStyle { Normal, Bold, Italic, Code }

/// <summary>Un frammento di testo in linea con il suo stile (e, se è un link, la URL).</summary>
public sealed record InlineSpan(string Text, InlineStyle Style = InlineStyle.Normal, string? Link = null);

/// <summary>
/// Parsing PURO del testo in linea di un capitolo della guida: grassetto <c>**...**</c>,
/// corsivo <c>*...*</c>, codice <c>`...`</c> e link <c>[testo](url)</c>. È un sottoinsieme
/// volutamente ristretto di Markdown — i contenuti della guida li scriviamo noi, quindi non
/// serve un parser completo. Separato dal rendering (che produce tipi WPF) così è testabile.
/// </summary>
public static class GuideInline
{
    public static IReadOnlyList<InlineSpan> Parse(string? line)
    {
        var spans = new List<InlineSpan>();
        var text = line ?? "";
        var buf = new System.Text.StringBuilder();
        var i = 0;

        void FlushNormal()
        {
            if (buf.Length > 0) { spans.Add(new InlineSpan(buf.ToString())); buf.Clear(); }
        }

        while (i < text.Length)
        {
            var c = text[i];

            // Codice `...`: per primo, così ** e * al suo interno restano letterali.
            if (c == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    FlushNormal();
                    spans.Add(new InlineSpan(text.Substring(i + 1, end - i - 1), InlineStyle.Code));
                    i = end + 1;
                    continue;
                }
            }
            // Grassetto **...** prima del corsivo, così ** non viene letto come due *.
            else if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, System.StringComparison.Ordinal);
                if (end > i)
                {
                    FlushNormal();
                    spans.Add(new InlineSpan(text.Substring(i + 2, end - i - 2), InlineStyle.Bold));
                    i = end + 2;
                    continue;
                }
            }
            // Corsivo *...*
            else if (c == '*')
            {
                var end = text.IndexOf('*', i + 1);
                if (end > i)
                {
                    FlushNormal();
                    spans.Add(new InlineSpan(text.Substring(i + 1, end - i - 1), InlineStyle.Italic));
                    i = end + 1;
                    continue;
                }
            }
            // Link [testo](url)
            else if (c == '[')
            {
                var close = text.IndexOf(']', i + 1);
                if (close > i && close + 1 < text.Length && text[close + 1] == '(')
                {
                    var urlEnd = text.IndexOf(')', close + 2);
                    if (urlEnd > close)
                    {
                        FlushNormal();
                        var label = text.Substring(i + 1, close - i - 1);
                        var url = text.Substring(close + 2, urlEnd - close - 2);
                        spans.Add(new InlineSpan(label, InlineStyle.Normal, url));
                        i = urlEnd + 1;
                        continue;
                    }
                }
            }

            // Nessun marcatore riconosciuto (o non chiuso): carattere letterale.
            buf.Append(c);
            i++;
        }

        FlushNormal();
        return spans;
    }
}
