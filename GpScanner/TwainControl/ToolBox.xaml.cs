using System;
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
using Extensions;
using Microsoft.Win32;
using Ocr;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using TwainControl.Properties;
using static Extensions.ExtensionMethods;

namespace TwainControl;

/// <summary>
///     Interaction logic for ToolBox.xaml
/// </summary>
public partial class ToolBox : UserControl, INotifyPropertyChanged
{
    public ToolBox()
    {
        InitializeComponent();

        SaveImage = new RelayCommand<object>(parameter =>
        {
            if (TwainCtrl.Filesavetask?.IsCompleted == false)
            {
                _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                return;
            }

            SaveFileDialog saveFileDialog = new()
            {
                Filter =
                    "Tif Resmi (*.tif)|*.tif|Jpg Resmi (*.jpg)|*.jpg|Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps|Txt Dosyası (*.txt)|*.txt",
                FileName = Scanner.FileName,
                FilterIndex = 3
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                TwainCtrl.Filesavetask = Task.Run(async () =>
                {
                    BitmapFrame bitmapFrame = BitmapFrame.Create(parameter as BitmapSource);
                    bitmapFrame.Freeze();
                    switch (saveFileDialog.FilterIndex)
                    {
                        case 1:
                            TwainCtrl.SaveTifImage(bitmapFrame, saveFileDialog.FileName);
                            bitmapFrame = null;
                            break;

                        case 2:
                            TwainCtrl.SaveJpgImage(bitmapFrame, saveFileDialog.FileName);
                            bitmapFrame = null;
                            break;

                        case 3:
                            await TwainCtrl.SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, Paper);
                            bitmapFrame = null;
                            break;

                        case 4:
                            await TwainCtrl.SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, Paper, true);
                            bitmapFrame = null;
                            break;

                        case 5:
                            TwainCtrl.SaveXpsImage(bitmapFrame, saveFileDialog.FileName);
                            bitmapFrame = null;
                            break;

                        case 6:
                            await TwainCtrl.SaveTxtFile(bitmapFrame, saveFileDialog.FileName, Scanner);
                            bitmapFrame = null;
                            break;
                    }
                });
            }
        }, parameter => Scanner?.CroppedImage is not null);

        PrintCroppedImage =
            new RelayCommand<object>(parameter => PdfViewer.PdfViewer.PrintImageSource(parameter as ImageSource),
                parameter => Scanner?.CroppedImage is not null);

        DeskewImage = new RelayCommand<object>(async parameter =>
        {
            double skewAngle = GetDeskewAngle(Scanner.CroppedImage, true);
            Scanner.CroppedImage = await Scanner.CroppedImage.RotateImageAsync(skewAngle);
        }, parameter => Scanner?.CroppedImage is not null);

        InvertImage = new RelayCommand<object>(parameter =>
        {
            using System.Drawing.Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
            Scanner.CroppedImage = bmp.InvertBitmap().ToBitmapImage(ImageFormat.Png);
        }, parameter => Scanner?.CroppedImage is not null);

        ApplyColorChange = new RelayCommand<object>(parameter => Scanner.CopyCroppedImage = Scanner.CroppedImage,
            parameter => Scanner?.CroppedImage is not null);

        ResetCroppedImage = new RelayCommand<object>(parameter => ResetCropMargin(),
            parameter => Scanner?.CroppedImage is not null);

        SetWatermark = new RelayCommand<object>(
            parameter => Scanner.CroppedImage = Scanner.CroppedImage.ÜstüneResimÇiz(
                new Point(Scanner.CroppedImage.Width / 2, Scanner.CroppedImage.Height / 2), Scanner.WatermarkColor,
                Scanner.WatermarkTextSize, Scanner.Watermark, Scanner.WatermarkAngle, Scanner.WatermarkFont),
            parameter => Scanner?.CroppedImage is not null && !string.IsNullOrWhiteSpace(Scanner?.Watermark));

        WebAdreseGit =
            new RelayCommand<object>(parameter => TwainCtrl.GotoPage(parameter as string), parameter => true);

        SplitImage = new RelayCommand<object>(async parameter =>
            {
                string savefolder = CreateSaveFolder("SPLIT");
                List<CroppedBitmap> croppedBitmaps = CropImageToList(Scanner.CroppedImage, Scanner.EnAdet, Scanner.BoyAdet);
                await Task.Run(() =>
                {
                    for (int i = 0; i < croppedBitmaps.Count; i++)
                    {
                        CroppedBitmap croppedBitmap = croppedBitmaps[i];
                        Dispatcher.Invoke(() =>
                        {
                            File.WriteAllBytes(savefolder.SetUniqueFile(Translation.GetResStringValue("SPLIT"), "jpg"),
                                croppedBitmap.ToTiffJpegByteArray(Format.Jpg));
                            ToolBoxPdfMergeProgressValue = (i + 1) / (double)croppedBitmaps.Count;
                        });
                    }
                });
                WebAdreseGit.Execute(savefolder);
                ToolBoxPdfMergeProgressValue = 0;
            },
            parameter => Scanner?.AutoSave == true && Scanner?.CroppedImage is not null &&
                         (Scanner?.EnAdet > 1 || Scanner?.BoyAdet > 1));

        TransferImage = new RelayCommand<object>(parameter =>
        {
            BitmapFrame bitmapFrame = TwainCtrl.GenerateBitmapFrame((BitmapSource)Scanner.CroppedImage, Paper);
            bitmapFrame.Freeze();
            ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
            Scanner?.Resimler.Add(scannedImage);
            scannedImage = null;
        }, parameter => Scanner?.CroppedImage is not null);

        SplitAllImage = new RelayCommand<object>(async parameter =>
        {
            if (DataContext is TwainCtrl twainControl)
            {
                List<ScannedImage> listcroppedimages;
                PdfDocument pdfdocument = null;
                bool splitpdfbypage = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
                await Task.Run(async () =>
                {
                    listcroppedimages = Scanner.Resimler.Where(z => z.Seçili).SelectMany(scannedimage =>
                            CropImageToList(scannedimage.Resim, (int)Scanner.SliceCountWidth,
                                    (int)Scanner.SliceCountHeight)
                                .Select(croppedBitmap => new ScannedImage
                                { Resim = BitmapFrame.Create(croppedBitmap) }))
                        .ToList();
                    pdfdocument = await listcroppedimages.GeneratePdf(Format.Jpg, Paper, Settings.Default.JpegQuality,
                        null, (int)Settings.Default.Çözünürlük);
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
        }, parameter => Scanner?.AutoSave == true && Scanner?.Resimler?.Count(z => z.Seçili) > 0);

        MergeHorizontal = new RelayCommand<object>(async parameter =>
        {
            if (DataContext is TwainCtrl twainControl)
            {
                List<ScannedImage> listcroppedimages;
                Orientation orientation = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)
                    ? Orientation.Vertical
                    : Orientation.Horizontal;
                string savefolder = CreateSaveFolder("MERGE");
                string path = savefolder.SetUniqueFile(Translation.GetResStringValue("MERGE"), "jpg");
                await Task.Run(() =>
                {
                    listcroppedimages = Scanner.Resimler.Where(z => z.Seçili).ToList();
                    File.WriteAllBytes(path,
                        listcroppedimages.CombineImages(orientation).ToTiffJpegByteArray(Format.Jpg));
                });
                WebAdreseGit.Execute(savefolder);
                listcroppedimages = null;
                if (Settings.Default.RemoveProcessedImage)
                {
                    twainControl.SeçiliListeTemizle.Execute(null);
                }
            }
        }, parameter => Scanner?.AutoSave == true && Scanner?.Resimler?.Count(z => z.Seçili) > 1);

        MergeAllImage = new RelayCommand<object>(async parameter =>
        {
            PageOrientation pageOrientation = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)
                ? PageOrientation.Portrait
                : PageOrientation.Landscape;
            string savefolder = CreateSaveFolder("MERGE");
            IEnumerable<ScannedImage> seçiliresimler = Scanner.Resimler.Where(z => z.Seçili);
            PdfDocument pdfdocument = new();
            XRect box;
            PdfPage page = null;
            int imageindex = 0;
            for (int i = 0; i < seçiliresimler.Count() / (Scanner.SliceCountWidth * Scanner.SliceCountHeight); i++)
            {
                page = pdfdocument.AddPage();
                Paper.SetPaperSize(page);
                page.Orientation = pageOrientation;
                for (int heighindex = 0; heighindex < Scanner.SliceCountHeight; heighindex++)
                {
                    for (int widthindex = 0; widthindex < Scanner.SliceCountWidth; widthindex++)
                    {
                        if (imageindex >= seçiliresimler.Count())
                        {
                            break;
                        }

                        await Task.Run(() =>
                    {
                        double x = widthindex * page.Width / Scanner.SliceCountWidth;
                        double y = heighindex * page.Height / Scanner.SliceCountHeight;
                        double width = page.Width / Scanner.SliceCountWidth;
                        double height = page.Height / Scanner.SliceCountHeight;
                        BitmapFrame currentimage = seçiliresimler.ElementAtOrDefault(imageindex).Resim;
                        double xratio = width / currentimage.PixelWidth;
                        BitmapSource bitmapsource = ResizeRatioImage ? currentimage.Resize(xratio) :
                            CompressImage ? currentimage.Resize(width, height) : currentimage;
                        using MemoryStream ms =
                            new(bitmapsource.ToTiffJpegByteArray(Format.Jpg, Settings.Default.JpegQuality));
                        using XImage xImage = XImage.FromStream(ms);
                        using XGraphics gfx = XGraphics.FromPdfPage(page);
                        box = new XRect(x + BorderSize, y + BorderSize, width + (BorderSize * -2),
                            height + (BorderSize * -2));
                        if (ResizeRatioImage)
                        {
                            gfx.DrawImage(xImage, new Point(x, y));
                        }
                        else
                        {
                            gfx.DrawImage(xImage, box);
                        }

                        imageindex++;
                        ToolBoxPdfMergeProgressValue = imageindex / (double)seçiliresimler.Count();
                        GC.Collect();
                    });
                    }
                }
            }

            pdfdocument.DefaultPdfCompression();
            pdfdocument.Save(savefolder.SetUniqueFile(Translation.GetResStringValue("MERGE"), "pdf"));
            WebAdreseGit.Execute(savefolder);
            pdfdocument = null;
            page = null;
            if (Settings.Default.RemoveProcessedImage)
            {
                (DataContext as TwainCtrl)?.SeçiliListeTemizle.Execute(null);
            }

            ToolBoxPdfMergeProgressValue = 0;
        }, parameter => Scanner?.AutoSave == true && Scanner?.Resimler?.Count(z => z.Seçili) > 1);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static Paper Paper { get; set; }

    public static Scanner Scanner { get; set; }

    public ICommand ApplyColorChange { get; }

    public double BorderSize {
        get => borderSize;

        set {
            if (borderSize != value)
            {
                borderSize = value;
                OnPropertyChanged(nameof(BorderSize));
            }
        }
    }

    public bool CompressImage {
        get => compressImage;

        set {
            if (compressImage != value)
            {
                compressImage = value;
                OnPropertyChanged(nameof(CompressImage));
            }
        }
    }

    public ICommand DeskewImage { get; }

    public ICommand InvertImage { get; }

    public ICommand MergeAllImage { get; }

    public ICommand MergeHorizontal { get; }

    public ICommand PrintCroppedImage { get; }

    public ICommand ResetCroppedImage { get; }

    public bool ResizeRatioImage {
        get => resizeRatioImage;

        set {
            if (resizeRatioImage != value)
            {
                resizeRatioImage = value;
                OnPropertyChanged(nameof(ResizeRatioImage));
            }
        }
    }

    public ICommand SaveImage { get; }

    public ICommand SetWatermark { get; }

    public ICommand SplitAllImage { get; }

    public ICommand SplitImage { get; }

    public double ToolBoxPdfMergeProgressValue {
        get => toolBoxPdfMergeProgressValue;

        set {
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

    public static double GetDeskewAngle(ImageSource ımageSource, bool fast = false)
    {
        Deskew sk = new((BitmapSource)ımageSource);
        return -1 * sk.GetSkewAngle(fast);
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
        Scanner.Watermark = string.Empty;
        Scanner.Chart = null;
        GC.Collect();
    }

    public List<CroppedBitmap> CropImageToList(ImageSource imageSource, int en, int boy)
    {
        List<CroppedBitmap> croppedBitmaps = new();
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

    protected virtual void OnPropertyChanged(string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private double borderSize;

    private bool compressImage = true;

    private bool resizeRatioImage;

    private double toolBoxPdfMergeProgressValue;

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

        if (e.PropertyName is "Brightness" && Scanner.CopyCroppedImage is not null)
        {
            using System.Drawing.Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
            Scanner.CroppedImage = bmp.AdjustBrightness((int)Scanner.Brightness).ToBitmapImage(ImageFormat.Png);
        }

        if (e.PropertyName is "CroppedImageAngle" && Scanner.CopyCroppedImage is not null)
        {
            TransformedBitmap transformedBitmap = new((BitmapSource)Scanner.CopyCroppedImage,
                new RotateTransform(Scanner.CroppedImageAngle));
            transformedBitmap.Freeze();
            Scanner.CroppedImage = transformedBitmap;
        }

        if (e.PropertyName is "Threshold" && Scanner.CopyCroppedImage is not null)
        {
            Color source = (Color)ColorConverter.ConvertFromString(Scanner.SourceColor);
            Color target = (Color)ColorConverter.ConvertFromString(Scanner.TargetColor);
            using System.Drawing.Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
            using System.Drawing.Bitmap replacedbmp = bmp.ReplaceColor(source, target, (int)Scanner.Threshold);
            Scanner.CroppedImage = replacedbmp.ToBitmapImage(ImageFormat.Png);
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