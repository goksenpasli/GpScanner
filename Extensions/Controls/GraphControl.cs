using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Extensions
{
    public class GraphControl : FrameworkElement, INotifyPropertyChanged
    {
        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register("Series", typeof(ObservableCollection<Chart>), typeof(GraphControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        static GraphControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphControl), new FrameworkPropertyMetadata(typeof(GraphControl)));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double FontSize
        {
            get => fontSize;

            set
            {
                if (fontSize != value)
                {
                    fontSize = value;
                    OnPropertyChanged(nameof(FontSize));
                }
            }
        }

        public ObservableCollection<Chart> Series
        {
            get => (ObservableCollection<Chart>)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        public Visibility SeriesTextVisibility
        {
            get => seriesTextVisibility;

            set
            {
                if (seriesTextVisibility != value)
                {
                    seriesTextVisibility = value;
                    OnPropertyChanged(nameof(SeriesTextVisibility));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _ = DrawGraph(drawingContext, Series);
            }
            else
            {
                MockData = new()
                {
                    new Chart() { ChartBrush = Brushes.Blue, ChartValue = 100, Description = "Sample Item 1" },
                    new Chart() { ChartBrush = Brushes.Red, ChartValue = 40, Description = "Sample Item 2" },
                    new Chart() { ChartBrush = Brushes.Yellow, ChartValue = 60, Description = "Sample Item 3" }
                };
                _ = DrawGraph(drawingContext, MockData);
            }
        }

        private static ObservableCollection<Chart> MockData;

        private double fontSize = 12;

        private Visibility seriesTextVisibility;

        private DrawingContext DrawGraph(DrawingContext drawingContext, ObservableCollection<Chart> Series)
        {
            if (Series is not null && Series.Any())
            {
                double max = Series.Max(z => z.ChartValue);
                for (int i = 1; i <= Series.Count; i++)
                {
                    DrawingGroup graphdrawinggroup = new();
                    using (DrawingContext graph = graphdrawinggroup.Open())
                    {
                        Chart item = Series[i - 1];
                        Pen pen = new(item.ChartBrush, ActualWidth / Series.Count);
                        pen.Freeze();
                        Point point0 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight);
                        Point point1 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight - (item.ChartValue / max * ActualHeight));

                        graph.DrawLine(pen, point0, point1);
                        if (SeriesTextVisibility == Visibility.Visible)
                        {
                            FormattedText formattedText = GenerateFormattedText(item, pen);
                            Point textpoint = new(point1.X - (formattedText.WidthIncludingTrailingWhitespace / 2), point1.Y);
                            graph.DrawText(formattedText, textpoint);
                        }
                    }
                    graphdrawinggroup.Freeze();
                    drawingContext.DrawDrawing(graphdrawinggroup);
                }
                return drawingContext;
            }
            return null;
        }

        private FormattedText GenerateFormattedText(Chart item, Pen pen)
        {
            return new(item.Description, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), FontSize, Brushes.Black)
            {
                MaxTextWidth = pen.Thickness
            };
        }
    }
}