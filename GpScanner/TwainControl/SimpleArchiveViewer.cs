using Extensions;
using SevenZipExtractor;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using System;
using System.Collections;
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
using static Extensions.ExtensionMethods;
using static Extensions.ShellIcon;

namespace TwainControl;

public class SimpleArchiveViewer : ArchiveViewer
{
    public static readonly DependencyProperty SimpleStyleProperty = DependencyProperty.Register("SimpleStyle", typeof(bool), typeof(SimpleArchiveViewer), new PropertyMetadata(false));
    private readonly string[] supportedFilesExtension = [".eyp", ".pdf", ".jpg", ".jpeg", ".jfif", ".jpe", ".png", ".gif", ".bmp", ".tiff", ".heic", ".tif", ".webp", ".xps", ".jb2"];

    public SimpleArchiveViewer()
    {
        PropertyChanged += SimpleArchiveViewer_PropertyChanged;
        ArşivTekDosyaÇıkar = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is string filename && !supportedFilesExtension.Contains(Path.GetExtension(filename).ToLowerInvariant()))
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
            parameter => !string.IsNullOrWhiteSpace(ArchivePath) && !((ExtendedArchiveData)SelectedFile).Encrypted);

        SeçiliAyıkla = new RelayCommand<object>(
            parameter =>
            {
                string path = FolderDialog.SelectFolder(Translation.GetResStringValue("AUTOFOLDER"), null);
                if (!string.IsNullOrEmpty(path))
                {
                    ExtractSelectedFiles(ArchivePath, Arşivİçerik.Where(z => z.IsChecked), path);
                    OpenFolderAndSelectItem(path, string.Empty);
                }
            },
            parameter => Arşivİçerik is not null && CollectionViewSource.GetDefaultView(Arşivİçerik).OfType<ArchiveData>().Count(z => z.IsChecked) > 0 && !((ExtendedArchiveData)SelectedFile).Encrypted);
    }

    public new RelayCommand<object> ArşivTekDosyaÇıkar { get; }

    public double PreviewPanelWidth
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PreviewPanelWidth));
            }
        }
    }

    public new RelayCommand<object> SeçiliAyıkla { get; }

    public bool SimpleStyle { get => (bool)GetValue(SimpleStyleProperty); set => SetValue(SimpleStyleProperty, value); }

    public string ThumbFile
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ThumbFile));
            }
        }
    }

    public static async Task ZipCompress(IList files, string savepath)
    {
        await Task.Run(
            () =>
            {
                using FileStream zip = File.OpenWrite(savepath);
                using IWriter zipWriter = WriterFactory.Open(zip, SharpCompress.Common.ArchiveType.Zip, new ZipWriterOptions(SharpCompress.Common.CompressionType.Deflate) { UseZip64 = true });
                foreach (string dosya in files)
                {
                    zipWriter.Write(Path.GetFileName(dosya), dosya);
                }
            });
    }

    protected override void OnDrop(DragEventArgs e)
    {
        if (e?.Data?.GetData(typeof(Scanner)) is Scanner scanner && File.Exists(scanner.FileName))
        {
            LoadDroppedZipFile([scanner.FileName]);
            return;
        }

        if ((e?.Data?.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
        {
            LoadDroppedZipFile(droppedfiles);
        }
    }

    protected override async Task<ObservableCollection<ArchiveData>> ReadArchiveContent(string ArchiveFilePath)
    {
        Arşivİçerik = [];
        ArchiveFileTypes = [];
        await Task.Run(
            async () =>
            {
                try
                {
                    using ArchiveFile archive = new(ArchiveFilePath);
                    if (archive is not null)
                    {
                        TotalFilesCount = archive.Entries?.Count(z => z.Size > 0) ?? 0;
                        foreach (Entry item in archive.Entries?.Where(z => z.Size > 0))
                        {
                            ExtendedArchiveData archiveData = new()
                            {
                                SıkıştırılmışBoyut = (long)item.PackedSize,
                                DosyaAdı = item.FileName,
                                DosyaTipi = GetFileType(item.FileName, new SHFILEINFO()),
                                TamYol = item.FileName,
                                Boyut = (long)item.Size,
                                Oran = (float)item.PackedSize / item.Size,
                                DüzenlenmeZamanı = item.LastWriteTime.Date,
                                Crc = item.CRC.ToString("X"),
                                HostOs = item.HostOS,
                                Method = item.Method,
                                Attributes = (FileAttributes)item.Attributes,
                                Encrypted = item.IsEncrypted
                            };
                            archiveData.PropertyChanged += ArchiveData_PropertyChanged;
                            CheckBoxItem checkBoxItem = new() { Name = archiveData.DosyaTipi };
                            checkBoxItem.PropertyChanged += CheckBoxItem_PropertyChanged;
                            await Dispatcher.InvokeAsync(
                                () =>
                                {
                                    Arşivİçerik.Add(archiveData);
                                    if (!ArchiveFileTypes.Any(z => z.Name == checkBoxItem.Name))
                                    {
                                        ArchiveFileTypes.Add(checkBoxItem);
                                    }
                                });
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

    private void ArchiveData_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "IsChecked")
        {
            CheckedCount = Arşivİçerik?.Count(z => z.IsChecked) ?? 0;
        }
    }

    private void CheckBoxItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "IsChecked")
        {
            cvs.Filter = ArchiveFileTypes?.Any(z => z.IsChecked) == true
                         ? (x =>
                            {
                                ArchiveData archiveData = x as ArchiveData;
                                return ArchiveFileTypes.Any(z => z.IsChecked && archiveData.DosyaTipi == z.Name);
                            })
                         : null;
        }
    }

    private new void ExtractSelectedFiles(string archivepath, IEnumerable<ArchiveData> list, string destinationfolder)
    {
        if (!Directory.Exists(destinationfolder))
        {
            throw new ArgumentException("Ayıklanacak Klasörün Yolu Hatalı Veya Klasör Yok");
        }
        using ArchiveFile archiveFile = new(archivepath);
        ArchiveData[] archivedata = [.. list];
        for (int i = 0; i < archivedata.Length; i++)
        {
            ArchiveData item = archivedata[i];
            Entry entry = archiveFile.Entries?.FirstOrDefault(z => z.FileName == item.DosyaAdı);
            entry?.Extract(Path.Combine(destinationfolder, Path.GetFileName(item.DosyaAdı)));
            SetValue(ProgressProperty, (i + 1) / (double)archivedata.Length);
        }
    }

    private new string ExtractToFile(string entryname)
    {
        using ArchiveFile archiveFile = new(ArchivePath);
        Entry entry = archiveFile?.Entries?.FirstOrDefault(z => z.FileName == entryname);
        string extractpath = $"{Path.GetTempPath()}{entryname}";
        if (!File.Exists(extractpath))
        {
            entry?.Extract(extractpath);
        }
        return extractpath;
    }

    private void SimpleArchiveViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SelectedFile")
        {
            if (Settings.Default.ShowArchiveViewerThumbs)
            {
                PreviewPanelWidth = double.PositiveInfinity;
                ThumbFile = ((ExtendedArchiveData)SelectedFile)?.Encrypted == false ? ExtractToFile(SelectedFile.DosyaAdı) : null;
            }
            else
            {
                PreviewPanelWidth = 0;
            }
        }
    }
}
