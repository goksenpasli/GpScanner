using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Extensions
{
    public enum FitImageOrientation
    {
        Width = 0,

        Height = 1
    }

    public class ImageViewer : Control, INotifyPropertyChanged
    {
        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(ImageViewer), new PropertyMetadata(0.0));

        public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.Register("DecodeHeight", typeof(int), typeof(ImageViewer), new PropertyMetadata(300, DecodeHeightChanged));

        public static readonly DependencyProperty ImageFilePathProperty = DependencyProperty.Register("ImageFilePath", typeof(string), typeof(ImageViewer), new PropertyMetadata(null, ImageFilePathChanged));

        public static readonly DependencyProperty SnapTickProperty = DependencyProperty.Register("SnapTick", typeof(bool), typeof(ImageViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(ImageViewer), new PropertyMetadata(null, SourceChanged));

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(ImageViewer), new PropertyMetadata(1.0));

        static ImageViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageViewer), new FrameworkPropertyMetadata(typeof(ImageViewer)));
        }

        public ImageViewer()
        {
            DosyaAç = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Resim Dosyaları (*.jpg;*.jpeg;*.tif;*.tiff;*.png)|*.jpg;*.jpeg;*.tif;*.tiff;*.png" };
                if (openFileDialog.ShowDialog() == true)
                {
                    ImageFilePath = openFileDialog.FileName;
                }
            });

            ViewerBack = new RelayCommand<object>(parameter =>
            {
                Sayfa--;
                Source = Decoder.Frames[Sayfa - 1];
            }, parameter => Decoder != null && Sayfa > 1 && Sayfa <= Decoder.Frames.Count);

            ViewerNext = new RelayCommand<object>(parameter =>
            {
                Sayfa++;
                Source = Decoder.Frames[Sayfa - 1];
            }, parameter => Decoder != null && Sayfa >= 1 && Sayfa < Decoder.Frames.Count);

            Resize = new RelayCommand<object>(parameter =>
            {
                Zoom = FitImageOrientation == FitImageOrientation.Width
                    ? !double.IsNaN(Width) ? Width == 0 ? 1 : Width / Source.Width : ActualWidth == 0 ? 1 : ActualWidth / Source.Width
                    : !double.IsNaN(Height) ? Height == 0 ? 1 : Height / Source.Height : ActualHeight == 0 ? 1 : ActualHeight / Source.Height;
            }, parameter => Source is not null);

            OrijinalResimDosyaAç = new RelayCommand<object>(parameter => _ = Process.Start(parameter as string), parameter => !DesignerProperties.GetIsInDesignMode(new DependencyObject()) && File.Exists(parameter as string));

            Yazdır = new RelayCommand<object>(parameter =>
            {
                PrintDialog pd = new();
                DrawingVisual dv = new();
                if (Decoder == null)
                {
                    if (pd.ShowDialog() == true)
                    {
                        using (DrawingContext dc = dv.RenderOpen())
                        {
                            BitmapSource imagesource = Source.Width > Source.Height
                                ? ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, 300, 300)
                                : ((BitmapSource)Source)?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, 300, 300);
                            imagesource.Freeze();
                            dc.DrawImage(imagesource, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                        }

                        pd.PrintVisual(dv, "");
                    }
                }
                else
                {
                    pd.PageRangeSelection = PageRangeSelection.AllPages;
                    pd.UserPageRangeEnabled = true;
                    pd.MaxPage = (uint)Decoder.Frames.Count;
                    pd.MinPage = 1;
                    if (pd.ShowDialog() == true)
                    {
                        int başlangıç;
                        int bitiş;
                        if (pd.PageRangeSelection == PageRangeSelection.AllPages)
                        {
                            başlangıç = 0;
                            bitiş = Decoder.Frames.Count - 1;
                        }
                        else
                        {
                            başlangıç = pd.PageRange.PageFrom - 1;
                            bitiş = pd.PageRange.PageTo - 1;
                        }

                        for (int i = başlangıç; i <= bitiş; i++)
                        {
                            using (DrawingContext dc = dv.RenderOpen())
                            {
                                BitmapSource imagesource = Source.Width > Source.Height
                                    ? Decoder.Frames[i]?.Resize((int)pd.PrintableAreaHeight, (int)pd.PrintableAreaWidth, 90, 300, 300)
                                    : Decoder.Frames[i]?.Resize((int)pd.PrintableAreaWidth, (int)pd.PrintableAreaHeight, 0, 300, 300);
                                imagesource.Freeze();
                                dc.DrawImage(imagesource, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                            }

                            pd.PrintVisual(dv, "");
                        }
                    }
                }
            }, parameter => Source is not null);

            PropertyChanged += ImageViewer_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public int DecodeHeight
        {
            get => (int)GetValue(DecodeHeightProperty);
            set => SetValue(DecodeHeightProperty, value);
        }

        [Browsable(false)]
        public TiffBitmapDecoder Decoder
        {
            get => decoder;

            set
            {
                if (decoder != value)
                {
                    decoder = value;
                    OnPropertyChanged(nameof(Decoder));
                }
            }
        }

        public ICommand DosyaAç { get; }

        public FitImageOrientation FitImageOrientation
        {
            get => fitImageOrientation;

            set
            {
                if (fitImageOrientation != value)
                {
                    fitImageOrientation = value;
                    OnPropertyChanged(nameof(FitImageOrientation));
                }
            }
        }

        public string ImageFilePath
        {
            get => (string)GetValue(ImageFilePathProperty);
            set => SetValue(ImageFilePathProperty, value);
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

        public ICommand OrijinalResimDosyaAç { get; }

        public Visibility OrijinalResimDosyaAçButtonVisibility
        {
            get => orijinalResimDosyaAçButtonVisibility;

            set
            {
                if (orijinalResimDosyaAçButtonVisibility != value)
                {
                    orijinalResimDosyaAçButtonVisibility = value;
                    OnPropertyChanged(nameof(OrijinalResimDosyaAçButtonVisibility));
                }
            }
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

        public ICommand Resize { get; }

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

        public bool ToolBarIsEnabled
        {
            get => toolBarIsEnabled;

            set
            {
                if (toolBarIsEnabled != value)
                {
                    toolBarIsEnabled = value;
                    OnPropertyChanged(nameof(ToolBarIsEnabled));
                }
            }
        }

        public Visibility ToolBarVisibility
        {
            get => toolBarVisibility;

            set
            {
                if (toolBarVisibility != value)
                {
                    toolBarVisibility = value;
                    OnPropertyChanged(nameof(ToolBarVisibility));
                }
            }
        }

        public ICommand ViewerBack { get; }

        public ICommand ViewerNext { get; }

        public ICommand Yazdır { get; }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private TiffBitmapDecoder decoder;

        private FitImageOrientation fitImageOrientation;

        private Visibility openButtonVisibility = Visibility.Collapsed;

        private Visibility orijinalResimDosyaAçButtonVisibility;

        private IEnumerable<int> pages;

        private Visibility printButtonVisibility = Visibility.Collapsed;

        private int sayfa = 1;

        private Visibility tifNavigasyonButtonEtkin = Visibility.Collapsed;

        private bool toolBarIsEnabled = true;

        private Visibility toolBarVisibility;

        private static void DecodeHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer imageViewer)
            {
                string path = imageViewer.ImageFilePath;
                imageViewer.DecodeHeight = (int)e.NewValue;
                LoadImage(path, imageViewer);
            }
        }

        private static void ImageFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer imageViewer)
            {
                if (e.NewValue is string filepath)
                {
                    LoadImage(filepath, imageViewer);
                }
                else
                {
                    imageViewer.Source = null;
                }
            }
        }

        private static void LoadImage(string filepath, ImageViewer imageViewer)
        {
            if (filepath is not null)
            {
                if (Path.GetExtension(filepath).ToLower() is ".tiff" or ".tif")
                {
                    imageViewer.Sayfa = 1;
                    imageViewer.Decoder = new TiffBitmapDecoder(new Uri(filepath), BitmapCreateOptions.None, BitmapCacheOption.None);
                    imageViewer.TifNavigasyonButtonEtkin = Visibility.Visible;
                    imageViewer.Source = imageViewer.Decoder.Frames[0];
                    imageViewer.Pages = Enumerable.Range(1, imageViewer.Decoder.Frames.Count);
                }
                else if (Path.GetExtension(filepath).ToLower() is ".png" or ".jpg" or ".jpeg")
                {
                    imageViewer.TifNavigasyonButtonEtkin = Visibility.Collapsed;
                    BitmapImage image = new();
                    image.BeginInit();
                    image.DecodePixelHeight = imageViewer.DecodeHeight;
                    image.CacheOption = BitmapCacheOption.None;
                    image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    image.UriSource = new Uri(filepath);
                    image.EndInit();
                    if (!image.IsFrozen && image.CanFreeze)
                    {
                        image.Freeze();
                    }
                    imageViewer.Source = image;
                }
                else
                {
                    FormattedText formattedText = new("ÖNİZLEME YOK EVRAKI DİREKT AÇIN", CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight, new Typeface("Arial"), 15, Brushes.Red) { TextAlignment = TextAlignment.Left };
                    DrawingVisual dv = new();
                    using (DrawingContext dc = dv.RenderOpen())
                    {
                        dc.DrawText(formattedText, new Point(10, 200));
                    }
                    RenderTargetBitmap rtb = new(315, 445, 96, 96, PixelFormats.Default);
                    rtb.Render(dv);
                    rtb.Freeze();
                    imageViewer.Source = rtb;
                }
            }
        }

        private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewer imageViewer && imageViewer.Source is not null)
            {
                if (imageViewer.FitImageOrientation == FitImageOrientation.Width)
                {
                    if (!double.IsNaN(imageViewer.Width))
                    {
                        imageViewer.Zoom = imageViewer.Width == 0 ? 1 : imageViewer.Width / imageViewer.Source.Width;
                    }
                    else if (imageViewer.ActualWidth == 0)
                    {
                        imageViewer.Zoom = 1;
                    }
                    else
                    {
                        ScrollViewer scrollViewer = (imageViewer.GetVisualChild(0) as Grid)?.Children[0] as ScrollViewer;
                        imageViewer.Zoom = Math.Round(scrollViewer.ActualWidth / imageViewer.Source.Width, 2);
                    }
                }
                else if (!double.IsNaN(imageViewer.Height))
                {
                    imageViewer.Zoom = imageViewer.Height == 0 ? 1 : imageViewer.Height / imageViewer.Source.Height;
                }
                else if (imageViewer.ActualHeight == 0)
                {
                    imageViewer.Zoom = 1;
                }
                else
                {
                    ScrollViewer scrollViewer = (imageViewer.GetVisualChild(0) as Grid)?.Children[0] as ScrollViewer;
                    imageViewer.Zoom = Math.Round(scrollViewer.ActualHeight / imageViewer.Source.Height, 2);
                }
            }
        }

        private void ImageViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Sayfa" && Decoder is not null)
            {
                Source = Decoder.Frames[Sayfa - 1];
            }
        }
    }
}