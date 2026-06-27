using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RoboKeep.Infra;

/// <summary>
/// "Fantasma" semitrasparente della riga trascinata, che segue il cursore durante
/// il riordino drag &amp; drop, così si vede chiaramente cosa si sta spostando.
/// </summary>
public sealed class DragAdorner : Adorner
{
    private readonly Rectangle _ghost;
    private double _left;
    private double _top;

    public DragAdorner(UIElement adornedElement, Visual source, Size size) : base(adornedElement)
    {
        var brush = new VisualBrush(source)
        {
            Opacity = 0.75,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
        };
        _ghost = new Rectangle
        {
            Width = size.Width,
            Height = size.Height,
            Fill = brush,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 2,
                Opacity = 0.4,
            },
        };
        IsHitTestVisible = false;
    }

    public void SetPosition(double left, double top)
    {
        _left = left;
        _top = top;
        (Parent as AdornerLayer)?.Update(AdornedElement);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _ghost;

    protected override Size MeasureOverride(Size constraint)
    {
        _ghost.Measure(constraint);
        return _ghost.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _ghost.Arrange(new Rect(_ghost.DesiredSize));
        return finalSize;
    }

    public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
    {
        var group = new GeneralTransformGroup();
        group.Children.Add(base.GetDesiredTransform(transform));
        group.Children.Add(new TranslateTransform(_left, _top));
        return group;
    }
}
