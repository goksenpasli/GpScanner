﻿using Extensions;
using GpScanner.Properties;
using PdfCompressor;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
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
                        List<BitmapImage> images = await AddToListAsync(loadedpdfdoc, Dpi);
                        pdfDocument = await GeneratePdfAsync(images, UseMozJpeg, BlackAndWhite, Quality, Dpi, progress => CompressionProgress = progress);
                        images = null;
                    }

                    string savefilename = Settings.Default.DirectlyOverwriteCompressedPdf ? LoadedPdfPath : $"{Path.GetDirectoryName(LoadedPdfPath)}\\{Path.GetFileNameWithoutExtension(LoadedPdfPath)}{Translation.GetResStringValue("COMPRESS")}.pdf";
                    pdfDocument?.Save(savefilename);
                    pdfDocument?.Dispose();
                    if (Application.Current?.MainWindow?.DataContext is GpScannerViewModel gpScannerViewModel)
                    {
                        DateTime date = gpScannerViewModel.SeçiliGün;
                        gpScannerViewModel.ReloadFileDatas();
                        gpScannerViewModel.SeçiliGün = date;
                    }
                }
            },
            parameter => !string.IsNullOrWhiteSpace(LoadedPdfPath));
    }

    public new RelayCommand<object> CompressFile { get; }
}