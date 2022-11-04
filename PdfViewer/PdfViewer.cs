using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;
using Freeware;
using Microsoft.Win32;
using static Extensions.ExtensionMethods;

namespace PdfViewer
{
    public enum FitImageOrientation
    {
        Width = 0,

        Height = 1
    }

    public class PdfViewer : Control, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(PdfViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty ContextMenuVisibilityPropertyProperty = DependencyProperty.Register("ContextMenuVisibilityProperty", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(PdfViewer), new PropertyMetadata(200, DpiChanged));

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(FitImageOrientation), typeof(PdfViewer), new PropertyMetadata(FitImageOrientation.Width, Changed));

        public static readonly DependencyProperty PdfFilePathProperty = DependencyProperty.Register("PdfFilePath", typeof(string), typeof(PdfViewer), new PropertyMetadata(null, async (o, e) => await PdfFilePathChangedAsync(o, e)));

        public static readonly DependencyProperty ScrollBarVisibleProperty = DependencyProperty.Register("ScrollBarVisible", typeof(ScrollBarVisibility), typeof(PdfViewer), new PropertyMetadata(ScrollBarVisibility.Auto));

        public static readonly DependencyProperty SnapTickProperty = DependencyProperty.Register("SnapTick", typeof(bool), typeof(PdfViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(PdfViewer), new PropertyMetadata(null, SourceChanged));

        public static readonly DependencyProperty ToolBarVisibilityProperty = DependencyProperty.Register("ToolBarVisibility", typeof(Visibility), typeof(PdfViewer), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(PdfViewer), new PropertyMetadata(1.0));

        static PdfViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfViewer), new FrameworkPropertyMetadata(typeof(PdfViewer)));
        }

        public PdfViewer()
        {
            PropertyChanged += PdfViewer_PropertyChanged;
            Unloaded += PdfViewer_Unloaded;

            DosyaAç = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Pdf Dosyaları (*.pdf)|*.pdf" };
                if (openFileDialog.ShowDialog() == true)
                {
                    PdfFilePath = openFileDialog.FileName;
                }
            });

            Yazdır = new RelayCommand<object>(async parameter =>
            {
                string pdfFilePath = PdfFilePath;
                PrintPdfFile(await ReadAllFileAsync(pdfFilePath));
                GC.Collect();
            }, parameter => PdfFilePath is not null);

            ViewAllThumbnailImage = new RelayCommand<object>(async parameter =>
            {
                if (Thumbnails is null)
                {
                    Thumbnails = await ConvertToAllPageThumbImgAsync(await ReadAllFileAsync(PdfFilePath), 4);
                }
            }, parameter => PdfFilePath is not null);

            ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => Source is not null && Sayfa > 1 && Sayfa <= ToplamSayfa);

            ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => Source is not null && Sayfa >= 1 && Sayfa < ToplamSayfa);

            SaveImage = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Jpg Dosyası(*.jpg)|*.jpg",
                    FileName = "Resim"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, Source.ToTiffJpegByteArray(Format.Jpg));
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }, parameter => Source is not null);

            Resize = new RelayCommand<object>(delegate
            {
                Zoom = (Orientation != 0) ? (double.IsNaN(Height) ? ((ActualHeight == 0.0) ? 1.0 : (ActualHeight / Source.Height)) : ((Height == 0.0) ? 1.0 : (Height / Source.Height))) : (double.IsNaN(Width) ? ((ActualWidth == 0.0) ? 1.0 : (ActualWidth / Source.Width)) : ((Width == 0.0) ? 1.0 : (Width / Source.Width)));
            }, (object parameter) => Source != null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ThumbClass> AllPagesThumb
        {
            get => allPagesThumb;

            set
            {
                if (allPagesThumb != value)
                {
                    allPagesThumb = value;
                    OnPropertyChanged(nameof(AllPagesThumb));
                }
            }
        }

        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public Visibility ContextMenuVisibilityProperty
        {
            get => (Visibility)GetValue(ContextMenuVisibilityPropertyProperty);
            set => SetValue(ContextMenuVisibilityPropertyProperty, value);
        }

        public RelayCommand<object> DosyaAç { get; }

        public int Dpi
        {
            get => (int)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        public int[] DpiList { get; } = new int[] { 12, 24, 36, 72, 96, 120, 150, 200, 300, 400, 500, 600 };

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

        public FitImageOrientation Orientation
        {
            get { return (FitImageOrientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

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

        public string PdfFilePath
        {
            get => (string)GetValue(PdfFilePathProperty);
            set => SetValue(PdfFilePathProperty, value);
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

        public RelayCommand<object> Resize { get; }

        public ICommand SaveImage { get; }

        public int Sayfa
        {
            get => sayfa;

            set
            {
                if (sayfa != value)
                {
                    sayfa = value;
                    OnPropertyChanged(nameof(Sayfa));
                }
            }
        }

        public ScrollBarVisibility ScrollBarVisible
        {
            get => (ScrollBarVisibility)GetValue(ScrollBarVisibleProperty);
            set => SetValue(ScrollBarVisibleProperty, value);
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

        public bool SnapTick
        {
            get => (bool)GetValue(SnapTickProperty);
            set => SetValue(SnapTickProperty, value);
        }

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public ObservableCollection<ThumbClass> Thumbnails
        {
            get => thumbnails;

            set
            {
                if (thumbnails != value)
                {
                    thumbnails = value;
                    OnPropertyChanged(nameof(Thumbnails));
                }
            }
        }

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

        public Visibility ToolBarVisibility
        {
            get => (Visibility)GetValue(ToolBarVisibilityProperty);
            set => SetValue(ToolBarVisibilityProperty, value);
        }

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

        public RelayCommand<object> ViewAllThumbnailImage { get; }

        public RelayCommand<object> ViewerBack { get; }

        public RelayCommand<object> ViewerNext { get; }

        public ICommand Yazdır { get; }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public static BitmapImage ConvertToImg(byte[] pdffilestream, int page, int dpi)
        {
            try
            {
                if (pdffilestream.Length > 0)
                {
                    byte[] buffer = Pdf2Png.Convert(pdffilestream, page, dpi);
                    BitmapImage bitmapImage = BitmapSourceFromByteArray(buffer);
                    pdffilestream = null;
                    buffer = null;
                    GC.Collect();
                    return bitmapImage;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public static async Task<BitmapImage> ConvertToImgAsync(byte[] pdffilestream, int page, int dpi)
        {
            try
            {
                if (pdffilestream.Length > 0)
                {
                    return await Task.Run(() =>
                        {
                            byte[] buffer = Pdf2Png.Convert(pdffilestream, page, dpi);
                            BitmapImage bitmapImage = BitmapSourceFromByteArray(buffer);
                            pdffilestream = null;
                            buffer = null;
                            GC.Collect();
                            return bitmapImage;
                        });
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public static async Task<MemoryStream> ConvertToImgStreamAsync(byte[] stream, int page, int dpi)
        {
            try
            {
                if (stream.Length > 0)
                {
                    return await Task.Run(() => new MemoryStream(Pdf2Png.Convert(stream, page, dpi)));
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public static async Task<int> PdfPageCountAsync(byte[] stream)
        {
            try
            {
                if (stream.Length > 0)
                {
                    return await Task.Run(() => Pdf2Png.ConvertAllPages(stream, 0).Count);
                }
            }
            catch (Exception)
            {
            }
            return 0;
        }

        public static void PrintImageSource(ImageSource Source, int Dpi = 300)
        {
            PrintDialog pd = new();
            DrawingVisual dv = new();
            if (pd.ShowDialog() == true)
            {
                using (DrawingContext dc = dv.RenderOpen())
                {
                    BitmapSource bs = Source.Width > Source.Height
                        ? ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, Dpi, Dpi)
                        : ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, Dpi, Dpi);
                    bs.Freeze();
                    dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                }
                pd.PrintVisual(dv, "");
            }
        }

        public static async Task<byte[]> ReadAllFileAsync(string filename)
        {
            try
            {
                using var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                byte[] buffer = new byte[file.Length];
                await file.ReadAsync(buffer, 0, (int)file.Length);
                return buffer;
            }
            catch (Exception)
            {
            }
            return null;
        }

        public async Task<ObservableCollection<ThumbClass>> ConvertToAllPageThumbImgAsync(byte[] pdffilestream, int dpi)
        {
            try
            {
                if (pdffilestream?.Length > 0)
                {
                    SynchronizationContext uiContext = SynchronizationContext.Current;
                    var pagecount = await PdfPageCountAsync(pdffilestream);
                    Thumbnails = new ObservableCollection<ThumbClass>();
                    await Task.Run(async () =>
                    {
                        for (int i = 1; i <= pagecount; i++)
                        {
                            BitmapImage bitmapImage = await PdfViewer.ConvertToImgAsync(pdffilestream, i, dpi);
                            uiContext.Send(_ =>
                            {
                                Thumbnails?.Add(new ThumbClass() { Page = i, Thumb = bitmapImage });
                            }, null);
                            bitmapImage = null;
                        }
                        pdffilestream = null;
                    });
                    return Thumbnails;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Source = null;
                    Thumbnails = null;
                }
                disposedValue = true;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<ThumbClass> allPagesThumb;

        private bool disposedValue;

        private Visibility dpiListVisibility = Visibility.Visible;

        private Visibility openButtonVisibility = Visibility.Collapsed;

        private IEnumerable<int> pages;

        private Visibility printButtonVisibility = Visibility.Collapsed;

        private int sayfa = 1;

        private Visibility sliderZoomAngleVisibility = Visibility.Visible;

        private ObservableCollection<ThumbClass> thumbnails;

        private Visibility tifNavigasyonButtonEtkin = Visibility.Visible;

        private int toplamSayfa;

        private static BitmapImage BitmapSourceFromByteArray(byte[] buffer)
        {
            if (buffer != null)
            {
                BitmapImage bitmap = new();
                using MemoryStream stream = new(buffer);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                buffer = null;
                return bitmap;
            }
            return null;
        }

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer)
            {
                pdfViewer.Resize.Execute(null);
            }
        }

        private static async void DpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer && pdfViewer.PdfFilePath is not null)
            {
                string pdfFilePath = pdfViewer.PdfFilePath;
                pdfViewer.Source = await ConvertToImgAsync(await ReadAllFileAsync(pdfFilePath), pdfViewer.Sayfa, (int)e.NewValue);
                GC.Collect();
            }
        }

        private static async Task PdfFilePathChangedAsync(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfViewer pdfViewer)
            {
                if (e.NewValue is not null && File.Exists(e.NewValue as string) && string.Equals(Path.GetExtension(e.NewValue as string), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var data = await ReadAllFileAsync(e.NewValue as string);
                        pdfViewer.Sayfa = 1;
                        int dpi = pdfViewer.Dpi;
                        pdfViewer.Source = await ConvertToImgAsync(data, 1, dpi);
                        pdfViewer.ToplamSayfa = await PdfPageCountAsync(data);
                        pdfViewer.Pages = Enumerable.Range(1, pdfViewer.ToplamSayfa);
                    }
                    catch (Exception)
                    {
                    }
                }
                else
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
                string pdfFilePath = pdfViewer.PdfFilePath;
                Source = await ConvertToImgAsync(await ReadAllFileAsync(pdfFilePath), sayfa, pdfViewer.Dpi);
                GC.Collect();
            }
        }

        private void PdfViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose(true);
        }

        private async void PrintPdfFile(byte[] stream, int Dpi = 300)
        {
            int pagecount = await PdfPageCountAsync(stream);
            PrintDialog pd = new()
            {
                PageRangeSelection = PageRangeSelection.AllPages,
                UserPageRangeEnabled = true,
                MaxPage = (uint)pagecount,
                MinPage = 1
            };
            DrawingVisual dv = new();
            if (pd.ShowDialog() == true)
            {
                int başlangıç;
                int bitiş;
                if (pd.PageRangeSelection == PageRangeSelection.AllPages)
                {
                    başlangıç = 1;
                    bitiş = pagecount;
                }
                else
                {
                    başlangıç = pd.PageRange.PageFrom;
                    bitiş = pd.PageRange.PageTo;
                }

                for (int i = başlangıç; i <= bitiş; i++)
                {
                    using (DrawingContext dc = dv.RenderOpen())
                    {
                        BitmapImage bitmapimage = await ConvertToImgAsync(stream, i, Dpi);
                        BitmapSource bs = bitmapimage.Width > bitmapimage.Height
                        ? bitmapimage?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, Dpi, Dpi)
                        : bitmapimage?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, Dpi, Dpi);
                        bs.Freeze();
                        dc.DrawImage(bs, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                        bitmapimage = null;
                        bs = null;
                    }
                    pd.PrintVisual(dv, "");
                }
            }
        }
    }
}