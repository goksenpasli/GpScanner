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
    public class GraphControl : FrameworkElement, INotifyPropertyChanged
    {
        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register("Series", typeof(ObservableCollection<Chart>), typeof(GraphControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        static GraphControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphControl), new FrameworkPropertyMetadata(typeof(GraphControl)));
        }

        public GraphControl()
        {
            Kaydet = new RelayCommand<object>(parameter => SaveFile(RenderVisual(this).ToTiffJpegByteArray(Format.Png)));
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

        public Visibility GraphContentVisibility
        {
            get => graphcontentVisibility;

            set
            {
                if (graphcontentVisibility != value)
                {
                    graphcontentVisibility = value;
                    OnPropertyChanged(nameof(GraphContentVisibility));
                }
            }
        }

        public ICommand Kaydet { get; }

        public Brush LineColor
        {
            get => lineColor;

            set
            {
                if (lineColor != value)
                {
                    lineColor = value;
                    OnPropertyChanged(nameof(LineColor));
                }
            }
        }

        public Visibility LineDotVisibility
        {
            get => lineDotVisibility;

            set
            {
                if (lineDotVisibility != value)
                {
                    lineDotVisibility = value;
                    OnPropertyChanged(nameof(LineDotVisibility));
                }
            }
        }

        public Visibility LineGraphVisibility
        {
            get => lineGraphVisibility;

            set
            {
                if (lineGraphVisibility != value)
                {
                    lineGraphVisibility = value;
                    OnPropertyChanged(nameof(LineGraphVisibility));
                }
            }
        }

        public double LineThickness
        {
            get => lineThickness;

            set
            {
                if (lineThickness != value)
                {
                    lineThickness = value;
                    OnPropertyChanged(nameof(LineThickness));
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

        public Brush TextColor
        {
            get => textColor; set

            {
                if (textColor != value)
                {
                    textColor = value;
                    OnPropertyChanged(nameof(TextColor));
                }
            }
        }

        public Brush ValueColor
        {
            get => valueColor;

            set
            {
                if (valueColor != value)
                {
                    valueColor = value;
                    OnPropertyChanged(nameof(ValueColor));
                }
            }
        }

        public Visibility ValueTextVisibility
        {
            get => valueTextVisibility;

            set
            {
                if (valueTextVisibility != value)
                {
                    valueTextVisibility = value;
                    OnPropertyChanged(nameof(ValueTextVisibility));
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

        private Visibility graphcontentVisibility;

        private Brush lineColor = Brushes.Blue;

        private Visibility lineDotVisibility;

        private Visibility lineGraphVisibility;

        private double lineThickness = 2;

        private Visibility seriesTextVisibility;

        private Brush textColor = Brushes.Black;

        private Brush valueColor = Brushes.Red;

        private Visibility valueTextVisibility;

        private DrawingContext DrawGraph(DrawingContext drawingContext, ObservableCollection<Chart> Series)
        {
            if (Series is not null && Series.Any())
            {
                double max = Series.Max(z => z.ChartValue);
                double thickness = ActualWidth / Series.Count;
                if (ValueTextVisibility == Visibility.Visible)
                {
                    Margin = new Thickness(0, 12, 0, 0);
                }

                StreamGeometry geometry = new();
                using (StreamGeometryContext gc = geometry.Open())
                {
                    gc.BeginFigure(new Point(thickness / 2, ActualHeight - (Series[0].ChartValue / max * ActualHeight)), false, false);
                    for (int i = 1; i <= Series.Count; i++)
                    {
                        DrawingGroup graphdrawinggroup = new();
                        using (DrawingContext graph = graphdrawinggroup.Open())
                        {
                            Chart item = Series[i - 1];
                            Pen pen = new(item.ChartBrush, thickness);
                            Pen linepen = new(LineColor, LineThickness);
                            linepen.Freeze();
                            pen.Freeze();
                            Point point0 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight);
                            Point point1 = new((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight - (item.ChartValue / max * ActualHeight));
                            if (GraphContentVisibility == Visibility.Visible)
                            {
                                graph.DrawLine(pen, point0, point1);
                            }
                            if (SeriesTextVisibility == Visibility.Visible)
                            {
                                FormattedText formattedText = GenerateFormattedText(item, pen);
                                Point textpoint = new(point1.X - (formattedText.WidthIncludingTrailingWhitespace / 2), point1.Y);
                                graph.DrawText(formattedText, textpoint);
                            }
                            if (ValueTextVisibility == Visibility.Visible)
                            {
                                FormattedText formattedValueText = GenerateFormattedValueText(item, pen);
                                Point textpointValue = new(point1.X - (formattedValueText.WidthIncludingTrailingWhitespace / 2), -16);
                                graph.DrawText(formattedValueText, textpointValue);
                            }
                            if (LineGraphVisibility == Visibility.Visible)
                            {
                                gc.LineTo(point1, true, true);
                                graph.DrawGeometry(null, linepen, geometry);
                                if (LineDotVisibility == Visibility.Visible)
                                {
                                    graph.DrawEllipse(null, linepen, point1, linepen.Thickness, linepen.Thickness);
                                }
                            }
                        }
                        graphdrawinggroup.Freeze();
                        geometry.Freeze();
                        drawingContext.DrawDrawing(graphdrawinggroup);
                    }
                }

                return drawingContext;
            }
            return null;
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