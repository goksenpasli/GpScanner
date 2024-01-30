using Extensions;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

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
                    if (parameter is string filename && !supportedFilesExtension.Contains(Path.GetExtension(filename).ToLower()))
                    {
                        string extractedfile = ExtractToFile(parameter as string);
                        _ = Process.Start(extractedfile);
                        return;
                    }
                    if (DataContext is TwainCtrl twainCtrl)
                    {
                        string extractedfile = ExtractToFile(parameter as string);
                        _ = twainCtrl.AddFiles([extractedfile], twainCtrl.DecodeHeight);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex.Message);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(ArchivePath));

        SeçiliAyıkla = new RelayCommand<object>(
            parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new() { Description = "Kaydedilecek Klasörü Seçin.", SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ExtractSelectedFiles(ArchivePath, Arşivİçerik.Where(z => z.IsChecked), dialog.SelectedPath);
                }
            },
            parameter => Arşivİçerik is not null && CollectionViewSource.GetDefaultView(Arşivİçerik).OfType<ArchiveData>().Count(z => z.IsChecked) > 0);
    }

    public new RelayCommand<object> ArşivTekDosyaÇıkar { get; }

    public new RelayCommand<object> SeçiliAyıkla { get; }

    protected override void OnDrop(DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Scanner)) is Scanner scanner && File.Exists(scanner.FileName))
        {
            LoadDroppedZipFile([scanner.FileName]);
            return;
        }

        if ((e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
        {
            LoadDroppedZipFile(droppedfiles);
        }
    }

    protected override async void ReadArchiveContent(string ArchiveFilePath, ArchiveViewer archiveViewer)
    {
        archiveViewer.Arşivİçerik = [];
        await Task.Run(
            async () =>
            {
                try
                {
                    using ArchiveFile archive = new(ArchiveFilePath);
                    if (archive != null)
                    {
                        archiveViewer.TotalFilesCount = archive.Entries.Count;
                        foreach (Entry item in archive.Entries)
                        {
                            ArchiveData archiveData = new()
                            {
                                SıkıştırılmışBoyut = (long)item.PackedSize,
                                DosyaAdı = item.FileName,
                                TamYol = item.FileName,
                                Boyut = (long)item.Size,
                                Oran = (float)item.PackedSize / item.Size,
                                DüzenlenmeZamanı = item.LastWriteTime.Date,
                                Crc = item.CRC.ToString("X")
                            };
                            await Dispatcher.InvokeAsync(() => archiveViewer.Arşivİçerik.Add(archiveData));
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex.Message);
                }

                archiveViewer.ToplamOran = (double)archiveViewer.Arşivİçerik.Sum(z => z.SıkıştırılmışBoyut) / archiveViewer.Arşivİçerik.Sum(z => z.Boyut) * 100;
            });
        cvs = CollectionViewSource.GetDefaultView(Arşivİçerik);
    }

    private new void ExtractSelectedFiles(string archivepath, IEnumerable<ArchiveData> list, string destinationfolder)
    {
        if (string.IsNullOrWhiteSpace(destinationfolder) || !Directory.Exists(destinationfolder))
        {
            throw new ArgumentException("Ayıklanacak Klasörün Yolu Hatalı Veya Klasör Yok");
        }
        using ArchiveFile archiveFile = new(archivepath);
        foreach (ArchiveData item in list)
        {
            Entry entry = archiveFile.Entries?.FirstOrDefault(z => z.FileName == item.DosyaAdı);
            entry?.Extract(Path.Combine(destinationfolder, Path.GetFileName(item.DosyaAdı)));
        }
    }

    private new string ExtractToFile(string entryname)
    {
        using ArchiveFile archiveFile = new(ArchivePath);
        Entry entry = archiveFile?.Entries?.FirstOrDefault(z => z.FileName == entryname);
        string extractpath = $"{Path.GetTempPath()}{Guid.NewGuid()}{Path.GetExtension(entryname)}";
        entry?.Extract(extractpath);
        return extractpath;
    }
}
