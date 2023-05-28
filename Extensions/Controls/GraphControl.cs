using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Extensions.ExtensionMethods;

namespace Extensions;

public class GraphControl : FrameworkElement
{
    static GraphControl() { DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphControl), new FrameworkPropertyMetadata(typeof(GraphControl))); }

    public GraphControl() { Kaydet = new RelayCommand<object>(parameter => SaveFile(RenderVisual(this).ToTiffJpegByteArray(Format.Png))); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Visibility ContextMenuVisibility { get => (Visibility)GetValue(ContextMenuVisibilityProperty); set => SetValue(ContextMenuVisibilityProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Brush DotColor { get => (Brush)GetValue(DotColorProperty); set => SetValue(DotColorProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Visibility GraphContentVisibility { get => (Visibility)GetValue(GraphContentVisibilityProperty); set => SetValue(GraphContentVisibilityProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public bool IsContextMenuEnabled { get => (bool)GetValue(IsContextMenuEnabledProperty); set => SetValue(IsContextMenuEnabledProperty, value); }

    public ICommand Kaydet { get; }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Brush LineColor { get => (Brush)GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Visibility LineDotVisibility { get => (Visibility)GetValue(LineDotVisibilityProperty); set => SetValue(LineDotVisibilityProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Visibility LineGraphVisibility { get => (Visibility)GetValue(LineGraphVisibilityProperty); set => SetValue(LineGraphVisibilityProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public double LineThickness { get => (double)GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public ObservableCollection<Chart> Series { get => (ObservableCollection<Chart>)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Visibility SeriesTextVisibility { get => (Visibility)GetValue(SeriesTextVisibilityProperty); set => SetValue(SeriesTextVisibilityProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Brush TextColor { get => (Brush)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Brush ValueColor { get => (Brush)GetValue(ValueColorProperty); set => SetValue(ValueColorProperty, value); }

    [Description("Graph Controls")]
    [Category("Graph")]
    public Visibility ValueTextVisibility { get => (Visibility)GetValue(ValueTextVisibilityProperty); set => SetValue(ValueTextVisibilityProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if(!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            DrawGraph(drawingContext, Series);
            return;
        }

        MockData = new ObservableCollection<Chart>
        {
            new Chart { ChartBrush = Brushes.Blue, ChartValue = 100, Description = "Sample Item 1" },
            new Chart { ChartBrush = Brushes.Red, ChartValue = 40, Description = "Sample Item 2" },
            new Chart { ChartBrush = Brushes.Yellow, ChartValue = 60, Description = "Sample Item 3" }
        };
        DrawGraph(drawingContext, MockData);
    }

    private void DrawGraph(DrawingContext drawingContext, ObservableCollection<Chart> Series)
    {
        if(Series?.Any() == true)
        {
            double max = Series.Max(z => z.ChartValue);
            double thickness = ActualWidth / Series.Count;
            DrawingGroup graphdrawinggroup = null;
            DrawingGroup graphgeometrygroup = null;
            Pen linepen = null;
            Pen pen = null;
            Chart item = null;
            Point point0 = default;
            Point point1 = default;
            StreamGeometry geometry = new();
            using StreamGeometryContext gc = geometry.Open();
            gc.BeginFigure(new Point(thickness / 2, ActualHeight - (Series[0].ChartValue / max * ActualHeight * 9 / 10)), false, false);
            for(int i = 1; i <= Series.Count; i++)
            {
                graphdrawinggroup = new DrawingGroup();
                graphgeometrygroup = new DrawingGroup();
                item = Series[i - 1];
                pen = new Pen(item.ChartBrush, thickness);
                linepen = new Pen(LineColor, LineThickness);
                linepen.Freeze();
                pen.Freeze();
                point0 = new Point((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight);
                point1 = new Point((pen.Thickness * i) - (pen.Thickness / 2), ActualHeight - (item.ChartValue / max * ActualHeight * 9 / 10));
                using DrawingContext graph = graphdrawinggroup.Open();
                using DrawingContext geometrygraph = graphgeometrygroup.Open();
                DrawMainContent(pen, point0, point1, graph);
                DrawValueText(pen, item, point1, graph);
                DrawLineGraph(linepen, point1, geometry, gc, geometrygraph);
                DrawLineDot(linepen, point1, graph);
                DrawSeriesText(pen, item, point1, graph);
                drawingContext.DrawDrawing(graphdrawinggroup);
            }

            drawingContext.DrawDrawing(graphgeometrygroup);
        }
    }

    private void DrawLineDot(Pen linepen, Point point1, DrawingContext graph)
    {
        if(LineDotVisibility == Visibility.Visible)
        {
            graph.DrawEllipse(DotColor, linepen, point1, linepen.Thickness, linepen.Thickness);
        }
    }

    private void DrawLineGraph(Pen linepen, Point point1, StreamGeometry geometry, StreamGeometryContext gc, DrawingContext geometrygraph)
    {
        if(LineGraphVisibility == Visibility.Visible)
        {
            gc.LineTo(point1, true, true);
            geometry.Freeze();
            geometrygraph.DrawGeometry(null, linepen, geometry);
        }
    }

    private void DrawMainContent(Pen pen, Point point0, Point point1, DrawingContext graph)
    {
        if(GraphContentVisibility == Visibility.Visible)
        {
            graph.DrawLine(pen, point0, point1);
        }
    }

    private void DrawSeriesText(Pen pen, Chart item, Point point1, DrawingContext graph)
    {
        if(SeriesTextVisibility == Visibility.Visible)
        {
            FormattedText formattedText = GenerateFormattedText(item, pen);
            Point textpoint = new(point1.X - (formattedText.WidthIncludingTrailingWhitespace / 2), point1.Y - (formattedText.Height / 3));
            graph.DrawText(formattedText, textpoint);
        }
    }

    private void DrawValueText(Pen pen, Chart item, Point point1, DrawingContext graph)
    {
        if(ValueTextVisibility == Visibility.Visible)
        {
            FormattedText formattedValueText = GenerateFormattedValueText(item, pen);
            graph.DrawText(formattedValueText, new Point(point1.X - (formattedValueText.WidthIncludingTrailingWhitespace / 2), 0));
        }
    }

    private FormattedText GenerateFormattedText(Chart item, Pen pen)
    {
        return new FormattedText(item.Description, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), FontSize, TextColor)
        {
            MaxTextWidth = pen.Thickness
        };
    }

    private FormattedText GenerateFormattedValueText(Chart item, Pen pen)
    {
        return new FormattedText(item.ChartValue.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Bold"), FontSize, ValueColor)
        {
            MaxTextWidth = pen.Thickness
        };
    }

    private RenderTargetBitmap RenderVisual(FrameworkElement frameworkElement)
    {
        RenderTargetBitmap rtb = new((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 72, 72, PixelFormats.Default);
        rtb.Render(frameworkElement);
        rtb.Freeze();
        return rtb;
    }

    private void SaveFile(byte[] imgdata)
    {
        SaveFileDialog saveFileDialog = new() { FileName = "Resim.png", Filter = "Png Dosyası (*.png)|*.png" };
        if(saveFileDialog.ShowDialog() == true)
        {
            File.WriteAllBytes(saveFileDialog.FileName, imgdata);
            GC.Collect();
        }
    }

    private static ObservableCollection<Chart> MockData;
    public static readonly DependencyProperty ContextMenuVisibilityProperty =
        DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(GraphControl), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty DotColorProperty = DependencyProperty.Register(
        "DotColor",
        typeof(Brush),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Brushes.Blue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        "FontSize",
        typeof(double),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(12.0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GraphContentVisibilityProperty =
        DependencyProperty.Register(
        "GraphContentVisibility",
        typeof(Visibility),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsContextMenuEnabledProperty =
        DependencyProperty.Register("IsContextMenuEnabled", typeof(bool), typeof(GraphControl), new PropertyMetadata(true));

    public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(
        "LineColor",
        typeof(Brush),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Brushes.Blue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineDotVisibilityProperty =
        DependencyProperty.Register(
        "LineDotVisibility",
        typeof(Visibility),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Visibility.Collapsed, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineGraphVisibilityProperty =
        DependencyProperty.Register(
        "LineGraphVisibility",
        typeof(Visibility),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Visibility.Collapsed, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineThicknessProperty = DependencyProperty.Register(
        "LineThickness",
        typeof(double),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(2.0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        "Series",
        typeof(ObservableCollection<Chart>),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(new ObservableCollection<Chart>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeriesTextVisibilityProperty =
        DependencyProperty.Register(
        "SeriesTextVisibility",
        typeof(Visibility),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextColorProperty = DependencyProperty.Register(
        "TextColor",
        typeof(Brush),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueColorProperty = DependencyProperty.Register(
        "ValueColor",
        typeof(Brush),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Brushes.Red, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueTextVisibilityProperty =
        DependencyProperty.Register(
        "ValueTextVisibility",
        typeof(Visibility),
        typeof(GraphControl),
        new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.AffectsRender));
}