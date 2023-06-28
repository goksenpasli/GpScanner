using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Extensions;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.IO;
using TwainControl.Properties;

namespace TwainControl;

/// <summary>
/// Interaction logic for PdfImportViewerControl.xaml
/// </summary>
public partial class PdfImportViewerControl : UserControl, INotifyPropertyChanged
{
    public PdfImportViewerControl()
    {
        InitializeComponent();
        LoadDrawImage = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    OpenFileDialog openFileDialog = new()
                    {
                        Filter =
                            "Resim Dosyası (*.pdf;*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle)|*.pdf;*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle",
                        Multiselect = false
                    };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        DrawnImage = XImage.FromFile(openFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            },
            parameter => true);

        ReadAnnotation = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (File.Exists(PdfViewer.PdfFilePath) && DataContext is TwainCtrl twainCtrl)
                    {
                        using PdfDocument reader = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.ReadOnly);
                        PdfPage page = reader.Pages[PdfViewer.Sayfa - 1];
                        twainCtrl.Annotations = page?.Annotations;
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            },
            parameter => true);

        RemoveAnnotation = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is PdfAnnotation selectedannotation && File.Exists(PdfViewer.PdfFilePath) && DataContext is TwainCtrl twainCtrl)
                    {
                        using PdfDocument reader = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify);
                        PdfPage page = reader.Pages[PdfViewer.Sayfa - 1];
                        PdfAnnotation annotation = page.Annotations.ToList().OfType<PdfAnnotation>().FirstOrDefault(z => z.Contents == selectedannotation.Contents);
                        page?.Annotations?.Remove(annotation);
                        twainCtrl?.Annotations?.Remove(selectedannotation);
                        reader.Save(PdfViewer.PdfFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            },
            parameter => true);

        OpenPdfHistoryFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is string filepath)
                {
                    if (File.Exists(filepath))
                    {
                        PdfViewer.PdfFilePath = filepath;
                    }
                    else
                    {
                        Settings.Default.PdfLoadHistory.Remove(filepath);
                        Settings.Default.Save();
                        Settings.Default.Reload();
                    }
                }
            },
            parameter => true);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string AnnotationText {
        get => annotationText;

        set {
            if (annotationText != value)
            {
                annotationText = value;
                OnPropertyChanged(nameof(AnnotationText));
            }
        }
    }

    public bool DrawAnnotation {
        get => drawAnnotation;

        set {
            if (drawAnnotation != value)
            {
                drawAnnotation = value;
                OnPropertyChanged(nameof(DrawAnnotation));
            }
        }
    }

    public bool DrawEllipse {
        get => drawEllipse;

        set {
            if (drawEllipse != value)
            {
                drawEllipse = value;
                OnPropertyChanged(nameof(DrawEllipse));
            }
        }
    }

    public bool DrawImage {
        get => drawImage;

        set {
            if (drawImage != value)
            {
                drawImage = value;
                OnPropertyChanged(nameof(DrawImage));
            }
        }
    }

    public bool DrawLine {
        get => drawLine;

        set {
            if (drawLine != value)
            {
                drawLine = value;
                OnPropertyChanged(nameof(DrawLine));
            }
        }
    }

    public XImage DrawnImage {
        get => drawnImage;

        set {
            if (drawnImage != value)
            {
                drawnImage = value;
                OnPropertyChanged(nameof(DrawnImage));
            }
        }
    }

    public bool DrawRect {
        get => drawRect;

        set {
            if (drawRect != value)
            {
                drawRect = value;
                OnPropertyChanged(nameof(DrawRect));
            }
        }
    }

    public bool DrawReverseLine {
        get => drawReverseLine;

        set {
            if (drawReverseLine != value)
            {
                drawReverseLine = value;
                OnPropertyChanged(nameof(DrawReverseLine));
            }
        }
    }

    public bool DrawRoundedRect {
        get => drawRoundedRect;

        set {
            if (drawRoundedRect != value)
            {
                drawRoundedRect = value;
                OnPropertyChanged(nameof(DrawRoundedRect));
            }
        }
    }

    public bool DrawString {
        get => drawString;

        set {
            if (drawString != value)
            {
                drawString = value;
                OnPropertyChanged(nameof(DrawString));
            }
        }
    }

    public XKnownColor GraphObjectColor {
        get => graphObjectColor;

        set {
            if (graphObjectColor != value)
            {
                graphObjectColor = value;
                OnPropertyChanged(nameof(GraphObjectColor));
            }
        }
    }

    public XKnownColor GraphObjectFillColor {
        get => graphObjectFillColor;

        set {
            if (graphObjectFillColor != value)
            {
                graphObjectFillColor = value;
                OnPropertyChanged(nameof(GraphObjectFillColor));
            }
        }
    }

    public RelayCommand<object> LoadDrawImage { get; }

    public RelayCommand<object> OpenPdfHistoryFile { get; }

    public XDashStyle PenDash {
        get => penDash;

        set {
            if (penDash != value)
            {
                penDash = value;
                OnPropertyChanged(nameof(PenDash));
            }
        }
    }

    public XLineCap PenLineCap {
        get => penLineCap;

        set {
            if (penLineCap != value)
            {
                penLineCap = value;
                OnPropertyChanged(nameof(PenLineCap));
            }
        }
    }

    public XLineJoin PenLineJoin {
        get => penLineJoin;

        set {
            if (penLineJoin != value)
            {
                penLineJoin = value;
                OnPropertyChanged(nameof(PenLineJoin));
            }
        }
    }

    public double PenWidth {
        get => penWidth;

        set {
            if (penWidth != value)
            {
                penWidth = value;
                OnPropertyChanged(nameof(PenWidth));
            }
        }
    }

    public RelayCommand<object> ReadAnnotation { get; }

    public RelayCommand<object> RemoveAnnotation { get; }

    public string Text {
        get => text;

        set {
            if (text != value)
            {
                text = value;
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    public double TextSize {
        get => textSize;

        set {
            if (textSize != value)
            {
                textSize = value;
                OnPropertyChanged(nameof(TextSize));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void PdfImportViewerControl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && e.LeftButton == MouseButtonState.Pressed)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                isMouseDown = true;
                mousedowncoord = e.GetPosition(scrollviewer);
            }

            if (Keyboard.IsKeyDown(Key.LeftShift) && (DrawAnnotation || DrawString || DrawImage || DrawEllipse || DrawRect || DrawLine || DrawReverseLine || DrawRoundedRect))
            {
                isDrawMouseDown = true;
                mousedowncoord = e.GetPosition(scrollviewer);
            }
        }
    }

    private void PdfImportViewerControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && DataContext is TwainCtrl twainctrl)
        {
            Point mousemovecoord = e.GetPosition(scrollviewer);
            double x1 = Math.Min(mousedowncoord.X, mousemovecoord.X);
            double x2 = Math.Max(mousedowncoord.X, mousemovecoord.X);
            double y1 = Math.Min(mousedowncoord.Y, mousemovecoord.Y);
            double y2 = Math.Max(mousedowncoord.Y, mousemovecoord.Y);

            if (isDrawMouseDown)
            {
                if (DrawRect || DrawImage || DrawRoundedRect || DrawAnnotation || DrawString)
                {
                    if (!cnv.Children.Contains(rectangleselectionbox))
                    {
                        _ = cnv.Children.Add(rectangleselectionbox);
                    }
                    rectangleselectionbox.StrokeThickness = PenWidth * TwainCtrl.Inch;
                    Canvas.SetLeft(rectangleselectionbox, x1);
                    Canvas.SetTop(rectangleselectionbox, y1);
                    rectangleselectionbox.Width = x2 - x1;
                    rectangleselectionbox.Height = y2 - y1;
                }
                if (DrawLine)
                {
                    if (!cnv.Children.Contains(linebox))
                    {
                        _ = cnv.Children.Add(linebox);
                    }
                    linebox.StrokeThickness = PenWidth * TwainCtrl.Inch;
                    linebox.X1 = x1;
                    linebox.Y1 = y1;
                    linebox.X2 = x2;
                    linebox.Y2 = y2;
                }
                if (DrawReverseLine)
                {
                    if (!cnv.Children.Contains(reverselinebox))
                    {
                        _ = cnv.Children.Add(reverselinebox);
                    }
                    reverselinebox.StrokeThickness = PenWidth * TwainCtrl.Inch;
                    reverselinebox.X1 = x2;
                    reverselinebox.Y1 = y1;
                    reverselinebox.X2 = x1;
                    reverselinebox.Y2 = y2;
                }
                if (DrawEllipse)
                {
                    if (!cnv.Children.Contains(ellipseselectionbox))
                    {
                        _ = cnv.Children.Add(ellipseselectionbox);
                    }
                    ellipseselectionbox.StrokeThickness = PenWidth * TwainCtrl.Inch;
                    Canvas.SetLeft(ellipseselectionbox, x1);
                    Canvas.SetTop(ellipseselectionbox, y1);
                    ellipseselectionbox.Width = x2 - x1;
                    ellipseselectionbox.Height = y2 - y1;
                }

                if (e.LeftButton == MouseButtonState.Released)
                {
                    cnv.Children?.Clear();

                    using PdfDocument reader = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify);
                    PdfPage page = reader.Pages[PdfViewer.Sayfa - 1];
                    using XGraphics gfx = XGraphics.FromPdfPage(page);

                    double coordx = 0, coordy = 0;
                    width = Math.Abs(x2 - x1);
                    height = Math.Abs(y2 - y1);
                    coordx = x1 + scrollviewer.HorizontalOffset;
                    coordy = y1 + scrollviewer.VerticalOffset;
                    double widthmultiply = page.Width / (scrollviewer.ExtentWidth < scrollviewer.ViewportWidth ? scrollviewer.ViewportWidth : scrollviewer.ExtentWidth);
                    double heightmultiply = page.Height / (scrollviewer.ExtentHeight < scrollviewer.ViewportHeight ? scrollviewer.ViewportHeight : scrollviewer.ExtentHeight);

                    Rect rect = page.Orientation == PageOrientation.Portrait
                        ? new(coordx * widthmultiply, coordy * heightmultiply, width * widthmultiply, height * heightmultiply)
                        : new(coordy * widthmultiply, page.Height - (coordx * heightmultiply) - (width * widthmultiply), height * widthmultiply, width * heightmultiply);

                    XPen pen = new(XColor.FromKnownColor(GraphObjectColor)) { DashStyle = PenDash, LineCap = PenLineCap, LineJoin = PenLineJoin, Width = PenWidth };
                    XBrush brush = new XSolidBrush(XColor.FromKnownColor(GraphObjectFillColor));

                    if (DrawRect)
                    {
                        if (GraphObjectFillColor == XKnownColor.Transparent)
                        {
                            gfx.DrawRectangle(pen, rect);
                        }
                        else
                        {
                            gfx.DrawRectangle(pen, brush, rect);
                        }
                    }

                    if (DrawEllipse)
                    {
                        if (GraphObjectFillColor == XKnownColor.Transparent)
                        {
                            gfx.DrawEllipse(pen, rect);
                        }
                        else
                        {
                            gfx.DrawEllipse(pen, brush, rect);
                        }
                    }

                    if (DrawLine)
                    {
                        if (page.Orientation == PageOrientation.Portrait)
                        {
                            gfx.DrawLine(pen, rect.TopLeft, rect.BottomRight);
                        }
                        else
                        {
                            gfx.DrawLine(pen, rect.TopRight, rect.BottomLeft);
                        }
                    }

                    if (DrawReverseLine)
                    {
                        if (page.Orientation == PageOrientation.Portrait)
                        {
                            gfx.DrawLine(pen, rect.TopRight, rect.BottomLeft);
                        }
                        else
                        {
                            gfx.DrawLine(pen, rect.TopLeft, rect.BottomRight);
                        }
                    }

                    if (DrawImage && DrawnImage is not null)
                    {
                        gfx.DrawImage(DrawnImage, rect);
                        DrawnImage = null;
                        GC.Collect();
                    }

                    if (DrawRoundedRect)
                    {
                        if (GraphObjectFillColor == XKnownColor.Transparent)
                        {
                            gfx.DrawRoundedRectangle(pen, rect, new Size(2, 2));
                        }
                        else
                        {
                            gfx.DrawRoundedRectangle(pen, brush, rect, new Size(2, 2));
                        }
                    }

                    if (DrawString && !string.IsNullOrWhiteSpace(Text))
                    {
                        XFont font = new("Times New Roman", TextSize, XFontStyle.Regular);

                        if (GraphObjectFillColor == XKnownColor.Transparent)
                        {
                            if (page.Orientation == PageOrientation.Portrait)
                            {
                                gfx.DrawString(Text, font, XBrushes.Black, rect, XStringFormats.TopLeft);
                            }
                            else
                            {
                                gfx.RotateAtTransform(-90, rect.Location);
                                gfx.DrawString(Text, font, XBrushes.Black, rect, XStringFormats.TopLeft);
                            }
                        }
                        else
                        {
                            if (page.Orientation == PageOrientation.Portrait)
                            {
                                gfx.DrawString(Text, font, brush, rect, XStringFormats.TopLeft);
                            }
                            else
                            {
                                gfx.RotateAtTransform(-90, rect.Location);
                                gfx.DrawString(Text, font, brush, rect, XStringFormats.TopLeft);
                            }
                        }
                    }

                    if (DrawAnnotation && !string.IsNullOrWhiteSpace(AnnotationText))
                    {
                        PdfTextAnnotation pdftextannotaiton = new() { Contents = AnnotationText, Icon = PdfTextAnnotationIcon.Note };
                        XRect annotrect = gfx.Transformer.WorldToDefaultPage(rect);
                        pdftextannotaiton.Rectangle = new PdfRectangle(annotrect);
                        page.Annotations.Add(pdftextannotaiton);
                    }

                    string oldpdfpath = PdfViewer.PdfFilePath;
                    reader.Save(PdfViewer.PdfFilePath);
                    PdfViewer.PdfFilePath = null;
                    PdfViewer.PdfFilePath = oldpdfpath;
                    mousedowncoord.X = mousedowncoord.Y = 0;
                    isDrawMouseDown = false;
                    Cursor = Cursors.Arrow;
                }
            }

            if (isMouseDown)
            {
                if (!cnv.Children.Contains(rectangleselectionbox))
                {
                    _ = cnv.Children.Add(rectangleselectionbox);
                }
                Canvas.SetLeft(rectangleselectionbox, x1);
                Canvas.SetTop(rectangleselectionbox, y1);
                rectangleselectionbox.Width = x2 - x1;
                rectangleselectionbox.Height = y2 - y1;

                if (e.LeftButton == MouseButtonState.Released)
                {
                    cnv.Children?.Clear();
                    width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                    height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);
                    double captureX, captureY;
                    captureX = mousedowncoord.X < mousemovecoord.X ? mousedowncoord.X : mousemovecoord.X;
                    captureY = mousedowncoord.Y < mousemovecoord.Y ? mousedowncoord.Y : mousemovecoord.Y;
                    BitmapFrame bitmapFrame = BitmapFrame.Create((BitmapSource)img.Source);
                    bitmapFrame.Freeze();
                    twainctrl.ImgData = BitmapMethods.CaptureScreen(captureX, captureY, width, height, scrollviewer, bitmapFrame);
                    mousedowncoord.X = mousedowncoord.Y = 0;
                    isMouseDown = false;
                    Cursor = Cursors.Arrow;
                }
            }
        }
    }

    private void PdfViewer_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            Cursor = Cursors.Cross;
        }
    }

    private void PdfViewer_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        Cursor = Cursors.Arrow;
    }

    private readonly Ellipse ellipseselectionbox = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
        Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
        StrokeDashArray = new DoubleCollection(new double[] { 1 })
    };

    private readonly Line linebox = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
        Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
        StrokeDashArray = new DoubleCollection(new double[] { 1 })
    };

    private readonly Rectangle rectangleselectionbox = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
        Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
        StrokeDashArray = new DoubleCollection(new double[] { 1 })
    };

    private readonly Line reverselinebox = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
        Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
        StrokeDashArray = new DoubleCollection(new double[] { 1 })
    };

    private string annotationText = string.Empty;

    private bool drawAnnotation;

    private bool drawEllipse;

    private bool drawImage;

    private bool drawLine;

    private XImage drawnImage;

    private bool drawRect;

    private bool drawReverseLine;

    private bool drawRoundedRect;

    private bool drawString;

    private XKnownColor graphObjectColor = XKnownColor.Black;

    private XKnownColor graphObjectFillColor = XKnownColor.Transparent;

    private double height;

    private bool isDrawMouseDown;

    private bool isMouseDown;

    private Point mousedowncoord;

    private XDashStyle penDash = XDashStyle.Solid;

    private XLineCap penLineCap = XLineCap.Flat;

    private XLineJoin penLineJoin = XLineJoin.Miter;

    private double penWidth = 0.5d;

    private string text = string.Empty;

    private double textSize = 12d;

    private double width;
}