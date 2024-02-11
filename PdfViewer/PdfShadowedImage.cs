using System.Windows;
using System.Windows.Media;

namespace PdfViewer;

public class PdfShadowedImage : ShadowedImage
{
    private readonly Pen pen = new() { Thickness = 1.5 };

    public PdfShadowedImage()
    {
        pen.Brush = ShadowColor;
        pen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(null, pen, new Rect(new Point(1, 1), new Size(ActualWidth, ActualHeight)));
        base.OnRender(dc);
    }
}
