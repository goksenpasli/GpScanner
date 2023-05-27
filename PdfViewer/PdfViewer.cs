using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;
using PdfiumViewer;
using static Extensions.ExtensionMethods;
using Control = System.Windows.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using PrintDialog = System.Windows.Controls.PrintDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PdfViewer;

public enum FitImageOrientation
{
    Width = 0,

    Height = 1
}

[TemplatePart(Name = "ScrollVwr", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "UpDown", Type = typeof(NumericUpDownControl))]
public class PdfViewer : Control, INotifyPropertyChanged, IDisposable
{
    public static readonly DependencyProperty AngleProperty =
        DependencyProperty.Register("Angle", typeof(double), typeof(PdfViewer), new PropertyMetadata(0.0));

    public static readonly DependencyProperty ContextMenuVisibilityProperty =
        DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty DpiProperty =
        DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200, DpiChanged));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        "Orientation",
        typeof(FitImageOrientation),
        typeof(PdfViewer),
        new PropertyMetadata(FitImageOrientation.Width, Changed));

    public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register(
        "PdfFilePath",
        typeof(string),
        typeof(PdfViewer),
        new PropertyMetadata(null, PdfFilePathChanged));

    public static readonly DependencyProperty ScrollBarVisibleProperty = DependencyProperty.Register(
        "ScrollBarVisible",
        typeof(ScrollBarVisibility),
        typeof(PdfViewer),
        new PropertyMetadata(ScrollBarVisibility.Auto));

    public static readonly DependencyProperty SeekingLowerPdfDpiProperty =
        DependencyProperty.Register("SeekingLowerPdfDpi", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty SeekingPdfDpiProperty =
            DependencyProperty.Register("SeekingPdfDpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200));

    public static readonly DependencyProperty SnapTickProperty =
        DependencyProperty.Register("SnapTick", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        "Source",
        typeof(ImageSource),
        typeof(PdfViewer),
        new PropertyMetadata(null, SourceChanged));

    public static readonly DependencyProperty ThumbsVisibleProperty =
        DependencyProperty.Register("ThumbsVisible", typeof(bool), typeof(PdfViewer), new PropertyMetadata(true));

    public static readonly DependencyProperty ToolBarVisibilityProperty =
        DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));

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
                PrintPdf(pdfDocument);
            },
            parameter => PdfFilePath is not null);

        ViewerBack = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    Sayfa = 1;
                    return;
                }

                Sayfa--;
            },
            parameter => Source is not null && Sayfa > 1 && Sayfa <= ToplamSayfa);

        ViewerNext = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    Sayfa = ToplamSayfa;
                    return;
                }

                Sayfa++;
            },
            parameter => Source is not null && Sayfa >= 1 && Sayfa < ToplamSayfa);

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
                        throw new ArgumentException("saveimage", ex);
                    }
                }
            },
            parameter => Source is not null);

        Resize = new RelayCommand<object>(
            delegate
            {
                if (Source is not null)
                {
                    Zoom = Orientation != FitImageOrientation.Width ? ActualHeight / Source.Height : ActualWidth / Source.Width;
                    if (Zoom == 0)
                    {
                        Zoom = 1;
                    }
                }
            },
            parameter => Source != null);

        ReadPdfText = new RelayCommand<object>(
            delegate
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfTextContent = pdfDocument.GetPdfText(Sayfa - 1);
            },
            parameter => Source != null);

        ReadPdfBookmarks = new RelayCommand<object>(
            delegate
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfBookmarks = pdfDocument.Bookmarks;
            },
            parameter => Source != null);

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
                PdfMatches = new ObservableCollection<PdfMatch>();
                foreach (PdfMatch match in matches.Items)
                {
                    PdfMatches.Add(match);
                }
            },
            parameter => Source != null && !string.IsNullOrWhiteSpace(SearchTextContent));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static int[] DpiList { get; } = { 12, 24, 36, 48, 72, 96, 120, 150, 200, 300, 400, 500, 600 };

    public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }

    public bool AutoFitContent {
        get => autoFitContent;

        set {
            if (autoFitContent != value)
            {
                autoFitContent = value;
                OnPropertyChanged(nameof(AutoFitContent));
            }
        }
    }

    public Visibility BookmarkContentVisibility {
        get => bookmarkContentVisibility;

        set {
            if (bookmarkContentVisibility != value)
            {
                bookmarkContentVisibility = value;
                OnPropertyChanged(nameof(BookmarkContentVisibility));
            }
        }
    }

    public Visibility ContextMenuVisibility {
        get => (Visibility)GetValue(ContextMenuVisibilityProperty);
        set => SetValue(ContextMenuVisibilityProperty, value);
    }

    public int CurrentDpi { get; set; }

    public string DefaultPrinter { get; set; } = LocalPrintServer.GetDefaultPrintQueue().FullName;

    public RelayCommand<object> DosyaAç { get; }

    public int Dpi { get => (int)GetValue(DpiProperty); set => SetValue(DpiProperty, value); }

    public Visibility DpiListVisibility {
        get => dpiListVisibility;

        set {
            if (dpiListVisibility != value)
            {
                dpiListVisibility = value;
                OnPropertyChanged(nameof(DpiListVisibility));
            }
        }
    }

    public RelayCommand<object> GoPdfBookMarkPage { get; }

    public bool MatchCase {
        get => matchCase;

        set {
            if (matchCase != value)
            {
                matchCase = value;
                OnPropertyChanged(nameof(MatchCase));
            }
        }
    }

    public Visibility OpenButtonVisibility {
        get => openButtonVisibility;

        set {
            if (openButtonVisibility != value)
            {
                openButtonVisibility = value;
                OnPropertyChanged(nameof(OpenButtonVisibility));
            }
        }
    }

    public FitImageOrientation Orientation { get => (FitImageOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    [Browsable(false)]
    public IEnumerable<int> Pages {
        get => pages;

        set {
            if (pages != value)
            {
                pages = value;
                OnPropertyChanged(nameof(Pages));
            }
        }
    }

    public PdfBookmarkCollection PdfBookmarks {
        get => pdfBookmarks;

        set {
            if (pdfBookmarks != value)
            {
                pdfBookmarks = value;
                OnPropertyChanged(nameof(PdfBookmarks));
            }
        }
    }

    public byte[] PdfData {
        get => pdfData;

        set {
            if (pdfData != value)
            {
                pdfData = value;
                OnPropertyChanged(nameof(PdfData));
            }
        }
    }

    public string PdfFilePath { get => (string)GetValue(PdfFilePathProperty); set => SetValue(PdfFilePathProperty, value); }

    public ObservableCollection<PdfMatch> PdfMatches {
        get => pdfMatches;

        set {
            if (pdfMatches != value)
            {
                pdfMatches = value;
                OnPropertyChanged(nameof(PdfMatches));
            }
        }
    }

    public string PdfTextContent {
        get => pdfTextContent;

        set {
            if (pdfTextContent != value)
            {
                pdfTextContent = value;
                OnPropertyChanged(nameof(PdfTextContent));
            }
        }
    }

    public Visibility PdfTextContentVisibility {
        get => pdfTextContentVisibility;

        set {
            if (pdfTextContentVisibility != value)
            {
                pdfTextContentVisibility = value;
                OnPropertyChanged(nameof(PdfTextContentVisibility));
            }
        }
    }

    public Visibility PrintButtonVisibility {
        get => printButtonVisibility;

        set {
            if (printButtonVisibility != value)
            {
                printButtonVisibility = value;
                OnPropertyChanged(nameof(PrintButtonVisibility));
            }
        }
    }

    public RelayCommand<object> ReadPdfBookmarks { get; }

    public RelayCommand<object> ReadPdfText { get; }

    public RelayCommand<object> Resize { get; }

    public ICommand SaveImage { get; }

    public int Sayfa {
        get => sayfa;

        set {
            if (sayfa != value)
            {
                sayfa = value;
                OnPropertyChanged(nameof(Sayfa));
            }
        }
    }

    public ScrollBarVisibility ScrollBarVisible {
        get => (ScrollBarVisibility)GetValue(ScrollBarVisibleProperty);
        set => SetValue(ScrollBarVisibleProperty, value);
    }

    public RelayCommand<object> ScrollToCurrentPage { get; }

    public PdfMatch SearchPdfMatch {
        get => searchPdfMatch;

        set {
            if (searchPdfMatch != value)
            {
                searchPdfMatch = value;
                OnPropertyChanged(nameof(SearchPdfMatch));
            }
        }
    }

    public RelayCommand<object> SearchPdfText { get; }

    public string SearchTextContent {
        get => searchTextContent;

        set {
            if (searchTextContent != value)
            {
                searchTextContent = value;
                OnPropertyChanged(nameof(SearchTextContent));
            }
        }
    }

    public Visibility SearchTextContentVisibility {
        get => searchTextContentVisibility;

        set {
            if (searchTextContentVisibility != value)
            {
                searchTextContentVisibility = value;
                OnPropertyChanged(nameof(SearchTextContentVisibility));
            }
        }
    }

    public bool SeekingLowerPdfDpi { get => (bool)GetValue(SeekingLowerPdfDpiProperty); set => SetValue(SeekingLowerPdfDpiProperty, value); }

    public int SeekingPdfDpi { get => (int)GetValue(SeekingPdfDpiProperty); set => SetValue(SeekingPdfDpiProperty, value); }

    public Visibility SliderZoomAngleVisibility {
        get => sliderZoomAngleVisibility;

        set {
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

    public Visibility TifNavigasyonButtonEtkin {
        get => tifNavigasyonButtonEtkin;

        set {
            if (tifNavigasyonButtonEtkin != value)
            {
                tifNavigasyonButtonEtkin = value;
                OnPropertyChanged(nameof(TifNavigasyonButtonEtkin));
            }
        }
    }

    public Visibility ToolBarVisibility { get => (Visibility)GetValue(ToolBarVisibilityProperty); set => SetValue(ToolBarVisibilityProperty, value); }

    [Browsable(false)]
    public int ToplamSayfa {
        get => toplamSayfa;

        set {
            if (toplamSayfa != value)
            {
                toplamSayfa = value;
                OnPropertyChanged(nameof(ToplamSayfa));
            }
        }
    }

    public RelayCommand<object> ViewerBack { get; }

    public RelayCommand<object> ViewerNext { get; }

    public bool WholeWord {
        get => wholeWord;

        set {
            if (wholeWord != value)
            {
                wholeWord = value;
                OnPropertyChanged(nameof(WholeWord));
            }
        }
    }

    public ICommand Yazdır { get; }

    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    public static async Task<BitmapSource> ConvertToImgAsync(string pdffilepath, int page, int dpi = 96)
    {
        try
        {
            return !File.Exists(pdffilepath)
                ? throw new ArgumentNullException(nameof(pdffilepath), "filepath can not be null")
                : await Task.Run(
                    () =>
                    {
                        using PdfDocument pdfDoc = PdfDocument.Load(pdffilepath);
                        int width = (int)(pdfDoc.PageSizes[page - 1].Width / 72 * dpi);
                        int height = (int)(pdfDoc.PageSizes[page - 1].Height / 72 * dpi);
                        using Bitmap bitmap = pdfDoc.Render(page - 1, width, height, dpi, dpi, false) as Bitmap;
                        BitmapSource bitmapImage = bitmap.ToBitmapSource();
                        bitmapImage.Freeze();
                        return bitmapImage;
                    });
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<MemoryStream> ConvertToImgStreamAsync(byte[] stream, int page, int dpi)
    {
        try
        {
            return stream?.Length == 0
                ? throw new ArgumentNullException(nameof(stream), "file can not be null or length zero")
                : await Task.Run(
                    () =>
                    {
                        using MemoryStream ms = new(stream);
                        using PdfDocument pdfDoc = PdfDocument.Load(ms);
                        int width = (int)(pdfDoc.PageSizes[page - 1].Width / 72 * dpi);
                        int height = (int)(pdfDoc.PageSizes[page - 1].Height / 72 * dpi);
                        System.Drawing.Image image = pdfDoc.Render(page - 1, width, height, dpi, dpi, false);
                        BitmapImage bitmapImage = image.ToBitmapImage(ImageFormat.Jpeg);
                        bitmapImage.Freeze();
                        return new MemoryStream(bitmapImage.ToTiffJpegByteArray(Format.Jpg));
                    });
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool IsValidPdfFile(string filename)
    {
        if (File.Exists(filename))
        {
            byte[] buffer = new byte[4];
            using FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
            int bytes_read = fs.Read(buffer, 0, buffer.Length);
            byte[] pdfheader = { 0x25, 0x50, 0x44, 0x46 };
            return buffer?.SequenceEqual(pdfheader) == true;
        }

        return false;
    }

    public static async Task<int> PdfPageCountAsync(byte[] stream)
    {
        try
        {
            return stream?.Length == 0
                ? throw new ArgumentNullException(nameof(stream), "file can not be null or length zero")
                : await Task.Run(
                    () =>
                    {
                        using MemoryStream ms = new(stream);
                        using PdfDocument pdfDoc = PdfDocument.Load(ms);
                        return pdfDoc.PageCount;
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
            byte[] buffer = new byte[file.Length];
            _ = await file.ReadAsync(buffer, 0, (int)file.Length);
            return buffer;
        }
        catch (Exception)
        {
            return null;
        }
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

        if (SeekingLowerPdfDpi)
        {
            updown = GetTemplateChild("UpDown") as NumericUpDownControl;
            if (updown != null)
            {
                updown.PreviewMouseLeftButtonUp -= UpDownMouseLeftButtonUp;
                updown.PreviewMouseLeftButtonUp += UpDownMouseLeftButtonUp;
            }
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

    protected virtual void OnPropertyChanged(string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

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

    private int sayfa = 1;

    private ScrollViewer scrollvwr;

    private PdfMatch searchPdfMatch;

    private string searchTextContent;

    private Visibility searchTextContentVisibility;

    private Visibility sliderZoomAngleVisibility = Visibility.Visible;

    private Visibility tifNavigasyonButtonEtkin = Visibility.Visible;

    private int toplamSayfa;

    private NumericUpDownControl updown;

    private bool wholeWord;

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && pdfViewer.Source is not null && !DesignerProperties.GetIsInDesignMode(pdfViewer))
        {
            pdfViewer.Resize.Execute(null);
        }
    }

    private static async void DpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && pdfViewer.PdfFilePath is not null)
        {
            string pdfFilePath = pdfViewer.PdfFilePath;
            pdfViewer.Source = await ConvertToImgAsync(pdfFilePath, pdfViewer.Sayfa, (int)e.NewValue);
            GC.Collect();
        }
    }

    private static void PdfFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer &&
            e.NewValue is not null &&
            File.Exists(e.NewValue as string) &&
            string.Equals(Path.GetExtension(e.NewValue as string), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                pdfViewer.Source = null;
                using PdfDocument pdfDoc = PdfDocument.Load(e.NewValue as string);
                int dpi = pdfViewer.Dpi;
                int page = pdfViewer.Sayfa - 1;
                int width = (int)(pdfDoc.PageSizes[page].Width / 72 * dpi);
                int height = (int)(pdfDoc.PageSizes[page].Height / 72 * dpi);
                using (System.Drawing.Image image = pdfDoc.Render(page, width, height, dpi, dpi, false))
                {
                    pdfViewer.Source = image.ToBitmapImage(ImageFormat.Jpeg);
                }

                pdfViewer.ToplamSayfa = pdfDoc.PageCount;
                pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
            }
            catch (Exception)
            {
                pdfViewer.Source = null;
            }
        }
    }

    private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && e.NewValue is not null)
        {
            pdfViewer.Resize.Execute(null);
        }
    }

    private async void PdfViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Sayfa" && sender is PdfViewer pdfViewer && pdfViewer.PdfFilePath is not null)
        {
            if (Sayfa > ToplamSayfa)
            {
                Sayfa = ToplamSayfa;
            }

            if (Sayfa < 1)
            {
                Sayfa = 1;
            }

            if (SeekingLowerPdfDpi && updown.FindVisualChildren<RepeatButton>().Any(z => z.IsMouseOver))
            {
                Dpi = Sayfa == 1 || Sayfa == ToplamSayfa ? SeekingPdfDpi : DpiList.Min();
            }

            string pdfFilePath = pdfViewer.PdfFilePath;
            Source = await ConvertToImgAsync(pdfFilePath, sayfa, pdfViewer.Dpi);
            GC.Collect();
        }

        if (e.PropertyName is "SearchPdfMatch" && SearchPdfMatch is not null)
        {
            Sayfa = SearchPdfMatch.Page + 1;
        }
    }

    private void PdfViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AutoFitContent)
        {
            Resize.Execute(null);
        }
    }

    private void PrintPdf(PdfDocument pdfDocument)
    {
        using System.Windows.Forms.PrintDialog form = new();
        using PrintDocument document = pdfDocument.CreatePrintDocument(PdfPrintMode.ShrinkToMargin);
        form.AllowSomePages = true;
        form.Document = document;
        form.UseEXDialog = true;
        form.Document.PrinterSettings.FromPage = 1;
        form.Document.PrinterSettings.ToPage = pdfDocument.PageCount;
        if (DefaultPrinter != null)
        {
            form.Document.PrinterSettings.PrinterName = DefaultPrinter;
        }

        if (form.ShowDialog() == DialogResult.OK)
        {
            try
            {
                if (form.Document.PrinterSettings.FromPage <= pdfDocument.PageCount)
                {
                    form.Document.Print();
                }
            }
            catch
            {
            }
        }
    }

    private void Scrollvwr_Drop(object sender, DragEventArgs e)
    {
        string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (droppedfiles?.Length > 0)
        {
            PdfFilePath = droppedfiles[0];
        }
    }

    private void UpDownMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SeekingLowerPdfDpi)
        {
            Dpi = SeekingPdfDpi;
        }
    }
}