using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Extensions;
using Microsoft.Win32;
using MozJpeg;
using Ocr;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using TwainControl.Properties;
using static Extensions.ExtensionMethods;

namespace TwainControl
{
    public enum PdfPageLayout
    {
        Left = 0, Middle = 1, Right = 2, LeftBottom = 3, MiddleBottom = 4, RightBottom = 5,
    }

    public static class PdfGeneration
    {
        public static Scanner Scanner { get; set; }

        public static PdfDocument ArrangePdfPages(this string filename, int oldindex, int newindex)
        {
            using PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Modify);
            inputDocument.Pages.MovePage(oldindex, newindex);
            return inputDocument;
        }

        public static int CalculateFontSize(this string text, XRect adjustedBounds, XGraphics gfx)
        {
            int fontSizeGuess = Math.Max(1, (int)adjustedBounds.Height);
            XSize measuredBoundsForGuess = gfx.MeasureString(text, new XFont("Times New Roman", fontSizeGuess, XFontStyle.Regular));
            double adjustmentFactor = adjustedBounds.Width / measuredBoundsForGuess.Width;
            return Math.Max(1, (int)Math.Floor(fontSizeGuess * adjustmentFactor));
        }

        public static void DefaultPdfCompression(this PdfDocument doc)
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

        public static void DrawText(this XGraphics gfx, XBrush xBrush, string item, double x, double y, double fontsize = 16)
        {
            XFont font = new("Times New Roman", fontsize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
            gfx.DrawString(item, font, xBrush, x, y);
        }

        public static PdfDocument ExtractPdfPages(this string filename, int startpage, int endpage)
        {
            if (startpage > endpage)
            {
                throw new ArgumentOutOfRangeException(nameof(startpage), "start page should not be greater than end page");
            }
            using PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Import);
            using PdfDocument outputDocument = new();
            for (int i = startpage - 1; i <= endpage - 1; i++)
            {
                _ = outputDocument.AddPage(inputDocument.Pages[i]);
            }
            return outputDocument;
        }

        public static Task<PdfDocument> GeneratePdf(this List<ScannedImage> bitmapFrames, Format format, Paper paper, int jpegquality = 80, List<ObservableCollection<OcrData>> ScannedText = null, int dpi = 120)
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
                    SetPaperSize(paper, page);

                    if (Scanner.UseMozJpegEncoding && format != Format.Tiff)
                    {
                        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                        using MozJpeg.MozJpeg mozJpeg = new();
                        BitmapSource resizedimage = scannedimage.Resim.Resize(page.Width, page.Height, 0, dpi, dpi);
                        byte[] data = mozJpeg.Encode(resizedimage.BitmapSourceToBitmap(), jpegquality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
                        using MemoryStream ms = new(data);
                        using XImage xImage = XImage.FromStream(ms);
                        XSize size = PageSizeConverter.ToSize(page.Size);
                        resizedimage = null;
                        data = null;

                        if (scannedimage.Resim.PixelWidth < scannedimage.Resim.PixelHeight)
                        {
                            if (ScannedText?.ElementAtOrDefault(i) != null)
                            {
                                WritePdfTextContent(scannedimage.Resim, ScannedText[i], page, gfx, XBrushes.Transparent);
                            }
                            gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                        }
                        else
                        {
                            page.Orientation = PageOrientation.Landscape;
                            if (ScannedText?.ElementAtOrDefault(i) != null)
                            {
                                WritePdfTextContent(scannedimage.Resim, ScannedText[i], page, gfx, XBrushes.Transparent);
                            }
                            gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                        }
                        if (Scanner.PdfPageNumberDraw)
                        {
                            gfx.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfAlignTextColor)), (i + 1).ToString(), GetPdfTextLayout(page)[0], GetPdfTextLayout(page)[1]);
                        }
                        Scanner.PdfSaveProgressValue = i / (double)bitmapFrames.Count;
                    }
                    else
                    {
                        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                        BitmapSource resizedimage = format == Format.Tiff
                            ? scannedimage.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Settings.Default.BwThreshold, false).ToBitmapImage(System.Drawing.Imaging.ImageFormat.Tiff).Resize(page.Width, page.Height, 0, dpi, dpi)
                            : scannedimage.Resim.Resize(page.Width, page.Height, 0, dpi, dpi);
                        using MemoryStream ms = new(resizedimage.ToTiffJpegByteArray(format, jpegquality));
                        using XImage xImage = XImage.FromStream(ms);
                        XSize size = PageSizeConverter.ToSize(page.Size);
                        resizedimage = null;

                        if (scannedimage.Resim.PixelWidth < scannedimage.Resim.PixelHeight)
                        {
                            if (ScannedText?.ElementAtOrDefault(i) != null)
                            {
                                WritePdfTextContent(scannedimage.Resim, ScannedText[i], page, gfx, XBrushes.Transparent);
                            }
                            gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                        }
                        else
                        {
                            page.Orientation = PageOrientation.Landscape;
                            if (ScannedText?.ElementAtOrDefault(i) != null)
                            {
                                WritePdfTextContent(scannedimage.Resim, ScannedText[i], page, gfx, XBrushes.Transparent);
                            }
                            gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                        }
                        if (Scanner.PdfPageNumberDraw)
                        {
                            gfx.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfAlignTextColor)), (i + 1).ToString(), GetPdfTextLayout(page)[0], GetPdfTextLayout(page)[1]);
                        }
                        Scanner.PdfSaveProgressValue = i / (double)bitmapFrames.Count;
                    }
                    if (Settings.Default.RemoveProcessedImage)
                    {
                        scannedimage.Resim =null;
                    }
                    GC.Collect();
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                document.DefaultPdfCompression();
                Scanner.PdfSaveProgressValue = 0;
            }
            catch (Exception ex)
            {
                bitmapFrames = null;
                ScannedText = null;
                throw new ArgumentException(nameof(document), ex);
            }
            return Task.FromResult(document);
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
                    SetPaperSize(paper, page);
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    using XImage xImage = XImage.FromFile(imagefile);
                    XSize size = PageSizeConverter.ToSize(page.Size);
                    if (xImage.PixelWidth < xImage.PixelHeight)
                    {
                        if (ScannedText?.ElementAtOrDefault(i) != null)
                        {
                            WritePdfTextContent(xImage, ScannedText[i], page, gfx, XBrushes.Transparent);
                        }
                        gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                    }
                    else
                    {
                        page.Orientation = PageOrientation.Landscape;
                        if (ScannedText?.ElementAtOrDefault(i) != null)
                        {
                            WritePdfTextContent(xImage, ScannedText[i], page, gfx, XBrushes.Transparent);
                        }
                        gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                    }
                    if (Scanner.PdfPageNumberDraw)
                    {
                        gfx.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfAlignTextColor)), (i + 1).ToString(), GetPdfTextLayout(page)[0], GetPdfTextLayout(page)[1]);
                    }
                    Scanner.PdfSaveProgressValue = i / (double)imagefiles.Count;
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                document.DefaultPdfCompression();
                Scanner.PdfSaveProgressValue = 0;
            }
            catch (Exception ex)
            {
                imagefiles = null;
                ScannedText = null;
                throw new ArgumentException(nameof(document), ex);
            }
            return document;
        }

        public static PdfDocument GeneratePdf(this string imagefile, Paper paper, ObservableCollection<OcrData> ScannedText = null)
        {
            using PdfDocument document = new();
            try
            {
                Scanner.ProgressState = TaskbarItemProgressState.Normal;

                PdfPage page = document.AddPage();
                SetPaperSize(paper, page);
                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                using XImage xImage = XImage.FromFile(imagefile);
                XSize size = PageSizeConverter.ToSize(page.Size);
                if (xImage.PixelWidth < xImage.PixelHeight)
                {
                    if (ScannedText != null)
                    {
                        WritePdfTextContent(xImage, ScannedText, page, gfx, XBrushes.Transparent);
                    }
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
                else
                {
                    page.Orientation = PageOrientation.Landscape;
                    if (ScannedText != null)
                    {
                        WritePdfTextContent(xImage, ScannedText, page, gfx, XBrushes.Transparent);
                    }
                    gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                }
                if (Scanner.PdfPageNumberDraw)
                {
                    gfx.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfAlignTextColor)), "1", GetPdfTextLayout(page)[0], GetPdfTextLayout(page)[1]);
                }

                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                document.DefaultPdfCompression();
                Scanner.PdfSaveProgressValue = 0;
            }
            catch (Exception ex)
            {
                imagefile = null;
                ScannedText = null;
                throw new ArgumentException(nameof(document), ex);
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
                SetPaperSize(paper, page);
                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                byte[] data = null;
                MemoryStream ms;
                if (Scanner.UseMozJpegEncoding && format != Format.Tiff)
                {
                    using MozJpeg.MozJpeg mozJpeg = new();
                    BitmapSource resizedimage = bitmapframe.Resize(page.Width, page.Height, 0, dpi, dpi);
                    data = mozJpeg.Encode(resizedimage.BitmapSourceToBitmap(), jpegquality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
                    ms = new MemoryStream(data);
                    resizedimage = null;
                }
                else
                {
                    BitmapSource resizedimage = format == Format.Tiff
                        ? bitmapframe.BitmapSourceToBitmap().ConvertBlackAndWhite(Settings.Default.BwThreshold, false).ToBitmapImage(System.Drawing.Imaging.ImageFormat.Tiff).Resize(page.Width, page.Height, 0, dpi, dpi)
                        : bitmapframe.Resize(page.Width, page.Height, 0, dpi, dpi);
                    ms = new(resizedimage.ToTiffJpegByteArray(format, jpegquality));
                    resizedimage = null;
                }
                using XImage xImage = XImage.FromStream(ms);
                XSize size = PageSizeConverter.ToSize(page.Size);

                if (bitmapframe.PixelWidth < bitmapframe.PixelHeight)
                {
                    if (ScannedText is not null)
                    {
                        WritePdfTextContent(bitmapframe, ScannedText, page, gfx, XBrushes.Transparent);
                    }
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
                else
                {
                    page.Orientation = PageOrientation.Landscape;
                    if (ScannedText is not null)
                    {
                        WritePdfTextContent(bitmapframe, ScannedText, page, gfx, XBrushes.Transparent);
                    }
                    gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                }
                if (Scanner.PdfPageNumberDraw)
                {
                    gfx.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfAlignTextColor)), "1", GetPdfTextLayout(page)[0], GetPdfTextLayout(page)[1]);
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }

                document.DefaultPdfCompression();
                ms = null;
                data = null;
                bitmapframe = null;
                GC.Collect();
                return document;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(nameof(bitmapframe), ex);
            }
        }

        public static string GetPdfScanPath()
        {
            return GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf");
        }

        public static double[] GetPdfTextLayout(PdfPage page)
        {
            return Scanner.Layout switch
            {
                PdfPageLayout.Left => new double[] { 30, 30 },
                PdfPageLayout.Middle => new double[] { page.Width / 2, 30 },
                PdfPageLayout.Right => new double[] { page.Width - 30, 30 },
                PdfPageLayout.LeftBottom => new double[] { 30, page.Height - 30 },
                PdfPageLayout.MiddleBottom => new double[] { page.Width / 2, page.Height - 30 },
                PdfPageLayout.RightBottom => new double[] { page.Width - 30, page.Height - 30 },
                _ => new double[] { 0, 0 }
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

        public static bool IsValidPdfFile(this IEnumerable<byte> buffer)
        {
            return buffer?.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 }) == true;
        }

        public static PdfDocument MergePdf(this string[] pdffiles)
        {
            try
            {
                using PdfDocument outputDocument = new();
                foreach (PdfDocument inputDocument in from string file in pdffiles let inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import) select inputDocument)
                {
                    for (int i = 0; i < inputDocument.PageCount; i++)
                    {
                        PdfPage page = inputDocument.Pages[i];
                        _ = outputDocument.AddPage(page);
                    }
                }

                return outputDocument;
            }
            catch (Exception ex)
            {
                pdffiles = null;
                throw new ArgumentException(nameof(pdffiles), ex);
            }
        }

        public static async Task SavePdfFiles(this string[] files)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                FileName = Translation.GetResStringValue("MERGE")
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await Task.Run(() => files.MergePdf().Save(saveFileDialog.FileName));
                }
                catch (Exception ex)
                {
                    files = null;
                    throw new ArgumentException(nameof(files), ex);
                }
            }
        }

        public static void SetPaperSize(this Paper paper, PdfPage page)
        {
            page.Size = paper == null || !paperSizes.TryGetValue(paper.PaperType, out PageSize pageSize) ? PageSize.A4 : pageSize;
        }

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
        };

        private static XRect AdjustBounds(this Rect rect, double hAdjust, double vAdjust)
        {
            return new(rect.X * hAdjust, rect.Y * vAdjust, rect.Width * hAdjust, rect.Height * vAdjust);
        }

        private static void ApplyPdfSecurity(this PdfDocument document)
        {
            PdfSecuritySettings securitySettings = document.SecuritySettings;
            if (Scanner.PdfPassword is not null)
            {
                securitySettings.OwnerPassword = Scanner.PdfPassword.ToString();
                securitySettings.PermitModifyDocument = Scanner.AllowEdit;
                securitySettings.PermitPrint = Scanner.AllowPrint;
                securitySettings.PermitExtractContent = Scanner.AllowCopy;
            }
        }

        private static void DrawGfx(this XGraphics gfx, XBrush xBrush, XTextFormatter textformatter, OcrData item, XRect adjustedBounds)
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
                    DrawGfx(gfx, xBrush, textformatter, item, adjustedBounds);
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
                    DrawGfx(gfx, xBrush, textformatter, item, adjustedBounds);
                }
            }
        }
    }
}