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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TwainControl.Converter;
using TwainControl.Properties;
using Viewer = PdfViewer.PdfViewer;

namespace TwainControl;

/// <summary>
/// Interaction logic for PdfImportViewerControl.xaml
/// </summary>
public partial class PdfImportViewerControl : UserControl, INotifyPropertyChanged
{
    private readonly Ellipse ellipseselectionbox = new() { Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), StrokeDashArray = new DoubleCollection([1]), };
    private readonly Line linebox = new() { Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), StrokeDashArray = new DoubleCollection([1]) };
    private readonly Rectangle rectangleselectionbox = new() { Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), StrokeDashArray = new DoubleCollection([1]) };
    private readonly Line reverselinebox = new() { Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), StrokeDashArray = new DoubleCollection([1]) };
    private Brush combinedLinearBrush;
    private bool isDrawMouseDown;
    private bool isMouseDown;
    private Point mousedowncoord;
    private XImage xImage;

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
                        xImage = XImage.FromFile(openFileDialog.FileName);
                        DrawnImage = xImage;
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
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
                    xImage = XImage.FromBitmapSource(renderTargetBitmap);
                    DrawnImage = xImage;
                    DrawImage = true;
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
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
                    xImage?.Dispose();
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
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
                    throw new ArgumentException(ex?.Message);
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
                    throw new ArgumentException(ex?.Message);
                }
            },
            parameter => true);

        RemoveAnnotation = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is not PdfAnnotation selectedannotation || !File.Exists(PdfViewer.PdfFilePath))
                    {
                        return;
                    }
                    using PdfDocument pdfdocument = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                    if (pdfdocument is null)
                    {
                        return;
                    }
                    PdfPage page = pdfdocument.Pages[PdfViewer.Sayfa - 1];
                    PdfAnnotation annotation = page.Annotations.ToList().Cast<PdfAnnotation>().FirstOrDefault(z => z.Contents == selectedannotation.Contents);
                    if (annotation is not null && DataContext is TwainCtrl twainCtrl)
                    {
                        page?.Annotations?.Remove(annotation);
                        twainCtrl.PdfToolBarControlIsEnabled = false;
                        pdfdocument.Save(PdfViewer.PdfFilePath);
                        twainCtrl.PdfToolBarControlIsEnabled = true;
                        ReadAnnotation.Execute(null);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
                }
            },
            parameter => true);

        OpenPdfHistoryFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not string filepath)
                {
                    return;
                }
                if (File.Exists(filepath))
                {
                    PdfViewer.PdfFilePath = filepath;
                }
                else
                {
                    RemovePdfFromHistoryList(filepath);
                }
            },
            parameter => true);

        RemovePdfHistoryFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is string filepath)
                {
                    RemovePdfFromHistoryList(filepath);
                }
            },
            parameter => true);

        SaveRefreshPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not PdfDocument pdfDocument || pdfDocument is null)
                {
                    return;
                }
                pdfDocument.ApplyDefaultPdfCompression();
                pdfDocument.Save(PdfViewer.PdfFilePath);
                double zoom = PdfViewer.Zoom;
                using PdfiumViewer.PdfDocument pdfDoc = PdfiumViewer.PdfDocument.Load(PdfViewer.PdfFilePath);
                int width = (int)(pdfDoc.PageSizes[PdfViewer.Sayfa - 1].Width / 96 * PdfViewer.Dpi);
                int height = (int)(pdfDoc.PageSizes[PdfViewer.Sayfa - 1].Height / 96 * PdfViewer.Dpi);
                PdfViewer.Source = await Viewer.ConvertToImgAsync(pdfDoc, PdfViewer.Dpi, PdfViewer.Sayfa - 1, width, height);
                PdfViewer.Zoom = zoom;
                DrawnImage = null;
            },
            parameter => true);

        OcrCurrentPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not TwainCtrl twainCtrl || PdfViewer.Source is null)
                {
                    return;
                }
                twainCtrl.PdfToolBarControlIsEnabled = false;
                OcrProgressIndeterminate = true;
                using PdfDocument pdfDocument = await GenerateOcredPdfPage(
                    PdfViewer,
                    Settings.Default.JpegQuality,
                    Settings.Default.ImgLoadResolution,
                    twainCtrl.Scanner?.SelectedTtsLanguage,
                    twainCtrl.SelectedPaper,
                    progress => twainCtrl.PdfImportControlProgressValue = progress);
                pdfDocument.Save(PdfViewer.PdfFilePath);
                twainCtrl.PdfToolBarControlIsEnabled = true;
                OcrProgressIndeterminate = false;
                OcrDialogOpen = false;
                OcrDialogOpen = true;
            },
            parameter => parameter is TwainCtrl twainCtrl && !string.IsNullOrWhiteSpace(twainCtrl.Scanner?.SelectedTtsLanguage) && File.Exists(PdfViewer.PdfFilePath) && !OcrProgressIndeterminate);

        WebAdreseGit = new RelayCommand<object>(parameter => TwainCtrl.GotoPage(parameter as string), parameter => true);

        ClearLines = new RelayCommand<object>(parameter => Points.Clear(), parameter => Points is not null);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public PdfAnnotations Annotations
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Annotations));
            }
        }
    }

    public string AnnotationText
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AnnotationText));
            }
        }
    } = string.Empty;

    public bool ApplyLandscape
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ApplyLandscape));
            }
        }
    } = true;

    public bool ApplyPortrait
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ApplyPortrait));
            }
        }
    } = true;

    public RelayCommand<object> ClearInkDrawImage { get; }

    public RelayCommand<object> ClearLines { get; }

    public Brush CombinedLinearBrush
    {
        get
        {
            Color firstColor = (Color)ColorConverter.ConvertFromString(GraphObjectFirstGradientColor.ToString());
            Color secondColor = (Color)ColorConverter.ConvertFromString(GraphObjectSecondGradientColor.ToString());
            return SelectedGradientMode switch
            {
                XLinearGradientMode.Horizontal => new LinearGradientBrush(firstColor, secondColor, new Point(0, 0.5), new Point(1, 0.5)),
                XLinearGradientMode.Vertical => new LinearGradientBrush(firstColor, secondColor, new Point(0.5, 0), new Point(0.5, 1)),
                XLinearGradientMode.ForwardDiagonal => new LinearGradientBrush(firstColor, secondColor, new Point(0, 0), new Point(1, 1)),
                XLinearGradientMode.BackwardDiagonal => new LinearGradientBrush(firstColor, secondColor, new Point(1, 1), new Point(0, 0)),
                _ => new LinearGradientBrush(firstColor, secondColor, new Point(0, 0.5), new Point(1, 0.5)),
            };
        }
        set
        {
            if (combinedLinearBrush != value)
            {
                combinedLinearBrush = value;
                OnPropertyChanged(nameof(CombinedLinearBrush));
            }
        }
    }

    public string Çeviri
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Çeviri));
            }
        }
    }

    public string ÇevrilenDil
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ÇevrilenDil));
            }
        }
    } = "en";

    public bool DrawAnnotation
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawAnnotation));
            }
        }
    }

    public bool DrawBeziers
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawBeziers));
            }
        }
    }

    public bool DrawCurve
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawCurve));
            }
        }
    }

    public bool DrawEllipse
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawEllipse));
            }
        }
    }

    public bool DrawImage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawImage));
            }
        }
    }

    public bool DrawLine
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawLine));
            }
        }
    }

    public bool DrawLines
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawLines));
            }
        }
    }

    public XImage DrawnImage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawnImage));
            }
        }
    }

    public bool DrawPolygon
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawPolygon));
            }
        }
    }

    public bool DrawRect
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawRect));
            }
        }
    }

    public bool DrawReverseLine
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawReverseLine));
            }
        }
    }

    public bool DrawRoundedRect
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawRoundedRect));
            }
        }
    }

    public bool DrawString
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DrawString));
            }
        }
    }

    public ToolTip EscToolTip { get; private set; }

    public XKnownColor GraphObjectColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(GraphObjectColor));
            }
        }
    } = XKnownColor.Black;

    public XKnownColor GraphObjectFillColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(GraphObjectFillColor));
                OnPropertyChanged(nameof(DrawString));
            }
        }
    } = XKnownColor.Transparent;

    public XKnownColor GraphObjectFirstGradientColor
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(GraphObjectFirstGradientColor));
                OnPropertyChanged(nameof(CombinedLinearBrush));
                OnPropertyChanged(nameof(IsLinearDraw));
            }
        }
    } = XKnownColor.Transparent;

    public XKnownColor GraphObjectSecondGradientColor
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(GraphObjectSecondGradientColor));
                OnPropertyChanged(nameof(CombinedLinearBrush));
                OnPropertyChanged(nameof(IsLinearDraw));
            }
        }
    } = XKnownColor.Transparent;

    public string InkDrawColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(InkDrawColor));
            }
        }
    } = "Black";

    public BitmapSource InkSource
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(InkSource));
            }
        }
    }

    public bool IsLinearDraw
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsLinearDraw));
            }
        }
    }

    public RelayCommand<object> LoadDrawImage { get; }

    public RelayCommand<object> LoadInkDrawImage { get; }

    public string MevcutDil
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MevcutDil));
            }
        }
    } = "auto";

    public RelayCommand<object> OcrCurrentPdfPage { get; }

    public bool OcrDialogOpen
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrDialogOpen));
            }
        }
    }

    public bool OcrProgressIndeterminate
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrProgressIndeterminate));
            }
        }
    }

    public string OcrText
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(OcrText));
            }
        }
    }

    public DoubleCollection OpacityList { get; } = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0];

    public RelayCommand<object> OpenPdfHistoryFile { get; }

    public XDashStyle PenDash
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PenDash));
            }
        }
    } = XDashStyle.Solid;

    public XLineCap PenLineCap
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PenLineCap));
            }
        }
    } = XLineCap.Flat;

    public XLineJoin PenLineJoin
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PenLineJoin));
            }
        }
    } = XLineJoin.Miter;

    public double PenWidth
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PenWidth));
            }
        }
    } = 0.5d;

    public ObservableCollection<XPoint> Points { get; set; } = [];

    public int PolygonCount
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PolygonCount));
            }
        }
    } = 3;

    public string QrText
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(QrText));
            }
        }
    }

    public RelayCommand<object> ReadAnnotation { get; }

    public RelayCommand<object> RemoveAnnotation { get; }

    public RelayCommand<object> RemovePdfHistoryFile { get; }

    public RelayCommand<object> SaveInkDrawImage { get; }

    public RelayCommand<object> SaveRefreshPdfPage { get; }

    public XLinearGradientMode SelectedGradientMode
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedGradientMode));
                OnPropertyChanged(nameof(CombinedLinearBrush));
            }
        }
    } = XLinearGradientMode.Horizontal;

    public string SelectedInk
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedInk));
            }
        }
    }

    public bool SinglePage
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SinglePage));
            }
        }
    } = true;

    public string Text
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Text));
            }
        }
    } = string.Empty;

    public double TextSize
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TextSize));
            }
        }
    } = 12d;

    public bool ToolBoxIsExpanded
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ToolBoxIsExpanded));
            }
        }
    }

    public double TransparentLevel
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TransparentLevel));
            }
        }
    } = 1;

    public RelayCommand<object> WebAdreseGit { get; }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher;
        if (dispatcher?.CheckAccess() == true)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            _ = dispatcher?.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    private void AddSelectionElement(Canvas canvas, UIElement element)
    {
        if (!canvas.Children.Contains(element))
        {
            _ = canvas.Children.Add(element);
        }
    }

    private Rect CalculateRect(ScrollViewer scrollviewer, double x1, double x2, double y1, double y2, PdfPage page)
    {
        if (scrollviewer is null)
        {
            return Rect.Empty;
        }

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
        double rectX = 0, rectY = 0, rectWidth = 0, rectHeight = 0;
        switch (page.Orientation)
        {
            case PageOrientation.Portrait:
                if (page.Rotate is 180 or (-180))
                {
                    rectX = page.Width - (coordx * widthmultiply) - (width * widthmultiply);
                    rectY = page.Height - (coordy * heightmultiply) - (height * heightmultiply);
                }
                else
                {
                    rectX = coordx * widthmultiply;
                    rectY = coordy * heightmultiply;
                }
                rectWidth = width * widthmultiply;
                rectHeight = height * heightmultiply;
                break;
            case PageOrientation.Landscape:
                if (page.Rotate is 270 or (-90))
                {
                    rectX = page.Height - (coordy * widthmultiply) - (height * heightmultiply);
                    rectY = (coordx * heightmultiply) - (page.Width - page.Height);
                    rectWidth = height * widthmultiply;
                    rectHeight = width * heightmultiply;
                }
                else
                {
                    rectX = coordy * widthmultiply;
                    rectY = page.Height - (coordx * heightmultiply) - (width * widthmultiply);
                    rectWidth = height * widthmultiply;
                    rectHeight = width * heightmultiply;
                }
                break;
        }
        return new Rect(rectX, rectY, rectWidth, rectHeight);
    }

    private void DrawAnnotations(PdfPage page, XGraphics gfx, Rect rect)
    {
        if (DrawAnnotation && !string.IsNullOrWhiteSpace(AnnotationText))
        {
            PdfTextAnnotation pdftextannotation = GeneratePdfTextAnnotation(gfx, rect, AnnotationText);
            page.Annotations.Add(pdftextannotation);
        }
    }

    private void DrawImages(PdfPage page, XGraphics gfx, Rect rect)
    {
        if (DrawImage && DrawnImage is not null)
        {
            XPoint center = new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
            double angle = page.Rotate switch
            {
                270 or -90 => 90,
                90 => -90,
                180 => -180,
                _ => 0,
            };
            gfx.RotateAtTransform(angle, center);
            gfx.DrawImage(DrawnImage, rect);
            xImage?.Dispose();
        }
    }

    private void DrawLinesAndCurves(PdfPage page, XGraphics gfx, Rect rect, XPen pen, XBrush brush)
    {
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
                gfx.DrawPolygon(pen, brush, Points.ToArray(), XFillMode.Winding);
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
    }

    private void DrawShapes(PdfPage page, XGraphics gfx, Rect rect, XPen pen, XBrush brush)
    {
        if (DrawRect)
        {
            gfx.DrawRectangle(pen, brush, rect);
        }

        if (DrawRoundedRect)
        {
            gfx.DrawRoundedRectangle(pen, brush, rect, new Size(2, 2));
        }

        if (DrawEllipse)
        {
            gfx.DrawEllipse(pen, brush, rect);
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
    }

    private void DrawTexts(PdfPage page, XGraphics gfx, Rect rect, XBrush brush)
    {
        if (DrawString && brush is not null && !string.IsNullOrWhiteSpace(Text))
        {
            XFont font = new("Times New Roman", TextSize, XFontStyle.Regular);
            XPoint center = new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
            double angle = page.Rotate switch
            {
                270 or -90 => 90,
                90 => -90,
                180 => -180,
                _ => 0,
            };
            gfx.RotateAtTransform(angle, center);
            gfx.DrawString(Text, font, brush, rect, XStringFormats.Center);
        }
    }

    private async Task<PdfDocument> GenerateOcredPdfPage(Viewer pdfViewer, int jpegquality, int dpi, string ocrlang, Paper paper, Action<double> progresscallback = null)
    {
        List<string> tempfiles = [];
        ObservableCollection<OcrData> ocrdata;
        OcrText = null;
        for (int i = 0; i < pdfViewer.ToplamSayfa; i++)
        {
            ocrdata = await (await Viewer.ConvertToImgAsync(pdfViewer.PdfFilePath, i + 1, dpi)).ToTiffJpegByteArray(ExtensionMethods.Format.Jpg).OcrAsync(ocrlang);
            using PdfDocument scanneddocument = (await Viewer.ConvertToImgAsync(pdfViewer.PdfFilePath, i + 1, dpi)).GeneratePdf(ocrdata, ExtensionMethods.Format.Jpg, paper, jpegquality, dpi);
            string pdffile = $"{System.IO.Path.GetTempPath()}{i}.pdf";
            scanneddocument.Save(pdffile);
            tempfiles.Add(pdffile);
            progresscallback?.Invoke((i + 1) / (double)pdfViewer.ToplamSayfa);
            OcrText = OcrText += $"{string.Join(" ", ocrdata?.Select(z => z.Text))}\n";
        }
        using PdfDocument document = tempfiles.ToArray().MergePdf();
        tempfiles.ForEach(File.Delete);
        return document;
    }

    private PdfTextAnnotation GeneratePdfTextAnnotation(XGraphics gfx, Rect rect, string content)
    {
        PdfTextAnnotation pdftextannotation = new() { Contents = content, Icon = PdfTextAnnotationIcon.Note };
        XRect annotrect = gfx.Transformer.WorldToDefaultPage(rect);
        pdftextannotation.Rectangle = new PdfRectangle(annotrect);
        return pdftextannotation;
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

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles && droppedfiles?.Length > 0 && DataContext is TwainCtrl twainCtrl)
        {
            foreach (string file in from string file in droppedfiles where Viewer.IsValidPdfFile(file) select file)
            {
                twainCtrl.Scanner.MergePdfFiles.Add(file);
            }
        }
    }

    private void PdfImportViewerControl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not Image img || img.Parent is not ScrollViewer scrollviewer || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            isMouseDown = true;
            mousedowncoord = e.GetPosition(scrollviewer);
        }

        if (Keyboard.Modifiers != ModifierKeys.Shift || (!DrawLines && !DrawBeziers && !DrawCurve && !DrawPolygon && !DrawAnnotation && !DrawString && !DrawImage && !DrawEllipse && !DrawRect && !DrawLine && !DrawReverseLine && !DrawRoundedRect))
        {
            return;
        }

        isDrawMouseDown = true;
        mousedowncoord = e.GetPosition(scrollviewer);
        Point mousemovecoord = e.GetPosition(scrollviewer);
        if (DrawLines || DrawBeziers || DrawCurve || DrawPolygon)
        {
            double x1 = Math.Min(mousedowncoord.X, mousemovecoord.X);
            double x2 = Math.Max(mousedowncoord.X, mousemovecoord.X);
            double y1 = Math.Min(mousedowncoord.Y, mousemovecoord.Y);
            double y2 = Math.Max(mousedowncoord.Y, mousemovecoord.Y);
            using PdfDocument reader = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.ReadOnly);
            Rect rect = CalculateRect(scrollviewer, x1, x2, y1, y2, reader?.Pages[PdfViewer.Sayfa - 1]);
            Points.Add(new XPoint(rect.X, rect.Y));
        }
    }

    private async void PdfImportViewerControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is not Image img || img.Parent is not ScrollViewer scrollviewer || DataContext is not TwainCtrl twainCtrl || scrollviewer.Parent is not Grid grid)
        {
            return;
        }
        Canvas cnv = grid.Children[1] as Canvas;
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
                AddSelectionElement(cnv, rectangleselectionbox);
                rectangleselectionbox.StrokeThickness = PenWidth * TwainCtrl.Inch;
                SetRectagleSelectionbox(x1, x2, y1, y2);
            }

            if (DrawLine)
            {
                AddSelectionElement(cnv, linebox);
                linebox.StrokeThickness = PenWidth * TwainCtrl.Inch;
                linebox.X1 = x1;
                linebox.Y1 = y1;
                linebox.X2 = x2;
                linebox.Y2 = y2;
            }

            if (DrawReverseLine)
            {
                AddSelectionElement(cnv, reverselinebox);
                reverselinebox.StrokeThickness = PenWidth * TwainCtrl.Inch;
                reverselinebox.X1 = x2;
                reverselinebox.Y1 = y1;
                reverselinebox.X2 = x1;
                reverselinebox.Y2 = y2;
            }

            if (DrawEllipse)
            {
                AddSelectionElement(cnv, ellipseselectionbox);
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
                using PdfDocument pdfDocument = PdfReader.Open(PdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                if (pdfDocument is not null)
                {
                    List<PdfPage> pdfpages = GetPdfPagesOrientation(pdfDocument);
                    foreach (PdfPage pdfpage in pdfpages)
                    {
                        PdfPage page = SinglePage ? pdfDocument.Pages[PdfViewer.Sayfa - 1] : pdfpage;
                        using XGraphics gfx = XGraphics.FromPdfPage(page);
                        Rect rect = CalculateRect(scrollviewer, x1, x2, y1, y2, page);
                        XPen pen = new(XColor.FromKnownColor(GraphObjectColor)) { DashStyle = PenDash, LineCap = PenLineCap, LineJoin = PenLineJoin, Width = PenWidth };
                        XBrush brush = SetBrush(GraphObjectFillColor, GraphObjectFirstGradientColor, GraphObjectSecondGradientColor, TransparentLevel, rect, IsLinearDraw);

                        DrawShapes(page, gfx, rect, pen, brush);
                        DrawLinesAndCurves(page, gfx, rect, pen, brush);
                        DrawImages(page, gfx, rect);
                        DrawTexts(page, gfx, rect, brush);
                        DrawAnnotations(page, gfx, rect);

                        if (SinglePage)
                        {
                            break;
                        }
                    }

                    pdfpages = null;
                    if (!Keyboard.IsKeyDown(Key.Escape) && SaveRefreshPdfPage.CanExecute(null))
                    {
                        twainCtrl.PdfToolBarControlIsEnabled = false;
                        SaveRefreshPdfPage.Execute(pdfDocument);
                        twainCtrl.PdfToolBarControlIsEnabled = true;
                    }
                }
                ResetMouse();
            }
        }

        if (isMouseDown)
        {
            AddSelectionElement(cnv, rectangleselectionbox);
            SetRectagleSelectionbox(x1, x2, y1, y2);
            if (e.LeftButton == MouseButtonState.Released)
            {
                cnv.Children?.Clear();
                double width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                double height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);
                double coordx = x1 + scrollviewer.HorizontalOffset;
                double coordy = y1 + scrollviewer.VerticalOffset;
                byte[] imgdata = BitmapFrame.Create((BitmapSource)img.Source).CaptureScreen(coordx, coordy, width, height, scrollviewer);
                if (imgdata is not null)
                {
                    OcrProgressIndeterminate = true;
                    OcrText = await GetOcrData(twainCtrl.Scanner?.SelectedTtsLanguage, imgdata);
                    QrCode.QrCode qrCode = new();
                    QrText = qrCode.GetImageBarcodeResult(imgdata);
                    imgdata = null;
                    OcrProgressIndeterminate = false;
                }
                ResetMouse();
            }
        }
    }

    private async void PdfImportViewerControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "InkDrawColor")
        {
            DrawingAttribute.Color = (Color)ColorConverter.ConvertFromString(InkDrawColor);
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
        if (e.PropertyName is "OcrText" or "ÇevrilenDil" && !string.IsNullOrWhiteSpace(OcrText))
        {
            Çeviri = await TranslateViewModel.DileÇevirAsync(OcrText, MevcutDil, ÇevrilenDil);
        }
        if (e.PropertyName is "IsLinearDraw" && GraphObjectFirstGradientColor == XKnownColor.Transparent && GraphObjectSecondGradientColor == XKnownColor.Transparent)
        {
            IsLinearDraw = false;
        }
        if (e.PropertyName is "DrawString" && GraphObjectFillColor == XKnownColor.Transparent)
        {
            DrawString = false;
        }
    }

    private void RemovePdfFromHistoryList(string filepath)
    {
        Settings.Default.PdfLoadHistory.Remove(filepath);
        Settings.Default.Save();
        Settings.Default.Reload();
    }

    private void ResetMouse()
    {
        mousedowncoord.X = mousedowncoord.Y = 0;
        isDrawMouseDown = false;
        isMouseDown = false;
        Cursor = Cursors.Arrow;
    }

    private XBrush SetBrush(XKnownColor fillColor, XKnownColor firstgradient, XKnownColor secondgradient, double transparentlevel = 1, Rect rect = default, bool isLinearColor = false)
    {
        if (transparentlevel > 1)
        {
            transparentlevel = 1;
        }

        if (isLinearColor && !DrawPolygon)
        {
            XColor color1 = XColor.FromKnownColor(firstgradient);
            XColor color2 = XColor.FromKnownColor(secondgradient);
            color1.A = transparentlevel;
            color2.A = transparentlevel;
            return (XLinearGradientBrush)new(rect, color1, color2, SelectedGradientMode);
        }

        if (fillColor == XKnownColor.Transparent)
        {
            return null;
        }
        XColor color = XColor.FromKnownColor(fillColor);
        color.A = transparentlevel;
        return new XSolidBrush(color);
    }

    private void SetRectagleSelectionbox(double x1, double x2, double y1, double y2)
    {
        Canvas.SetLeft(rectangleselectionbox, x1);
        Canvas.SetTop(rectangleselectionbox, y1);
        rectangleselectionbox.Width = x2 - x1;
        rectangleselectionbox.Height = y2 - y1;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e) => EscToolTip = new() { Content = Translation.GetResStringValue("ESCTOCANCEL") };

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            ToolBoxIsExpanded = true;
        }
    }
}