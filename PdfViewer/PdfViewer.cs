using Extensions;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using static Extensions.ExtensionMethods;
using Control = System.Windows.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using PrintDialog = System.Windows.Controls.PrintDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PdfViewer;

[TemplatePart(Name = "ScrollVwr", Type = typeof(ScrollViewer))]
public class PdfViewer : Control, INotifyPropertyChanged, IDisposable
{
    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(PdfViewer), new PropertyMetadata(0.0));
    public static readonly DependencyProperty ContextMenuVisibilityProperty =
        DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));
    public static readonly DependencyProperty DpiProperty =
        DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200, DpiChangedAsync));
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        "Orientation",
        typeof(FitImageOrientation),
        typeof(PdfViewer),
        new PropertyMetadata(FitImageOrientation.Width, Changed));
    public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, PdfFilePathChanged));
    public static readonly DependencyProperty PrintDpiProperty = DependencyProperty.Register("PrintDpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(300));
    public static readonly DependencyProperty SayfaProperty =
        DependencyProperty.Register("Sayfa", typeof(int), typeof(PdfViewer), new PropertyMetadata(1, SayfaChangedAsync));
    public static readonly DependencyProperty ScrollBarVisibleProperty = DependencyProperty.Register(
        "ScrollBarVisible",
        typeof(ScrollBarVisibility),
        typeof(PdfViewer),
        new PropertyMetadata(ScrollBarVisibility.Auto));
    public static readonly DependencyProperty SnapTickProperty =
        DependencyProperty.Register("SnapTick", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(PdfViewer), new PropertyMetadata(null, SourceChanged));
    public static readonly DependencyProperty ThumbsVisibleProperty =
        DependencyProperty.Register("ThumbsVisible", typeof(bool), typeof(PdfViewer), new PropertyMetadata(true));
    public static readonly DependencyProperty ToolBarVisibilityProperty =
        DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty ZoomEnabledProperty = DependencyProperty.Register("ZoomEnabled", typeof(bool), typeof(PdfViewer), new PropertyMetadata(true));
    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));
    private bool autoFitContent;
    private Visibility bookmarkContentVisibility;
    private bool disposedValue;
    private Visibility dpiListVisibility = Visibility.Visible;
    private bool matchCase;
    private Visibility openButtonVisibility = Visibility.Collapsed;
    private IEnumerable<int> pages;
    private PdfBookmarkCollection pdfBookmarks;
    private byte[] pdfData;
    private ObservableCollection<PdfMatch> pdfMatches;
    private string pdfTextContent;
    private Visibility pdfTextContentVisibility;
    private Visibility printButtonVisibility = Visibility.Collapsed;
    private bool printDpiSettingsListEnabled = true;
    private ScrollViewer scrollvwr;
    private PdfMatch searchPdfMatch;
    private string searchTextContent;
    private Visibility searchTextContentVisibility;
    private Visibility sliderZoomAngleVisibility = Visibility.Visible;
    private Visibility tifNavigasyonButtonEtkin = Visibility.Visible;
    private int toplamSayfa;
    private bool wholeWord;

    static PdfViewer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfViewer), new FrameworkPropertyMetadata(typeof(PdfViewer))); }

    public PdfViewer()
    {
        PropertyChanged += PdfViewer_PropertyChanged;
        SizeChanged += PdfViewer_SizeChanged;
        DosyaAç = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Pdf Dosyaları (*.pdf)|*.pdf" };
                if (openFileDialog.ShowDialog() == true && IsValidPdfFile(openFileDialog.FileName))
                {
                    PdfFilePath = openFileDialog.FileName;
                }
            });

        Yazdır = new RelayCommand<object>(
            parameter =>
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PrintPdf(pdfDocument, PrintDpi);
            },
            parameter => PdfFilePath is not null);

        PrintSinglePage = new RelayCommand<object>(
            parameter =>
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PrintPdf(pdfDocument, (int)parameter, (int)parameter, PrintDpi);
            },
            parameter => PdfFilePath is not null);

        SaveImage = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Jpg Dosyası(*.jpg)|*.jpg", FileName = "Resim" };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, Source.ToTiffJpegByteArray(Format.Jpg));
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex.Message);
                    }
                }
            },
            parameter => Source is not null);

        Resize = new RelayCommand<object>(
            delegate
            {
                Zoom = Orientation != FitImageOrientation.Width ? ActualHeight / Source.Height : ActualWidth / Source.Width;
                if (Zoom == 0)
                {
                    Zoom = 1;
                }
            },
            parameter => Source is not null);

        ReadPdfText = new RelayCommand<object>(
            delegate
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfTextContent = pdfDocument.GetPdfText(Sayfa - 1);
            },
            parameter => Source is not null);

        ReadPdfBookmarks = new RelayCommand<object>(
            delegate
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfBookmarks = pdfDocument.Bookmarks;
            },
            parameter => Source is not null);

        GoPdfBookMarkPage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is int pagenumber)
                {
                    Sayfa = pagenumber + 1;
                }
            });

        ScrollToCurrentPage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    listBox.ScrollIntoView(Sayfa);
                }
            });

        SearchPdfText = new RelayCommand<object>(
            delegate
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfMatches matches = pdfDocument.Search(SearchTextContent, MatchCase, WholeWord);
                PdfMatches = [.. matches.Items];
            },
            parameter => Source is not null && !string.IsNullOrWhiteSpace(SearchTextContent));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static int[] DpiList { get; } = [12, 24, 36, 48, 72, 96, 120, 150, 200, 300, 400, 500, 600, 1200];

    public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }

    public bool AutoFitContent
    {
        get => autoFitContent;

        set
        {
            if (autoFitContent != value)
            {
                autoFitContent = value;
                OnPropertyChanged(nameof(AutoFitContent));
            }
        }
    }

    public Visibility BookmarkContentVisibility
    {
        get => bookmarkContentVisibility;

        set
        {
            if (bookmarkContentVisibility != value)
            {
                bookmarkContentVisibility = value;
                OnPropertyChanged(nameof(BookmarkContentVisibility));
            }
        }
    }

    public Visibility ContextMenuVisibility { get => (Visibility)GetValue(ContextMenuVisibilityProperty); set => SetValue(ContextMenuVisibilityProperty, value); }

    public RelayCommand<object> DosyaAç { get; }

    public int Dpi { get => (int)GetValue(DpiProperty); set => SetValue(DpiProperty, value); }

    public Visibility DpiListVisibility
    {
        get => dpiListVisibility;

        set
        {
            if (dpiListVisibility != value)
            {
                dpiListVisibility = value;
                OnPropertyChanged(nameof(DpiListVisibility));
            }
        }
    }

    public RelayCommand<object> GoPdfBookMarkPage { get; }

    public bool MatchCase
    {
        get => matchCase;

        set
        {
            if (matchCase != value)
            {
                matchCase = value;
                OnPropertyChanged(nameof(MatchCase));
            }
        }
    }

    public Visibility OpenButtonVisibility
    {
        get => openButtonVisibility;

        set
        {
            if (openButtonVisibility != value)
            {
                openButtonVisibility = value;
                OnPropertyChanged(nameof(OpenButtonVisibility));
            }
        }
    }

    public FitImageOrientation Orientation { get => (FitImageOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    [Browsable(false)]
    public IEnumerable<int> Pages
    {
        get => pages;

        set
        {
            if (pages != value)
            {
                pages = value;
                OnPropertyChanged(nameof(Pages));
            }
        }
    }

    public PdfBookmarkCollection PdfBookmarks
    {
        get => pdfBookmarks;

        set
        {
            if (pdfBookmarks != value)
            {
                pdfBookmarks = value;
                OnPropertyChanged(nameof(PdfBookmarks));
            }
        }
    }

    public byte[] PdfData
    {
        get => pdfData;

        set
        {
            if (pdfData != value)
            {
                pdfData = value;
                OnPropertyChanged(nameof(PdfData));
            }
        }
    }

    public string PdfFilePath { get => (string)GetValue(PdfFilePathProperty); set => SetValue(PdfFilePathProperty, value); }

    public ObservableCollection<PdfMatch> PdfMatches
    {
        get => pdfMatches;

        set
        {
            if (pdfMatches != value)
            {
                pdfMatches = value;
                OnPropertyChanged(nameof(PdfMatches));
            }
        }
    }

    public string PdfTextContent
    {
        get => pdfTextContent;

        set
        {
            if (pdfTextContent != value)
            {
                pdfTextContent = value;
                OnPropertyChanged(nameof(PdfTextContent));
            }
        }
    }

    public Visibility PdfTextContentVisibility
    {
        get => pdfTextContentVisibility;

        set
        {
            if (pdfTextContentVisibility != value)
            {
                pdfTextContentVisibility = value;
                OnPropertyChanged(nameof(PdfTextContentVisibility));
            }
        }
    }

    public Visibility PrintButtonVisibility
    {
        get => printButtonVisibility;

        set
        {
            if (printButtonVisibility != value)
            {
                printButtonVisibility = value;
                OnPropertyChanged(nameof(PrintButtonVisibility));
            }
        }
    }

    public int PrintDpi { get => (int)GetValue(PrintDpiProperty); set => SetValue(PrintDpiProperty, value); }

    public bool PrintDpiSettingsListEnabled
    {
        get => printDpiSettingsListEnabled;
        set
        {
            if (printDpiSettingsListEnabled != value)
            {
                printDpiSettingsListEnabled = value;
                OnPropertyChanged(nameof(PrintDpiSettingsListEnabled));
            }
        }
    }

    public RelayCommand<object> PrintSinglePage { get; }

    public RelayCommand<object> ReadPdfBookmarks { get; }

    public RelayCommand<object> ReadPdfText { get; }

    public RelayCommand<object> Resize { get; }

    public ICommand SaveImage { get; }

    public int Sayfa { get => (int)GetValue(SayfaProperty); set => SetValue(SayfaProperty, value); }

    public ScrollBarVisibility ScrollBarVisible { get => (ScrollBarVisibility)GetValue(ScrollBarVisibleProperty); set => SetValue(ScrollBarVisibleProperty, value); }

    public RelayCommand<object> ScrollToCurrentPage { get; }

    public PdfMatch SearchPdfMatch
    {
        get => searchPdfMatch;

        set
        {
            if (searchPdfMatch != value)
            {
                searchPdfMatch = value;
                OnPropertyChanged(nameof(SearchPdfMatch));
            }
        }
    }

    public RelayCommand<object> SearchPdfText { get; }

    public string SearchTextContent
    {
        get => searchTextContent;

        set
        {
            if (searchTextContent != value)
            {
                searchTextContent = value;
                OnPropertyChanged(nameof(SearchTextContent));
            }
        }
    }

    public Visibility SearchTextContentVisibility
    {
        get => searchTextContentVisibility;

        set
        {
            if (searchTextContentVisibility != value)
            {
                searchTextContentVisibility = value;
                OnPropertyChanged(nameof(SearchTextContentVisibility));
            }
        }
    }

    public Visibility SliderZoomAngleVisibility
    {
        get => sliderZoomAngleVisibility;

        set
        {
            if (sliderZoomAngleVisibility != value)
            {
                sliderZoomAngleVisibility = value;
                OnPropertyChanged(nameof(SliderZoomAngleVisibility));
            }
        }
    }

    public bool SnapTick { get => (bool)GetValue(SnapTickProperty); set => SetValue(SnapTickProperty, value); }

    public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public bool ThumbsVisible { get => (bool)GetValue(ThumbsVisibleProperty); set => SetValue(ThumbsVisibleProperty, value); }

    public Visibility TifNavigasyonButtonEtkin
    {
        get => tifNavigasyonButtonEtkin;

        set
        {
            if (tifNavigasyonButtonEtkin != value)
            {
                tifNavigasyonButtonEtkin = value;
                OnPropertyChanged(nameof(TifNavigasyonButtonEtkin));
            }
        }
    }

    public Visibility ToolBarVisibility { get => (Visibility)GetValue(ToolBarVisibilityProperty); set => SetValue(ToolBarVisibilityProperty, value); }

    [Browsable(false)]
    public int ToplamSayfa
    {
        get => toplamSayfa;

        set
        {
            if (toplamSayfa != value)
            {
                toplamSayfa = value;
                OnPropertyChanged(nameof(ToplamSayfa));
            }
        }
    }

    public bool WholeWord
    {
        get => wholeWord;

        set
        {
            if (wholeWord != value)
            {
                wholeWord = value;
                OnPropertyChanged(nameof(WholeWord));
            }
        }
    }

    public ICommand Yazdır { get; }

    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    public bool ZoomEnabled { get => (bool)GetValue(ZoomEnabledProperty); set => SetValue(ZoomEnabledProperty, value); }

    public static async Task<BitmapImage> ConvertToImgAsync(string pdffilepath, int page, int dpi = 72)
    {
        try
        {
            return !IsValidPdfFile(pdffilepath)
                   ? throw new ArgumentNullException(nameof(pdffilepath), "pdf is not valid")
                   : await Task.Run(
                () =>
                {
                    using PdfDocument pdfDoc = PdfDocument.Load(pdffilepath);
                    if (pdfDoc is null)
                    {
                        return null;
                    }
                    int width = (int)(pdfDoc.PageSizes[page - 1].Width / 72 * dpi);
                    int height = (int)(pdfDoc.PageSizes[page - 1].Height / 72 * dpi);
                    using Bitmap bitmap = pdfDoc.Render(page - 1, width, height, dpi, dpi, false) as Bitmap;
                    BitmapImage bitmapImage = bitmap.ToBitmapImage(ImageFormat.Jpeg);
                    if (bitmapImage is null)
                    {
                        return null;
                    }

                    bitmapImage.Freeze();
                    return bitmapImage;
                });
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<MemoryStream> ConvertToImgStreamAsync(byte[] pdffilestream, int page, int dpi)
    {
        try
        {
            return pdffilestream?.Length == 0
                   ? throw new ArgumentNullException(nameof(pdffilestream), "stream can not be null or length zero")
                   : await Task.Run(
                () =>
                {
                    using MemoryStream ms = new(pdffilestream);
                    using PdfDocument pdfDoc = PdfDocument.Load(ms);
                    if (pdfDoc is null)
                    {
                        return null;
                    }
                    int width = (int)(pdfDoc.PageSizes[page - 1].Width / 72 * dpi);
                    int height = (int)(pdfDoc.PageSizes[page - 1].Height / 72 * dpi);
                    System.Drawing.Image image = pdfDoc.Render(page - 1, width, height, dpi, dpi, false);
                    if (image is null)
                    {
                        return null;
                    }

                    MemoryStream stream = new();
                    image.Save(stream, ImageFormat.Jpeg);
                    pdffilestream = null;
                    return stream;
                });
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool IsValidPdfFile(string filename)
    {
        if (!File.Exists(filename))
        {
            return false;
        }

        byte[] buffer = new byte[4];
        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
        _ = fs.Read(buffer, 0, buffer.Length);
        byte[] pdfheader = [0x25, 0x50, 0x44, 0x46];
        return buffer?.SequenceEqual(pdfheader) == true;
    }

    public static async Task<int> PdfPageCountAsync(byte[] stream)
    {
        try
        {
            return stream?.Length == 0
                   ? throw new ArgumentNullException(nameof(stream), "stream can not be null or length zero")
                   : await Task.Run(
                () =>
                {
                    using MemoryStream ms = new(stream);
                    using PdfDocument pdfDoc = PdfDocument.Load(ms);
                    return (pdfDoc?.PageCount) ?? 0;
                });
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static void PrintImageSource(ImageSource Source, int Dpi = 300, bool resize = true)
    {
        PrintDialog pd = new();
        DrawingVisual dv = new();
        if (pd.ShowDialog() == true)
        {
            using (DrawingContext dc = dv.RenderOpen())
            {
                BitmapSource bs;
                if (resize)
                {
                    bs = Source.Width > Source.Height
                         ? ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, Dpi, Dpi)
                         : ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, Dpi, Dpi);
                    bs.Freeze();
                    dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                }
                else
                {
                    bs = (BitmapSource)Source;
                    bs.Freeze();
                    dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                }
            }

            pd.PrintVisual(dv, string.Empty);
        }
    }

    public static async Task<byte[]> ReadAllFileAsync(string filename)
    {
        try
        {
            using FileStream file = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            if (file != null)
            {
                byte[] buffer = new byte[file.Length];
                _ = await file.ReadAsync(buffer, 0, (int)file.Length);
                return buffer;
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        scrollvwr = GetTemplateChild("ScrollVwr") as ScrollViewer;
        if (scrollvwr != null)
        {
            scrollvwr.Drop -= Scrollvwr_Drop;
            scrollvwr.Drop += Scrollvwr_Drop;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Source = null;
            }

            disposedValue = true;
        }
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && pdfViewer.Source is not null && pdfViewer.Resize.CanExecute(null) && !DesignerProperties.GetIsInDesignMode(pdfViewer))
        {
            pdfViewer.Resize.Execute(null);
        }
    }

    private static async void DpiChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && pdfViewer.PdfFilePath is not null)
        {
            string pdfFilePath = pdfViewer.PdfFilePath;
            pdfViewer.Source = await ConvertToImgAsync(pdfFilePath, pdfViewer.Sayfa, (int)e.NewValue);
        }
    }

    private static async void PdfFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && IsValidPdfFile(e.NewValue as string))
        {
            try
            {
                using PdfDocument pdfDoc = PdfDocument.Load(e.NewValue as string);
                int dpi = pdfViewer.Dpi;
                pdfViewer.Sayfa = 1;
                int width = (int)(pdfDoc.PageSizes[pdfViewer.Sayfa - 1].Width / 72 * dpi);
                int height = (int)(pdfDoc.PageSizes[pdfViewer.Sayfa - 1].Height / 72 * dpi);
                pdfViewer.ToplamSayfa = pdfDoc.PageCount;
                pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
                pdfViewer.Source = await RenderPdf(pdfDoc, dpi, pdfViewer.Sayfa - 1, width, height);
            }
            catch (Exception)
            {
                pdfViewer.Source = null;
            }
        }
    }

    private static Task<BitmapSource> RenderPdf(PdfDocument pdfDoc, int dpi, int page, int width, int height)
    {
        return Task.Run(
            () =>
            {
                using Bitmap image = pdfDoc.Render(page, width, height, dpi, dpi, false) as Bitmap;
                IntPtr gdibitmap = image.GetHbitmap();
                BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(gdibitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                _ = Helpers.DeleteObject(gdibitmap);
                bitmapSource?.Freeze();
                return bitmapSource;
            });
    }

    private static async void SayfaChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && pdfViewer.ToplamSayfa > 0)
        {
            int sayfa = (int)e.NewValue;
            if (sayfa > pdfViewer.ToplamSayfa)
            {
                sayfa = pdfViewer.ToplamSayfa;
            }

            if (sayfa < 1)
            {
                sayfa = 1;
            }
            pdfViewer.Source = await ConvertToImgAsync(pdfViewer.PdfFilePath, sayfa, pdfViewer.Dpi);
        }
    }

    private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && e.NewValue is not null && pdfViewer.Resize.CanExecute(null))
        {
            pdfViewer.Resize.Execute(null);
        }
    }

    private void PdfViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SearchPdfMatch" && SearchPdfMatch is not null)
        {
            Sayfa = SearchPdfMatch.Page + 1;
        }
    }

    private void PdfViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AutoFitContent && Resize.CanExecute(null))
        {
            Resize.Execute(null);
        }
    }

    private void PrintPdf(PdfDocument document, int Dpi = 300)
    {
        PrintDialog pd = new() { PageRangeSelection = PageRangeSelection.AllPages, UserPageRangeEnabled = true, MaxPage = (uint)document.PageCount, MinPage = 1 };
        if (pd.ShowDialog() == true)
        {
            int startPage;
            int endPage;
            if (pd.PageRangeSelection == PageRangeSelection.AllPages)
            {
                startPage = 1;
                endPage = document.PageCount;
            }
            else
            {
                startPage = pd.PageRange.PageFrom;
                endPage = pd.PageRange.PageTo;
            }

            FixedDocument fixedDocument = new();
            for (int i = startPage; i <= endPage; i++)
            {
                RenderPageContents(document, Dpi, pd.PrintableAreaWidth, pd.PrintableAreaHeight, fixedDocument, i);
            }
            XpsDocumentWriter xpsWriter = PrintQueue.CreateXpsDocumentWriter(pd.PrintQueue);
            xpsWriter.WriteAsync(fixedDocument);
        }
    }

    private void PrintPdf(PdfDocument document, int startPage, int endPage, int Dpi = 300)
    {
        PrintDialog pd = new() { CurrentPageEnabled = true, PageRangeSelection = PageRangeSelection.CurrentPage, UserPageRangeEnabled = false, MaxPage = (uint)document.PageCount, MinPage = 1 };
        if (pd.ShowDialog() == true)
        {
            pd.PageRange = new PageRange(startPage, endPage);
            FixedDocument fixedDocument = new();
            for (int i = startPage; i <= endPage; i++)
            {
                RenderPageContents(document, Dpi, pd.PrintableAreaWidth, pd.PrintableAreaHeight, fixedDocument, i);
            }
            XpsDocumentWriter xpsWriter = PrintQueue.CreateXpsDocumentWriter(pd.PrintQueue);
            xpsWriter.WriteAsync(fixedDocument);
        }
    }

    private void RenderPageContents(PdfDocument pdfiumdocument, int Dpi, double printwidth, double printheight, FixedDocument fixedDocument, int pagenumber)
    {
        PageContent pageContent = new();
        FixedPage fixedPage = new();
        int width = (int)(pdfiumdocument.PageSizes[pagenumber - 1].Width / 72 * Dpi);
        int height = (int)(pdfiumdocument.PageSizes[pagenumber - 1].Height / 72 * Dpi);
        using Bitmap bitmap = pdfiumdocument.Render(pagenumber - 1, width, height, Dpi, Dpi, true) as Bitmap;
        BitmapImage bitmapimage = bitmap.ToBitmapImage(ImageFormat.Jpeg);
        bitmapimage.Freeze();

        System.Windows.Controls.Image image = new() { Source = bitmapimage };
        fixedPage.Width = width < height ? printwidth : printheight;
        fixedPage.Height = width > height ? printwidth : printheight;
        _ = fixedPage.Children.Add(image);
        fixedPage.SetValue(WidthProperty, fixedPage.Width);
        fixedPage.SetValue(HeightProperty, fixedPage.Height);

        ((IAddChild)pageContent).AddChild(fixedPage);
        _ = fixedDocument.Pages.Add(pageContent);
        GC.Collect();
    }

    private void Scrollvwr_Drop(object sender, DragEventArgs e)
    {
        string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (IsValidPdfFile(droppedfiles?[0]))
        {
            PdfFilePath = droppedfiles?[0];
        }
    }
}