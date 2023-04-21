﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace Ocr
{
    public static class Ocr
    {
        public static CancellationTokenSource ocrcancellationToken;

        static Ocr()
        {
            TesseractPath = $@"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\tessdata";
            if (Directory.Exists(TesseractPath))
            {
                TesseractDataExists = Directory.EnumerateFiles(TesseractPath).Any(z => string.Equals(Path.GetExtension(z), ".traineddata", StringComparison.OrdinalIgnoreCase));
                return;
            }
            TesseractDataExists = false;
        }

        public static bool TesseractDataExists { get; }

        public static async Task<ObservableCollection<OcrData>> OcrAsyc(this byte[] dosya, string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                throw new ArgumentNullException(nameof(lang));
            }
            if (!Directory.Exists(TesseractPath))
            {
                throw new ArgumentNullException(nameof(TesseractPath));
            }
            ocrcancellationToken = new CancellationTokenSource();
            return await Task.Run(() => dosya.GetOcrData(lang), ocrcancellationToken.Token);
        }

        public static async Task<ObservableCollection<OcrData>> OcrAsyc(this string dosya, string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                throw new ArgumentNullException(nameof(lang));
            }
            if (!Directory.Exists(TesseractPath))
            {
                throw new ArgumentNullException(nameof(TesseractPath));
            }
            ocrcancellationToken = new CancellationTokenSource();
            return await Task.Run(() => dosya.GetOcrData(lang), ocrcancellationToken.Token);
        }

        private static string TesseractPath { get; }

        private static ObservableCollection<OcrData> GetOcrData(this byte[] dosya, string lang)
        {
            if (dosya is null)
            {
                throw new ArgumentNullException(nameof(dosya));
            }
            using TesseractEngine engine = new(TesseractPath, lang, EngineMode.LstmOnly);
            using Pix pixImage = Pix.LoadFromMemory(dosya);
            using Page page = engine.Process(pixImage);
            using ResultIterator iterator = page.GetIterator();
            iterator.Begin();
            ObservableCollection<OcrData> ocrdata = iterator.IterateOcr(PageIteratorLevel.Word);
            dosya = null;
            GC.Collect();
            return ocrdata;
        }

        private static ObservableCollection<OcrData> GetOcrData(this string dosya, string lang)
        {
            if (dosya is null)
            {
                throw new ArgumentNullException(nameof(dosya));
            }
            using TesseractEngine engine = new(TesseractPath, lang, EngineMode.LstmOnly);
            using Pix pixImage = Pix.LoadFromFile(dosya);
            using Page page = engine.Process(pixImage);
            using ResultIterator iterator = page.GetIterator();
            iterator.Begin();
            ObservableCollection<OcrData> ocrdata = iterator.IterateOcr(PageIteratorLevel.Word);
            dosya = null;
            return ocrdata;
        }

        private static ObservableCollection<OcrData> IterateOcr(this ResultIterator iterator, PageIteratorLevel pageIteratorLevel)
        {
            ObservableCollection<OcrData> ocrdata = new();
            do
            {
                if (iterator.TryGetBoundingBox(pageIteratorLevel, out Rect rect))
                {
                    System.Windows.Rect imgrect = new(rect.X1, rect.Y1, rect.Width, rect.Height);
                    OcrData item = new() { Text = iterator.GetText(pageIteratorLevel), Rect = imgrect };
                    if (!string.IsNullOrWhiteSpace(item.Text))
                    {
                        ocrdata.Add(item);
                    }
                }
            } while (iterator.Next(pageIteratorLevel));
            return ocrdata;
        }
    }
}