using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Extensions
{
    public enum RectangleStartPosition
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    [DefaultProperty("Content")]
    [ContentProperty("Content")]
    public class RectangleProgressBar : Control
    {

        public static readonly DependencyProperty ContentMarginProperty = DependencyProperty.Register(nameof(ContentMargin), typeof(Thickness), typeof(RectangleProgressBar), new PropertyMetadata(new Thickness(3)));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(RectangleProgressBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RectangleProgressBar), new PropertyMetadata(0.0));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RectangleProgressBar), new PropertyMetadata(100.0));
        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(RectangleProgressBar), new PropertyMetadata(null));
        public static readonly DependencyProperty ProgressThicknessProperty = DependencyProperty.Register(nameof(ProgressThickness), typeof(double), typeof(RectangleProgressBar), new PropertyMetadata(3.0));
        public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(nameof(ProgressBrush), typeof(Brush), typeof(RectangleProgressBar), new PropertyMetadata(Brushes.DodgerBlue));
        public static readonly DependencyProperty StartPositionProperty = DependencyProperty.Register(
            nameof(StartPosition),
            typeof(RectangleStartPosition),
            typeof(RectangleProgressBar),
            new FrameworkPropertyMetadata(RectangleStartPosition.TopLeft, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));
        public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(RectangleProgressBar), new PropertyMetadata(false, OnIndeterminateChanged));
        private double _indeterminateOffset;
        private Path _path;
        private DispatcherTimer _timer;

        static RectangleProgressBar() { DefaultStyleKeyProperty.OverrideMetadata(typeof(RectangleProgressBar), new FrameworkPropertyMetadata(typeof(RectangleProgressBar))); }

        public object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public Thickness ContentMargin { get => (Thickness)GetValue(ContentMarginProperty); set => SetValue(ContentMarginProperty, value); }

        public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

        public Brush ProgressBrush { get => (Brush)GetValue(ProgressBrushProperty); set => SetValue(ProgressBrushProperty, value); }

        public double ProgressThickness { get => (double)GetValue(ProgressThicknessProperty); set => SetValue(ProgressThicknessProperty, value); }

        public RectangleStartPosition StartPosition { get => (RectangleStartPosition)GetValue(StartPositionProperty); set => SetValue(StartPositionProperty, value); }

        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _path = GetTemplateChild("PART_Path") as Path;
            SizeChanged += (_, __) => UpdateProgress();
            UpdateProgress();
        }

        private static void OnIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RectangleProgressBar ctrl)
            {
                ctrl.UpdateIndeterminateState();
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RectangleProgressBar ctrl && !ctrl.IsIndeterminate)
            {
                ctrl.UpdateProgress();
            }
        }

        private static Point Snap(Point p) => new(Math.Round(p.X), Math.Round(p.Y));

        private void OnIndeterminateTick(object sender, EventArgs e)
        {
            _indeterminateOffset += 0.02;
            if (_indeterminateOffset > 1)
            {
                _indeterminateOffset = 0;
            }

            UpdateProgress(indeterminate: true);
        }

        private void UpdateIndeterminateState()
        {
            if (IsIndeterminate)
            {
                _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                _timer.Tick -= OnIndeterminateTick;
                _timer.Tick += OnIndeterminateTick;
                _timer.Start();
            }
            else
            {
                _timer?.Stop();
                UpdateProgress();
            }
        }

        private void UpdateProgress(bool indeterminate = false)
        {
            if (_path == null || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            double w = ActualWidth;
            double h = ActualHeight;

            double perimeter = 2 * (w + h);

            double percent = indeterminate ? 0.25 : (Value - Minimum) / (Maximum - Minimum);

            percent = Math.Max(0, Math.Min(1, percent));

            double drawLength = perimeter * percent;
            double offset = indeterminate ? perimeter * _indeterminateOffset % perimeter : 0;

            Point[] corners = { new(0, 0), new(w, 0), new(w, h), new(0, h) };

            int startIndex = (int)StartPosition;

            StreamGeometry geo = new();
            using (StreamGeometryContext ctx = geo.Open())
            {
                double accumulated = 0;
                Point startPoint = corners[startIndex];
                int currentEdgeIndex = startIndex;

                for (int i = 0; i < 4; i++)
                {
                    int from = (startIndex + i) % 4;
                    int to = (from + 1) % 4;

                    double edgeLength = (corners[to] - corners[from]).Length;

                    if (accumulated + edgeLength >= offset)
                    {
                        double local = offset - accumulated;
                        Vector dir = corners[to] - corners[from];
                        dir.Normalize();

                        startPoint = corners[from] + (dir * local);
                        currentEdgeIndex = from;
                        break;
                    }

                    accumulated += edgeLength;
                }

                startPoint = Snap(startPoint);

                ctx.BeginFigure(startPoint, false, false);

                double remaining = drawLength;
                Point current = startPoint;

                for (int i = 0; i < 4 && remaining > 0; i++)
                {
                    int from = (currentEdgeIndex + i) % 4;
                    int to = (from + 1) % 4;

                    Point p1 = corners[from];
                    Point p2 = corners[to];

                    Vector dir = p2 - p1;
                    double edgeLength = dir.Length;
                    dir.Normalize();

                    double alreadyOnEdge = (current - p1).Length;
                    double available = edgeLength - alreadyOnEdge;

                    double draw = Math.Min(available, remaining);

                    Point next = Snap(current + (dir * draw));

                    ctx.LineTo(next, true, false);

                    current = next;
                    remaining -= draw;
                }
            }

            geo.Freeze();
            _path.Data = geo;
        }
    }
}

