using Extensions;
using Microsoft.Win32;
using MozJpeg;
using PdfCompressor.Properties;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
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
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;

namespace PdfCompressor;

public class BatchPdfData : InpcBase
{
    private bool completed;
    private string filename;

    public bool Completed
    {
        get => completed;
        set
        {
            if (completed != value)
            {
                completed = value;
                OnPropertyChanged(nameof(Completed));
            }
        }
    }

    public string Filename
    {
        get => filename;
        set
        {
            if (filename != value)
            {
                filename = value;
                OnPropertyChanged(nameof(Filename));
            }
        }
    }
}

public class Compressor : Control, INotifyPropertyChanged
{
    public static readonly DependencyProperty BatchProcessIsEnabledProperty = DependencyProperty.Register("BatchProcessIsEnabled", typeof(bool), typeof(Compressor), new PropertyMetadata(true));
    public static readonly DependencyProperty BlackAndWhiteProperty = DependencyProperty.Register("BlackAndWhite", typeof(bool), typeof(Compressor), new PropertyMetadata(Settings.Default.Bw, BwChanged));
    public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(Compressor), new PropertyMetadata(Settings.Default.Dpi, DpiChanged));
    public static readonly DependencyProperty LoadedPdfPathProperty = DependencyProperty.Register("LoadedPdfPath", typeof(string), typeof(Compressor), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty QualityProperty = DependencyProperty.Register("Quality", typeof(int), typeof(Compressor), new PropertyMetadata(Settings.Default.Quality, QualityChanged));
    public static readonly DependencyProperty UseMozJpegProperty = DependencyProperty.Register("UseMozJpeg", typeof(bool), typeof(Compressor), new PropertyMetadata(false, MozpegChanged));
    private List<BatchPdfData> batchPdfList;
    private double compressionProgress;

    static Compressor() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Compressor), new FrameworkPropertyMetadata(typeof(Compressor))); }

    public Compressor()
    {
        CompressFile = new RelayCommand<object>(
            async parameter =>
            {
                if (IsValidPdfFile(LoadedPdfPath))
                {
                    PdfDocument pdfDocument = await CompressFilePdfDocumentAsync(LoadedPdfPath);
                    SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası (*.pdf)|*.pdf", FileName = $"{Path.GetFileNameWithoutExtension(LoadedPdfPath)}_Compressed.pdf" };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        pdfDocument.Save(saveFileDialog.FileName);
                        LoadedPdfPath = null;
                    }
                    GC.Collect();
                }
            },
            parameter => !string.IsNullOrWhiteSpace(LoadedPdfPath));

        BatchCompressFile = new RelayCommand<object>(
            async parameter =>
            {
                foreach (BatchPdfData file in BatchPdfList)
                {
                    if (IsValidPdfFile(file.Filename))
                    {
                        PdfDocument pdfDocument = await CompressFilePdfDocumentAsync(file.Filename);
                        pdfDocument.Save($"{Path.GetDirectoryName(file.Filename)}\\{Path.GetFileNameWithoutExtension(file.Filename)}_Compressed.pdf");
                        GC.Collect();
                        file.Completed = true;
                    }
                }
            },
            parameter => BatchPdfList?.Count > 0);

        OpenFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Pdf Dosyaları (*.pdf)|*.pdf" };
                if (openFileDialog.ShowDialog() == true && IsValidPdfFile(openFileDialog.FileName))
                {
                    LoadedPdfPath = openFileDialog.FileName;
                }
            },
            parameter => true);

        OpenBatchPdfFolder = new RelayCommand<object>(
            parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new() { Description = "PDF Klasörü Seç." };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    BatchPdfList = new List<BatchPdfData>();
                    foreach (string item in Directory.EnumerateFiles(dialog.SelectedPath, "*.pdf", SearchOption.TopDirectoryOnly))
                    {
                        BatchPdfList.Add(new BatchPdfData() { Filename = item });
                    }
                }
            },
            parameter => true);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RelayCommand<object> BatchCompressFile { get; }

    public List<BatchPdfData> BatchPdfList
    {
        get => batchPdfList;
        set
        {
            if (batchPdfList != value)
            {
                batchPdfList = value;
                OnPropertyChanged(nameof(BatchPdfList));
            }
        }
    }

    public bool BatchProcessIsEnabled { get => (bool)GetValue(BatchProcessIsEnabledProperty); set => SetValue(BatchProcessIsEnabledProperty, value); }

    public bool BlackAndWhite { get => (bool)GetValue(BlackAndWhiteProperty); set => SetValue(BlackAndWhiteProperty, value); }

    public RelayCommand<object> CompressFile { get; }

    public double CompressionProgress
    {
        get => compressionProgress;

        set
        {
            if (compressionProgress != value)
            {
                compressionProgress = value;
                OnPropertyChanged(nameof(CompressionProgress));
            }
        }
    }

    public int Dpi { get => (int)GetValue(DpiProperty); set => SetValue(DpiProperty, value); }

    public string LoadedPdfPath { get => (string)GetValue(LoadedPdfPathProperty); set => SetValue(LoadedPdfPathProperty, value); }

    public RelayCommand<object> OpenBatchPdfFolder { get; }

    public RelayCommand<object> OpenFile { get; }

    public int Quality { get => (int)GetValue(QualityProperty); set => SetValue(QualityProperty, value); }

    public bool UseMozJpeg { get => (bool)GetValue(UseMozJpegProperty); set => SetValue(UseMozJpegProperty, value); }

    public static Bitmap BitmapSourceToBitmap(BitmapSource bitmapsource)
    {
        if (bitmapsource is null)
        {
            throw new ArgumentNullException(nameof(bitmapsource));
        }

        FormatConvertedBitmap src = new();
        src.BeginInit();
        src.Source = bitmapsource;
        src.DestinationFormat = PixelFormats.Bgra32;
        src.EndInit();
        src.Freeze();
        Bitmap bitmap = new(src.PixelWidth, src.PixelHeight, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        src.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
        bitmap.UnlockBits(data);
        return bitmap;
    }

    public static void DefaultPdfCompression(PdfDocument doc)
    {
        if (doc is null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

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
            _ = fs.Read(buffer, 0, buffer.Length);
            byte[] pdfheader = { 0x25, 0x50, 0x44, 0x46 };
            return buffer?.SequenceEqual(pdfheader) == true;
        }

        return false;
    }

    public async Task<List<BitmapImage>> AddToListAsync(PdfiumViewer.PdfDocument pdfDoc, int dpi)
    {
        List<BitmapImage> images = new();
        await Task.Run(
            () =>
            {
                for (int i = 0; i < pdfDoc.PageCount; i++)
                {
                    int width = (int)(pdfDoc.PageSizes[i].Width / 96 * dpi);
                    int height = (int)(pdfDoc.PageSizes[i].Height / 96 * dpi);
                    using System.Drawing.Image image = pdfDoc.Render(i, width, height, dpi, dpi, false);
                    images.Add(image.ToBitmapImage(ImageFormat.Jpeg));
                    CompressionProgress = (i + 1) / (double)pdfDoc.PageCount;
                }
            });
        return images;
    }

    public async Task<PdfDocument> GeneratePdfAsync(List<BitmapImage> bitmapFrames, bool UseMozJpegEncoding, bool bw, int jpegquality = 80, int dpi = 200)
    {
        if (bitmapFrames?.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitmapFrames), "bitmap frames count should be greater than zero");
        }

        using PdfDocument document = new();
        try
        {
            await Task.Run(
                () =>
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
                            BitmapSource resizedimage = bw ? pdfimage.Resize(page.Width, page.Height, 0, dpi, dpi).ConvertBlackAndWhite() : pdfimage.Resize(page.Width, page.Height, 0, dpi, dpi);
                            using MemoryStream ms =
                                new(resizedimage.ToTiffJpegByteArray(ExtensionMethods.Format.Jpg, jpegquality));
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

    protected virtual void OnPropertyChanged(string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

    private static void BwChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Compressor compressor)
        {
            if (!compressor.UseMozJpeg)
            {
                compressor.BlackAndWhite = Settings.Default.Bw = (bool)e.NewValue;
                Settings.Default.Save();
                return;
            }

            compressor.BlackAndWhite = Settings.Default.Bw = false;
            Settings.Default.Save();
        }
    }

    private static void DpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Settings.Default.Dpi = (int)e.NewValue;
        Settings.Default.Save();
    }

    private static void MozpegChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Compressor compressor && (bool)e.NewValue)
        {
            compressor.BlackAndWhite = false;
        }
    }

    private static void QualityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Settings.Default.Quality = (int)e.NewValue;
        Settings.Default.Save();
    }

    private async Task<PdfDocument> CompressFilePdfDocumentAsync(string path)
    {
        PdfiumViewer.PdfDocument loadedpdfdoc = PdfiumViewer.PdfDocument.Load(path);
        List<BitmapImage> images = await AddToListAsync(loadedpdfdoc, Dpi);
        loadedpdfdoc?.Dispose();
        return await GeneratePdfAsync(images, UseMozJpeg, BlackAndWhite, Quality, Dpi);
    }
}