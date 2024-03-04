using Extensions;
using Microsoft.Win32;
using MozJpeg;
using PdfCompressor.Properties;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

[TemplatePart(Name = "ListBox", Type = typeof(ListBox))]
public class Compressor : Control, INotifyPropertyChanged
{
    public static readonly DependencyProperty BatchPdfListProperty = DependencyProperty.Register("BatchPdfList", typeof(ObservableCollection<BatchPdfData>), typeof(Compressor), new PropertyMetadata(new ObservableCollection<BatchPdfData>()));
    public static readonly DependencyProperty BatchProcessIsEnabledProperty = DependencyProperty.Register("BatchProcessIsEnabled", typeof(bool), typeof(Compressor), new PropertyMetadata(true));
    public static readonly DependencyProperty BlackAndWhiteProperty = DependencyProperty.Register("BlackAndWhite", typeof(bool), typeof(Compressor), new PropertyMetadata(Settings.Default.Bw, BwChanged));
    public static readonly DependencyPropertyKey CompressFinishedProperty = DependencyProperty.RegisterReadOnly("CompressFinished", typeof(bool), typeof(Compressor), new PropertyMetadata(true));
    public static readonly DependencyProperty DpiProperty = DependencyProperty.Register("Dpi", typeof(int), typeof(Compressor), new PropertyMetadata(Settings.Default.Dpi, DpiChanged));
    public static readonly DependencyProperty LoadedPdfPathProperty = DependencyProperty.Register("LoadedPdfPath", typeof(string), typeof(Compressor), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty QualityProperty = DependencyProperty.Register("Quality", typeof(int), typeof(Compressor), new PropertyMetadata(Settings.Default.Quality, QualityChanged));
    public static readonly DependencyProperty UseMozJpegProperty = DependencyProperty.Register("UseMozJpeg", typeof(bool), typeof(Compressor), new PropertyMetadata(false, MozpegChanged));
    private readonly List<string> imagefileextensions = [".tiff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".png", ".bmp"];
    private double compressionProgress;
    private ListBox listbox;

    static Compressor() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Compressor), new FrameworkPropertyMetadata(typeof(Compressor))); }

    public Compressor()
    {
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            BatchPdfList =
            [
                new() { Filename = "FileName", Completed = true },
                new() { Filename = "FileName", Completed = true },
                new() { Filename = "FileName" },
                new() { Filename = "FileName" },
                new() { Filename = "FileName" },
            ];
        }

        BatchCompressFile = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    SetValue(CompressFinishedProperty, false);
                    foreach (BatchPdfData file in BatchPdfList)
                    {
                        string outputFile = $"{Path.GetDirectoryName(file.Filename)}\\{Path.GetFileNameWithoutExtension(file.Filename)}_Compressed.pdf";
                        bool isPdf = Path.GetExtension(file.Filename.ToLowerInvariant()) == ".pdf" && IsValidPdfFile(file.Filename);
                        using PdfDocument pdfDocument = isPdf ? await CompressFilePdfDocumentAsync(file.Filename) : await GeneratePdfAsync(file.Filename, Quality);
                        ApplyDefaultPdfCompression(pdfDocument);
                        pdfDocument.Save(outputFile);
                        long outputFileSize = new FileInfo(outputFile).Length;
                        long originalFileSize = new FileInfo(file.Filename).Length;
                        file.CompressionRatio = (double)outputFileSize / originalFileSize * 100;
                        file.Completed = true;
                    }
                    CompressionProgress = 0;
                }
                finally
                {
                    SetValue(CompressFinishedProperty, true);
                }
            },
            parameter => BatchPdfList?.Count > 0);

        OpenBatchPdfFolder = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = true, Filter = "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf" };
                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (string item in openFileDialog.FileNames)
                    {
                        BatchPdfList.Add(new BatchPdfData() { Filename = item });
                    }
                }
            },
            parameter => true);

        RemovePdfFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is BatchPdfData batchPdfData)
                {
                    _ = BatchPdfList?.Remove(batchPdfData);
                }
            },
            parameter => CompressFinished);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RelayCommand<object> BatchCompressFile { get; }

    public ObservableCollection<BatchPdfData> BatchPdfList { get => (ObservableCollection<BatchPdfData>)GetValue(BatchPdfListProperty); set => SetValue(BatchPdfListProperty, value); }

    public bool BatchProcessIsEnabled { get => (bool)GetValue(BatchProcessIsEnabledProperty); set => SetValue(BatchProcessIsEnabledProperty, value); }

    public bool BlackAndWhite { get => (bool)GetValue(BlackAndWhiteProperty); set => SetValue(BlackAndWhiteProperty, value); }

    public bool CompressFinished => (bool)GetValue(CompressFinishedProperty.DependencyProperty);

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

    public int Quality { get => (int)GetValue(QualityProperty); set => SetValue(QualityProperty, value); }

    public RelayCommand<object> RemovePdfFile { get; }

    public bool UseMozJpeg { get => (bool)GetValue(UseMozJpegProperty); set => SetValue(UseMozJpegProperty, value); }

    public bool IsValidPdfFile(string filename)
    {
        if (File.Exists(filename))
        {
            byte[] buffer = new byte[4];
            using FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
            _ = fs.Read(buffer, 0, buffer.Length);
            byte[] pdfheader = [0x25, 0x50, 0x44, 0x46];
            return buffer?.SequenceEqual(pdfheader) == true;
        }

        return false;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        listbox = GetTemplateChild("ListBox") as ListBox;
        if (listbox != null)
        {
            listbox.Drop -= Listbox_Drop;
            listbox.Drop += Listbox_Drop;
        }
    }

    protected void ApplyDefaultPdfCompression(PdfDocument doc)
    {
        doc.Info.CreationDate = DateTime.Now;
        doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        doc.Options.CompressContentStreams = true;
        doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
        doc.Options.NoCompression = false;
        doc.Options.EnableCcittCompressionForBilevelImages = true;
    }

    protected async Task<PdfDocument> CompressFilePdfDocumentAsync(string path)
    {
        using PdfiumViewer.PdfDocument loadedpdfdoc = PdfiumViewer.PdfDocument.Load(path);
        List<BitmapImage> images = await AddToListAsync(loadedpdfdoc, Dpi);
        return await GeneratePdfAsync(images, UseMozJpeg, BlackAndWhite, Quality, Dpi, progress => CompressionProgress = progress);
    }

    protected async Task<PdfDocument> GeneratePdfAsync(string imagefile, int jpegquality = 80)
    {
        using PdfDocument document = new();
        await Task.Run(
            () =>
            {
                try
                {
                    PdfPage page = document.AddPage();
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    using MemoryStream ms = new(BitmapFrame.Create(new Uri(imagefile)).ToTiffJpegByteArray(ExtensionMethods.Format.Jpg, jpegquality));
                    using XImage xImage = XImage.FromStream(ms);
                    XSize size = PageSizeConverter.ToSize(page.Size);
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
                }
                catch (Exception ex)
                {
                    imagefile = null;
                    throw new ArgumentException(ex?.Message);
                }
            });
        return document;
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

    private async Task<List<BitmapImage>> AddToListAsync(PdfiumViewer.PdfDocument pdfDoc, int dpi)
    {
        List<BitmapImage> images = [];
        await Task.Run(
            () =>
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

    private Bitmap BitmapSourceToBitmap(BitmapSource bitmapsource)
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
        if (data != null)
        {
            src.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private async Task<PdfDocument> GeneratePdfAsync(List<BitmapImage> bitmapFrames, bool UseMozJpegEncoding, bool bw, int jpegquality = 80, int dpi = 200, Action<double> progresscallback = null)
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
                        double ratio = pdfimage.PixelWidth / (double)pdfimage.PixelHeight;
                        bool portrait = pdfimage.PixelWidth < pdfimage.PixelHeight;
                        if (UseMozJpegEncoding)
                        {
                            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                            using MozJpeg.MozJpeg mozJpeg = new();
                            BitmapSource resizedimage = pdfimage.Resize(page.Width, page.Height, 0, dpi, dpi);
                            byte[] data = mozJpeg.Encode(BitmapSourceToBitmap(resizedimage), jpegquality, false, TJFlags.ACCURATEDCT | TJFlags.DC_SCAN_OPT2 | TJFlags.TUNE_MS_SSIM);
                            using MemoryStream ms = new(data);
                            using XImage xImage = XImage.FromStream(ms);
                            resizedimage = null;
                            data = null;

                            if (portrait)
                            {
                                gfx.DrawImage(xImage, 0, 0, page.Height * ratio, page.Height);
                            }
                            else
                            {
                                page.Orientation = PageOrientation.Landscape;
                                gfx.DrawImage(xImage, 0, 0, page.Width, page.Width / ratio);
                            }
                        }
                        else
                        {
                            using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                            BitmapSource resizedimage = bw
                                                        ? BitmapSourceToBitmap(pdfimage).ConvertBlackAndWhite().ToBitmapImage(ImageFormat.Tiff).Resize(page.Height * ratio, page.Height, 0, dpi, dpi)
                                                        : pdfimage.Resize(page.Height * ratio, page.Height, 0, dpi, dpi);
                            using MemoryStream ms = new(resizedimage.ToTiffJpegByteArray(ExtensionMethods.Format.Jpg, jpegquality));
                            using XImage xImage = XImage.FromStream(ms);
                            resizedimage = null;

                            if (portrait)
                            {
                                gfx.DrawImage(xImage, 0, 0, page.Height * ratio, page.Height);
                            }
                            else
                            {
                                page.Orientation = PageOrientation.Landscape;
                                gfx.DrawImage(xImage, 0, 0, page.Width, page.Width / ratio);
                            }
                        }

                        if (progresscallback is not null)
                        {
                            progresscallback((i + 1) / (double)bitmapFrames.Count);
                        }
                    }

                    ApplyDefaultPdfCompression(document);
                });
        }
        catch (Exception ex)
        {
            bitmapFrames = null;
            throw new ArgumentException(ex?.Message);
        }

        return document;
    }

    private void Listbox_Drop(object sender, DragEventArgs e)
    {
        string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (droppedfiles?.Length > 0)
        {
            foreach (string file in droppedfiles.Where(file => imagefileextensions.Contains(Path.GetExtension(file).ToLowerInvariant()) || IsValidPdfFile(file)))
            {
                BatchPdfList.Add(new BatchPdfData() { Filename = file });
            }
        }
    }
}