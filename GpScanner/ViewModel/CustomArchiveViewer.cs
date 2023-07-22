﻿using Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using TwainControl;

namespace GpScanner.ViewModel;

public class CustomArchiveViewer : ArchiveViewer
{
    public static readonly string[] supportedFilesExtension = { ".eyp", ".pdf", ".jpg", ".jpeg", ".jfif", ".jfıf", ".jpe", ".png", ".gif", ".gıf", ".bmp", ".tıf", ".tiff", ".tıff", ".heic", ".tif", ".webp", ".xps" };

    public CustomArchiveViewer()
    {
        Drop -= CustomArchiveViewer_Drop;
        Drop += CustomArchiveViewer_Drop;

        ArşivTekDosyaÇıkar = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if(parameter is string filename && !supportedFilesExtension.Contains(Path.GetExtension(filename)))
                    {
                        string extractedfile = ExtractToFile(parameter as string);
                        _ = Process.Start(extractedfile);
                        return;
                    }
                    if(Tag is TwainCtrl twainCtrl)
                    {
                        string extractedfile = ExtractToFile(parameter as string);
                        twainCtrl.AddFiles(new string[] { extractedfile }, twainCtrl.DecodeHeight);
                    }
                } catch(Exception ex)
                {
                    throw new ArgumentException(ArchivePath, ex);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(ArchivePath));
    }

    public new RelayCommand<object> ArşivTekDosyaÇıkar { get; }

    private void CustomArchiveViewer_Drop(object sender, DragEventArgs e)
    {
        if(e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles && droppedfiles?.Length > 0)
        {
            SelectedFiles = droppedfiles;
            ArşivDosyaEkle.Execute(null);
            ReadArchiveContent(ArchivePath, this);
            return;
        }
        if(e.Data.GetData(typeof(ScannedImage)) is ScannedImage scannedimage && scannedimage.FilePath is not null)
        {
            SelectedFiles = new string[] { scannedimage.FilePath };
            ArşivDosyaEkle.Execute(null);
            ReadArchiveContent(ArchivePath, this);
        }
    }
}