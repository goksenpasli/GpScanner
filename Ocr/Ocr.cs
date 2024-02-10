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
    public static CancellationTokenSource ocrcancellationToken;

    static Ocr()
    {
        TesseractPath = $@"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\tessdata";
        try
        {
            TesseractDataExists = Directory.Exists(TesseractPath) && Directory.EnumerateFiles(TesseractPath, "*.traineddata")?.Any() == true;
        }
        catch (Exception)
        {
            TesseractDataExists = false;
        }
    }

    public static bool TesseractDataExists { get; }

    private static string TesseractPath { get; }

    public static ObservableCollection<OcrData> GetOcrData(this string dosya, string tesseractlanguage)
    {
        if (!File.Exists(dosya))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(tesseractlanguage))
        {
            throw new ArgumentNullException(nameof(tesseractlanguage));
        }

        using TesseractEngine engine = CreateTesseractEngine(tesseractlanguage);
        using Pix pixImage = Pix.LoadFromFile(dosya);
        using Page page = engine.Process(pixImage);
        using ResultIterator iterator = page?.GetIterator();
        if (iterator != null)
        {
            iterator.Begin();
            ObservableCollection<OcrData> ocrdata = iterator.IterateOcr(PageIteratorLevel.Word);
            dosya = null;
            return ocrdata;
        }

        return null;
    }

    public static async Task<ObservableCollection<OcrData>> OcrAsync(this string dosya, string tesseractlanguage)
    {
        if (!File.Exists(dosya))
        {
            return null;
        }
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

    public static async Task<ObservableCollection<OcrData>> OcrAsync(this byte[] dosya, string tesseractlanguage, bool paragraphblock = false)
    {
        if (dosya is null)
        {
            throw new ArgumentNullException(nameof(dosya));
        }

        if (string.IsNullOrWhiteSpace(tesseractlanguage))
        {
            throw new ArgumentNullException(nameof(tesseractlanguage));
        }

        if (!Directory.Exists(TesseractPath))
        {
            throw new ArgumentNullException(nameof(TesseractPath));
        }

        ocrcancellationToken = new CancellationTokenSource();
        return paragraphblock ? await Task.Run(() => dosya.GetOcrData(tesseractlanguage, PageIteratorLevel.Para), ocrcancellationToken.Token) : await Task.Run(() => dosya.GetOcrData(tesseractlanguage), ocrcancellationToken.Token);
    }

    private static TesseractEngine CreateTesseractEngine(string tesseractLanguage) => new(TesseractPath, tesseractLanguage, EngineMode.LstmOnly);

    private static ObservableCollection<OcrData> GetOcrData(this byte[] dosya, string tesseractlanguage)
    {
        if (dosya is null)
        {
            throw new ArgumentNullException(nameof(dosya));
        }

        if (string.IsNullOrWhiteSpace(tesseractlanguage))
        {
            throw new ArgumentNullException(nameof(tesseractlanguage));
        }

        using TesseractEngine engine = CreateTesseractEngine(tesseractlanguage);
        using Pix pixImage = Pix.LoadFromMemory(dosya);
        using Page page = engine.Process(pixImage);
        using ResultIterator iterator = page?.GetIterator();
        if (iterator != null)
        {
            iterator.Begin();
            ObservableCollection<OcrData> ocrdata = iterator.IterateOcr(PageIteratorLevel.Word);
            dosya = null;

            return ocrdata;
        }

        return null;
    }

    private static ObservableCollection<OcrData> GetOcrData(this byte[] dosya, string tesseractlanguage, PageIteratorLevel pageIteratorLevel)
    {
        if (dosya is null)
        {
            throw new ArgumentNullException(nameof(dosya));
        }

        using TesseractEngine engine = CreateTesseractEngine(tesseractlanguage);
        using Pix pixImage = Pix.LoadFromMemory(dosya);
        using Page page = engine.Process(pixImage);
        using ResultIterator iterator = page?.GetIterator();
        if (iterator != null)
        {
            iterator.Begin();
            ObservableCollection<OcrData> ocrdata = iterator.IterateOcr(pageIteratorLevel);
            dosya = null;

            return ocrdata;
        }

        return null;
    }

    private static ObservableCollection<OcrData> IterateOcr(this ResultIterator iterator, PageIteratorLevel pageIteratorLevel)
    {
        ObservableCollection<OcrData> ocrdata = [];
        do
        {
            if (iterator?.TryGetBoundingBox(pageIteratorLevel, out Tesseract.Rect rect) == true)
            {
                Rect imgrect = new(rect.X1, rect.Y1, rect.Width, rect.Height);
                OcrData item = new() { Text = iterator?.GetText(pageIteratorLevel).Trim(), Rect = imgrect };
                if (!string.IsNullOrWhiteSpace(item.Text))
                {
                    ocrdata.Add(item);
                }
            }
        }
        while (iterator.Next(pageIteratorLevel));

        return ocrdata;
    }
}