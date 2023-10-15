using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TwainControl;

public class ZoomableInkCanvas : InkCanvas
{
    public static readonly DependencyProperty CurrentZoomProperty =
        DependencyProperty.Register("CurrentZoom", typeof(double), typeof(ZoomableInkCanvas), new PropertyMetadata(1d, Changed));
    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register("MaxZoom", typeof(double), typeof(ZoomableInkCanvas), new PropertyMetadata(3d));
    public static readonly DependencyProperty MinZoomProperty =
        DependencyProperty.Register("MinZoom", typeof(double), typeof(ZoomableInkCanvas), new PropertyMetadata(0.01d));

    public double CurrentZoom { get => (double)GetValue(CurrentZoomProperty); set => SetValue(CurrentZoomProperty, value); }

    public double MaxZoom { get => (double)GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    public double MinZoom { get => (double)GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomableInkCanvas zoomableInkCanvas && zoomableInkCanvas.CurrentZoom >= zoomableInkCanvas.MinZoom && zoomableInkCanvas.CurrentZoom <= zoomableInkCanvas.MaxZoom)
        {
            zoomableInkCanvas.ApplyZoom();
        }
    }

    private void ApplyZoom()
    {
        ScaleTransform scaleTransform = new(CurrentZoom, CurrentZoom);
        LayoutTransform = scaleTransform;
    }
}