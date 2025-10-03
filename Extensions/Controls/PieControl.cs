using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Extensions;

public class PieControl : GraphControl
{
    public static readonly DependencyProperty RandomColorProperty = DependencyProperty.Register("RandomColor", typeof(bool), typeof(PieControl), new PropertyMetadata(true));
    private readonly Random RandomGenerator = new();

    static PieControl()
    {
        IsContextMenuEnabledProperty.OverrideMetadata(typeof(PieControl), new FrameworkPropertyMetadata(false));
        ContextMenuVisibilityProperty.OverrideMetadata(typeof(PieControl), new FrameworkPropertyMetadata(Visibility.Collapsed));
    }

    public bool RandomColor { get => (bool)GetValue(RandomColorProperty); set => SetValue(RandomColorProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()) || Series is null)
        {
            return;
        }

        double total = 0;
        foreach (Chart value in Series)
        {
            total += value.ChartValue;
        }

        double startAngle = 0;
        double centerX = RenderSize.Width / 2;
        double centerY = RenderSize.Height / 2;
        double radius = Math.Min(centerX, centerY);

        for (int i = 0; i < Series.Count; i++)
        {
            double sweepAngle = Series[i].ChartValue / total * 360;
            Brush brush = RandomColor ? GenerateRandomBrush() : Series[i].ChartBrush;
            brush.Freeze();
            DrawPieSlice(drawingContext, brush, centerX, centerY, radius, startAngle, sweepAngle);
            startAngle += sweepAngle;
        }
    }

    private void DrawPieSlice(DrawingContext context, Brush brush, double centerX, double centerY, double radius, double startAngle, double sweepAngle)
    {
        double startRad = startAngle * Math.PI / 180;
        double endRad = (startAngle + sweepAngle) * Math.PI / 180;

        Point startPoint = new(centerX + (radius * Math.Cos(startRad)), centerY + (radius * Math.Sin(startRad)));
        Point endPoint = new(centerX + (radius * Math.Cos(endRad)), centerY + (radius * Math.Sin(endRad)));

        bool isLargeArc = sweepAngle > 180.0;

        PathFigure figure = new() { StartPoint = new Point(centerX, centerY), IsClosed = true };

        figure.Segments.Add(new LineSegment(startPoint, true));
        figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));
        figure.Freeze();
        PathGeometry geometry = new([ figure ]);
        geometry.Freeze();
        context.DrawGeometry(brush, null, geometry);
    }

    private SolidColorBrush GenerateRandomBrush()
    {
        byte r = (byte)RandomGenerator.Next(256);
        byte g = (byte)RandomGenerator.Next(256);
        byte b = (byte)RandomGenerator.Next(256);
        SolidColorBrush brush = new(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}