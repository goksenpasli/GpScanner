using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfViewer;

public class ShadowedImage : Image
{
    public static readonly DependencyProperty LocationProperty = DependencyProperty.Register(
        "Location",
        typeof(Point),
        typeof(ShadowedImage),
        new PropertyMetadata(new Point(2.5, 2.5)));

    public static readonly DependencyProperty OverlayColorProperty = DependencyProperty.Register(
        "OverlayColor",
        typeof(SolidColorBrush),
        typeof(ShadowedImage),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 255, 0, 0))));

    public static readonly DependencyProperty ShadowColorProperty = DependencyProperty.Register(
        "ShadowColor",
        typeof(SolidColorBrush),
        typeof(ShadowedImage),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(70, 128, 128, 128))));

    public static readonly DependencyProperty ShowOverlayColorProperty = DependencyProperty.Register(
        "ShowOverlayColor",
        typeof(bool),
        typeof(ShadowedImage),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowShadowProperty =
        DependencyProperty.Register("ShowShadow", typeof(bool), typeof(ShadowedImage), new PropertyMetadata(false));

    public ShadowedImage()
    {
        pen.Brush = OverlayColor;
        pen.Freeze();
    }

    public Point Location { get => (Point)GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    public SolidColorBrush OverlayColor
    {
        get => (SolidColorBrush)GetValue(OverlayColorProperty);
        set => SetValue(OverlayColorProperty, value);
    }

    public SolidColorBrush ShadowColor
    {
        get => (SolidColorBrush)GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    public bool ShowOverlayColor
    {
        get => (bool)GetValue(ShowOverlayColorProperty);
        set => SetValue(ShowOverlayColorProperty, value);
    }

    public bool ShowShadow { get => (bool)GetValue(ShowShadowProperty); set => SetValue(ShowShadowProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        if(ShowShadow)
        {
            dc.DrawRectangle(ShadowColor, null, new Rect(Location, new Size(ActualWidth, ActualHeight)));
        }

        base.OnRender(dc);

        if(ShowOverlayColor)
        {
            dc.DrawLine(pen, new Point(ActualWidth, 0), new Point(0, ActualHeight));
            dc.DrawLine(pen, new Point(0, 0), new Point(ActualWidth, ActualHeight));
        }
    }

    private readonly Pen pen = new() { Thickness = 3 };
}