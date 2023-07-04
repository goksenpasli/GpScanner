using System;
using System.IO;
using System.Windows;
using Extensions;
using GpScanner.Properties;
using PdfCompressor;
using PdfSharp.Pdf;
using TwainControl;

namespace GpScanner.ViewModel;

public class PdfCompressorControl : Compressor
{
    public PdfCompressorControl()
    {
        CompressFile = new RelayCommand<object>(
            async parameter =>
            {
                if (IsValidPdfFile(LoadedPdfPath))
                {
                    PdfDocument pdfDocument;
                    using (PdfiumViewer.PdfDocument loadedpdfdoc = PdfiumViewer.PdfDocument.Load(LoadedPdfPath))
                    {
                        System.Collections.Generic.List<System.Windows.Media.Imaging.BitmapImage> images = await AddToListAsync(loadedpdfdoc, Dpi);
                        pdfDocument = await GeneratePdfAsync(images, UseMozJpeg, BlackAndWhite, Quality, Dpi);
                        images = null;
                    }

                    string savefilename = Settings.Default.DirectlyOverwriteCompressedPdf
                        ? LoadedPdfPath
                        : $"{Path.GetDirectoryName(LoadedPdfPath)}\\{Path.GetFileNameWithoutExtension(LoadedPdfPath)}{Translation.GetResStringValue("COMPRESS")}.pdf";
                    pdfDocument?.Save(savefilename);
                    pdfDocument?.Dispose();
                    if (Application.Current?.MainWindow?.DataContext is GpScannerViewModel gpScannerViewModel)
                    {
                        DateTime? date = gpScannerViewModel.SeçiliGün;
                        gpScannerViewModel.ReloadFileDatas();
                        gpScannerViewModel.SeçiliGün = date;
                    }

                    GC.Collect();
                }
            },
            parameter => !string.IsNullOrWhiteSpace(LoadedPdfPath));
    }

    public new RelayCommand<object> CompressFile { get; }
}