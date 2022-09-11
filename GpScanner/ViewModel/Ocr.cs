using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Tesseract;

namespace GpScanner.ViewModel
{
    public static class Ocr
    {
        public static ObservableCollection<OcrData> OcrYap(this byte[] dosya, string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                _ = Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show("Dil Seçimini Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error));
                return null;
            }
            if (Directory.Exists(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + @"\tessdata"))
            {
                return GetText(dosya, lang);
            }
            _ = Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show("Tesseract Engine Klasörünü Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error));
            return null;
        }

        private static ObservableCollection<OcrData> GetText(byte[] dosya, string lang)
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
                        OcrData item = new() { DisplayName = iterator.GetText(PageIteratorLevel.Word), Rect = rect };
                        if (!string.IsNullOrWhiteSpace(item.DisplayName))
                        {
                            ocrdata.Add(item);
                        }
                    }
                } while (iterator.Next(PageIteratorLevel.Word));

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