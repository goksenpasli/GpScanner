using Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TwainControl;

public class SimpleArchiveViewer : ArchiveViewer
{
    private readonly string[] supportedFilesExtension = [".eyp", ".pdf", ".jpg", ".jpeg", ".jfif", ".jfıf", ".jpe", ".png", ".gif", ".gıf", ".bmp", ".tıf", ".tiff", ".tıff", ".heic", ".tif", ".webp", ".xps"];

    public SimpleArchiveViewer()
    {
        ArşivTekDosyaÇıkar = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is string filename && !supportedFilesExtension.Contains(Path.GetExtension(filename)))
                    {
                        string extractedfile = ExtractToFile(parameter as string);
                        _ = Process.Start(extractedfile);
                        return;
                    }
                    if (DataContext is TwainCtrl twainCtrl)
                    {
                        string extractedfile = ExtractToFile(parameter as string);
                        twainCtrl.AddFiles([extractedfile], twainCtrl.DecodeHeight);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ArchivePath, ex);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(ArchivePath));
    }

    public new RelayCommand<object> ArşivTekDosyaÇıkar { get; }
}
