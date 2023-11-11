using Extensions;
using PdfCompressor;
using PdfSharp.Pdf;
using System;
using System.IO;

namespace GpScanner.ViewModel;

public class PdfCompressorControl : Compressor
{
    public PdfCompressorControl()
    {
        BatchCompressFile = new RelayCommand<object>(
            async parameter =>
            {
                foreach (BatchPdfData file in BatchPdfList)
                {
                    if (IsValidPdfFile(file.Filename))
                    {
                        PdfDocument pdfDocument = await CompressFilePdfDocumentAsync(file.Filename);
                        pdfDocument.Save($"{Path.GetDirectoryName(file.Filename)}\\{Path.GetFileNameWithoutExtension(file.Filename)}_Compressed.pdf");
                        file.Completed = true;
                    }
                }
                if (DataContext is GpScannerViewModel gpScannerViewModel)
                {
                    DateTime date = gpScannerViewModel.SeçiliGün;
                    gpScannerViewModel.ReloadFileDatas();
                    gpScannerViewModel.SeçiliGün = date;
                }
            },
            parameter => BatchPdfList?.Count > 0);
    }

    public new RelayCommand<object> BatchCompressFile { get; }
}