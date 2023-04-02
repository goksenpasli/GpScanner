using System;
using System.Drawing;

namespace PdfiumViewer
{
    public class PdfMarker : IPdfMarker
    {
        public PdfMarker(int page, RectangleF bounds, Color color)
            : this(page, bounds, color, Color.Transparent, 0)
        {
        }

        public PdfMarker(int page, RectangleF bounds, Color color, Color borderColor, float borderWidth)
        {
            Page = page;
            Bounds = bounds;
            Color = color;
            BorderColor = borderColor;
            BorderWidth = borderWidth;
        }

        public Color BorderColor { get; }

        public float BorderWidth { get; }

        public RectangleF Bounds { get; }

        public Color Color { get; }

        public int Page { get; }

        public void Draw(PdfRenderer renderer, Graphics graphics)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (graphics == null)
            {
                throw new ArgumentNullException(nameof(graphics));
            }

            Rectangle bounds = renderer.BoundsFromPdf(new PdfRectangle(Page, Bounds));

            using (SolidBrush brush = new SolidBrush(Color))
            {
                graphics.FillRectangle(brush, bounds);
            }

            if (BorderWidth > 0)
            {
                using (Pen pen = new Pen(BorderColor, BorderWidth))
                {
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
            }
        }
    }
}