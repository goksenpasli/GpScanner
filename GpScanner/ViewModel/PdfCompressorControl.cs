using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Extensions;
using PdfCompressor;
using PdfSharp.Pdf;

namespace GpScanner.ViewModel
{
    public class PdfCompressorControl : Compressor
    {
        public PdfCompressorControl()
        {
            CompressFile = new RelayCommand<object>(async parameter =>
            {
                if (IsValidPdfFile(LoadedPdfPath))
                {
                    PdfiumViewer.PdfDocument loadedpdfdoc = PdfiumViewer.PdfDocument.Load(LoadedPdfPath);
                    List<BitmapImage> images = await AddToList(loadedpdfdoc, Dpi);
                    using PdfDocument pdfDocument = await GeneratePdf(images, UseMozJpeg, Quality, Dpi);
                    images = null;
                    pdfDocument.Save($"{Path.GetDirectoryName(LoadedPdfPath)}\\{Path.GetFileNameWithoutExtension(LoadedPdfPath) + "_Compressed.pdf"}");
                    if (Application.Current?.MainWindow?.DataContext is GpScannerViewModel gpScannerViewModel)
                    {
                        gpScannerViewModel.ReloadFileDatas();
                    }
                    GC.Collect();
                }
            }, parameter => !string.IsNullOrWhiteSpace(LoadedPdfPath));
        }

        public new RelayCommand<object> CompressFile { get; }
    }
}