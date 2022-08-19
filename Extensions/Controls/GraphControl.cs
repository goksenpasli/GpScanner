using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using static Extensions.ExtensionMethods;

namespace Extensions
{
    public class GraphControl : FrameworkElement
    {
        // Using a DependencyProperty as the backing store for ContextMenuVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ContextMenuVisibilityProperty =
            DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(GraphControl), new PropertyMetadata(Visibility.Visible));

        // Using a DependencyProperty as the backing store for DotColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DotColorProperty =
            DependencyProperty.Register("DotColor", typeof(Brush), typeof(GraphControl), new FrameworkPropertyMetadata(Brushes.Blue, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for FontSize.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(GraphControl), new FrameworkPropertyMetadata(12.0d, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for GraphContentVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GraphContentVisibilityProperty =
            DependencyProperty.Register("GraphContentVisibility", typeof(Visibility), typeof(GraphControl), new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for IsContextMenuEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsContextMenuEnabledProperty =
            DependencyProperty.Register("IsContextMenuEnabled", typeof(bool), typeof(GraphControl), new PropertyMetadata(true));

        // Using a DependencyProperty as the backing store for LineColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineColorProperty =
            DependencyProperty.Register("LineColor", typeof(Brush), typeof(GraphControl), new FrameworkPropertyMetadata(Brushes.Blue, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for LineDotVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineDotVisibilityProperty =
            DependencyProperty.Register("LineDotVisibility", typeof(Visibility), typeof(GraphControl), new FrameworkPropertyMetadata(Visibility.Collapsed, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for LineGraphVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineGraphVisibilityProperty =
            DependencyProperty.Register("LineGraphVisibility", typeof(Visibility), typeof(GraphControl), new FrameworkPropertyMetadata(Visibility.Collapsed, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for LineThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register("LineThickness", typeof(double), typeof(GraphControl), new FrameworkPropertyMetadata(2.0d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register("Series", typeof(ObservableCollection<Chart>), typeof(GraphControl), new FrameworkPropertyMetadata(new ObservableCollection<Chart>(), FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for SeriesTextVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SeriesTextVisibilityProperty =
            DependencyProperty.Register("SeriesTextVisibility", typeof(Visibility), typeof(GraphControl), new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for TextColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register("TextColor", typeof(Brush), typeof(GraphControl), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for ValueColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueColorProperty =
            DependencyProperty.Register("ValueColor", typeof(Brush), typeof(GraphControl), new FrameworkPropertyMetadata(Brushes.Red, FrameworkPropertyMetadataOptions.AffectsRender));

        // Using a DependencyProperty as the backing store for ValueTextVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueTextVisibilityProperty =
            DependencyProperty.Register("ValueTextVisibility", typeof(Visibility), typeof(GraphControl), new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.AffectsRender));

        static GraphControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphControl), new FrameworkPropertyMetadata(typeof(GraphControl)));
        }

        public GraphControl()
        {
            Kaydet = new RelayCommand<object>(parameter => SaveFile(RenderVisual(this).ToTiffJpegByteArray(Format.Png)));
        }

        public Visibility ContextMenuVisibility
        {
            get { return (Visibility)GetValue(ContextMenuVisibilityProperty); }
            set { SetValue(ContextMenuVisibilityProperty, value); }
        }

        public Brush DotColor
        {
            get => (Brush)GetValue(DotColorProperty);
            set => SetValue(DotColorProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public Visibility GraphContentVisibility
        {
            get => (Visibility)GetValue(GraphContentVisibilityProperty);
            set => SetValue(GraphContentVisibilityProperty, value);
        }

        public bool IsContextMenuEnabled
        {
            get { return (bool)GetValue(IsContextMenuEnabledProperty); }
            set { SetValue(IsContextMenuEnabledProperty, value); }
        }

        public ICommand Kaydet { get; }

        public Brush LineColor
        {
            get => (Brush)GetValue(LineColorProperty);
            set => SetValue(LineColorProperty, value);
        }

        public Visibility LineDotVisibility
        {
            get => (Visibility)GetValue(LineDotVisibilityProperty);
            set => SetValue(LineDotVisibilityProperty, value);
        }

        public Visibility LineGraphVisibility
        {
            get => (Visibility)GetValue(LineGraphVisibilityProperty);
            set => SetValue(LineGraphVisibilityProperty, value);
        }

        public double LineThickness
        {
            get => (double)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        public ObservableCollection<Chart> Series
        {
            get => (ObservableCollection<Chart>)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        public Visibility SeriesTextVisibility
        {
            get => (Visibility)GetValue(SeriesTextVisibilityProperty);
            set => SetValue(SeriesTextVisibilityProperty, value);
        }

        public Brush TextColor
        {
            get => (Brush)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public Brush ValueColor
        {
            get => (Brush)GetValue(ValueColorProperty);
            set => SetValue(ValueColorProperty, value);
        }

        public Visibility ValueTextVisibility
        {
            get => (Visibility)GetValue(ValueTextVisibilityProperty);
            set => SetValue(ValueTextVisibilityProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                DrawGraph(drawingContext, Series);
            }
            else
            {
                MockData = new()
                {
                    new Chart() { ChartBrush = Brushes.Blue, ChartValue = 100, Description = "Sample Item 1" },
                    new Chart() { ChartBrush = Brushes.Red, ChartValue = 40, Description = "Sample Item 2" },
                    new Chart() { ChartBrush = Brushes.Yellow, ChartValue = 60, Description = "Sample Item 3" }
                };
                DrawGraph(drawingContext, MockData);
            }
        }

        private static ObservableCollection<Chart> MockData;

        private void DrawGraph(DrawingContext drawingContext, ObservableCollection<Chart> Series)
        {
            if (Series is not null && Series.Any())
            {
                double max = Series.Max(z => z.ChartValue);
                double thickness = ActualWidth / Series.Count;
                StreamGeometry geometry = new();
                using StreamGeometryContext gc = geometry.Open();
                gc.BeginFigure(new Point(thickness / 2, ActualHeight - (Series[0].ChartValue / max * ActualHeight)), false, false);
                for (int i = 1; i <= Series.Count; i++)
                {
                    DrawingGroup graphdrawinggroup = new();
                    Chart item = Series[i - 1];
                    Pen pen = new(item.ChartBrush, thickness);
                    Pen linepen = new(LineColor, LineThickness);
                    linepen.Freeze();
                    pen.Freeze();
                    Point point0 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight);
                    Point point1 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight - (item.ChartValue / max * ActualHeight * 9 / 10));
                    using DrawingContext graph = graphdrawinggroup.Open();
                    if (GraphContentVisibility == Visibility.Visible)
                    {
                        graph.DrawLine(pen, point0, point1);
                    }
                    if (ValueTextVisibility == Visibility.Visible)
                    {
                        FormattedText formattedValueText = GenerateFormattedValueText(item, pen);
                        Point textpointValue = new(point1.X - (formattedValueText.WidthIncludingTrailingWhitespace / 2), 0);
                        graph.DrawText(formattedValueText, textpointValue);
                    }
                    if (LineGraphVisibility == Visibility.Visible)
                    {
                        gc.LineTo(point1, true, true);
                        graph.DrawGeometry(null, linepen, geometry);
                    }
                    if (LineDotVisibility == Visibility.Visible)
                    {
                        graph.DrawEllipse(DotColor, linepen, point1, linepen.Thickness, linepen.Thickness);
                    }
                    if (SeriesTextVisibility == Visibility.Visible)
                    {
                        FormattedText formattedText = GenerateFormattedText(item, pen);
                        Point textpoint = new(point1.X - (formattedText.WidthIncludingTrailingWhitespace / 2), point1.Y);
                        graph.DrawText(formattedText, textpoint);
                    }
                    drawingContext.DrawDrawing(graphdrawinggroup);
                }
            }
        }

        private FormattedText GenerateFormattedText(Chart item, Pen pen)
        {
            return new(item.Description, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), FontSize, TextColor)
            {
                MaxTextWidth = pen.Thickness
            };
        }

        private FormattedText GenerateFormattedValueText(Chart item, Pen pen)
        {
            return new(item.ChartValue.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Bold"), FontSize, ValueColor)
            {
                MaxTextWidth = pen.Thickness
            };
        }

        private RenderTargetBitmap RenderVisual(FrameworkElement frameworkElement)
        {
            RenderTargetBitmap rtb = new((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96, 96, PixelFormats.Default);
            rtb.Render(frameworkElement);
            rtb.Freeze();
            return rtb;
        }

        private void SaveFile(byte[] imgdata)
        {
            SaveFileDialog saveFileDialog = new()
            {
                FileName = "Resim.png",
                Filter = "Png Dosyası (*.png)|*.png"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllBytes(saveFileDialog.FileName, imgdata);
            }
        }
    }
}