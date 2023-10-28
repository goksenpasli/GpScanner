using Extensions;
using Microsoft.Win32;
using Ocr;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Annotations;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TwainControl.Converter;
using TwainControl.Properties;

namespace TwainControl;

/// <summary>
/// Interaction logic for PdfImportViewerControl.xaml
/// </summary>
public partial class PdfImportViewerControl : UserControl, INotifyPropertyChanged
{
    private readonly string AppName = Application.Current?.MainWindow?.Title;
    private readonly Ellipse ellipseselectionbox = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
        Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
        StrokeDashArray = new DoubleCollection(new double[] { 1 }),
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
    private PdfAnnotations annotations;
    private string annotationText = string.Empty;
    private bool applyLandscape = true;
    private bool applyPortrait = true;
    private bool drawAnnotation;
    private bool drawBeziers;
    private bool drawCurve;
    private bool drawEllipse;
    private bool drawImage;
    private bool drawLine;
    private bool drawLines;
    private XImage drawnImage;
    private bool drawPolygon;
    private bool drawRect;
    private bool drawReverseLine;
    private bool drawRoundedRect;
    private bool drawString;
    private XKnownColor graphObjectColor = XKnownColor.Black;
    private XKnownColor graphObjectFillColor = XKnownColor.Transparent;
    private string ınkDrawColor = "Black";
    private BitmapSource ınkSource;
    private bool isDrawMouseDown;
    private bool isMouseDown;
    private Point mousedowncoord;
    private bool ocrDialogOpen;
    private string ocrText;
    private XDashStyle penDash = XDashStyle.Solid;
    private XLineCap penLineCap = XLineCap.Flat;
    private XLineJoin penLineJoin = XLineJoin.Miter;
    private double penWidth = 0.5d;
    private int polygonCount = 3;
    private string selectedInk;
    private bool singlePage = true;
    private string text = string.Empty;
    private double textSize = 12d;

    public PdfImportViewerControl()
    {
        InitializeComponent();
        PropertyChanged += PdfImportViewerControl_PropertyChanged;
        LoadDrawImage = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    OpenFileDialog openFileDialog = new()
                    {
                        Filter = "Resim Dosyası (*.pdf;*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle)|*.pdf;*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle",
                        Multiselect = false
                    };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        DrawnImage = XImage.FromFile(openFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, AppName);
                }
            },
            parameter => true);

        LoadInkDrawImage = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    RenderTargetBitmap renderTargetBitmap = new((int)Ink.DesiredSize.Width, (int)Ink.DesiredSize.Height, 96, 96, PixelFormats.Default);
                    renderTargetBitmap.Render(Ink);
                    renderTargetBitmap.Freeze();
                    DrawnImage = XImage.FromBitmapSource(renderTargetBitmap);
                    DrawImage = true;
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, AppName);
                }
            },
            parameter => Ink?.Strokes?.Any() == true);

        ClearInkDrawImage = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    Ink?.Strokes?.Clear();
                    DrawImage = false;
                    DrawnImage = null;
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, AppName);
                }
            },
            parameter => Ink?.Strokes?.Any() == true);

        SaveInkDrawImage = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    using MemoryStream ms = new();
                    Ink.Strokes?.Save(ms, true);
                    _ = Settings.Default.InkCollection.Add(Convert.ToBase64String(ms.ToArray()));
                    Settings.Default.Save();
                    Settings.Default.Reload();
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, AppName);
                }
            },
            parameter => Ink?.Strokes?.Any() == true);

        ReadAnnotation = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (File.Exists(PdfViewer.PdfFilePath))
                    {
                        using PdfDocument reader = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.ReadOnly);
                        Annotations = (reader?.Pages[PdfViewer.Sayfa - 1])?.Annotations;
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, AppName);
                }
            },
            parameter => true);

        RemoveAnnotation = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is PdfAnnotation selectedannotation && File.Exists(PdfViewer.PdfFilePath))
                    {
                        using PdfDocument reader = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify);
                        PdfPage page = reader?.Pages[PdfViewer.Sayfa - 1];
                        PdfAnnotation annotation = page.Annotations.ToList().Cast<PdfAnnotation>().FirstOrDefault(z => z.Contents == selectedannotation.Contents);
                        if (annotation is not null)
                        {
                            page?.Annotations?.Remove(annotation);
                            reader.Save(PdfViewer.PdfFilePath);
                            ReadAnnotation.Execute(null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, AppName);
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

        SaveRefreshPdfPage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is PdfDocument pdfDocument && pdfDocument is not null)
                {
                    int currentpage = PdfViewer.Sayfa;
                    string oldpdfpath = PdfViewer.PdfFilePath;
                    pdfDocument.Save(PdfViewer.PdfFilePath);
                    PdfViewer.PdfFilePath = null;
                    PdfViewer.PdfFilePath = oldpdfpath;
                    PdfViewer.Sayfa = currentpage;
                    Thread.Sleep(1000);
                }
            },
            parameter => true);

        ClearLines = new RelayCommand<object>(parameter => Points.Clear(), parameter => Points is not null);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public PdfAnnotations Annotations
    {
        get => annotations;

        set
        {
            if (annotations != value)
            {
                annotations = value;
                OnPropertyChanged(nameof(Annotations));
            }
        }
    }

    public string AnnotationText
    {
        get => annotationText;

        set
        {
            if (annotationText != value)
            {
                annotationText = value;
                OnPropertyChanged(nameof(AnnotationText));
            }
        }
    }

    public bool ApplyLandscape
    {
        get => applyLandscape;
        set
        {
            if (applyLandscape != value)
            {
                applyLandscape = value;
                OnPropertyChanged(nameof(ApplyLandscape));
            }
        }
    }

    public bool ApplyPortrait
    {
        get => applyPortrait;
        set
        {
            if (applyPortrait != value)
            {
                applyPortrait = value;
                OnPropertyChanged(nameof(ApplyPortrait));
            }
        }
    }

    public RelayCommand<object> ClearInkDrawImage { get; }

    public RelayCommand<object> ClearLines { get; }

    public bool DrawAnnotation
    {
        get => drawAnnotation;

        set
        {
            if (drawAnnotation != value)
            {
                drawAnnotation = value;
                OnPropertyChanged(nameof(DrawAnnotation));
            }
        }
    }

    public bool DrawBeziers
    {
        get => drawBeziers;

        set
        {
            if (drawBeziers != value)
            {
                drawBeziers = value;
                OnPropertyChanged(nameof(DrawBeziers));
            }
        }
    }

    public bool DrawCurve
    {
        get => drawCurve;

        set
        {
            if (drawCurve != value)
            {
                drawCurve = value;
                OnPropertyChanged(nameof(DrawCurve));
            }
        }
    }

    public bool DrawEllipse
    {
        get => drawEllipse;

        set
        {
            if (drawEllipse != value)
            {
                drawEllipse = value;
                OnPropertyChanged(nameof(DrawEllipse));
            }
        }
    }

    public bool DrawImage
    {
        get => drawImage;

        set
        {
            if (drawImage != value)
            {
                drawImage = value;
                OnPropertyChanged(nameof(DrawImage));
            }
        }
    }

    public bool DrawLine
    {
        get => drawLine;

        set
        {
            if (drawLine != value)
            {
                drawLine = value;
                OnPropertyChanged(nameof(DrawLine));
            }
        }
    }

    public bool DrawLines
    {
        get => drawLines;

        set
        {
            if (drawLines != value)
            {
                drawLines = value;
                OnPropertyChanged(nameof(DrawLines));
            }
        }
    }

    public XImage DrawnImage
    {
        get => drawnImage;

        set
        {
            if (drawnImage != value)
            {
                drawnImage = value;
                OnPropertyChanged(nameof(DrawnImage));
            }
        }
    }

    public bool DrawPolygon
    {
        get => drawPolygon;

        set
        {
            if (drawPolygon != value)
            {
                drawPolygon = value;
                OnPropertyChanged(nameof(DrawPolygon));
            }
        }
    }

    public bool DrawRect
    {
        get => drawRect;

        set
        {
            if (drawRect != value)
            {
                drawRect = value;
                OnPropertyChanged(nameof(DrawRect));
            }
        }
    }

    public bool DrawReverseLine
    {
        get => drawReverseLine;

        set
        {
            if (drawReverseLine != value)
            {
                drawReverseLine = value;
                OnPropertyChanged(nameof(DrawReverseLine));
            }
        }
    }

    public bool DrawRoundedRect
    {
        get => drawRoundedRect;

        set
        {
            if (drawRoundedRect != value)
            {
                drawRoundedRect = value;
                OnPropertyChanged(nameof(DrawRoundedRect));
            }
        }
    }

    public bool DrawString
    {
        get => drawString;

        set
        {
            if (drawString != value)
            {
                drawString = value;
                OnPropertyChanged(nameof(DrawString));
            }
        }
    }

    public ToolTip EscToolTip { get; private set; }

    public XKnownColor GraphObjectColor
    {
        get => graphObjectColor;

        set
        {
            if (graphObjectColor != value)
            {
                graphObjectColor = value;
                OnPropertyChanged(nameof(GraphObjectColor));
            }
        }
    }

    public XKnownColor GraphObjectFillColor
    {
        get => graphObjectFillColor;

        set
        {
            if (graphObjectFillColor != value)
            {
                graphObjectFillColor = value;
                OnPropertyChanged(nameof(GraphObjectFillColor));
            }
        }
    }

    public string InkDrawColor
    {
        get => ınkDrawColor;

        set
        {
            if (ınkDrawColor != value)
            {
                ınkDrawColor = value;
                OnPropertyChanged(nameof(InkDrawColor));
            }
        }
    }

    public BitmapSource InkSource
    {
        get => ınkSource;

        set
        {
            if (ınkSource != value)
            {
                ınkSource = value;
                OnPropertyChanged(nameof(InkSource));
            }
        }
    }

    public RelayCommand<object> LoadDrawImage { get; }

    public RelayCommand<object> LoadInkDrawImage { get; }

    public bool OcrDialogOpen
    {
        get => ocrDialogOpen;
        set
        {
            if (ocrDialogOpen != value)
            {
                ocrDialogOpen = value;
                OnPropertyChanged(nameof(OcrDialogOpen));
            }
        }
    }

    public string OcrText
    {
        get => ocrText;
        set
        {
            if (ocrText != value)
            {
                ocrText = value;
                OnPropertyChanged(nameof(OcrText));
            }
        }
    }

    public RelayCommand<object> OpenPdfHistoryFile { get; }

    public XDashStyle PenDash
    {
        get => penDash;

        set
        {
            if (penDash != value)
            {
                penDash = value;
                OnPropertyChanged(nameof(PenDash));
            }
        }
    }

    public XLineCap PenLineCap
    {
        get => penLineCap;

        set
        {
            if (penLineCap != value)
            {
                penLineCap = value;
                OnPropertyChanged(nameof(PenLineCap));
            }
        }
    }

    public XLineJoin PenLineJoin
    {
        get => penLineJoin;

        set
        {
            if (penLineJoin != value)
            {
                penLineJoin = value;
                OnPropertyChanged(nameof(PenLineJoin));
            }
        }
    }

    public double PenWidth
    {
        get => penWidth;

        set
        {
            if (penWidth != value)
            {
                penWidth = value;
                OnPropertyChanged(nameof(PenWidth));
            }
        }
    }

    public ObservableCollection<XPoint> Points { get; set; } = [];

    public int PolygonCount
    {
        get => polygonCount;

        set
        {
            if (polygonCount != value)
            {
                polygonCount = value;
                OnPropertyChanged(nameof(PolygonCount));
            }
        }
    }

    public RelayCommand<object> ReadAnnotation { get; }

    public RelayCommand<object> RemoveAnnotation { get; }

    public RelayCommand<object> SaveInkDrawImage { get; }

    public RelayCommand<object> SaveRefreshPdfPage { get; }

    public string SelectedInk
    {
        get => selectedInk;
        set
        {
            if (selectedInk != value)
            {
                selectedInk = value;
                OnPropertyChanged(nameof(SelectedInk));
            }
        }
    }

    public bool SinglePage
    {
        get => singlePage;
        set
        {
            if (singlePage != value)
            {
                singlePage = value;
                OnPropertyChanged(nameof(SinglePage));
            }
        }
    }

    public string Text
    {
        get => text;

        set
        {
            if (text != value)
            {
                text = value;
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    public double TextSize
    {
        get => textSize;

        set
        {
            if (textSize != value)
            {
                textSize = value;
                OnPropertyChanged(nameof(TextSize));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private Rect CalculateRect(ScrollViewer scrollviewer, double x1, double x2, double y1, double y2, PdfPage page)
    {
        double width = Math.Abs(x2 - x1);
        double height = Math.Abs(y2 - y1);
        double coordx = x1 + scrollviewer.HorizontalOffset;
        double coordy = y1 + scrollviewer.VerticalOffset;
        double widthmultiply = page.Width / scrollviewer.ExtentWidth;
        double heightmultiply = page.Height / scrollviewer.ExtentHeight;
        if (scrollviewer.ExtentWidth < scrollviewer.ViewportWidth)
        {
            coordx -= (scrollviewer.ViewportWidth - scrollviewer.ExtentWidth) / 2;
        }
        if (scrollviewer.ExtentHeight < scrollviewer.ViewportHeight)
        {
            coordy -= (scrollviewer.ViewportHeight - scrollviewer.ExtentHeight) / 2;
        }
        return page.Orientation == PageOrientation.Portrait
               ? new Rect(coordx * widthmultiply, coordy * heightmultiply, width * widthmultiply, height * heightmultiply)
               : new Rect(coordy * widthmultiply, page.Height - (coordx * heightmultiply) - (width * widthmultiply), height * widthmultiply, width * heightmultiply);
    }

    private async Task<string> GetOcrData(string tesseractlanguage, byte[] imgdata)
    {
        if (string.IsNullOrWhiteSpace(tesseractlanguage))
        {
            return string.Empty;
        }
        OcrDialogOpen = false;
        OcrDialogOpen = true;
        return string.Join(" ", (await imgdata.OcrAsync(tesseractlanguage))?.Select(z => z.Text));
    }

    private List<PdfPage> GetPdfPagesOrientation(PdfDocument pdfDocument)
    {
        List<PdfPage> pdfpages = null;
        if (ApplyPortrait)
        {
            pdfpages = pdfDocument?.Pages?.Cast<PdfPage>().Where(item => item.Width < item.Height).ToList();
        }
        if (ApplyLandscape)
        {
            pdfpages = pdfDocument?.Pages?.Cast<PdfPage>().Where(item => item.Width > item.Height).ToList();
        }
        if (ApplyLandscape && ApplyPortrait)
        {
            pdfpages = pdfDocument?.Pages?.Cast<PdfPage>().ToList();
        }

        return pdfpages;
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

            if (Keyboard.IsKeyDown(Key.LeftShift) &&
            (DrawLines || DrawBeziers || DrawCurve || DrawPolygon || DrawAnnotation || DrawString || DrawImage || DrawEllipse || DrawRect || DrawLine || DrawReverseLine || DrawRoundedRect))

            {
                isDrawMouseDown = true;
                mousedowncoord = e.GetPosition(scrollviewer);
                if (DrawLines || DrawBeziers || DrawCurve || DrawPolygon)
                {
                    using PdfDocument reader = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.ReadOnly);
                    Rect rect = CalculateRect(scrollviewer, mousedowncoord.X, 0, mousedowncoord.Y, 0, reader?.Pages[PdfViewer.Sayfa - 1]);
                    Points.Add(new XPoint(rect.X, rect.Y));
                    GC.Collect();
                }
            }
        }
    }

    private async void PdfImportViewerControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is Image img && img.Parent is ScrollViewer scrollviewer && DataContext is TwainCtrl twainCtrl)
        {
            Point mousemovecoord = e.GetPosition(scrollviewer);
            double x1 = Math.Min(mousedowncoord.X, mousemovecoord.X);
            double x2 = Math.Max(mousedowncoord.X, mousemovecoord.X);
            double y1 = Math.Min(mousedowncoord.Y, mousemovecoord.Y);
            double y2 = Math.Max(mousedowncoord.Y, mousemovecoord.Y);

            if (isDrawMouseDown)
            {
                cnv.ToolTip = EscToolTip;
                EscToolTip.IsOpen = true;
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
                    EscToolTip.IsOpen = false;
                    cnv.Children?.Clear();
                    using PdfDocument pdfDocument = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify);
                    List<PdfPage> pdfpages = GetPdfPagesOrientation(pdfDocument);
                    foreach (PdfPage pdfpage in pdfpages)
                    {
                        PdfPage page = SinglePage ? pdfDocument.Pages[PdfViewer.Sayfa - 1] : pdfpage;
                        using XGraphics gfx = XGraphics.FromPdfPage(page);
                        Rect rect = CalculateRect(scrollviewer, x1, x2, y1, y2, page);
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

                        if (DrawLines && Points?.Count > 1)
                        {
                            gfx.DrawLines(pen, Points.ToArray());
                        }

                        if (DrawBeziers && (Points?.Count - 1) % 3 == 0)
                        {
                            gfx.DrawBeziers(pen, Points.ToArray());
                        }

                        if ((DrawCurve || DrawPolygon) && Points?.Count == PolygonCount)
                        {
                            if (DrawCurve)
                            {
                                gfx.DrawCurve(pen, Points.ToArray());
                            }

                            if (DrawPolygon)
                            {
                                if (GraphObjectFillColor == XKnownColor.Transparent)
                                {
                                    gfx.DrawPolygon(pen, Points.ToArray());
                                }
                                else
                                {
                                    gfx.DrawPolygon(pen, brush, Points.ToArray(), XFillMode.Winding);
                                }
                            }
                            Points.Clear();
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
                        }

                        if (DrawString && !string.IsNullOrWhiteSpace(Text))
                        {
                            XFont font = new("Times New Roman", TextSize, XFontStyle.Regular);
                            rect.Height = 0;
                            if (GraphObjectFillColor == XKnownColor.Transparent)
                            {
                                if (page.Orientation == PageOrientation.Portrait)
                                {
                                    gfx.DrawString(Text, font, XBrushes.Black, rect, XStringFormats.Default);
                                }
                                else
                                {
                                    gfx.RotateAtTransform(-90, rect.Location);
                                    gfx.DrawString(Text, font, XBrushes.Black, rect, XStringFormats.Default);
                                }
                            }
                            else
                            {
                                if (page.Orientation == PageOrientation.Portrait)
                                {
                                    gfx.DrawString(Text, font, brush, rect, XStringFormats.Default);
                                }
                                else
                                {
                                    gfx.RotateAtTransform(-90, rect.Location);
                                    gfx.DrawString(Text, font, brush, rect, XStringFormats.Default);
                                }
                            }
                        }

                        if (DrawAnnotation && !string.IsNullOrWhiteSpace(AnnotationText))
                        {
                            PdfTextAnnotation pdftextannotation = new() { Contents = AnnotationText, Icon = PdfTextAnnotationIcon.Note };
                            XRect annotrect = gfx.Transformer.WorldToDefaultPage(rect);
                            pdftextannotation.Rectangle = new PdfRectangle(annotrect);
                            page.Annotations.Add(pdftextannotation);
                        }

                        if (SinglePage)
                        {
                            break;
                        }
                    }

                    mousedowncoord.X = mousedowncoord.Y = 0;
                    isDrawMouseDown = false;
                    Cursor = Cursors.Arrow;
                    pdfpages = null;
                    DrawnImage = null;

                    if (!Keyboard.IsKeyDown(Key.Escape) && SaveRefreshPdfPage.CanExecute(null))
                    {
                        SaveRefreshPdfPage.Execute(pdfDocument);
                    }
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
                    double width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                    double height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);
                    double coordx = x1 + scrollviewer.HorizontalOffset;
                    double coordy = y1 + scrollviewer.VerticalOffset;
                    byte[] imgdata = BitmapMethods.CaptureScreen(coordx, coordy, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));
                    mousedowncoord.X = mousedowncoord.Y = 0;
                    isMouseDown = false;
                    Cursor = Cursors.Arrow;
                    OcrText = await GetOcrData(twainCtrl.Scanner?.SelectedTtsLanguage, imgdata);
                }
            }
        }
    }

    private void PdfImportViewerControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "InkDrawColor")
        {
            DrawingAttribute.Color = (Color)ColorConverter.ConvertFromString(InkDrawColor);
        }
        if (e.PropertyName is "SinglePage" && !SinglePage)
        {
            DrawAnnotation = false;
            DrawLines = false;
            DrawBeziers = false;
            DrawCurve = false;
            DrawPolygon = false;
        }
        if (e.PropertyName is "DrawCurve" or "DrawPolygon" && (DrawCurve || DrawPolygon) && Points?.Count > PolygonCount)
        {
            Points.Clear();
        }
        if (e.PropertyName is "SelectedInk" && !string.IsNullOrWhiteSpace(SelectedInk))
        {
            Ink.Strokes.Clear();
            Ink.Strokes.Add((StrokeCollection)new Base64StringToStrokeCollectionConverter().Convert(SelectedInk, null, null, CultureInfo.CurrentCulture));
        }
    }

    private void PdfViewer_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            Cursor = Cursors.Cross;
        }
    }

    private void PdfViewer_PreviewKeyUp(object sender, KeyEventArgs e) => Cursor = Cursors.Arrow;

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => EscToolTip = new() { Content = Translation.GetResStringValue("ESCTOCANCEL") };
}