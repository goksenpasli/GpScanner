using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Extensions
{
    public partial class GraphControl : Control, INotifyPropertyChanged
    {
        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register("Series", typeof(ObservableCollection<Chart>), typeof(GraphControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        static GraphControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphControl), new FrameworkPropertyMetadata(typeof(GraphControl)));
        }

        public GraphControl()
        {
            Margin = SeriesTextVisibility == Visibility.Visible ? new Thickness(0, 12, 0, 0) : new Thickness(0);
            PropertyChanged += GraphControl_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Chart> Series
        {
            get => (ObservableCollection<Chart>)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        public Visibility SeriesListVisibility
        {
            get => seriesListVisibility;

            set
            {
                if (seriesListVisibility != value)
                {
                    seriesListVisibility = value;
                    OnPropertyChanged(nameof(SeriesListVisibility));
                }
            }
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

        protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                if (Series?.Any() == true)
                {
                    DrawGraph(drawingContext, Series);
                }
            }
            else
            {
                MockData = new();
                MockData.Add(new Chart() { ChartBrush = Brushes.Blue, ChartValue = 100, Description = "Sample Item 1" });
                MockData.Add(new Chart() { ChartBrush = Brushes.Red, ChartValue = 40, Description = "Sample Item 2" });
                MockData.Add(new Chart() { ChartBrush = Brushes.Yellow, ChartValue = 60, Description = "Sample Item 3" });
                DrawGraph(drawingContext, MockData);
            }
        }

        private static ObservableCollection<Chart> MockData;

        private Visibility seriesListVisibility = Visibility.Collapsed;

        private Visibility seriesTextVisibility;

        private DrawingContext DrawGraph(DrawingContext drawingContext, ObservableCollection<Chart> Series)
        {
            double max = Series.Max(z => z.ChartValue);
            Pen pen = null;
            DrawingGroup dg = null;

            for (int i = 1; i <= Series.Count; i++)
            {
                Chart item = Series[i - 1];
                pen = new Pen(item.ChartBrush, ActualWidth / Series.Count);
                pen.Freeze();
                dg = new();
                using (DrawingContext graph = dg.Open())
                {
                    Point point0 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight);
                    Point point1 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight - (item.ChartValue / max * ActualHeight));
                    graph.DrawLine(pen, point0, point1);
                    if (SeriesTextVisibility == Visibility.Visible)
                    {
                        FormattedText formattedText = new(item.Description, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black)
                        {
                            MaxTextWidth = pen.Thickness
                        };
                        graph.DrawText(formattedText, new Point(point1.X - (pen.Thickness / 2), point1.Y - 16));
                    }
                    drawingContext.DrawDrawing(dg);
                }
                dg.Freeze();
            }
            return drawingContext;
        }

        private void GraphControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeriesTextVisibility")
            {
                Margin = SeriesTextVisibility == Visibility.Visible ? new Thickness(0, 12, 0, 0) : new Thickness(0);
            }
        }
    }
}