using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;
using Microsoft.Win32;
using Ocr;
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
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static Paper Paper { get; set; }

        public static Scanner Scanner { get; set; }

        public ICommand SaveImage { get; }

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