using Microsoft.VisualBasic;
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
using System.Text;
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

    public static MemoryStream AddPdfPassword_PdfSharp(this MemoryStream unencryptedPdf)
    {
        if (!string.IsNullOrWhiteSpace(Scanner.PdfPassword))
        {
            unencryptedPdf.Position = 0;
            using PdfDocument document = PdfReader.Open(unencryptedPdf, PdfDocumentOpenMode.Modify);
            document.SecuritySettings.OwnerPassword = Scanner.PdfPassword;
            document.SecuritySettings.PermitModifyDocument = Scanner.AllowEdit;
            document.SecuritySettings.PermitPrint = Scanner.AllowPrint;
            document.SecuritySettings.PermitExtractContent = Scanner.AllowCopy;
            MemoryStream encryptedPdf = new();
            document.Save(encryptedPdf);
            encryptedPdf.Position = 0;
            return encryptedPdf;
        }
        else
        {
            unencryptedPdf.Position = 0;
            return unencryptedPdf;
        }
    }

    public static void AddTextContentIfNeeded(BitmapSource image, ObservableCollection<OcrData> textData, PdfPage page, XGraphics gfx)
    {
        if (textData?.Any() == true)
        {
            WritePdfTextContent(image, textData, page, gfx, XBrushes.Transparent);
        }
    }

    public static void ApplyDefaultPdfCompression(this PdfDocument doc)
    {
        if (doc is null)
        {
            return;
        }

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
        if (inputDocument is not null)
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

    public static MemoryStream CreateMultipagePdfWithJbig2Images(this List<ScannedImage> pages, IProgress<double> progress = null)
    {
        MemoryStream output = new();
        List<long> offsets = [ 0 ];
        int objNum = 1;

        Write(output, "%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");

        offsets.Add(output.Position);
        Write(output, $"{objNum++} 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        int pagesObjNum = objNum++;
        List<int> pageObjs = [];

        double totalPages = pages.Count;

        for (int i = 0; i < pages.Count; i++)
        {
            ScannedImage page = pages[i];
            int width = page.Resim.PixelWidth;
            int height = page.Resim.PixelHeight;

            int pageObj = objNum++;
            int contentObj = objNum++;
            int imageObj = objNum++;

            pageObjs.Add(pageObj);

            offsets.Add(output.Position);
            Write(output, $"{pageObj} 0 obj\n<< /Type /Page /Parent {pagesObjNum} 0 R\n   /Resources << /XObject << /Im1 {imageObj} 0 R >> >>\n   /MediaBox [0 0 {width} {height}]\n   /Contents {contentObj} 0 R >>\nendobj\n");

            string content = $"q\n{width} 0 0 {height} 0 0 cm\n/Im1 Do\nQ\n";
            offsets.Add(output.Position);
            Write(output, $"{contentObj} 0 obj\n<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");

            offsets.Add(output.Position);
            using Bitmap bmp = page.Resim.BwAdaptiveThreshold(Settings.Default.Jb2Saturation, Settings.Default.Jb2Threshold).BitmapSourceToBitmap();
            byte[] data = JBig2Encoder.Encode(bmp, false);

            Write(output, $"{imageObj} 0 obj\n" + "<< /Type /XObject /Subtype /Image\n" + $"/Width {width} /Height {height}\n" + "/ColorSpace /DeviceGray\n /BitsPerComponent 1\n" + "/Filter /JBIG2Decode\n" + $"/Length {data.Length} >>\nstream\n");
            output.Write(data, 0, data.Length);
            Write(output, "\nendstream\nendobj\n");

            progress?.Report((i + 1) / totalPages);
        }

        long pagesOffset = output.Position;
        string kids = string.Join(" ", pageObjs.Select(id => $"{id} 0 R"));
        Write(output, $"2 0 obj\n<< /Type /Pages /Count {pageObjs.Count} /Kids [ {kids} ] >>\nendobj\n");
        offsets.Insert(2, pagesOffset);

        offsets.Add(output.Position);
        int infoObj = objNum++;
        Write(output, $"{infoObj} 0 obj\n" + $"<< /Producer (GPSCANNER) /Creator (GPSCANNER) /CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>\n" + "endobj\n");

        long xrefPos = output.Position;
        StringBuilder xref = new();
        _ = xref.AppendLine("xref");
        _ = xref.AppendLine($"0 {offsets.Count}");
        _ = xref.AppendLine("0000000000 65535 f ");
        for (int i = 1; i < offsets.Count; i++)
        {
            _ = xref.AppendLine($"{offsets[i]:D10} 00000 n ");
        }

        StringBuilder trailer = new();
        _ = trailer.AppendLine("trailer");
        _ = trailer.AppendLine($"<< /Size {offsets.Count} /Root 1 0 R /Info {infoObj} 0 R >>");
        _ = trailer.AppendLine("startxref");
        _ = trailer.AppendLine($"{xrefPos}");
        _ = trailer.AppendLine("%%EOF");

        byte[] xrefBytes = Encoding.ASCII.GetBytes(xref.ToString());
        output.Write(xrefBytes, 0, xrefBytes.Length);
        byte[] trailerBytes = Encoding.ASCII.GetBytes(trailer.ToString());
        output.Write(trailerBytes, 0, trailerBytes.Length);

        output.Position = 0;
        return output;
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

    public static void DrawText(this XGraphics gfx, XBrush xBrush, string item, string fontname, double x, double y, double fontsize = 16)
    {
        XFont font = new(fontname, fontsize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
        gfx?.DrawString(item, font, xBrush, x, y);
    }

    public static PdfDocument ExtractPdfPages(this string filename, int startpage, int endpage)
    {
        if (startpage > endpage)
        {
            throw new ArgumentOutOfRangeException(nameof(startpage), "start page should not be greater than end page");
        }

        using PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Import, PasswordProvider);
        if (inputDocument is not null)
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

    public static PdfDocument GenerateFromBitmapSourcePdf(this PdfDocument pdfdocument, int sayfa, BitmapSource bitmapSource)
    {
        if (bitmapSource is null)
        {
            return null;
        }

        PdfPage page = pdfdocument.Pages[sayfa];
        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        gfx.DrawImage(XImage.FromBitmapSource(bitmapSource), 0, 0, page.Width, page.Height);
        return pdfdocument;
    }

    public static PdfDocument GeneratePdf(this string imagefile, Paper paper, ObservableCollection<OcrData> ScannedText = null)
    {
        using PdfDocument document = new();
        try
        {
            PdfPage page = document.AddPage();
            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            using XImage xImage = XImage.FromFile(imagefile);
            XSize size = GetPageSize(paper, xImage, page);
            if (xImage.PixelWidth < xImage.PixelHeight)
            {
                page.Orientation = PageOrientation.Portrait;
                if (ScannedText is not null)
                {
                    WritePdfTextContent(xImage, ScannedText, page, gfx, XBrushes.Transparent);
                }

                gfx?.DrawImage(xImage, 0, 0, size.Width, size.Height);
            }
            else
            {
                page.Orientation = PageOrientation.Landscape;
                if (ScannedText is not null)
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

    public static PdfDocument GeneratePdf(this List<string> imagefiles, Paper paper, Action<double> progressCallback = null)
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
                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                using XImage xImage = XImage.FromFile(imagefile);
                XSize size = GetPageSize(paper, xImage, page);
                if (xImage.PixelWidth < xImage.PixelHeight)
                {
                    page.Orientation = PageOrientation.Portrait;
                    gfx?.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
                else
                {
                    page.Orientation = PageOrientation.Landscape;
                    gfx?.DrawImage(xImage, 0, 0, size.Height, size.Width);
                }
                progressCallback?.Invoke((i + 1) / (double)imagefiles.Count);
            }

            if (Scanner.PasswordProtect)
            {
                document.ApplyPdfSecurity();
            }

            document.ApplyDefaultPdfCompression();
            progressCallback?.Invoke(0);
        }
        catch (Exception ex)
        {
            imagefiles = null;
            throw new ArgumentException(ex?.Message);
        }

        return document;
    }

    public static PdfDocument GeneratePdf(this BitmapSource bitmapFrame, ObservableCollection<OcrData> scannedText, Format format, Paper paper, int jpegQuality = 80, int dpi = 120)
    {
        if (bitmapFrame is null)
        {
            throw new ArgumentNullException(nameof(bitmapFrame), "bitmapFrame cannot be null");
        }

        try
        {
            using PdfDocument document = new();
            PdfPage page = document.AddPage();
            XSize size = GetPageSize(paper, bitmapFrame, page);
            bool resizePaper = paper.GetPaperSize() != PageSize.Undefined;
            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            byte[] imageData;
            using (MemoryStream ms = new())
            {
                BitmapSource resizedImage = resizePaper ? bitmapFrame.Resize(page.Width, page.Height, 0, dpi, dpi) : bitmapFrame;
                if (format == Format.Tiff)
                {
                    using Bitmap bitmap = bitmapFrame.BitmapSourceToBitmap();
                    BitmapImage bwImage = bitmap.ConvertBlackAndWhite(Settings.Default.BwThreshold).ToBitmapImage(ImageFormat.Tiff);
                    resizedImage = resizePaper ? bwImage.Resize(page.Width, page.Height, 0, dpi, dpi) : bwImage;
                    imageData = resizedImage?.ToTiffJpegByteArray(format, jpegQuality);
                }
                else if (Scanner.UseMozJpegEncoding)
                {
                    using MozJpeg.MozJpeg mozJpeg = new();
                    using Bitmap bitmap = resizedImage.BitmapSourceToBitmap();
                    imageData = mozJpeg.Encode(bitmap, jpegQuality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
                }
                else
                {
                    imageData = resizedImage?.ToTiffJpegByteArray(format, jpegQuality);
                }

                ms.Write(imageData, 0, imageData.Length);
                _ = ms.Seek(0, SeekOrigin.Begin);
                using XImage xImage = XImage.FromStream(ms);

                if (scannedText is not null)
                {
                    WritePdfTextContent(bitmapFrame, scannedText, page, gfx, XBrushes.Transparent);
                }

                if (page.Orientation == PageOrientation.Portrait)
                {
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
                else
                {
                    gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                }
            }

            if (Scanner.PasswordProtect)
            {
                document.ApplyPdfSecurity();
            }
            document.ApplyDefaultPdfCompression();

            return document;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex.Message, ex);
        }
    }

    public static async Task<PdfDocument> GeneratePdfAsync(this List<ScannedImage> bitmapFrames,
                                                           Format format,
                                                           Paper paper,
                                                           int jpegQuality = 80,
                                                           List<ObservableCollection<OcrData>> scannedText = null,
                                                           int dpi = 120,
                                                           Action<double> progressCallback = null)
    {
        if (bitmapFrames == null || bitmapFrames.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitmapFrames), "Bitmap frames count should be greater than zero.");
        }

        using PdfDocument document = new();

        try
        {
            Scanner.ProgressState = TaskbarItemProgressState.Normal;

            for (int i = 0; i < bitmapFrames.Count; i++)
            {
                ScannedImage scannedImage = bitmapFrames[i];
                PdfPage page = document.AddPage();
                XSize pageSize = GetPageSize(paper, scannedImage, page);
                bool resizePaper = paper.GetPaperSize() != PageSize.Undefined;

                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                BitmapSource resizedImage;

                if (Scanner.UseMozJpegEncoding && format != Format.Tiff)
                {
                    resizedImage = resizePaper ? scannedImage.Resim.Resize(page.Width, page.Height, 0, dpi, dpi) : scannedImage.Resim;
                    byte[] imageData = EncodeImageWithMozJpeg(resizedImage, jpegQuality);
                    using MemoryStream imageStream = new(imageData);
                    using XImage xImage = XImage.FromStream(imageStream);
                    DrawImageOnPage(gfx, xImage, page, pageSize);
                }
                else
                {
                    resizedImage = format == Format.Tiff ? ProcessTiffImage(scannedImage.Resim, resizePaper, page, dpi) : (resizePaper ? scannedImage.Resim.Resize(page.Width, page.Height, 0, dpi, dpi) : scannedImage.Resim);
                    byte[] imageData = resizedImage.ToTiffJpegByteArray(format, jpegQuality);
                    using MemoryStream imageStream = new(imageData);
                    using XImage xImage = XImage.FromStream(imageStream);
                    DrawImageOnPage(gfx, xImage, page, pageSize);
                }

                if (scannedText?.Count > 0)
                {
                    AddTextContentIfNeeded(scannedImage.Resim, scannedText[i], page, gfx);
                }

                if (Settings.Default.UsePdfInternalTextData)
                {
                    ObservableCollection<OcrData> pdfInternalOcrData = TwainCtrl.ConvertPdfCharacterToOcrData(scannedImage.Resim.PixelHeight, scannedImage.Resim.PixelWidth, scannedImage.GetPdfCharacterInformations, Settings.Default.ImgLoadResolution);
                    AddTextContentIfNeeded(scannedImage.Resim, pdfInternalOcrData, page, gfx);
                }

                progressCallback?.Invoke((i + 1) / (double)bitmapFrames.Count);
            }

            if (Scanner.PasswordProtect)
            {
                document.ApplyPdfSecurity();
            }

            document.ApplyDefaultPdfCompression();
            progressCallback?.Invoke(0);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex.Message, ex);
        }

        return await Task.FromResult(document);
    }

    public static PdfDocument GenerateWatermarkedPdf(this PdfDocument pdfdocument, int sayfa, double rotation, SolidColorBrush textcolor, double textsize, string text, string font)
    {
        PdfPage page = pdfdocument.Pages[sayfa];
        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        XBrush brush = new XSolidBrush(XColor.FromArgb(textcolor.Color.A, textcolor.Color.R, textcolor.Color.G, textcolor.Color.B));
        DrawPdfOverlayText(page, gfx, textsize, text, brush, font, rotation);
        return pdfdocument;
    }

    public static PageSize GetPaperSize(this Paper paper) => paper is null || !paperSizes.TryGetValue(paper.PaperType, out PageSize pageSize) ? PageSize.A4 : pageSize;

    public static string GetPdfScanPath() => GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf");

    public static double[] GetPdfTextLayout(PdfPage page, double x = 30)
    {
        return Scanner.Layout switch
        {
            PdfPageLayout.Left => [ 30, 30 ],
            PdfPageLayout.Middle => [ (page.Width / 2) - (x / 2), 30 ],
            PdfPageLayout.Right => [ page.Width - x - 30, 30 ],
            PdfPageLayout.LeftBottom => [ 30, page.Height - 30 ],
            PdfPageLayout.MiddleBottom => [ (page.Width / 2) - (x / 2), page.Height - 30 ],
            PdfPageLayout.RightBottom => [ page.Width - x - 30, page.Height - 30 ],
            _ => [ 0, 0 ]
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

    public static async Task<string[]> WritePdfToJpgFileAsync(string pdffilepath, int dpi, Action<double> progresscallback = null)
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
                        progresscallback?.Invoke((i + 1) / (double)pdfDoc.PageCount);
                        continue;
                    }
                    int width = (int)(pdfDoc.PageSizes[i].Width / 96 * dpi);
                    int height = (int)(pdfDoc.PageSizes[i].Height / 96 * dpi);
                    Image image = pdfDoc.Render(i, width, height, dpi, dpi, false);
                    image.Save(outfilename, ImageFormat.Jpeg);
                    jpgfiles.Add(outfilename);
                    progresscallback?.Invoke((i + 1) / (double)pdfDoc.PageCount);
                }
            });
        return[ .. jpgfiles ];
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

    private static void DrawImageOnPage(XGraphics gfx, XImage xImage, PdfPage page, XSize size)
    {
        if (page.Orientation == PageOrientation.Portrait)
        {
            gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
        }
        else
        {
            gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
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

    private static byte[] EncodeImageWithMozJpeg(BitmapSource image, int quality)
    {
        using MozJpeg.MozJpeg mozJpeg = new();
        using Bitmap bitmap = image.BitmapSourceToBitmap();
        return mozJpeg.Encode(bitmap, quality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
    }

    private static XSize GetPageSize(Paper paper, ScannedImage scannedimage, PdfPage page) => GetPageSize(paper, scannedimage, page, img => img.Resim.PixelWidth, img => img.Resim.PixelHeight);

    private static XSize GetPageSize(Paper paper, BitmapSource bitmapframe, PdfPage page) => GetPageSize(paper, bitmapframe, page, img => img.PixelWidth, img => img.PixelHeight);

    private static XSize GetPageSize(Paper paper, XImage ximage, PdfPage page) => GetPageSize(paper, ximage, page, img => img.PixelWidth, img => img.PixelHeight);

    private static XSize GetPageSize<T>(Paper paper, T image, PdfPage page, Func<T, int> getWidth, Func<T, int> getHeight)
    {
        page.Orientation = getWidth(image) < getHeight(image) ? PageOrientation.Portrait : PageOrientation.Landscape;
        XSize size = default;
        switch (paper.PaperType)
        {
            case "Custom":
                size.Width = XUnit.FromCentimeter(paper.Width);
                size.Height = XUnit.FromCentimeter(paper.Height);
                page.MediaBox = new PdfRectangle(new XRect(0, 0, size.Width, size.Height));
                break;

            case "Original":
                page.Width = getWidth(image);
                page.Height = getHeight(image);
                size.Width = page.Orientation == PageOrientation.Portrait ? getWidth(image) : getHeight(image);
                size.Height = page.Orientation == PageOrientation.Portrait ? getHeight(image) : getWidth(image);
                break;

            default:
                page.Size = paper.GetPaperSize();
                size = PageSizeConverter.ToSize(page.Size);
                break;
        }

        return size;
    }

    private static BitmapSource ProcessTiffImage(BitmapSource image, bool resizePaper, PdfPage page, int dpi)
    {
        using Bitmap bitmap = image.BitmapSourceToBitmap();
        BitmapImage bwImage = bitmap.ConvertBlackAndWhite(Settings.Default.BwThreshold).ToBitmapImage(ImageFormat.Tiff);

        return resizePaper ? bwImage.Resize(page.Width, page.Height, 0, dpi, dpi) : bwImage;
    }

    private static void Write(Stream stream, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
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