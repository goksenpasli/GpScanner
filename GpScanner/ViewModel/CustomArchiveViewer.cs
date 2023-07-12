using Extensions;
using System;
using System.IO;
using System.IO.Compression;
using System.Windows;
using TwainControl;

namespace GpScanner.ViewModel;

public class CustomArchiveViewer : ArchiveViewer
{
    public CustomArchiveViewer()
    {
        Drop -= CustomArchiveViewer_Drop;
        Drop += CustomArchiveViewer_Drop;

        ArşivTekDosyaÇıkar = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    using ZipArchive archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Read);
                    ZipArchiveEntry dosya = archive.GetEntry(parameter as string);
                    string extractpath = $"{Path.GetTempPath()}{Guid.NewGuid()}{Path.GetExtension(dosya.Name)}";
                    dosya?.ExtractToFile(extractpath, true);
                    if(Tag is TwainCtrl twainCtrl)
                    {
                        twainCtrl.AddFiles(new string[] { extractpath }, twainCtrl.DecodeHeight);
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
        if(e.Data.GetData(typeof(ScannedImage)) is ScannedImage scannedimage && scannedimage.FilePath is not null)
        {
            SelectedFiles = new string[] { scannedimage.FilePath };
            ArşivDosyaEkle.Execute(null);
            ReadArchiveContent(ArchivePath, this);
        }
    }
}
