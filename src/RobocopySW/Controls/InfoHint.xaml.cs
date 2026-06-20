using System.Windows;
using System.Windows.Controls;

namespace RobocopySW.Controls;

/// <summary>Piccola icona "i" con tooltip esplicativo, per spiegare le opzioni tecniche.</summary>
public partial class InfoHint : UserControl
{
    public InfoHint() => InitializeComponent();

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(InfoHint),
            new PropertyMetadata("", OnTextChanged));

    /// <summary>Testo della spiegazione mostrato nel tooltip.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var hint = (InfoHint)d;
        var content = new TextBlock
        {
            Text = (string)e.NewValue,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 340,
            LineHeight = 18,
        };
        ToolTipService.SetInitialShowDelay(hint.Icon, 150);
        ToolTipService.SetShowDuration(hint.Icon, 60000);
        hint.Icon.ToolTip = content;
    }
}
