using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
using PdfSharp.Pdf;
using static Extensions.ExtensionMethods;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for ToolBox.xaml
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
                    _ = MessageBox.Show("İşlem Devam Ediyor. Bitmesini Bekleyin.");
                    return;
                }
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi (*.jpg)|*.jpg|Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps",
                    FileName = Scanner.FileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    TwainCtrl.Filesavetask = Task.Run(async () =>
                    {
                        BitmapFrame bitmapFrame = BitmapFrame.Create(parameter as BitmapSource);
                        bitmapFrame.Freeze();
                        if (saveFileDialog.FilterIndex == 1)
                        {
                            TwainCtrl.SaveTifImage(bitmapFrame, saveFileDialog.FileName);
                        }
                        if (saveFileDialog.FilterIndex == 2)
                        {
                            TwainCtrl.SaveJpgImage(bitmapFrame, saveFileDialog.FileName);
                        }
                        if (saveFileDialog.FilterIndex == 3)
                        {
                            await TwainCtrl.SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, Paper);
                        }
                        if (saveFileDialog.FilterIndex == 4)
                        {
                            await TwainCtrl.SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, Paper, true);
                        }
                        if (saveFileDialog.FilterIndex == 5)
                        {
                            TwainCtrl.SaveXpsImage(bitmapFrame, saveFileDialog.FileName);
                        }
                        bitmapFrame = null;
                    });
                }
            }, parameter => Scanner?.CroppedImage is not null);

            PrintCroppedImage = new RelayCommand<object>(parameter => PdfViewer.PdfViewer.PrintImageSource(parameter as ImageSource), parameter => Scanner?.CroppedImage is not null);

            LoadHistogram = new RelayCommand<object>(parameter =>
            {
                Scanner.RedChart = ((BitmapSource)Scanner.CroppedImage).BitmapSourceToBitmap().GenerateHistogram(System.Windows.Media.Brushes.Red);
                Scanner.GreenChart = ((BitmapSource)Scanner.CroppedImage).BitmapSourceToBitmap().GenerateHistogram(System.Windows.Media.Brushes.Green);
                Scanner.BlueChart = ((BitmapSource)Scanner.CroppedImage).BitmapSourceToBitmap().GenerateHistogram(System.Windows.Media.Brushes.Blue);
            }, parameter => Scanner?.CroppedImage is not null);

            DeskewImage = new RelayCommand<object>(async parameter =>
            {
                double skewAngle = GetDeskewAngle(Scanner.CroppedImage, true);
                Scanner.CroppedImage = await Scanner.CroppedImage.RotateImageAsync(skewAngle);
            }, parameter => Scanner?.CroppedImage is not null);

            ApplyColorChange = new RelayCommand<object>(parameter => Scanner.CopyCroppedImage = Scanner.CroppedImage, parameter => Scanner?.CroppedImage is not null);

            ResetCroppedImage = new RelayCommand<object>(parameter => ResetCropMargin(), parameter => Scanner?.CroppedImage is not null);

            SetWatermark = new RelayCommand<object>(parameter => Scanner.CroppedImage = Scanner.CroppedImage.ÜstüneResimÇiz(new System.Windows.Point(Scanner.CroppedImage.Width / 2, Scanner.CroppedImage.Height / 2), System.Windows.Media.Brushes.Red, Scanner.WatermarkTextSize, Scanner.Watermark, Scanner.WatermarkAngle, Scanner.WatermarkFont), parameter => Scanner?.CroppedImage is not null && !string.IsNullOrWhiteSpace(Scanner?.Watermark));

            WebAdreseGit = new RelayCommand<object>(parameter => TwainCtrl.GotoPage(parameter as string), parameter => true);

            SplitImage = new RelayCommand<object>(parameter =>
            {
                string savefolder = $@"{PdfGeneration.GetSaveFolder()}\{Translation.GetResStringValue("SPLIT")}";
                if (!Directory.Exists(savefolder))
                {
                    _ = Directory.CreateDirectory(savefolder);
                }
                foreach (CroppedBitmap croppedBitmap in CropImageToList(Scanner.CroppedImage, Scanner.EnAdet, Scanner.BoyAdet))
                {
                    File.WriteAllBytes(savefolder.SetUniqueFile(Translation.GetResStringValue("SPLIT"), "jpg"), croppedBitmap.ToTiffJpegByteArray(Format.Jpg));
                }
                WebAdreseGit.Execute(savefolder);
            }, parameter => Scanner?.AutoSave == true && Scanner?.CroppedImage is not null && (Scanner?.EnAdet > 1 || Scanner?.BoyAdet > 1));

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
                string savefolder = $@"{PdfGeneration.GetSaveFolder()}\{Translation.GetResStringValue("SPLIT")}";
                if (!Directory.Exists(savefolder))
                {
                    _ = Directory.CreateDirectory(savefolder);
                }
                List<ScannedImage> listcroppedimages = Scanner.Resimler.Where(z => z.Seçili).SelectMany(scannedimage => CropImageToList(scannedimage.Resim, 2, 1).Select(croppedBitmap => new ScannedImage { Resim = BitmapFrame.Create(croppedBitmap) })).ToList();
                PdfDocument pdfdocument = await listcroppedimages.GeneratePdf(Format.Jpg, null, 80, null);
                pdfdocument.Save(savefolder.SetUniqueFile(Translation.GetResStringValue("SPLIT"), "pdf"));
                WebAdreseGit.Execute(savefolder);
                listcroppedimages = null;
                pdfdocument = null;
                (DataContext as TwainCtrl)?.SeçiliListeTemizle.Execute(null);
            }, parameter => Scanner?.AutoSave == true && Scanner?.Resimler?.Count(z => z.Seçili) > 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static Paper Paper { get; set; }

        public static Scanner Scanner { get; set; }

        public ICommand ApplyColorChange { get; }

        public ICommand DeskewImage { get; }

        public ICommand LoadHistogram { get; }

        public ICommand PrintCroppedImage { get; }

        public ICommand ResetCroppedImage { get; }

        public ICommand SaveImage { get; }

        public ICommand SetWatermark { get; }

        public ICommand SplitAllImage { get; }

        public ICommand SplitImage { get; }

        public ICommand TransferImage { get; }

        public ICommand WebAdreseGit { get; }

        public static List<CroppedBitmap> CropImageToList(ImageSource imageSource, int en, int boy)
        {
            List<CroppedBitmap> croppedBitmaps = new();
            BitmapSource image = (BitmapSource)imageSource;
            for (int i = 0; i < en; i++)
            {
                for (int j = 0; j < boy; j++)
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

        public static double GetDeskewAngle(ImageSource ımageSource, bool fast = false)
        {
            Deskew sk = new((BitmapSource)ımageSource);
            return (double)(-1 * sk.GetSkewAngle(fast));
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
            Scanner.RedChart = null;
            Scanner.BlueChart = null;
            Scanner.GreenChart = null;
            GC.Collect();
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
                using Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
                Scanner.CroppedImage = bmp.AdjustBrightness((int)Scanner.Brightness).ToBitmapImage(ImageFormat.Png);
            }
            if (e.PropertyName is "CroppedImageAngle" && Scanner.CopyCroppedImage is not null)
            {
                TransformedBitmap transformedBitmap = new((BitmapSource)Scanner.CopyCroppedImage, new RotateTransform(Scanner.CroppedImageAngle));
                transformedBitmap.Freeze();
                Scanner.CroppedImage = transformedBitmap;
            }
            if (e.PropertyName is "Threshold" && Scanner.CopyCroppedImage is not null)
            {
                System.Windows.Media.Color source = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Scanner.SourceColor);
                System.Windows.Media.Color target = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Scanner.TargetColor);
                using Bitmap bmp = ((BitmapSource)Scanner.CopyCroppedImage).BitmapSourceToBitmap();
                using Bitmap replacedbmp = bmp.ReplaceColor(source, target, (int)Scanner.Threshold);
                Scanner.CroppedImage = replacedbmp.ToBitmapImage(ImageFormat.Png);
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
}