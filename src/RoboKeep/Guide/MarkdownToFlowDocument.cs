using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace RoboKeep.Guide;

/// <summary>
/// Converte il Markdown di un capitolo in un <see cref="FlowDocument"/> nativo, senza dipendenze
/// esterne. Gestisce il sottoinsieme usato dalla guida: titoli, paragrafi, elenchi puntati e
/// numerati, blocchi di codice, citazioni e linee separatrici; il testo in linea passa da
/// <see cref="GuideInline"/>. Non imposta il colore del testo: lo eredita dal tema della finestra.
/// </summary>
public static class MarkdownToFlowDocument
{
    private static readonly Brush CodeBg = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80));
    private static readonly Brush QuoteBar = new SolidColorBrush(Color.FromArgb(0x66, 0x80, 0x80, 0x80));
    private static readonly Brush LinkBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0x9A, 0xFF));
    private static readonly FontFamily Mono = new("Consolas, Cascadia Mono, monospace");

    static MarkdownToFlowDocument()
    {
        CodeBg.Freeze(); QuoteBar.Freeze(); LinkBrush.Freeze();
    }

    public static FlowDocument Build(string markdown)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            // Nessuna altezza di riga fissa: quella naturale (~1,3×) è più compatta e uniforme
            // tra paragrafi ed elenchi. Lo spazio tra i blocchi lo diamo con i margini.
            // Margine destro ampio nel PagePadding (non nel contenitore): restringe il TESTO così
            // finisce prima della barra di scorrimento in sovrapposizione, che altrimenti gli
            // galleggia sopra.
            PagePadding = new Thickness(14, 2, 28, 8),
        };

        var lines = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];

            // Blocco di codice ```...```
            if (line.TrimStart().StartsWith("```", System.StringComparison.Ordinal))
            {
                var code = new System.Text.StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", System.StringComparison.Ordinal))
                {
                    code.AppendLine(lines[i]);
                    i++;
                }
                i++; // salta il ``` di chiusura
                doc.Blocks.Add(CodeBlock(code.ToString().TrimEnd('\n')));
                continue;
            }

            // Riga vuota → separa i blocchi
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            // Linea separatrice
            if (line.Trim() is "---" or "***" or "___")
            {
                doc.Blocks.Add(Rule());
                i++;
                continue;
            }

            // Titoli # / ## / ###
            if (line.StartsWith("### ", System.StringComparison.Ordinal)) { doc.Blocks.Add(Heading(line[4..], 15, 14)); i++; continue; }
            if (line.StartsWith("## ", System.StringComparison.Ordinal)) { doc.Blocks.Add(Heading(line[3..], 18, 18)); i++; continue; }
            if (line.StartsWith("# ", System.StringComparison.Ordinal)) { doc.Blocks.Add(Heading(line[2..], 23, 22)); i++; continue; }

            // Citazione > ...
            if (line.StartsWith("> ", System.StringComparison.Ordinal))
            {
                var quote = new System.Text.StringBuilder();
                while (i < lines.Length && lines[i].StartsWith("> ", System.StringComparison.Ordinal))
                {
                    if (quote.Length > 0) quote.Append(' ');
                    quote.Append(lines[i][2..].Trim());
                    i++;
                }
                doc.Blocks.Add(Quote(quote.ToString()));
                continue;
            }

            // Elenco puntato (- ) o numerato (1. )
            if (IsBullet(line) || IsNumbered(line))
            {
                var numbered = IsNumbered(line);
                var list = new List
                {
                    MarkerStyle = numbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(0, 2, 0, 8),
                    Padding = new Thickness(20, 0, 0, 0),
                };
                while (i < lines.Length && (numbered ? IsNumbered(lines[i]) : IsBullet(lines[i])))
                {
                    var item = new ListItem(Body(ItemText(lines[i]), 0));
                    list.ListItems.Add(item);
                    i++;
                }
                doc.Blocks.Add(list);
                continue;
            }

            // Paragrafo: righe consecutive non vuote unite fino a un blocco o a una riga vuota.
            var para = new System.Text.StringBuilder(line);
            i++;
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])
                   && !IsBlockStart(lines[i]))
            {
                para.Append(' ').Append(lines[i].Trim());
                i++;
            }
            doc.Blocks.Add(Body(para.ToString(), 8));
        }

        return doc;
    }

    private static bool IsBullet(string l) => l.StartsWith("- ", System.StringComparison.Ordinal) || l.StartsWith("* ", System.StringComparison.Ordinal);

    private static bool IsNumbered(string l)
    {
        var t = l.TrimStart();
        var dot = t.IndexOf(". ", System.StringComparison.Ordinal);
        return dot > 0 && int.TryParse(t[..dot], out _);
    }

    private static string ItemText(string l)
    {
        if (IsBullet(l)) return l[2..].Trim();
        var t = l.TrimStart();
        return t[(t.IndexOf(". ", System.StringComparison.Ordinal) + 2)..].Trim();
    }

    private static bool IsBlockStart(string l) =>
        l.StartsWith("#", System.StringComparison.Ordinal) || IsBullet(l) || IsNumbered(l)
        || l.StartsWith("> ", System.StringComparison.Ordinal)
        || l.TrimStart().StartsWith("```", System.StringComparison.Ordinal)
        || l.Trim() is "---" or "***" or "___";

    private static Paragraph Heading(string text, double size, double topMargin)
    {
        var p = new Paragraph { FontSize = size, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, topMargin, 0, 6) };
        AddInlines(p, text);
        return p;
    }

    private static Paragraph Body(string text, double bottomMargin)
    {
        var p = new Paragraph { Margin = new Thickness(0, 0, 0, bottomMargin) };
        AddInlines(p, text);
        return p;
    }

    private static Paragraph CodeBlock(string code)
    {
        var p = new Paragraph
        {
            FontFamily = Mono,
            FontSize = 13,
            Background = CodeBg,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 2, 0, 10),
        };
        p.Inlines.Add(new Run(code));
        return p;
    }

    private static Section Quote(string text)
    {
        var p = new Paragraph { Margin = new Thickness(0), FontStyle = FontStyles.Italic };
        AddInlines(p, text);
        return new Section(p)
        {
            BorderBrush = QuoteBar,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 2, 0, 2),
            Margin = new Thickness(0, 2, 0, 10),
        };
    }

    private static BlockUIContainer Rule() => new(new System.Windows.Controls.Border
    {
        Height = 1,
        Background = QuoteBar,
        Margin = new Thickness(0, 6, 0, 12),
    });

    private static void AddInlines(Paragraph p, string text)
    {
        foreach (var span in GuideInline.Parse(text))
        {
            if (span.Link is not null)
            {
                var link = new Hyperlink(new Run(span.Text)) { NavigateUri = SafeUri(span.Link), Foreground = LinkBrush };
                link.RequestNavigate += OnNavigate;
                p.Inlines.Add(link);
                continue;
            }
            var run = new Run(span.Text);
            switch (span.Style)
            {
                case InlineStyle.Bold: run.FontWeight = FontWeights.SemiBold; break;
                case InlineStyle.Italic: run.FontStyle = FontStyles.Italic; break;
                case InlineStyle.Code: run.FontFamily = Mono; run.Background = CodeBg; break;
            }
            p.Inlines.Add(run);
        }
    }

    private static System.Uri? SafeUri(string url)
    {
        return System.Uri.TryCreate(url, System.UriKind.Absolute, out var u) ? u : null;
    }

    private static void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Apre i link http/https nel browser predefinito; ignora il resto.
        if (e.Uri is { Scheme: "http" or "https" })
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { /* nessun browser: non è un errore da mostrare */ }
        }
        e.Handled = true;
    }
}
