using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace RobocopySW.Infra;

/// <summary>
/// Linea orizzontale (stile mobile) che indica DOVE cadrà la riga trascinata:
/// disegnata sul bordo superiore o inferiore della riga di destinazione.
/// </summary>
public sealed class InsertionAdorner : Adorner
{
    private readonly bool _below;
    private readonly Pen _pen;

    public InsertionAdorner(UIElement targetRow, bool below) : base(targetRow)
    {
        _below = below;
        IsHitTestVisible = false;

        var brush = Application.Current.TryFindResource("SystemAccentColorBrush") as Brush
                    ?? Brushes.DodgerBlue;
        _pen = new Pen(brush, 2.5);
        if (_pen.CanFreeze) _pen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = AdornedElement.RenderSize.Width;
        var y = _below ? AdornedElement.RenderSize.Height : 0;
        dc.DrawLine(_pen, new Point(0, y), new Point(w, y));
        // pallini alle estremità, per il tocco "moderno"
        dc.DrawEllipse(_pen.Brush, null, new Point(2, y), 3, 3);
        dc.DrawEllipse(_pen.Brush, null, new Point(w - 2, y), 3, 3);
    }
}
