using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Extensions;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
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

        public static void DefaultPdfCompression(PdfDocument doc)
        {
            doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
            doc.Options.CompressContentStreams = true;
            doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
            doc.Options.NoCompression = false;
            doc.Options.EnableCcittCompressionForBilevelImages = true;
        }

        public static PdfDocument GeneratePdf(IList<ScannedImage> bitmapFrames, Format format, bool rotate = false)
        {
            using PdfDocument document = new();
            try
            {
                for (int i = 0; i < bitmapFrames.Count; i++)
                {
                    ScannedImage scannedimage = bitmapFrames[i];
                    PdfPage page = document.AddPage();
                    if (rotate)
                    {
                        page.Rotate = (int)Scanner.RotateAngle;
                    }
                    if (Scanner.PasswordProtect)
                    {
                        ApplyPdfSecurity(document);
                    }
                    using XGraphics gfx = XGraphics.FromPdfPage(page);
                    using MemoryStream ms = new(scannedimage.Resim.ToTiffJpegByteArray(format));
                    using XImage xImage = XImage.FromStream(ms);
                    XSize size = PageSizeConverter.ToSize(PageSize.A4);
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
            DefaultPdfCompression(document);
            return document;
        }

        public static PdfDocument GeneratePdf(BitmapSource bitmapframe, Format format, bool rotate = false)
        {
            try
            {
                using PdfDocument document = new();
                PdfPage page = document.AddPage();
                if (rotate)
                {
                    page.Rotate = (int)Scanner.RotateAngle;
                }
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                using XGraphics gfx = XGraphics.FromPdfPage(page);
                using MemoryStream ms = new(bitmapframe.ToTiffJpegByteArray(format));
                using XImage xImage = XImage.FromStream(ms);
                XSize size = PageSizeConverter.ToSize(PageSize.A4);
                gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                DefaultPdfCompression(document);
                return document;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
                return null;
            }
        }

        public static PdfDocument MergePdf(string[] pdffiles)
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

        public static void SavePdfFiles(string[] files)
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
                   MergePdf(files).Save(saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message);
                }
            }
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

        public static string GetPdfScanPath()
        {
            return GetSaveFolder().SetUniqueFile(Scanner.SaveFileName, "pdf");
        }

        protected PdfGeneration(Scanner scanner)
        {
            Scanner = scanner;
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
    }
}