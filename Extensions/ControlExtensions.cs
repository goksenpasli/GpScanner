using System.Windows;
using System.Windows.Media;

namespace Extensions;

public static class ControlExtensions
{
    public static readonly DependencyProperty FlipHorizontallyProperty = DependencyProperty.RegisterAttached("FlipHorizontally", typeof(FlowDirection), typeof(ControlExtensions), new PropertyMetadata(FlowDirection.LeftToRight, OnFlipHorizontallyChanged));

    public static FlowDirection GetFlipHorizontally(this DependencyObject obj) => (FlowDirection)obj.GetValue(FlipHorizontallyProperty);

    public static void SetFlipHorizontally(this DependencyObject obj, FlowDirection value) => obj.SetValue(FlipHorizontallyProperty, value);

    private static void OnFlipHorizontallyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            UpdateRenderTransform(element);
        }
    }

    private static void UpdateRenderTransform(UIElement element)
    {
        FlowDirection flipdirection = GetFlipHorizontally(element);
        if (flipdirection == FlowDirection.RightToLeft)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(-1, 1, element.RenderTransformOrigin.X, element.RenderTransformOrigin.Y);
        }
        else
        {
            element.RenderTransform = Transform.Identity;
        }
    }
}
