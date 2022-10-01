﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Extensions;
using Microsoft.Win32;
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
    public class OcrData : InpcBase
    {
        public Rect Rect
        {
            get => rect;

            set
            {
                if (rect != value)
                {
                    rect = value;
                    OnPropertyChanged(nameof(Rect));
                }
            }
        }

        public string Text
        {
            get => text;

            set
            {
                if (text != value)
                {
                    text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        private Rect rect;

        private string text;
    }

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
                outputDocument.AddPage(inputDocument.Pages[i]);
            }
            return outputDocument;
        }

        public static PdfDocument GeneratePdf(IList<ScannedImage> bitmapFrames, Format format)
        {
            using PdfDocument document = new();
            try
            {
                foreach (ScannedImage scannedimage in bitmapFrames)
                {
                    PdfPage page = document.AddPage();
                    if (Scanner.PasswordProtect)
                    {
                        ApplyPdfSecurity(document);
                    }
                    using XGraphics gfx = XGraphics.FromPdfPage(page);
                    using MemoryStream ms = new(scannedimage.Resim.ToTiffJpegByteArray(format));
                    using XImage xImage = XImage.FromStream(ms);
                    XSize size = PageSizeConverter.ToSize(PageSize.A4);
                    if (scannedimage.Resim.PixelWidth < scannedimage.Resim.PixelHeight)
                    {
                        gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                    }
                    else
                    {
                        page.Orientation = PageOrientation.Landscape;
                        gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
            DefaultPdfCompression(document);
            return document;
        }

        public static PdfDocument GeneratePdf(BitmapSource bitmapframe, Format format)
        {
            try
            {
                using PdfDocument document = new();
                PdfPage page = document.AddPage();
                if (Scanner.PasswordProtect)
                {
                    ApplyPdfSecurity(document);
                }
                using XGraphics gfx = XGraphics.FromPdfPage(page);
                using MemoryStream ms = new(bitmapframe.ToTiffJpegByteArray(format));
                using XImage xImage = XImage.FromStream(ms);
                XSize size = PageSizeConverter.ToSize(PageSize.A4);
                if (bitmapframe.PixelWidth < bitmapframe.PixelHeight)
                {
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
                else
                {
                    page.Orientation = PageOrientation.Landscape;
                    gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                }
                DefaultPdfCompression(document);
                return document;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
                return null;
            }
        }

        public static PdfDocument GeneratePdf(BitmapFrame bitmapframe, ObservableCollection<OcrData> ScannedText)
        {
            try
            {
                using PdfDocument document = new();
                PdfPage page = document.AddPage();
                using XGraphics gfx = XGraphics.FromPdfPage(page);
                using MemoryStream ms = new(bitmapframe.ToTiffJpegByteArray(Format.Jpg));
                using XImage xImage = XImage.FromStream(ms);
                XSize size = PageSizeConverter.ToSize(PageSize.A4);
                if (bitmapframe.PixelWidth < bitmapframe.PixelHeight)
                {
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                }
                else
                {
                    page.Orientation = PageOrientation.Landscape;
                    gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                }
                XTextFormatter textformatter = new(gfx);
                foreach (OcrData item in ScannedText)
                {
                    XRect adjustedBounds = AdjustBounds(item.Rect, page.Width / bitmapframe.PixelWidth, page.Height / bitmapframe.PixelHeight);
                    int adjustedFontSize = CalculateFontSize(item.Text, adjustedBounds, gfx);
                    XFont font = new("Times New Roman", adjustedFontSize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
                    XSize adjustedTextSize = gfx.MeasureString(item.Text, font);
                    double verticalOffset = (adjustedBounds.Height - adjustedTextSize.Height) / 2;
                    double horizontalOffset = (adjustedBounds.Width - adjustedTextSize.Width) / 2;
                    adjustedBounds.Offset(horizontalOffset, verticalOffset);
                    textformatter.DrawString(item.Text, font, XBrushes.Transparent, adjustedBounds);
                }

                DefaultPdfCompression(document);
                return document;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
                return null;
            }
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

        protected PdfGeneration(Scanner scanner)
        {
            Scanner = scanner;
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
    }
}