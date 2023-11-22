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
                    if (Path.GetExtension(file.Filename.ToLower()) == ".pdf" && IsValidPdfFile(file.Filename))
                    {
                        using (PdfDocument pdfDocument = await CompressFilePdfDocumentAsync(file.Filename))
                        {
                            pdfDocument.Save($"{Path.GetDirectoryName(file.Filename)}\\{Path.GetFileNameWithoutExtension(file.Filename)}_Compressed.pdf");
                            ApplyDefaultPdfCompression(pdfDocument);
                        }

                        file.Completed = true;
                    }
                    else if (imagefileextensions.Contains(Path.GetExtension(file.Filename.ToLower())))
                    {
                        using (PdfDocument pdfDocument = await GeneratePdf(file.Filename))
                        {
                            pdfDocument.Save($"{Path.GetDirectoryName(file.Filename)}\\{Path.GetFileNameWithoutExtension(file.Filename)}_Compressed.pdf");
                            ApplyDefaultPdfCompression(pdfDocument);
                        }

                        file.Completed = true;
                    }
                }

                if (DataContext is GpScannerViewModel gpScannerViewModel)
                {
                    DateTime date = gpScannerViewModel.SeçiliGün;
                    gpScannerViewModel.ReloadFileDatas();
                    gpScannerViewModel.SeçiliGün = date;

                    if (gpScannerViewModel.Shutdown)
                    {
                        Shutdown.DoExitWin(Shutdown.EWX_SHUTDOWN);
                    }
                }
            },
            parameter => BatchPdfList?.Count > 0);
    }

    public new RelayCommand<object> BatchCompressFile { get; }
}