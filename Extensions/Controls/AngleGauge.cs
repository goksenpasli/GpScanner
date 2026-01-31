using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Extensions;

public class AngleGauge : Control
{
    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(nameof(Angle), typeof(double), typeof(AngleGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceAngleValue));

    static AngleGauge() { DefaultStyleKeyProperty.OverrideMetadata(typeof(AngleGauge), new FrameworkPropertyMetadata(typeof(AngleGauge))); }

    public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double size = Math.Min(ActualWidth, ActualHeight);
        double radius = size * 0.45;

        Point center = new(ActualWidth / 2, ActualHeight * 0.95);

        PathFigure fig = new() { StartPoint = PointOnCircle(center, radius, 180), IsClosed = false };

        ArcSegment arc = new() { Size = new Size(radius, radius), Point = PointOnCircle(center, radius, 0), SweepDirection = SweepDirection.Clockwise, IsLargeArc = false };

        fig.Segments.Add(arc);
        PathGeometry geo = new([ fig ]);

        dc.DrawGeometry(null, new Pen(Brushes.Gray, 5), geo);

        for (int i = 0; i <= 180; i += 10)
        {
            double innerR = radius * 0.88;
            double outerR = radius;

            bool bigpoints = i % 30 == 0;
            if (bigpoints)
            {
                outerR = radius * 0.75;
            }

            Pen pen = new(Brushes.DarkGreen, bigpoints ? 2 : 1);
            dc.DrawLine(pen, PointOnCircle(center, innerR, i), PointOnCircle(center, outerR, i));

            if (bigpoints)
            {
                string text = $"{i}°";
                FormattedText ft = new(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size * 0.06, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);

                Point pos = PointOnCircle(center, radius * 0.63, i);
                pos.Offset(-ft.Width / 2, -ft.Height / 2);
                dc.PushOpacity(0.85);
                dc.PushTransform(new RotateTransform(-i + 90, pos.X + (ft.Width / 2), pos.Y + (ft.Height / 2)));
                dc.DrawText(ft, pos);
                dc.Pop();
                dc.Pop();
            }
        }

        double ang = Angle;
        Point needleEnd = PointOnCircle(center, radius * 0.9, ang);

        Pen needlePen = new(Brushes.Red, 3) { EndLineCap = PenLineCap.Triangle };

        dc.DrawLine(needlePen, center, needleEnd);

        dc.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 2), center, radius * 0.08, radius * 0.08);

        string big = $"{Angle:0}°";
        FormattedText bigFt = new(big, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), size * 0.10, Brushes.DarkBlue, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(bigFt, new Point(center.X - (bigFt.Width / 2), center.Y - (radius * 0.45)));
    }

    private static object CoerceAngleValue(DependencyObject d, object v)
    {
        double val = (double)v;
        return Math.Max(0, Math.Min(180, val));
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        double rad = degrees * Math.PI / 180.0;
        return new Point(center.X + (radius * Math.Cos(rad)), center.Y - (radius * Math.Sin(rad)));
    }
}
