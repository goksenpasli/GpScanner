using Extensions;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TwainControl.Properties;

namespace TwainControl;

public class SimpleArchiveViewer : ArchiveViewer
{
    private readonly string[] supportedFilesExtension = [".eyp", ".pdf", ".jpg", ".jpeg", ".jfif", ".jfıf", ".jpe", ".png", ".gif", ".gıf", ".bmp", ".tıf", ".tiff", ".tıff", ".heic", ".tif", ".webp", ".xps"];
    private double previewPanelWidth;
    private string thumbFile;

    public SimpleArchiveViewer()
    {
        PropertyChanged += SimpleArchiveViewer_PropertyChanged;
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
                    throw new ArgumentException(ex?.Message);
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

    public double PreviewPanelWidth
    {
        get => previewPanelWidth;

        set
        {
            if (previewPanelWidth != value)
            {
                previewPanelWidth = value;
                OnPropertyChanged(nameof(PreviewPanelWidth));
            }
        }
    }

    public new RelayCommand<object> SeçiliAyıkla { get; }

    public string ThumbFile
    {
        get => thumbFile;
        set
        {
            if (thumbFile != value)
            {
                thumbFile = value;
                OnPropertyChanged(nameof(ThumbFile));
            }
        }
    }

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

    protected override async Task<ObservableCollection<ArchiveData>> ReadArchiveContent(string ArchiveFilePath)
    {
        Arşivİçerik = [];
        await Task.Run(
            async () =>
            {
                try
                {
                    using ArchiveFile archive = new(ArchiveFilePath);
                    if (archive != null)
                    {
                        TotalFilesCount = archive.Entries.Count;
                        foreach (Entry item in archive.Entries)
                        {
                            ExtendedArchiveData archiveData = new()
                            {
                                SıkıştırılmışBoyut = (long)item.PackedSize,
                                DosyaAdı = item.FileName,
                                TamYol = item.FileName,
                                Boyut = (long)item.Size,
                                Oran = (float)item.PackedSize / item.Size,
                                DüzenlenmeZamanı = item.LastWriteTime.Date,
                                Crc = item.CRC.ToString("X"),
                                HostOs = item.HostOS,
                                Method = item.Method,
                                Attributes = (FileAttributes)item.Attributes,
                            };
                            await Dispatcher.InvokeAsync(() => Arşivİçerik.Add(archiveData));
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
                }

                ToplamOran = (double)Arşivİçerik.Sum(z => z.SıkıştırılmışBoyut) / Arşivİçerik.Sum(z => z.Boyut) * 100;
            });
        cvs = CollectionViewSource.GetDefaultView(Arşivİçerik);
        return Arşivİçerik;
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

    private void SimpleArchiveViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SelectedFile")
        {
            if (Settings.Default.ShowArchiveViewerThumbs)
            {
                PreviewPanelWidth = double.PositiveInfinity;
                ThumbFile = SelectedFile is not null ? ExtractToFile(SelectedFile.DosyaAdı) : null;
            }
            else
            {
                PreviewPanelWidth = 0;
            }
        }
    }
}
