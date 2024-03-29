﻿using Microsoft.VisualBasic;
using Microsoft.Win32;
using MozJpeg;
using Ocr;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using TwainControl.Properties;
using static Extensions.ExtensionMethods;

namespace TwainControl;

public static class PdfGeneration
{
    private static readonly Dictionary<string, PageSize> paperSizes = new()
    {
        { "A0", PageSize.A0 },
        { "A1", PageSize.A1 },
        { "A2", PageSize.A2 },
        { "A3", PageSize.A3 },
        { "A4", PageSize.A4 },
        { "A5", PageSize.A5 },
        { "B0", PageSize.B0 },
        { "B1", PageSize.B1 },
        { "B2", PageSize.B2 },
        { "B3", PageSize.B3 },
        { "B4", PageSize.B4 },
        { "B5", PageSize.B5 },
        { "Letter", PageSize.Letter },
        { "Legal", PageSize.Legal },
        { "Executive", PageSize.Executive },
        { "Original", PageSize.Undefined }
    };

    public static Scanner Scanner { get; set; }

    public static void ApplyDefaultPdfCompression(this PdfDocument doc)
    {
        doc.Info.Author = Scanner.UserName;
        doc.Info.Creator = Scanner.CreatorAppName;
        doc.Info.CreationDate = DateTime.Now;
        doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        doc.Options.CompressContentStreams = true;
        doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
        doc.Options.NoCompression = false;
        doc.Options.EnableCcittCompressionForBilevelImages = true;
    }

    public static PdfDocument ArrangePdfPages(this string filename, int oldindex, int newindex)
    {
        using PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Modify, PasswordProvider);
        if (inputDocument != null)
        {
            inputDocument.Pages.MovePage(oldindex, newindex);
            return inputDocument;
        }
        return null;
    }

    public static int CalculateFontSize(this string text, XRect adjustedBounds, XGraphics gfx)
    {
        int fontSizeGuess = Math.Max(1, (int)adjustedBounds.Height);
        XSize measuredBoundsForGuess =
            gfx.MeasureString(text, new XFont("Times New Roman", fontSizeGuess, XFontStyle.Regular));
        double adjustmentFactor = adjustedBounds.Width / measuredBoundsForGuess.Width;
        return Math.Max(1, (int)Math.Floor(fontSizeGuess * adjustmentFactor));
    }

    public static void DrawPdfOverlayText(PdfPage page, XGraphics gfx, double textsize, string text, XBrush xBrush, string familyName, double angle = 315)
    {
        XFont font = new(familyName, textsize);
        XSize fontsize = gfx.MeasureString(text, font);
        XStringFormat textformat = new() { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Near };
        gfx.TranslateTransform(page.Width / 2, page.Height / 2);
        gfx.RotateTransform(angle);
        gfx.TranslateTransform(-page.Width / 2, -page.Height / 2);
        gfx?.DrawString(text, font, xBrush, new XPoint((page.Width - fontsize.Width) / 2, (page.Height - fontsize.Height) / 2), textformat);
    }

    public static void DrawText(this XGraphics gfx, XBrush xBrush, string item, double x, double y, double fontsize = 16)
    {
        XFont font = new("Times New Roman", fontsize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
        gfx?.DrawString(item, font, xBrush, x, y);
    }

    public static PdfDocument ExtractPdfPages(this string filename, int startpage, int endpage)
    {
        if (startpage > endpage)
        {
            throw new ArgumentOutOfRangeException(nameof(startpage), "start page should not be greater than end page");
        }

        using PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Import, PasswordProvider);
        if (inputDocument != null)
        {
            using PdfDocument outputDocument = new();
            for (int i = startpage - 1; i <= endpage - 1; i++)
            {
                _ = outputDocument.AddPage(inputDocument?.Pages[i]);
            }
            return outputDocument;
        }
        return null;
    }

    public static PdfDocument GeneratePdf(this List<string> imagefiles, Paper paper, List<ObservableCollection<OcrData>> ScannedText = null)
    {
        if (imagefiles?.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imagefiles), "bitmapframes count should be greater than zero");
        }

        using PdfDocument document = new();
        try
        {
            Scanner.ProgressState = TaskbarItemProgressState.Normal;
            for (int i = 0; i < imagefiles.Count; i++)
            {
                string imagefile = imagefiles[i];
                PdfPage page = document.AddPage();
                page.Size = paper.GetPaperSize();
                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                using XImage xImage = XImage.FromFile(imagefile);
                XSize size = PageSizeConverter.ToSize(page.Size);
                if (xImage.PixelWidth < xImage.PixelHeight)
                {
                    page.Orientation = PageOrientation.Portrait;
                    if (ScannedText?.ElementAtOrDefault(i) != null)
                    {
                        WritePdfTextContent(xImage, ScannedText[i], page, gfx, XBrushes.Transparent);
                    }

                    gfx?.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
                else
                {
                    page.Orientation = PageOrientation.Landscape;
                    if (ScannedText?.ElementAtOrDefault(i) != null)
                    {
                        WritePdfTextContent(xImage, ScannedText[i], page, gfx, XBrushes.Transparent);
                    }

                    gfx?.DrawImage(xImage, 0, 0, size.Height, size.Width);
                }
                Scanner.PdfSaveProgressValue = i / (double)imagefiles.Count;
            }

            if (Scanner.PasswordProtect)
            {
                document.ApplyPdfSecurity();
            }

            document.ApplyDefaultPdfCompression();
            Scanner.PdfSaveProgressValue = 0;
        }
        catch (Exception ex)
        {
            imagefiles = null;
            ScannedText = null;
            throw new ArgumentException(ex?.Message);
        }

        return document;
    }

    public static PdfDocument GeneratePdf(this string imagefile, Paper paper, ObservableCollection<OcrData> ScannedText = null)
    {
        using PdfDocument document = new();
        try
        {
            PdfPage page = document.AddPage();
            page.Size = paper.GetPaperSize();
            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            using XImage xImage = XImage.FromFile(imagefile);
            XSize size = PageSizeConverter.ToSize(page.Size);
            if (xImage.PixelWidth < xImage.PixelHeight)
            {
                page.Orientation = PageOrientation.Portrait;
                if (ScannedText != null)
                {
                    WritePdfTextContent(xImage, ScannedText, page, gfx, XBrushes.Transparent);
                }

                gfx?.DrawImage(xImage, 0, 0, size.Width, size.Height);
            }
            else
            {
                page.Orientation = PageOrientation.Landscape;
                if (ScannedText != null)
                {
                    WritePdfTextContent(xImage, ScannedText, page, gfx, XBrushes.Transparent);
                }

                gfx?.DrawImage(xImage, 0, 0, size.Height, size.Width);
            }
            if (Scanner.PasswordProtect)
            {
                document.ApplyPdfSecurity();
            }
            document.ApplyDefaultPdfCompression();
        }
        catch (Exception ex)
        {
            imagefile = null;
            ScannedText = null;
            throw new ArgumentException(ex?.Message);
        }

        return document;
    }

    public static PdfDocument GeneratePdf(this BitmapSource bitmapframe, ObservableCollection<OcrData> ScannedText, Format format, Paper paper, int jpegquality = 80, int dpi = 120)
    {
        if (bitmapframe is null)
        {
            throw new ArgumentNullException(nameof(bitmapframe), "bitmapframe can not be null");
        }

        try
        {
            using PdfDocument document = new();
            PdfPage page = document.AddPage();
            page.Orientation = bitmapframe.PixelWidth < bitmapframe.PixelHeight ? PageOrientation.Portrait : PageOrientation.Landscape;
            bool resizepaper = paper.GetPaperSize() != PageSize.Undefined;
            XSize size = default;
            switch (paper.PaperType)
            {
                case "Custom":
                    size.Width = XUnit.FromCentimeter(paper.Width);
                    size.Height = XUnit.FromCentimeter(paper.Height);
                    page.MediaBox = new PdfRectangle(new XRect(0, 0, size.Width, size.Height));
                    break;

                case "Original":
                    page.Width = bitmapframe.PixelWidth;
                    page.Height = bitmapframe.PixelHeight;
                    size.Width = page.Orientation == PageOrientation.Portrait ? bitmapframe.PixelWidth : bitmapframe.PixelHeight;
                    size.Height = page.Orientation == PageOrientation.Portrait ? bitmapframe.PixelHeight : bitmapframe.PixelWidth;
                    break;

                default:
                    page.Size = paper.GetPaperSize();
                    size = PageSizeConverter.ToSize(page.Size);
                    break;
            }

            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            byte[] data = null;
            MemoryStream ms;
            if (Scanner.UseMozJpegEncoding && format != Format.Tiff)
            {
                using MozJpeg.MozJpeg mozJpeg = new();
                BitmapSource resizedimage = resizepaper ? bitmapframe.Resize(page.Width, page.Height, 0, dpi, dpi) : bitmapframe;
                data = mozJpeg.Encode(resizedimage.BitmapSourceToBitmap(), jpegquality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
                ms = new MemoryStream(data);
                resizedimage = null;
            }
            else
            {
                BitmapSource resizedimage;
                if (format == Format.Tiff)
                {
                    BitmapImage bitmapImage = bitmapframe.BitmapSourceToBitmap().ConvertBlackAndWhite(Settings.Default.BwThreshold).ToBitmapImage(ImageFormat.Tiff);
                    resizedimage = resizepaper ? bitmapImage.Resize(page.Width, page.Height, 0, dpi, dpi) : bitmapImage;
                    bitmapImage = null;
                }
                else
                {
                    resizedimage = resizepaper ? bitmapframe.Resize(page.Width, page.Height, 0, dpi, dpi) : bitmapframe;
                }

                ms = new MemoryStream(resizedimage?.ToTiffJpegByteArray(format, jpegquality));
                resizedimage = null;
            }

            using XImage xImage = XImage.FromStream(ms);

            if (ScannedText is not null)
            {
                WritePdfTextContent(bitmapframe, ScannedText, page, gfx, XBrushes.Transparent);
            }

            if (page.Orientation == PageOrientation.Portrait)
            {
                gfx?.DrawImage(xImage, 0, 0, size.Width, size.Height);
            }
            else
            {
                gfx?.DrawImage(xImage, 0, 0, size.Height, size.Width);
            }

            if (Scanner.PasswordProtect)
            {
                document.ApplyPdfSecurity();
            }
            document.ApplyDefaultPdfCompression();
            ms?.Dispose();
            ms = null;
            data = null;
            bitmapframe = null;
            return document;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
        }
    }

    public static Task<PdfDocument> GeneratePdfAsync(this List<ScannedImage> bitmapFrames, Format format, Paper paper, int jpegquality = 80, List<ObservableCollection<OcrData>> ScannedText = null, int dpi = 120)
    {
        if (bitmapFrames?.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitmapFrames), "bitmap frames count should be greater than zero");
        }

        using PdfDocument document = new();
        try
        {
            Scanner.ProgressState = TaskbarItemProgressState.Normal;
            for (int i = 0; i < bitmapFrames.Count; i++)
            {
                ScannedImage scannedimage = bitmapFrames[i];
                PdfPage page = document.AddPage();
                page.Orientation = scannedimage.Resim.PixelWidth < scannedimage.Resim.PixelHeight ? PageOrientation.Portrait : PageOrientation.Landscape;
                bool resizepaper = paper.GetPaperSize() != PageSize.Undefined;
                XSize size = default;
                switch (paper.PaperType)
                {
                    case "Custom":
                        size.Width = XUnit.FromCentimeter(paper.Width);
                        size.Height = XUnit.FromCentimeter(paper.Height);
                        page.MediaBox = new PdfRectangle(new XRect(0, 0, size.Width, size.Height));
                        break;

                    case "Original":
                        page.Width = scannedimage.Resim.PixelWidth;
                        page.Height = scannedimage.Resim.PixelHeight;
                        size.Width = page.Orientation == PageOrientation.Portrait ? scannedimage.Resim.PixelWidth : scannedimage.Resim.PixelHeight;
                        size.Height = page.Orientation == PageOrientation.Portrait ? scannedimage.Resim.PixelHeight : scannedimage.Resim.PixelWidth;
                        break;

                    default:
                        page.Size = paper.GetPaperSize();
                        size = PageSizeConverter.ToSize(page.Size);
                        break;
                }

                if (Scanner.UseMozJpegEncoding && format != Format.Tiff)
                {
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    using MozJpeg.MozJpeg mozJpeg = new();
                    BitmapSource resizedimage = resizepaper ? scannedimage.Resim.Resize(page.Width, page.Height, 0, dpi, dpi) : scannedimage.Resim;
                    byte[] data = mozJpeg.Encode(resizedimage.BitmapSourceToBitmap(), jpegquality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
                    using MemoryStream ms = new(data);
                    using XImage xImage = XImage.FromStream(ms);
                    resizedimage = null;
                    data = null;

                    if (ScannedText?[i] != null)
                    {
                        WritePdfTextContent(scannedimage.Resim, ScannedText[i], page, gfx, XBrushes.Transparent);
                    }

                    if (page.Orientation == PageOrientation.Portrait)
                    {
                        gfx?.DrawImage(xImage, 0, 0, size.Width, size.Height);
                    }
                    else
                    {
                        gfx?.DrawImage(xImage, 0, 0, size.Height, size.Width);
                    }
                }
                else
                {
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    BitmapSource resizedimage;
                    if (format == Format.Tiff)
                    {
                        BitmapImage bitmapImage = scannedimage.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Settings.Default.BwThreshold).ToBitmapImage(ImageFormat.Tiff);
                        resizedimage = resizepaper ? bitmapImage.Resize(page.Width, page.Height, 0, dpi, dpi) : bitmapImage;
                        bitmapImage = null;
                    }
                    else
                    {
                        resizedimage = resizepaper ? scannedimage.Resim.Resize(page.Width, page.Height, 0, dpi, dpi) : scannedimage.Resim;
                    }

                    using MemoryStream ms = new(resizedimage?.ToTiffJpegByteArray(format, jpegquality));
                    using XImage xImage = XImage.FromStream(ms);
                    resizedimage = null;

                    if (ScannedText?[i] != null)
                    {
                        WritePdfTextContent(scannedimage.Resim, ScannedText?[i], page, gfx, XBrushes.Transparent);
                    }

                    if (page.Orientation == PageOrientation.Portrait)
                    {
                        gfx?.DrawImage(xImage, 0, 0, size.Width, size.Height);
                    }
                    else
                    {
                        gfx?.DrawImage(xImage, 0, 0, size.Height, size.Width);
                    }
                }

                Scanner.PdfSaveProgressValue = i / (double)bitmapFrames.Count;
                if (Settings.Default.RemoveProcessedImage)
                {
                    scannedimage.Resim = null;
                }
            }

            if (Scanner.PasswordProtect)
            {
                document.ApplyPdfSecurity();
            }

            document.ApplyDefaultPdfCompression();
            Scanner.PdfSaveProgressValue = 0;
        }
        catch (Exception ex)
        {
            bitmapFrames = null;
            ScannedText = null;
            throw new ArgumentException(ex?.Message);
        }

        return Task.FromResult(document);
    }

    public static PdfDocument GenerateWatermarkedPdf(this PdfDocument pdfdocument, int sayfa, double rotation, SolidColorBrush textcolor, double textsize, string text, string font)
    {
        PdfPage page = pdfdocument.Pages[sayfa];
        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        XBrush brush = new XSolidBrush(XColor.FromArgb(textcolor.Color.A, textcolor.Color.R, textcolor.Color.G, textcolor.Color.B));
        DrawPdfOverlayText(page, gfx, textsize, text, brush, font, rotation);
        return pdfdocument;
    }

    public static PageSize GetPaperSize(this Paper paper) => paper == null || !paperSizes.TryGetValue(paper.PaperType, out PageSize pageSize) ? PageSize.A4 : pageSize;

    public static string GetPdfScanPath() => GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf");

    public static double[] GetPdfTextLayout(PdfPage page, double x = 30)
    {
        return Scanner.Layout switch
        {
            PdfPageLayout.Left => [30, 30],
            PdfPageLayout.Middle => [(page.Width / 2) - (x / 2), 30],
            PdfPageLayout.Right => [page.Width - x - 30, 30],
            PdfPageLayout.LeftBottom => [30, page.Height - 30],
            PdfPageLayout.MiddleBottom => [(page.Width / 2) - (x / 2), page.Height - 30],
            PdfPageLayout.RightBottom => [page.Width - x - 30, page.Height - 30],
            _ => [0, 0]
        };
    }

    public static string GetSaveFolder()
    {
        string datefolder = DateTime.Today.ToString(Settings.Default.FolderDateFormat);
        string savefolder = $@"{Settings.Default.AutoFolder}\{datefolder}";
        if (!Directory.Exists(savefolder))
        {
            _ = Directory.CreateDirectory(savefolder);
        }

        return savefolder;
    }

    public static PdfDocument MergePdf(this string[] pdffiles)
    {
        try
        {
            using PdfDocument outputDocument = new();
            foreach (PdfDocument inputDocument in from string file in pdffiles let inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import, PasswordProvider) select inputDocument)
            {
                if (inputDocument is null)
                {
                    return null;
                }
                for (int i = 0; i < inputDocument.PageCount; i++)
                {
                    PdfPage page = inputDocument.Pages[i];
                    _ = outputDocument.AddPage(page);
                }
                inputDocument.Dispose();
            }
            return outputDocument;
        }
        catch (Exception ex)
        {
            pdffiles = null;
            throw new ArgumentException(ex?.Message);
        }
    }

    public static void PasswordProvider(PdfPasswordProviderArgs args)
    {
        string password = Interaction.InputBox($"{Translation.GetResStringValue("DOCUMENT")} {Translation.GetResStringValue("PASSWORD")}", Translation.GetResStringValue("PASSWORD"), string.Empty);
        if (!string.IsNullOrWhiteSpace(password))
        {
            args.Password = password;
        }
        else
        {
            args.Abort = true;
        }
    }

    public static async Task SavePdfFilesAsync(this string[] files)
    {
        SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = Translation.GetResStringValue("MERGE") };
        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                await Task.Run(() => files?.MergePdf()?.Save(saveFileDialog.FileName));
            }
            catch (Exception ex)
            {
                files = null;
                throw new ArgumentException(ex?.Message);
            }
        }
    }

    public static async Task<string[]> WritePdfToJpgFileAsync(string pdffilepath, int dpi, Action<double> progresscallback)
    {
        if (!PdfViewer.PdfViewer.IsValidPdfFile(pdffilepath))
        {
            return null;
        }
        List<string> jpgfiles = [];
        string filename = Path.GetFileNameWithoutExtension(pdffilepath);
        await Task.Run(
            () =>
            {
                using PdfiumViewer.PdfDocument pdfDoc = PdfiumViewer.PdfDocument.Load(pdffilepath);
                for (int i = 0; i < pdfDoc.PageCount; i++)
                {
                    string outfilename = $"{Path.GetTempPath()}{filename}{i}.jpg";
                    if (File.Exists(outfilename))
                    {
                        jpgfiles.Add(outfilename);
                        progresscallback((i + 1) / (double)pdfDoc.PageCount);
                        continue;
                    }
                    int width = (int)(pdfDoc.PageSizes[i].Width / 72 * dpi);
                    int height = (int)(pdfDoc.PageSizes[i].Height / 72 * dpi);
                    Image image = pdfDoc.Render(i, width, height, dpi, dpi, false);
                    image.Save(outfilename, ImageFormat.Jpeg);
                    jpgfiles.Add(outfilename);
                    progresscallback((i + 1) / (double)pdfDoc.PageCount);
                }
            });
        return [.. jpgfiles];
    }

    private static XRect AdjustBounds(this Rect rect, double hAdjust, double vAdjust) => new(rect.X * hAdjust, rect.Y * vAdjust, rect.Width * hAdjust, rect.Height * vAdjust);

    private static void ApplyPdfSecurity(this PdfDocument document)
    {
        PdfSecuritySettings securitySettings = document.SecuritySettings;
        if (!string.IsNullOrWhiteSpace(Scanner.PdfPassword))
        {
            securitySettings.OwnerPassword = Scanner.PdfPassword;
            securitySettings.PermitModifyDocument = Scanner.AllowEdit;
            securitySettings.PermitPrint = Scanner.AllowPrint;
            securitySettings.PermitExtractContent = Scanner.AllowCopy;
        }
    }

    private static void DrawPdfOcrGfx(this XGraphics gfx, XBrush xBrush, XTextFormatter textformatter, OcrData item, XRect adjustedBounds)
    {
        int adjustedFontSize = CalculateFontSize(item.Text, adjustedBounds, gfx);
        XFont font = new("Times New Roman", adjustedFontSize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
        XSize adjustedTextSize = gfx.MeasureString(item.Text, font);
        double verticalOffset = (adjustedBounds.Height - adjustedTextSize.Height) / 2;
        double horizontalOffset = (adjustedBounds.Width - adjustedTextSize.Width) / 2;
        adjustedBounds.Offset(horizontalOffset, verticalOffset);
        textformatter.DrawString(item.Text, font, xBrush, adjustedBounds);
    }

    private static void WritePdfTextContent(this BitmapSource bitmapframe, ObservableCollection<OcrData> ScannedText, PdfPage page, XGraphics gfx, XBrush xBrush)
    {
        if (ScannedText is not null)
        {
            if (bitmapframe is null)
            {
                throw new ArgumentNullException(nameof(bitmapframe), "bitmapframe can not be null");
            }

            XTextFormatter textformatter = new(gfx);
            foreach (OcrData item in ScannedText)
            {
                XRect adjustedBounds = AdjustBounds(item.Rect, page.Width / bitmapframe.PixelWidth, page.Height / bitmapframe.PixelHeight);
                DrawPdfOcrGfx(gfx, xBrush, textformatter, item, adjustedBounds);
            }
        }
    }

    private static void WritePdfTextContent(this XImage xImage, ObservableCollection<OcrData> ScannedText, PdfPage page, XGraphics gfx, XBrush xBrush)
    {
        if (ScannedText is not null)
        {
            if (xImage is null)
            {
                throw new ArgumentNullException(nameof(xImage), "bitmapframe can not be null");
            }

            XTextFormatter textformatter = new(gfx);
            foreach (OcrData item in ScannedText)
            {
                XRect adjustedBounds = AdjustBounds(item.Rect, page.Width / xImage.PixelWidth, page.Height / xImage.PixelHeight);
                DrawPdfOcrGfx(gfx, xBrush, textformatter, item, adjustedBounds);
            }
        }
    }
}