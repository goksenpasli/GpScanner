using Extensions;
using Ocr;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwainControl.Properties;
using static Extensions.ExtensionMethods;

namespace TwainControl;

/// <summary>
/// Interaction logic for ToolBox.xaml
/// </summary>
public partial class ToolBox : UserControl, INotifyPropertyChanged
{
    private bool autoRotate;
    private double borderSize;
    private bool compressImage = true;
    private bool resizeRatioImage;
    private PageRotation selectedRotation = PageRotation.NONE;
    private double toolBoxPdfMergeProgressValue;

    public ToolBox()
    {
        InitializeComponent();

        PrintCroppedImage = new RelayCommand<object>(parameter => PdfViewer.PdfViewer.PrintImageSource(parameter as ImageSource), parameter => Scanner?.CroppedImage is not null);

        InvertImage = new RelayCommand<object>(parameter => Scanner.CroppedImage = ((BitmapSource)Scanner.CroppedImage).InvertBitmap().BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg), parameter => Scanner?.CroppedImage is not null);

        AutoCropImage = new RelayCommand<object>(
            parameter =>
            {
                Color color = (Color)ColorConverter.ConvertFromString(Scanner.AutoCropColor);
                Scanner.CroppedImage = ((BitmapSource)Scanner.CroppedImage).AutoCropImage(color);
            },
            parameter => Scanner?.CroppedImage is not null);

        BlackAndWhiteImage = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    Scanner.CroppedImage = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold, true).ToBitmapImage(ImageFormat.Jpeg);
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    foreach (ScannedImage image in Scanner?.Resimler?.Where(z => z.Seçili)?.ToList())
                    {
                        BitmapFrame bitmapframe = BitmapFrame.Create(image.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Jpeg));
                        bitmapframe.Freeze();
                        image.Resim = bitmapframe;
                    }
                    return;
                }

                Scanner.CroppedImage = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Jpeg);
            },
            parameter => Scanner?.CroppedImage is not null);

        ApplyColorChange = new RelayCommand<object>(parameter => Scanner.CopyCroppedImage = Scanner.CroppedImage, parameter => Scanner?.CroppedImage is not null);

        ResetCroppedImage = new RelayCommand<object>(parameter => ResetCropMargin(), parameter => Scanner?.CroppedImage is not null);

        SetWatermark = new RelayCommand<object>(
            parameter => Scanner.CroppedImage =
            Scanner.CroppedImage
            .ÜstüneResimÇiz(new Point(Scanner.CroppedImage.Width / 2, Scanner.CroppedImage.Height / 2), Scanner.WatermarkColor, VisualTreeHelper.GetDpi(this), Scanner.WatermarkTextSize, Scanner.Watermark, Scanner.WatermarkAngle, Scanner.WatermarkFont),
            parameter => Scanner?.CroppedImage is not null && !string.IsNullOrWhiteSpace(Scanner?.Watermark));

        WebAdreseGit =
            new RelayCommand<object>(parameter => TwainCtrl.GotoPage(parameter as string), parameter => true);

        SplitImage = new RelayCommand<object>(
            async parameter =>
            {
                bool altkeypressed = Keyboard.Modifiers == ModifierKeys.Alt;
                List<CroppedBitmap> croppedBitmaps = CropImageToList(Scanner.CroppedImage, Scanner.EnAdet, Scanner.BoyAdet);
                if (altkeypressed)
                {
                    foreach (CroppedBitmap bitmap in croppedBitmaps)
                    {
                        BitmapFrame bitmapFrame = BitmapFrame.Create(bitmap.BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg));
                        bitmapFrame.Freeze();
                        ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                        Scanner?.Resimler.Insert(Scanner.CroppedImageIndex, scannedImage);
                    }
                    return;
                }

                string savefolder = CreateSaveFolder("SPLIT");
                await Task.Run(
                    () =>
                    {
                        for (int i = 0; i < croppedBitmaps.Count; i++)
                        {
                            CroppedBitmap croppedBitmap = croppedBitmaps[i];
                            Dispatcher.Invoke(
                                () =>
                                {
                                    File.WriteAllBytes(savefolder.SetUniqueFile(Translation.GetResStringValue("SPLIT"), "jpg"), croppedBitmap.ToTiffJpegByteArray(Format.Jpg));
                                    ToolBoxPdfMergeProgressValue = (i + 1) / (double)croppedBitmaps.Count;
                                });
                        }
                    });
                WebAdreseGit.Execute(savefolder);
                ToolBoxPdfMergeProgressValue = 0;
            },
            parameter => Scanner?.AutoSave == true && Scanner?.CroppedImage is not null && (Scanner?.EnAdet > 1 || Scanner?.BoyAdet > 1));

        TransferImage = new RelayCommand<object>(
            parameter =>
            {
                BitmapFrame bitmapFrame = TwainCtrl.GenerateBitmapFrame((BitmapSource)Scanner.CroppedImage);
                bitmapFrame.Freeze();
                ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                Scanner?.Resimler.Insert(Scanner.CroppedImageIndex, scannedImage);
                scannedImage = null;
            },
            parameter => Scanner?.CroppedImage is not null);

        SplitAllImage = new RelayCommand<object>(
            async parameter =>
            {
                if (DataContext is TwainCtrl twainControl)
                {
                    List<ScannedImage> listcroppedimages;
                    PdfDocument pdfdocument = null;
                    bool splitpdfbypage = Keyboard.Modifiers == ModifierKeys.Alt;
                    await Task.Run(
                        async () =>
                        {
                            listcroppedimages = Scanner.Resimler
                            .Where(z => z.Seçili)
                            .SelectMany(scannedimage => CropImageToList(scannedimage.Resim, (int)Scanner.SliceCountWidth, (int)Scanner.SliceCountHeight).Select(croppedBitmap => new ScannedImage { Resim = BitmapFrame.Create(croppedBitmap) }))
                            .ToList();
                            pdfdocument = await listcroppedimages.GeneratePdfAsync(Format.Jpg, Paper, Settings.Default.JpegQuality, null, Settings.Default.ImgLoadResolution);
                        });
                    string savefolder = CreateSaveFolder("SPLIT");
                    string path = savefolder.SetUniqueFile(Translation.GetResStringValue("SPLIT"), "pdf");
                    pdfdocument.Save(path);
                    if (splitpdfbypage)
                    {
                        twainControl.SplitPdfPageCount(path, savefolder, 1);
                    }

                    WebAdreseGit.Execute(savefolder);
                    listcroppedimages = null;
                    pdfdocument = null;
                    if (Settings.Default.RemoveProcessedImage)
                    {
                        twainControl.SeçiliListeTemizle.Execute(null);
                    }
                }
            },
            parameter => Scanner?.AutoSave == true && Scanner?.Resimler?.Count(z => z.Seçili) > 0);

        MergeHorizontal = new RelayCommand<object>(
            async parameter =>
            {
                if (DataContext is TwainCtrl twainControl)
                {
                    List<ScannedImage> listcroppedimages;
                    Orientation orientation = Keyboard.Modifiers == ModifierKeys.Alt ? Orientation.Vertical : Orientation.Horizontal;
                    string savefolder = CreateSaveFolder("MERGE");
                    string path = savefolder.SetUniqueFile(Translation.GetResStringValue("MERGE"), "jpg");
                    await Task.Run(
                        () =>
                        {
                            listcroppedimages = Scanner.Resimler.Where(z => z.Seçili).ToList();
                            File.WriteAllBytes(path, listcroppedimages.CombineImages(orientation).ToTiffJpegByteArray(Format.Jpg));
                        });
                    WebAdreseGit.Execute(savefolder);
                    listcroppedimages = null;
                    if (Settings.Default.RemoveProcessedImage)
                    {
                        twainControl.SeçiliListeTemizle.Execute(null);
                    }
                }
            },
            parameter => Scanner?.AutoSave == true && Scanner?.Resimler?.Count(z => z.Seçili) > 1);

        MergeAllImage = new RelayCommand<object>(
            async parameter =>
            {
                PageOrientation pageOrientation = Keyboard.Modifiers == ModifierKeys.Alt ? PageOrientation.Portrait : PageOrientation.Landscape;
                string savefolder = CreateSaveFolder("MERGE");
                List<ScannedImage> seçiliresimler = Scanner.Resimler.Where(z => z.Seçili).ToList();
                PdfDocument pdfdocument = new();
                XRect box;
                PdfPage page = null;
                int imageindex = 0;
                for (int i = 0; i < seçiliresimler.Count / (Scanner.SliceCountWidth * Scanner.SliceCountHeight); i++)
                {
                    page = pdfdocument.AddPage();
                    switch (Paper.PaperType)
                    {
                        case "Custom":
                            page.Width = XUnit.FromCentimeter(Paper.Width);
                            page.Height = XUnit.FromCentimeter(Paper.Height);
                            break;

                        case "Original":
                            page.Width = XUnit.FromPoint(seçiliresimler[i].Resim.PixelWidth);
                            page.Height = XUnit.FromPoint(seçiliresimler[i].Resim.PixelHeight);
                            break;

                        default:
                            page.Size = Paper.GetPaperSize();
                            break;
                    }

                    page.Orientation = pageOrientation;
                    for (int heighindex = 0; heighindex < Scanner.SliceCountHeight; heighindex++)
                    {
                        for (int widthindex = 0; widthindex < Scanner.SliceCountWidth; widthindex++)
                        {
                            if (imageindex >= seçiliresimler.Count)
                            {
                                break;
                            }

                            await Task.Run(
                                () =>
                                {
                                    double x = widthindex * page.Width / Scanner.SliceCountWidth;
                                    double y = heighindex * page.Height / Scanner.SliceCountHeight;
                                    double width = page.Width / Scanner.SliceCountWidth;
                                    double height = page.Height / Scanner.SliceCountHeight;
                                    BitmapFrame currentimage = seçiliresimler.ElementAtOrDefault(imageindex).Resim;
                                    double xratio = width / currentimage.PixelWidth;
                                    BitmapSource bitmapsource = ResizeRatioImage
                                                                ? currentimage.Resize(xratio)
                                                                : CompressImage ? AutoRotate ? currentimage.Resize(width, height, 90 * (int)SelectedRotation) : currentimage.Resize(width, height) : currentimage;
                                    using MemoryStream ms = new(bitmapsource.ToTiffJpegByteArray(Format.Jpg, Settings.Default.JpegQuality));
                                    using XImage xImage = XImage.FromStream(ms);
                                    using XGraphics gfx = XGraphics.FromPdfPage(page);
                                    box = new XRect(x + BorderSize, y + BorderSize, width + (BorderSize * -2), height + (BorderSize * -2));
                                    if (ResizeRatioImage)
                                    {
                                        gfx.DrawImage(xImage, new Point(x, y));
                                    }
                                    else
                                    {
                                        gfx.DrawImage(xImage, box);
                                    }

                                    imageindex++;
                                    ToolBoxPdfMergeProgressValue = imageindex / (double)seçiliresimler.Count;
                                });
                        }
                    }
                }

                pdfdocument.ApplyDefaultPdfCompression();
                pdfdocument.Save(savefolder.SetUniqueFile(Translation.GetResStringValue("MERGE"), "pdf"));
                WebAdreseGit.Execute(savefolder);
                pdfdocument = null;
                page = null;
                if (Settings.Default.RemoveProcessedImage)
                {
                    (DataContext as TwainCtrl)?.SeçiliListeTemizle.Execute(null);
                }

                ToolBoxPdfMergeProgressValue = 0;
            },
            parameter => Scanner?.AutoSave == true && Scanner?.Resimler?.Count(z => z.Seçili) > 1);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static Paper Paper { get; set; }

    public static Scanner Scanner { get; set; }

    public ICommand ApplyColorChange { get; }

    public RelayCommand<object> AutoCropImage { get; }

    public bool AutoRotate
    {
        get => autoRotate;
        set
        {
            if (autoRotate != value)
            {
                autoRotate = value;
                OnPropertyChanged(nameof(AutoRotate));
            }
        }
    }

    public ICommand BlackAndWhiteImage { get; }

    public double BorderSize
    {
        get => borderSize;

        set
        {
            if (borderSize != value)
            {
                borderSize = value;
                OnPropertyChanged(nameof(BorderSize));
            }
        }
    }

    public bool CompressImage
    {
        get => compressImage;

        set
        {
            if (compressImage != value)
            {
                compressImage = value;
                OnPropertyChanged(nameof(CompressImage));
            }
        }
    }

    public ICommand InvertImage { get; }

    public ICommand MergeAllImage { get; }

    public ICommand MergeHorizontal { get; }

    public ICommand PrintCroppedImage { get; }

    public ICommand ResetCroppedImage { get; }

    public bool ResizeRatioImage
    {
        get => resizeRatioImage;

        set
        {
            if (resizeRatioImage != value)
            {
                resizeRatioImage = value;
                OnPropertyChanged(nameof(ResizeRatioImage));
            }
        }
    }

    public PageRotation SelectedRotation
    {
        get => selectedRotation;
        set
        {
            if (selectedRotation != value)
            {
                selectedRotation = value;
                OnPropertyChanged(nameof(SelectedRotation));
            }
        }
    }

    public ICommand SetWatermark { get; }

    public ICommand SplitAllImage { get; }

    public ICommand SplitImage { get; }

    public double ToolBoxPdfMergeProgressValue
    {
        get => toolBoxPdfMergeProgressValue;

        set
        {
            if (toolBoxPdfMergeProgressValue != value)
            {
                toolBoxPdfMergeProgressValue = value;
                OnPropertyChanged(nameof(ToolBoxPdfMergeProgressValue));
            }
        }
    }

    public ICommand TransferImage { get; }

    public ICommand WebAdreseGit { get; }

    public static string CreateSaveFolder(string langdata)
    {
        string savefolder = $@"{PdfGeneration.GetSaveFolder()}\{Translation.GetResStringValue(langdata)}";
        if (!Directory.Exists(savefolder))
        {
            _ = Directory.CreateDirectory(savefolder);
        }

        return savefolder;
    }

    public static void ResetCropMargin()
    {
        Scanner.CroppedImage = null;
        Scanner.CopyCroppedImage = null;
        Scanner.CropBottom = 0;
        Scanner.CropLeft = 0;
        Scanner.CropTop = 0;
        Scanner.CropRight = 0;
        Scanner.EnAdet = 1;
        Scanner.BoyAdet = 1;
        Scanner.Brightness = 0;
        Scanner.CroppedImageAngle = 0;
        Scanner.Threshold = 0;
        Scanner.Hue = 0;
        Scanner.Saturation = 1;
        Scanner.Lightness = 1;
        Scanner.Watermark = string.Empty;
        Scanner.Chart = null;
    }

    public List<CroppedBitmap> CropImageToList(ImageSource imageSource, int en, int boy)
    {
        List<CroppedBitmap> croppedBitmaps = [];
        BitmapSource image = (BitmapSource)imageSource;

        for (int j = 0; j < boy; j++)
        {
            for (int i = 0; i < en; i++)
            {
                int x = i * image.PixelWidth / en;
                int y = j * image.PixelHeight / boy;
                int width = image.PixelWidth / en;
                int height = image.PixelHeight / boy;
                Int32Rect sourceRect = new(x, y, width, height);
                if (sourceRect.HasArea)
                {
                    CroppedBitmap croppedBitmap = new(image, sourceRect);
                    croppedBitmaps.Add(croppedBitmap);
                }
            }
        }

        return croppedBitmaps;
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "EnAdet")
        {
            LineGrid.ColumnDefinitions.Clear();
            for (int i = 0; i < Scanner.EnAdet; i++)
            {
                LineGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }
        }

        if (e.PropertyName is "BoyAdet")
        {
            LineGrid.RowDefinitions.Clear();
            for (int i = 0; i < Scanner.BoyAdet; i++)
            {
                LineGrid.RowDefinitions.Add(new RowDefinition());
            }
        }

        if (e.PropertyName is "CroppedImageAngle" && Scanner.CopyCroppedImage is not null)
        {
            TransformedBitmap transformedBitmap = new((BitmapSource)Scanner.CopyCroppedImage, new RotateTransform(Scanner.CroppedImageAngle));
            transformedBitmap.Freeze();
            Scanner.CroppedImage = transformedBitmap;
        }

        if (e.PropertyName is "Threshold" && Scanner.CopyCroppedImage is not null)
        {
            Color source = (Color)ColorConverter.ConvertFromString(Scanner.SourceColor);
            Color target = (Color)ColorConverter.ConvertFromString(Scanner.TargetColor);
            Scanner.CroppedImage =
                ((BitmapSource)Scanner.CopyCroppedImage).ReplaceColor(source, target, (int)Scanner.Threshold);
        }

        if (e.PropertyName is "Hue" or "Saturation" or "Lightness" && Scanner.CopyCroppedImage is not null)
        {
            Scanner.CroppedImage =
                ((BitmapSource)Scanner.CopyCroppedImage).ApplyHueSaturationLightness(Scanner.Hue, Scanner.Saturation, Scanner.Lightness);
        }

        if (e.PropertyName is "MedianValue" && Scanner.CopyCroppedImage is not null)
        {
            WriteableBitmap writeableBitmap = ((BitmapSource)Scanner.CopyCroppedImage).MedianFilterBitmap(Scanner.MedianValue);
            writeableBitmap.Freeze();
            Scanner.CroppedImage = writeableBitmap;
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TwainCtrl twainCtrl)
        {
            Scanner = twainCtrl.Scanner;
            Scanner.PropertyChanged += Scanner_PropertyChanged;
        }
    }
}