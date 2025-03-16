using PdfCompressor;
using PdfSharp.Pdf;
using System.Threading.Tasks;

namespace TwainControl;

public class PdfCompressor : Compressor
{
    public async Task<PdfDocument> Compress(string pdffilepath)
    {
        using PdfDocument pdfDocument = await CompressFilePdfDocumentAsync(pdffilepath);
        ApplyDefaultPdfCompression(pdfDocument);
        return pdfDocument;
    }
}
