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
    public abstract class PdfGeneration
    {
        public static Scanner Scanner { get; set; }

        public static int CalculateFontSize(string text, XRect adjustedBounds, XGraphics gfx)
        {
            int fontSizeGuess = Math.Max(1, (int)adjustedBounds.Height);
            XSize measuredBoundsForGuess = gfx.MeasureString(text, new XFont("Times New Roman", fontSizeGuess, XFontStyle.Regular));
            double adjustmentFactor = adjustedBounds.Width / measuredBoundsForGuess.Width;
            return Math.Max(1, (int)Math.Floor(fontSizeGuess * adjustmentFactor));
        }

        public static void DefaultPdfCompression(PdfDocument doc)
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

        public static PdfDocument ExtractPdfPages(string filename, int startpage, int endpage)
        {
            using PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Import);
            using PdfDocument outputDocument = new();
            for (int i = startpage - 1; i <= endpage - 1; i++)
            {
                _ = outputDocument.AddPage(inputDocument.Pages[i]);
            }
            return outputDocument;
        }

        public static PdfDocument GeneratePdf(List<ScannedImage> bitmapFrames, Format format, Paper paper, int jpegquality = 80, List<ObservableCollection<OcrData>> ScannedText = null, int dpi = 120)
        {
            using PdfDocument document = new();
            double index = 0;
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
                        if (ScannedText != null)
                        {
                            WritePdfTextContent(scannedimage.Resim, ScannedText[i], page, gfx, XBrushes.Transparent);
                        }
                        if (scannedimage.Resim.PixelWidth < scannedimage.Resim.PixelHeight)
                        {
                            gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                        }
                        else
                        {
                            page.Orientation = PageOrientation.Landscape;
                            gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                        }
                        index++;
                        Scanner.PdfSaveProgressValue = index / bitmapFrames.Count;
                    }
                    else
                    {
                        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                        BitmapSource resizedimage = scannedimage.Resim.Resize(page.Width, page.Height, 0, dpi, dpi);
                        using MemoryStream ms = new(resizedimage.ToTiffJpegByteArray(format, jpegquality));
                        using XImage xImage = XImage.FromStream(ms);
                        XSize size = PageSizeConverter.ToSize(page.Size);
                        resizedimage = null;
                        if (ScannedText != null)
                        {
                            WritePdfTextContent(scannedimage.Resim, ScannedText[i], page, gfx, XBrushes.Transparent);
                        }
                        if (scannedimage.Resim.PixelWidth < scannedimage.Resim.PixelHeight)
                        {
                            gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                        }
                        else
                        {
                            page.Orientation = PageOrientation.Landscape;
                            gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                        }
                        index++;
                        Scanner.PdfSaveProgressValue = index / bitmapFrames.Count;
                    }
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                DefaultPdfCompression(document);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
            return document;
        }

        public static PdfDocument GeneratePdf(List<string> imagefiles, Paper paper, List<ObservableCollection<OcrData>> ScannedText = null)
        {
            using PdfDocument document = new();
            double index = 0;
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
                    if (ScannedText != null)
                    {
                        WritePdfTextContent(xImage, ScannedText[i], page, gfx, XBrushes.Transparent);
                    }
                    if (xImage.PixelWidth < xImage.PixelHeight)
                    {
                        gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                    }
                    else
                    {
                        page.Orientation = PageOrientation.Landscape;
                        gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                    }
                    index++;
                    Scanner.PdfSaveProgressValue = index / imagefiles.Count;
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                DefaultPdfCompression(document);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
            return document;
        }

        public static PdfDocument GeneratePdf(BitmapSource bitmapframe, ObservableCollection<OcrData> ScannedText, Format format, Paper paper, int jpegquality = 80, bool pdfonlytext = false, int dpi = 120)
        {
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
                    BitmapSource resizedimage = bitmapframe.Resize(page.Width, page.Height, 0, dpi, dpi);
                    ms = new(resizedimage.ToTiffJpegByteArray(format, jpegquality));
                    resizedimage = null;
                }
                using XImage xImage = XImage.FromStream(ms);
                XSize size = PageSizeConverter.ToSize(page.Size);
                if (ScannedText is not null)
                {
                    if (pdfonlytext)
                    {
                        WritePdfTextContent(bitmapframe, ScannedText, page, gfx, XBrushes.Black);
                    }
                    else
                    {
                        WritePdfTextContent(bitmapframe, ScannedText, page, gfx, XBrushes.Transparent);
                    }
                }
                if (!pdfonlytext)
                {
                    if (bitmapframe.PixelWidth < bitmapframe.PixelHeight)
                    {
                        gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                    }
                    else
                    {
                        page.Orientation = PageOrientation.Landscape;
                        gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                    }
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }

                DefaultPdfCompression(document);
                ms = null;
                data = null;
                bitmapframe = null;
                return document;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
            return null;
        }

        public static string GetPdfScanPath()
        {
            return GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf");
        }

        public static string GetSaveFolder()
        {
            string datefolder = $@"{Settings.Default.AutoFolder}\{DateTime.Today.ToShortDateString()}";
            if (!Directory.Exists(datefolder))
            {
                _ = Directory.CreateDirectory(datefolder);
            }
            return datefolder;
        }

        public static bool IsValidPdfFile(IEnumerable<byte> buffer)
        {
            return buffer.SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        }

        public static PdfDocument MergePdf(string[] pdffiles)
        {
            try
            {
                using PdfDocument outputDocument = new();
                foreach (string file in pdffiles)
                {
                    PdfDocument inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                    int count = inputDocument.PageCount;
                    for (int idx = 0; idx < count; idx++)
                    {
                        PdfPage page = inputDocument.Pages[idx];
                        _ = outputDocument.AddPage(page);
                    }
                }
                return outputDocument;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }

            return null;
        }

        public static async void SavePdfFiles(string[] files)
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
                    await Task.Run(() => MergePdf(files).Save(saveFileDialog.FileName));
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }
        }

        private static XRect AdjustBounds(Rect rect, double hAdjust, double vAdjust)
        {
            return new(rect.X * hAdjust, rect.Y * vAdjust, rect.Width * hAdjust, rect.Height * vAdjust);
        }

        private static void ApplyPdfSecurity(PdfDocument document)
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

        private static void DrawGfx(XGraphics gfx, XBrush xBrush, XTextFormatter textformatter, OcrData item, XRect adjustedBounds)
        {
            int adjustedFontSize = CalculateFontSize(item.Text, adjustedBounds, gfx);
            XFont font = new("Times New Roman", adjustedFontSize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
            XSize adjustedTextSize = gfx.MeasureString(item.Text, font);
            double verticalOffset = (adjustedBounds.Height - adjustedTextSize.Height) / 2;
            double horizontalOffset = (adjustedBounds.Width - adjustedTextSize.Width) / 2;
            adjustedBounds.Offset(horizontalOffset, verticalOffset);
            textformatter.DrawString(item.Text, font, xBrush, adjustedBounds);
        }

        private static void SetPaperSize(Paper paper, PdfPage page)
        {
            if (paper is null)
            {
                page.Size = PageSize.A4;
                return;
            }
            switch (paper.PaperType)
            {
                case "A1":
                    page.Size = PageSize.A1;
                    break;

                case "A2":
                    page.Size = PageSize.A2;
                    break;

                case "A3":
                    page.Size = PageSize.A3;
                    break;

                case "A4":
                    page.Size = PageSize.A4;
                    break;

                case "A5":
                    page.Size = PageSize.A5;
                    break;

                case "B1":
                    page.Size = PageSize.B1;
                    break;

                case "B2":
                    page.Size = PageSize.B2;
                    break;

                case "B3":
                    page.Size = PageSize.B3;
                    break;

                case "B4":
                    page.Size = PageSize.B4;
                    break;

                case "B5":
                    page.Size = PageSize.B5;
                    break;

                case "Letter":
                    page.Size = PageSize.Letter;
                    break;

                case "Legal":
                    page.Size = PageSize.Legal;
                    break;

                case "Executive":
                    page.Size = PageSize.Executive;
                    break;
            }
        }

        private static void WritePdfTextContent(BitmapSource bitmapframe, ObservableCollection<OcrData> ScannedText, PdfPage page, XGraphics gfx, XBrush xBrush)
        {
            if (ScannedText is not null)
            {
                XTextFormatter textformatter = new(gfx);
                foreach (OcrData item in ScannedText)
                {
                    XRect adjustedBounds = AdjustBounds(item.Rect, page.Width / bitmapframe.PixelWidth, page.Height / bitmapframe.PixelHeight);
                    DrawGfx(gfx, xBrush, textformatter, item, adjustedBounds);
                }
            }
        }

        private static void WritePdfTextContent(XImage xImage, ObservableCollection<OcrData> ScannedText, PdfPage page, XGraphics gfx, XBrush xBrush)
        {
            if (ScannedText is not null)
            {
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