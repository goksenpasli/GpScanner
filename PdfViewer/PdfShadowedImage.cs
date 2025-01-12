using System.Windows;
using System.Windows.Media;

namespace PdfViewer;

public class PdfShadowedImage : ShadowedImage
{
    private readonly Pen pen = new() { Thickness = 1.5 };

    protected override void OnRender(DrawingContext dc)
    {
        pen.Brush = ShadowColor;
        dc.DrawRectangle(null, pen, new Rect(new Point(1, 1), new Size(ActualWidth, ActualHeight)));
        base.OnRender(dc);
    }
}
