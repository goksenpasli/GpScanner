using Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using TwainControl;

namespace GpScanner.ViewModel;

public class CustomArchiveViewer : ArchiveViewer
{
    public static readonly string[] supportedFilesExtension = [".eyp", ".pdf", ".jpg", ".jpeg", ".jfif", ".jfıf", ".jpe", ".png", ".gif", ".gıf", ".bmp", ".tıf", ".tiff", ".tıff", ".heic", ".tif", ".webp", ".xps"];

    public CustomArchiveViewer()
    {
        ArşivTekDosyaÇıkar = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is string filename)
                    {
                        if (!supportedFilesExtension.Contains(Path.GetExtension(filename.ToLower())))
                        {
                            string extractedfile = ExtractToFile(filename);
                            _ = Process.Start(extractedfile);
                            return;
                        }
                        if (DataContext is TwainCtrl twainCtrl)
                        {
                            string extractedfile = ExtractToFile(filename);
                            _ = twainCtrl.AddFiles([extractedfile], twainCtrl.DecodeHeight);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Title);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(ArchivePath));
    }

    public new RelayCommand<object> ArşivTekDosyaÇıkar { get; }

    protected override void OnDrop(DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles && droppedfiles?.Length > 0)
        {
            SelectedFiles = droppedfiles;
            ArşivDosyaEkle.Execute(null);
            ReadArchiveContent(ArchivePath, this);
            return;
        }
        if (e.Data.GetData(typeof(ScannedImage)) is ScannedImage scannedimage && scannedimage.FilePath is not null)
        {
            SelectedFiles = [scannedimage.FilePath];
            ArşivDosyaEkle.Execute(null);
            ReadArchiveContent(ArchivePath, this);
        }
    }
}
