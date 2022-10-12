using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Tesseract;
using TwainControl;

namespace GpScanner.ViewModel
{
    public static class Ocr
    {
        public static async Task<ObservableCollection<OcrData>> OcrAsyc(this byte[] dosya, string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                MessageBox.Show("Dil Seçimini Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            if (Directory.Exists(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + @"\tessdata"))
            {
                return await Task.Run(() => GetOcrData(dosya, lang));
            }
            MessageBox.Show("Tesseract Engine Klasörünü Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        public static ObservableCollection<OcrData> GetOcrData(this byte[] dosya, string lang)
        {
            try
            {
                using TesseractEngine engine = new("./tessdata", lang, EngineMode.TesseractAndLstm);
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
    }
}