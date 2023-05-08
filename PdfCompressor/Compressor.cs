using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Extensions;
using Microsoft.Win32;
using MozJpeg;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PdfCompressor
{
    public class Compressor : Control, INotifyPropertyChanged
    {
        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(Compressor), new PropertyMetadata(72));

        public static readonly DependencyProperty LoadedPdfPathProperty = DependencyProperty.Register("LoadedPdfPath", typeof(string), typeof(Compressor), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty QualityProperty = DependencyProperty.Register("Quality", typeof(int), typeof(Compressor), new PropertyMetadata(80));

        public static readonly DependencyProperty UseMozJpegProperty = DependencyProperty.Register("UseMozJpeg", typeof(bool), typeof(Compressor), new PropertyMetadata(false));

        static Compressor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Compressor), new FrameworkPropertyMetadata(typeof(Compressor)));
        }

        public Compressor()
        {
            CompressFile = new RelayCommand<object>(async parameter =>
            {
                if (IsValidPdfFile(LoadedPdfPath))
                {
                    PdfiumViewer.PdfDocument loadedpdfdoc = PdfiumViewer.PdfDocument.Load(LoadedPdfPath);
                    List<BitmapImage> images = await AddToList(loadedpdfdoc, Dpi);
                    using PdfDocument pdfDocument = await GeneratePdf(images, UseMozJpeg, Quality, Dpi);
                    images = null;
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Pdf Dosyası (*.pdf)|*.pdf",
                        FileName = $"{Path.GetFileNameWithoutExtension(LoadedPdfPath)}_Compressed.pdf"
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        pdfDocument.Save(saveFileDialog.FileName);
                    }
                    GC.Collect();
                }
            }, parameter => !string.IsNullOrWhiteSpace(LoadedPdfPath));

            OpenFile = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Pdf Dosyaları (*.pdf)|*.pdf" };
                if (openFileDialog.ShowDialog() == true && IsValidPdfFile(openFileDialog.FileName))
                {
                    LoadedPdfPath = openFileDialog.FileName;
                }
            }, parameter => true);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RelayCommand<object> CompressFile { get; }

        public double CompressionProgress {
            get => compressionProgress;

            set {
                if (compressionProgress != value)
                {
                    compressionProgress = value;
                    OnPropertyChanged(nameof(CompressionProgress));
                }
            }
        }

        public int Dpi {
            get => (int)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        public string LoadedPdfPath {
            get => (string)GetValue(LoadedPdfPathProperty);
            set => SetValue(LoadedPdfPathProperty, value);
        }

        public RelayCommand<object> OpenFile { get; }

        public int Quality {
            get => (int)GetValue(QualityProperty);
            set => SetValue(QualityProperty, value);
        }

        public bool UseMozJpeg {
            get => (bool)GetValue(UseMozJpegProperty);
            set => SetValue(UseMozJpegProperty, value);
        }

        public static Bitmap BitmapSourceToBitmap(BitmapSource bitmapsource)
        {
            FormatConvertedBitmap src = new();
            src.BeginInit();
            src.Source = bitmapsource;
            src.DestinationFormat = PixelFormats.Bgra32;
            src.EndInit();
            src.Freeze();
            Bitmap bitmap = new(src.PixelWidth, src.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            src.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bitmap.UnlockBits(data);
            return bitmap;
        }

        public static void DefaultPdfCompression(PdfDocument doc)
        {
            doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
            doc.Options.CompressContentStreams = true;
            doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
            doc.Options.NoCompression = false;
            doc.Options.EnableCcittCompressionForBilevelImages = true;
        }

        public static bool IsValidPdfFile(string filename)
        {
            if (File.Exists(filename))
            {
                byte[] buffer = new byte[4];
                using FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
                int bytes_read = fs.Read(buffer, 0, buffer.Length);
                byte[] pdfheader = new byte[] { 0x25, 0x50, 0x44, 0x46 };
                return buffer?.SequenceEqual(pdfheader) == true;
            }
            return false;
        }

        public async Task<List<BitmapImage>> AddToList(PdfiumViewer.PdfDocument pdfDoc, int dpi)
        {
            List<BitmapImage> images = new();
            await Task.Run(() =>
            {
                for (int i = 0; i < pdfDoc.PageCount; i++)
                {
                    int width = (int)(pdfDoc.PageSizes[i].Width / 72 * dpi);
                    int height = (int)(pdfDoc.PageSizes[i].Height / 72 * dpi);
                    using System.Drawing.Image image = pdfDoc.Render(i, width, height, dpi, dpi, false);
                    images.Add(image.ToBitmapImage(ImageFormat.Jpeg));
                    CompressionProgress = (i + 1) / (double)pdfDoc.PageCount;
                }
            });
            return images;
        }

        public async Task<PdfDocument> GeneratePdf(List<BitmapImage> bitmapFrames, bool UseMozJpegEncoding, int jpegquality = 80, int dpi = 200)
        {
            if (bitmapFrames?.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bitmapFrames), "bitmap frames count should be greater than zero");
            }
            using PdfDocument document = new();
            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < bitmapFrames.Count; i++)
                    {
                        BitmapImage pdfimage = bitmapFrames[i];
                        PdfPage page = document.AddPage();

                        if (UseMozJpegEncoding)
                        {
                            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                            using MozJpeg.MozJpeg mozJpeg = new();
                            BitmapSource resizedimage = pdfimage.Resize(page.Width, page.Height, 0, dpi, dpi);
                            byte[] data = mozJpeg.Encode(BitmapSourceToBitmap(resizedimage), jpegquality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
                            using MemoryStream ms = new(data);
                            using XImage xImage = XImage.FromStream(ms);
                            XSize size = PageSizeConverter.ToSize(page.Size);
                            resizedimage = null;
                            data = null;

                            if (pdfimage.PixelWidth < pdfimage.PixelHeight)
                            {
                                gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                            }
                            else
                            {
                                page.Orientation = PageOrientation.Landscape;
                                gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                            }
                            CompressionProgress = (i + 1) / (double)bitmapFrames.Count;
                        }
                        else
                        {
                            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                            BitmapSource resizedimage = pdfimage.Resize(page.Width, page.Height, 0, dpi, dpi);
                            using MemoryStream ms = new(resizedimage.ToTiffJpegByteArray(ExtensionMethods.Format.Jpg, jpegquality));
                            using XImage xImage = XImage.FromStream(ms);
                            XSize size = PageSizeConverter.ToSize(page.Size);
                            resizedimage = null;

                            if (pdfimage.PixelWidth < pdfimage.PixelHeight)
                            {
                                gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                            }
                            else
                            {
                                page.Orientation = PageOrientation.Landscape;
                                gfx.DrawImage(xImage, 0, 0, size.Height, size.Width);
                            }
                            CompressionProgress = (i + 1) / (double)bitmapFrames.Count;
                        }
                        GC.Collect();
                    }
                    DefaultPdfCompression(document);
                });
            }
            catch (Exception ex)
            {
                bitmapFrames = null;
                throw new ArgumentException(nameof(document), ex);
            }
            return document;
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private double compressionProgress;
    }
}