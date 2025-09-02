using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Xps;
using Extensions;
using PdfiumViewer;
using static Extensions.ExtensionMethods;
using Control = System.Windows.Controls.Control;
using ListBox = System.Windows.Controls.ListBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using PrintDialog = System.Windows.Controls.PrintDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PdfViewer;

public partial class PdfViewer : Control, INotifyPropertyChanged, IDisposable
{
    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(PdfViewer), new PropertyMetadata(0.0));
    public static readonly DependencyProperty BookmarkContentVisibilityProperty = DependencyProperty.Register("BookmarkContentVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty ContextMenuVisibilityProperty = DependencyProperty.Register("ContextMenuVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));
    public static readonly DependencyProperty DpiListVisibilityProperty = DependencyProperty.Register("DpiListVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200, DpiChangedAsync));
    public static readonly DependencyProperty MatchCaseProperty = DependencyProperty.Register("MatchCase", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));
    public static readonly DependencyProperty OpenButtonVisibilityProperty = DependencyProperty.Register("OpenButtonVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(FitImageOrientation), typeof(PdfViewer), new PropertyMetadata(FitImageOrientation.Width, Changed));
    public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, PdfFilePathChanged));
    public static readonly DependencyProperty PdfScrollBarVisibilityProperty = DependencyProperty.Register("PdfScrollBarVisibility", typeof(ScrollBarVisibility), typeof(PdfViewer), new PropertyMetadata(ScrollBarVisibility.Auto));
    public static readonly DependencyProperty PdfTextContentVisibilityProperty = DependencyProperty.Register("PdfTextContentVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty PrintButtonVisibilityProperty = DependencyProperty.Register("PrintButtonVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));
    public static readonly DependencyProperty PrintDpiProperty = DependencyProperty.Register("PrintDpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(300));
    public static readonly DependencyProperty PrintDpiSettingsListEnabledProperty = DependencyProperty.Register("PrintDpiSettingsListEnabled", typeof(bool), typeof(PdfViewer), new PropertyMetadata(true));
    public static readonly DependencyProperty SayfaProperty = DependencyProperty.Register("Sayfa", typeof(int), typeof(PdfViewer), new PropertyMetadata(1, SayfaChangedAsync));
    public static readonly DependencyProperty SearchTextContentVisibilityProperty = DependencyProperty.Register("SearchTextContentVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty SliderZoomAngleVisibilityProperty = DependencyProperty.Register("SliderZoomAngleVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty SnapTickProperty = DependencyProperty.Register("SnapTick", typeof(bool), typeof(PdfViewer), new PropertyMetadata(true));
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(PdfViewer), new PropertyMetadata(null, SourceChanged));
    public static readonly DependencyProperty TifNavigasyonButtonEtkinProperty = DependencyProperty.Register("TifNavigasyonButtonEtkin", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty ToolBarVisibilityProperty = DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty UpDownButtonVisibilityProperty = DependencyProperty.Register("UpDownButtonVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty WholeWordProperty = DependencyProperty.Register("WholeWord", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));
    public static readonly DependencyProperty ZoomEnabledProperty = DependencyProperty.Register("ZoomEnabled", typeof(bool), typeof(PdfViewer), new PropertyMetadata(true));
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));
    private bool disposedValue;

    static PdfViewer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfViewer), new FrameworkPropertyMetadata(typeof(PdfViewer))); }

    public PdfViewer()
    {
        PropertyChanged += PdfViewer_PropertyChanged;
        SizeChanged += PdfViewer_SizeChanged;
        SpeechViewModel = new SpeechViewModel(this);

        DosyaAç = new RelayCommand<object>(
            async parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Pdf Dosyaları (*.pdf)|*.pdf" };
                if (openFileDialog.ShowDialog() == true && await IsValidPdfFileAsync(openFileDialog.FileName))
                {
                    PdfFilePath = openFileDialog.FileName;
                }
            });

        Yazdır = new RelayCommand<object>(
            async parameter =>
            {
                if (!File.Exists(PdfFilePath))
                {
                    return;
                }

                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);

                PrintDialog printdialog = new() { PageRangeSelection = PageRangeSelection.AllPages, UserPageRangeEnabled = true, MaxPage = (uint)pdfDocument.PageCount, MinPage = 1 };

                if (printdialog.ShowDialog() != true)
                {
                    return;
                }

                int startPage, endPage;

                if (printdialog.PageRangeSelection == PageRangeSelection.AllPages)
                {
                    startPage = 1;
                    endPage = pdfDocument.PageCount;
                }
                else
                {
                    startPage = printdialog.PageRange.PageFrom;
                    endPage = printdialog.PageRange.PageTo;
                    printdialog.PageRange = new PageRange(startPage, endPage);
                }
                int printDpi = PrintDpi;
                await Task.Run(
                    async () =>
                    {
                        FixedDocument fixedDoc = await RenderPageContents(printdialog, pdfDocument, startPage, endPage, printDpi);
                        Application.Current.Dispatcher
                        .Invoke(
                            () =>
                            {
                                XpsDocumentWriter xpsWriter = PrintQueue.CreateXpsDocumentWriter(printdialog.PrintQueue);
                                xpsWriter.Write(fixedDoc, printdialog.PrintTicket);
                            });
                    });
            },
            parameter => File.Exists(PdfFilePath));

        PrintSinglePage = new RelayCommand<object>(
            async parameter =>
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PrintDialog printdialog = new() { CurrentPageEnabled = true, PageRangeSelection = PageRangeSelection.CurrentPage, UserPageRangeEnabled = false, MaxPage = (uint)pdfDocument.PageCount, MinPage = 1 };
                if (printdialog.ShowDialog() == true)
                {
                    await GenerateDocument(printdialog, pdfDocument, (int)parameter, (int)parameter, PrintDpi);
                }
            },
            parameter => File.Exists(PdfFilePath));

        DeleteSinglePage = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = "Dosya" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                        int selectedpage = (int)parameter - 1;
                        pdfDocument.DeletePage(selectedpage);
                        pdfDocument.Save(saveFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex?.Message);
                    }
                }
            },
            parameter => Pages?.Count() > 1 && File.Exists(PdfFilePath));

        SaveImage = new RelayCommand<object>(
            async parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Jpg Dosyası(*.jpg)|*.jpg", FileName = "Resim" };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        byte[] image = (await ConvertToImgAsync(PdfFilePath, (int)parameter, PrintDpi)).ToTiffJpegByteArray(Format.Jpg);
                        File.WriteAllBytes(saveFileDialog.FileName, image);
                        image = null;
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex?.Message);
                    }
                }
            },
            parameter => File.Exists(PdfFilePath));

        Resize = new RelayCommand<object>(
            parameter =>
            {
                double zoomFactor = Orientation != FitImageOrientation.Width ? ActualHeight / Source.Height : ActualWidth / Source.Width;
                Zoom = Math.Truncate(zoomFactor * 100) / 100 != 0 ? Math.Truncate(zoomFactor * 100) / 100 : 1;
            },
            parameter => Source is not null && File.Exists(PdfFilePath));

        ZoomIncrease = new RelayCommand<object>(parameter => Zoom = Math.Min(MaxZoom, Zoom + ZoomIncreaseLevel), parameter => true);

        ZoomDecrease = new RelayCommand<object>(parameter => Zoom = Math.Max(MinZoom, Zoom - ZoomIncreaseLevel), parameter => true);

        PageIncrease = new RelayCommand<object>(parameter => Sayfa = Math.Min(ToplamSayfa, Sayfa + 1), parameter => Sayfa < ToplamSayfa);

        PageDecrease = new RelayCommand<object>(parameter => Sayfa = Math.Max(0, Sayfa - 1), parameter => Sayfa > 1);

        ReadPdfText = new RelayCommand<object>(
            parameter =>
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfAllPagesContent = [];
                for (int i = 0; i < pdfDocument.PageCount; i++)
                {
                    PdfAllPagesContent.Add(new Dictionary<int, string> { { i + 1, pdfDocument.GetPdfText(i) } });
                }
            },
            parameter => File.Exists(PdfFilePath));

        ReadPdfBookmarks = new RelayCommand<object>(
            parameter =>
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfBookmarks = pdfDocument.Bookmarks;
            },
            parameter => File.Exists(PdfFilePath));

        GoPdfBookMarkPage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is int pagenumber)
                {
                    Sayfa = pagenumber + 1;
                }
            },
            parameter => File.Exists(PdfFilePath));

        ScrollToCurrentPage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    listBox.ScrollIntoView(Sayfa);
                }
            },
            parameter => File.Exists(PdfFilePath));

        SearchPdfText = new RelayCommand<object>(
            parameter =>
            {
                using PdfDocument pdfDocument = PdfDocument.Load(PdfFilePath);
                PdfMatches matches = pdfDocument.Search(SearchTextContent, MatchCase, WholeWord);
                PdfMatches = [.. matches.Items];
            },
            parameter => !string.IsNullOrWhiteSpace(SearchTextContent) && File.Exists(PdfFilePath));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static IEnumerable<PdfCharacterInformation> CharacterInformations { get; private set; }

    public static int[] DpiList { get; } = [12, 24, 36, 48, 72, 96, 120, 150, 200, 300, 400, 500, 600, 1200];

    public double Angle { get => (double)GetValue(AngleProperty); set => SetValue(AngleProperty, value); }

    public bool AutoFitContent
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AutoFitContent));
            }
        }
    }

    public Visibility BookmarkContentVisibility { get => (Visibility)GetValue(BookmarkContentVisibilityProperty); set => SetValue(BookmarkContentVisibilityProperty, value); }

    public Visibility ContextMenuVisibility { get => (Visibility)GetValue(ContextMenuVisibilityProperty); set => SetValue(ContextMenuVisibilityProperty, value); }

    public RelayCommand<object> DeleteSinglePage { get; }

    public RelayCommand<object> DosyaAç { get; }

    public int Dpi { get => (int)GetValue(DpiProperty); set => SetValue(DpiProperty, value); }

    public Visibility DpiListVisibility { get => (Visibility)GetValue(DpiListVisibilityProperty); set => SetValue(DpiListVisibilityProperty, value); }

    public RelayCommand<object> GoPdfBookMarkPage { get; }

    public bool MatchCase { get => (bool)GetValue(MatchCaseProperty); set => SetValue(MatchCaseProperty, value); }

    public double MaxZoom { get; } = 10;

    public double MinZoom { get; } = 0.01;

    public Visibility OpenButtonVisibility { get => (Visibility)GetValue(OpenButtonVisibilityProperty); set => SetValue(OpenButtonVisibilityProperty, value); }

    public FitImageOrientation Orientation { get => (FitImageOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    public RelayCommand<object> PageDecrease { get; }

    public RelayCommand<object> PageIncrease { get; }

    [Browsable(false)]
    public IEnumerable<int> Pages
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Pages));
            }
        }
    }

    public ObservableCollection<Dictionary<int, string>> PdfAllPagesContent
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfAllPagesContent));
            }
        }
    }

    public PdfBookmarkCollection PdfBookmarks
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfBookmarks));
            }
        }
    }

    public string PdfFilePath { get => (string)GetValue(PdfFilePathProperty); set => SetValue(PdfFilePathProperty, value); }

    public ObservableCollection<PdfMatch> PdfMatches
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfMatches));
            }
        }
    }

    public ScrollBarVisibility PdfScrollBarVisibility { get => (ScrollBarVisibility)GetValue(PdfScrollBarVisibilityProperty); set => SetValue(PdfScrollBarVisibilityProperty, value); }

    public string PdfTextContent
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfTextContent));
            }
        }
    }

    public Visibility PdfTextContentVisibility { get => (Visibility)GetValue(PdfTextContentVisibilityProperty); set => SetValue(PdfTextContentVisibilityProperty, value); }

    public Visibility PrintButtonVisibility { get => (Visibility)GetValue(PrintButtonVisibilityProperty); set => SetValue(PrintButtonVisibilityProperty, value); }

    public int PrintDpi { get => (int)GetValue(PrintDpiProperty); set => SetValue(PrintDpiProperty, value); }

    public bool PrintDpiSettingsListEnabled { get => (bool)GetValue(PrintDpiSettingsListEnabledProperty); set => SetValue(PrintDpiSettingsListEnabledProperty, value); }

    public RelayCommand<object> PrintSinglePage { get; }

    public RelayCommand<object> ReadPdfBookmarks { get; }

    public RelayCommand<object> ReadPdfText { get; }

    public RelayCommand<object> Resize { get; }

    public ICommand SaveImage { get; }

    public int Sayfa { get => (int)GetValue(SayfaProperty); set => SetValue(SayfaProperty, value); }

    public RelayCommand<object> ScrollToCurrentPage { get; }

    public PdfMatch SearchPdfMatch
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SearchPdfMatch));
            }
        }
    }

    public RelayCommand<object> SearchPdfText { get; }

    public string SearchTextContent
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SearchTextContent));
            }
        }
    }

    public Visibility SearchTextContentVisibility { get => (Visibility)GetValue(SearchTextContentVisibilityProperty); set => SetValue(SearchTextContentVisibilityProperty, value); }

    public Visibility SliderZoomAngleVisibility { get => (Visibility)GetValue(SliderZoomAngleVisibilityProperty); set => SetValue(SliderZoomAngleVisibilityProperty, value); }

    public bool SnapTick { get => (bool)GetValue(SnapTickProperty); set => SetValue(SnapTickProperty, value); }

    public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public SpeechViewModel SpeechViewModel { get; set; }

    public Visibility TifNavigasyonButtonEtkin { get => (Visibility)GetValue(TifNavigasyonButtonEtkinProperty); set => SetValue(TifNavigasyonButtonEtkinProperty, value); }

    public Visibility ToolBarVisibility { get => (Visibility)GetValue(ToolBarVisibilityProperty); set => SetValue(ToolBarVisibilityProperty, value); }

    [Browsable(false)]
    public int ToplamSayfa
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ToplamSayfa));
            }
        }
    }

    public Visibility UpDownButtonVisibility { get => (Visibility)GetValue(UpDownButtonVisibilityProperty); set => SetValue(UpDownButtonVisibilityProperty, value); }

    public bool WholeWord { get => (bool)GetValue(WholeWordProperty); set => SetValue(WholeWordProperty, value); }

    public ICommand Yazdır { get; }

    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    public RelayCommand<object> ZoomDecrease { get; }

    public bool ZoomEnabled { get => (bool)GetValue(ZoomEnabledProperty); set => SetValue(ZoomEnabledProperty, value); }

    public RelayCommand<object> ZoomIncrease { get; }

    public double ZoomIncreaseLevel { get; } = 0.1;

    public static async Task<BitmapImage> ConvertToImgAsync(string pdffilepath, int page, int dpi = 72)
    {
        if (string.IsNullOrEmpty(pdffilepath) || !await IsValidPdfFileAsync(pdffilepath))
        {
            throw new ArgumentException("Invalid PDF file", nameof(pdffilepath));
        }
        try
        {
            return await Task.Run(
                () =>
                {
                    using PdfDocument pdfDoc = PdfDocument.Load(pdffilepath);
                    if (pdfDoc?.PageCount < page)
                    {
                        return null;
                    }
                    int width = (int)(pdfDoc.PageSizes[page - 1].Width / 96 * dpi);
                    int height = (int)(pdfDoc.PageSizes[page - 1].Height / 96 * dpi);
                    using Bitmap bitmap = pdfDoc.Render(page - 1, width, height, dpi, dpi, false) as Bitmap;
                    BitmapImage bitmapImage = bitmap.ToBitmapImage(ImageFormat.Jpeg);
                    if (bitmapImage is null)
                    {
                        return null;
                    }
                    bitmapImage.Freeze();
                    try
                    {
                        CharacterInformations = pdfDoc.GetCharacterInformation(page - 1);
                    }
                    catch (Exception)
                    {
                    }
                    return bitmapImage;
                })
            .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Task<BitmapSource> ConvertToImgAsync(PdfDocument pdfDoc, int dpi, int page, int width, int height)
    {
        return Task.Run(
            () =>
            {
                if (pdfDoc.Render(page, width, height, dpi, dpi, false) is not Bitmap image)
                {
                    return null;
                }
                IntPtr gdibitmap = image.GetHbitmap();
                BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(gdibitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                _ = Helpers.DeleteObject(gdibitmap);
                bitmapSource?.Freeze();
                return bitmapSource;
            });
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
                    int width = (int)(pdfDoc.PageSizes[page - 1].Width / 96 * dpi);
                    int height = (int)(pdfDoc.PageSizes[page - 1].Height / 96 * dpi);
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

    public static async Task GenerateDocument(PrintDialog pd, PdfDocument document, int startPage, int endPage, int Dpi)
    {
        FixedDocument fixeddocument = await RenderPageContents(pd, document, startPage, endPage, Dpi);
        XpsDocumentWriter xpsWriter = PrintQueue.CreateXpsDocumentWriter(pd.PrintQueue);
        xpsWriter.WriteAsync(fixeddocument, pd.PrintTicket);
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

    public static int PdfPageCount(string pdffile)
    {
        try
        {
            if (!IsValidPdfFile(pdffile))
            {
                return 0;
            }
            using PdfDocument pdfDoc = PdfDocument.Load(pdffile);
            return pdfDoc.PageCount;
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
                    bs?.Freeze();
                    dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                }
                else
                {
                    bs = (BitmapSource)Source;
                    bs?.Freeze();
                    dc.DrawImage(bs, new Rect(0, 0, bs.PixelWidth, bs.PixelHeight));
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
            if (file is not null)
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

    private static async Task<bool> IsValidPdfFileAsync(string filename)
    {
        if (!File.Exists(filename))
        {
            return false;
        }

        byte[] buffer = new byte[4];
        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
        int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
        byte[] pdfheader = [0x25, 0x50, 0x44, 0x46];
        return bytesRead == buffer.Length && buffer.SequenceEqual(pdfheader);
    }

    private static async void PdfFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer)
        {
            if (await IsValidPdfFileAsync(e.NewValue as string))
            {
                try
                {
                    using PdfDocument pdfDoc = PdfDocument.Load(e.NewValue as string);
                    int dpi = pdfViewer.Dpi;
                    pdfViewer.Sayfa = 1;
                    int width = (int)(pdfDoc.PageSizes[pdfViewer.Sayfa - 1].Width / 96 * dpi);
                    int height = (int)(pdfDoc.PageSizes[pdfViewer.Sayfa - 1].Height / 96 * dpi);
                    pdfViewer.ToplamSayfa = pdfDoc.PageCount;
                    pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
                    pdfViewer.Source = await ConvertToImgAsync(pdfDoc, dpi, pdfViewer.Sayfa - 1, width, height);
                }
                catch (Exception)
                {
                    pdfViewer.Source = null;
                }
            }
            else
            {
                pdfViewer.SpeechViewModel?.Dur?.Execute(null);
            }
        }
    }

    private static async Task<FixedDocument> RenderPageContents(PrintDialog printdialog, PdfDocument pdfiumdocument, int start, int end, int Dpi)
    {
        FixedDocument fixedDocument = null;
        _ = await Application.Current.Dispatcher.InvokeAsync(() => fixedDocument = new());
        BitmapImage bitmapimage = null;
        for (int i = start; i <= end; i++)
        {
            await Task.Run(
                () =>
                {
                    SizeF pageSize = pdfiumdocument.PageSizes[i - 1];
                    using Bitmap bitmap = pdfiumdocument.Render(i - 1, (int)(pageSize.Width / 96 * Dpi), (int)(pageSize.Height / 96 * Dpi), Dpi, Dpi, PdfRenderFlags.ForPrinting) as Bitmap;
                    if (pageSize.Width > pageSize.Height)
                    {
                        bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    }
                    bitmapimage = bitmap.ToBitmapImage(ImageFormat.Jpeg);
                    bitmapimage.Freeze();
                });
            await Application.Current.Dispatcher
            .InvokeAsync(
                () =>
                {
                    FixedPage fixedPage = new() { Width = printdialog.PrintableAreaWidth, Height = printdialog.PrintableAreaHeight };
                    System.Windows.Controls.Image image = new() { Source = bitmapimage, Width = printdialog.PrintableAreaWidth, Height = printdialog.PrintableAreaHeight };
                    _ = fixedPage.Children.Add(image);
                    PageContent pageContent = new();
                    ((IAddChild)pageContent).AddChild(fixedPage);
                    _ = fixedDocument.Pages.Add(pageContent);
                });
        }
        return fixedDocument;
    }

    private static async void SayfaChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdfViewer pdfViewer && pdfViewer.ToplamSayfa > 0)
        {
            if (pdfViewer.PdfFilePath is null)
            {
                return;
            }
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
        if (e.PropertyName is "PdfTextContent")
        {
            SpeechViewModel?.Dur?.Execute(null);
        }
    }

    private void PdfViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AutoFitContent && Resize.CanExecute(null))
        {
            Resize.Execute(null);
        }
    }
}