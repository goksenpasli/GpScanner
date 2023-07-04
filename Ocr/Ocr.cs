using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using Rect = System.Windows.Rect;

namespace Ocr;

public static class Ocr
{
    static Ocr()
    {
        TesseractPath = $@"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\tessdata";
        if (Directory.Exists(TesseractPath))
        {
            TesseractDataExists = Directory.EnumerateFiles(TesseractPath).Any(z =>
                string.Equals(Path.GetExtension(z), ".traineddata", StringComparison.OrdinalIgnoreCase));
            return;
        }

        TesseractDataExists = false;
    }

    public static bool TesseractDataExists { get; }

    public static ObservableCollection<OcrData> GetOcrData(this string dosya, string tesseractlanguage)
    {
        if (dosya is null)
        {
            throw new ArgumentNullException(nameof(dosya));
        }

        using TesseractEngine engine = new(TesseractPath, tesseractlanguage, EngineMode.LstmOnly);
        using Pix pixImage = Pix.LoadFromFile(dosya);
        using Page page = engine.Process(pixImage);
        using ResultIterator iterator = page.GetIterator();
        iterator.Begin();
        ObservableCollection<OcrData> ocrdata = iterator.IterateOcr(PageIteratorLevel.Word);
        dosya = null;
        return ocrdata;
    }

    public static async Task<ObservableCollection<OcrData>> OcrAsync(this byte[] dosya, string tesseractlanguage)
    {
        if (string.IsNullOrWhiteSpace(tesseractlanguage))
        {
            throw new ArgumentNullException(nameof(tesseractlanguage));
        }

        if (!Directory.Exists(TesseractPath))
        {
            throw new ArgumentNullException(nameof(TesseractPath));
        }

        ocrcancellationToken = new CancellationTokenSource();
        return await Task.Run(() => dosya.GetOcrData(tesseractlanguage), ocrcancellationToken.Token);
    }

    public static async Task<ObservableCollection<OcrData>> OcrAsync(this string dosya, string tesseractlanguage)
    {
        if (string.IsNullOrWhiteSpace(tesseractlanguage))
        {
            throw new ArgumentNullException(nameof(tesseractlanguage));
        }

        if (!Directory.Exists(TesseractPath))
        {
            throw new ArgumentNullException(nameof(TesseractPath));
        }

        ocrcancellationToken = new CancellationTokenSource();
        return await Task.Run(() => dosya.GetOcrData(tesseractlanguage), ocrcancellationToken.Token);
    }

    public static CancellationTokenSource ocrcancellationToken;

    private static string TesseractPath { get; }

    private static ObservableCollection<OcrData> GetOcrData(this byte[] dosya, string tesseractlanguage)
    {
        if (dosya is null)
        {
            throw new ArgumentNullException(nameof(dosya));
        }

        using TesseractEngine engine = new(TesseractPath, tesseractlanguage, EngineMode.LstmOnly);
        using Pix pixImage = Pix.LoadFromMemory(dosya);
        using Page page = engine.Process(pixImage);
        using ResultIterator iterator = page.GetIterator();
        iterator.Begin();
        ObservableCollection<OcrData> ocrdata = iterator.IterateOcr(PageIteratorLevel.Word);
        dosya = null;
        GC.Collect();
        return ocrdata;
    }

    private static ObservableCollection<OcrData> IterateOcr(this ResultIterator iterator,
        PageIteratorLevel pageIteratorLevel)
    {
        ObservableCollection<OcrData> ocrdata = new();
        do
        {
            if (iterator.TryGetBoundingBox(pageIteratorLevel, out Tesseract.Rect rect))
            {
                Rect imgrect = new(rect.X1, rect.Y1, rect.Width, rect.Height);
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