using System.Windows;
using System.Windows.Controls;
using RoboKeep.Guide;
using RoboKeep.Localization;

namespace RoboKeep;

/// <summary>Finestra della guida: elenco capitoli a sinistra, capitolo reso a destra,
/// con navigazione precedente/successivo in fondo.</summary>
public partial class GuideWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly IReadOnlyList<GuideChapter> _chapters;

    public GuideWindow()
    {
        InitializeComponent();

        _chapters = GuideLibrary.Chapters(Loc.Instance.Language);
        foreach (var ch in _chapters)
            ChapterList.Items.Add(new ListBoxItem { Content = ch.Title });

        if (ChapterList.Items.Count > 0)
            ChapterList.SelectedIndex = 0; // mostra subito il primo capitolo
    }

    private void OnChapterSelected(object sender, SelectionChangedEventArgs e)
    {
        var idx = ChapterList.SelectedIndex;
        if (idx < 0 || idx >= _chapters.Count) return;

        var doc = MarkdownToFlowDocument.Build(GuideLibrary.Read(_chapters[idx].FilePath));
        // Il testo di un FlowDocument e' nero di default e NON eredita dal tema: lo leghiamo al
        // colore di testo del tema WPF-UI, cosi' resta leggibile anche in tema scuro.
        doc.SetResourceReference(System.Windows.Documents.FlowDocument.ForegroundProperty,
            "TextFillColorPrimaryBrush");
        ContentView.Document = doc;

        UpdateNavButtons(idx);
    }

    // Le frecce mostrano il titolo del capitolo vicino e spariscono ai due estremi.
    private void UpdateNavButtons(int idx)
    {
        if (idx > 0)
        {
            PrevButton.Visibility = Visibility.Visible;
            PrevButton.Content = $"←  {_chapters[idx - 1].Title}";
        }
        else PrevButton.Visibility = Visibility.Collapsed;

        if (idx < _chapters.Count - 1)
        {
            NextButton.Visibility = Visibility.Visible;
            NextButton.Content = $"{_chapters[idx + 1].Title}  →";
        }
        else NextButton.Visibility = Visibility.Collapsed;
    }

    private void OnPrevChapter(object sender, RoutedEventArgs e)
    {
        if (ChapterList.SelectedIndex > 0) ChapterList.SelectedIndex--;
    }

    private void OnNextChapter(object sender, RoutedEventArgs e)
    {
        if (ChapterList.SelectedIndex < _chapters.Count - 1) ChapterList.SelectedIndex++;
    }
}
