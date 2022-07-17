using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Tesseract;
using Path = System.IO.Path;

namespace GpScanner.ViewModel
{
    public static class Ocr
    {
        public static string OcrYap(this byte[] dosya, string lang)
        {
            if (Directory.Exists(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + @"\tessdata"))
            {
                try
                {
                    return GetText(dosya, lang);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return string.Empty;
                }
            }
            _ = MessageBox.Show("Tesseract Engine Klasörünü Kontrol Edin.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return string.Empty;
        }

        private static string GetText(byte[] dosya, string lang)
        {
            using TesseractEngine engine = new("./tessdata", lang, EngineMode.TesseractAndLstm);
            using Pix pixImage = Pix.LoadFromMemory(dosya);
            using Page page = engine.Process(pixImage);
            return page.GetText();
        }
    }
}