using PdfCompressor;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TwainControl;

public class PdfCompressor : Compressor
{
    public bool EncodeAsJb2File
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(EncodeAsJb2File));
            }
        }
    }

    public async Task<PdfDocument> Compress(string pdffilepath)
    {

        using PdfDocument pdfDocument = EncodeAsJb2File ? await Jb2CompressAsync(pdffilepath) : await CompressFilePdfDocumentAsync(pdffilepath);
        ApplyDefaultPdfCompression(pdfDocument);
        return pdfDocument;
    }

    private async Task<PdfDocument> Jb2CompressAsync(string path)
    {
        List<BitmapImage> bitmapframes = await GetBitmapImagesAsync(path);
        List<ScannedImage> scannedimages = bitmapframes.ConvertAll(img => new ScannedImage() { Resim = BitmapFrame.Create(img) });
        Progress<double> progress = new(percent => CompressionProgress = percent);
        using MemoryStream memorystream = await Task.Run(() => scannedimages.CreateMultipagePdfWithJbig2Images(progress).AddPdfPassword_PdfSharp());
        using PdfDocument document = PdfReader.Open(memorystream);
        return document;
    }
}
