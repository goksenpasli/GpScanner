using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Tesseract;

namespace Ocr
{
    public static class Ocr
    {
        public static CancellationTokenSource ocrcancellationToken;

        public static async Task<ObservableCollection<OcrData>> OcrAsyc(this byte[] dosya, string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                _ = MessageBox.Show("Tesseract Dil Seçimini Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            if (Directory.Exists(TesseractPath))
            {
                ocrcancellationToken = new CancellationTokenSource();
                return await Task.Run(() => dosya.GetOcrData(lang), ocrcancellationToken.Token);
            }
            _ = MessageBox.Show("Tesseract Engine Klasörünü Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        public static async Task<ObservableCollection<OcrData>> OcrAsyc(this string dosya, string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                _ = MessageBox.Show("Tesseract Dil Seçimini Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            if (Directory.Exists(TesseractPath))
            {
                ocrcancellationToken = new CancellationTokenSource();
                return await Task.Run(() => dosya.GetOcrData(lang), ocrcancellationToken.Token);
            }
            _ = MessageBox.Show("Tesseract Engine Klasörünü Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        private static string TesseractPath { get; } = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + @"\tessdata";

        private static ObservableCollection<OcrData> GetOcrData(this byte[] dosya, string lang)
        {
            try
            {
                using TesseractEngine engine = new(TesseractPath, lang, EngineMode.LstmOnly);
                using Pix pixImage = Pix.LoadFromMemory(dosya);
                using Page page = engine.Process(pixImage);
                using ResultIterator iterator = page.GetIterator();
                iterator.Begin();
                ObservableCollection<OcrData> ocrdata = new();
                do
                {
                    if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out Tesseract.Rect rect))
                    {
                        System.Windows.Rect imgrect = new(rect.X1, rect.Y1, rect.Width, rect.Height);
                        OcrData item = new() { Text = iterator.GetText(PageIteratorLevel.Word), Rect = imgrect };
                        if (!string.IsNullOrWhiteSpace(item.Text))
                        {
                            ocrdata.Add(item);
                        }
                    }
                } while (iterator.Next(PageIteratorLevel.Word));
                dosya = null;
                return ocrdata;
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation));
                return null;
            }
        }

        private static ObservableCollection<OcrData> GetOcrData(this string dosya, string lang)
        {
            try
            {
                using TesseractEngine engine = new(TesseractPath, lang, EngineMode.LstmOnly);
                using Pix pixImage = Pix.LoadFromFile(dosya);
                using Page page = engine.Process(pixImage);
                using ResultIterator iterator = page.GetIterator();
                iterator.Begin();
                ObservableCollection<OcrData> ocrdata = new();
                do
                {
                    if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out Tesseract.Rect rect))
                    {
                        System.Windows.Rect imgrect = new(rect.X1, rect.Y1, rect.Width, rect.Height);
                        OcrData item = new() { Text = iterator.GetText(PageIteratorLevel.Word), Rect = imgrect };
                        if (!string.IsNullOrWhiteSpace(item.Text))
                        {
                            ocrdata.Add(item);
                        }
                    }
                } while (iterator.Next(PageIteratorLevel.Word));
                dosya = null;
                return ocrdata;
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation));
                return null;
            }
        }
    }
}