using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using static Extensions.ExtensionMethods;
using static Extensions.ShellIcon;

namespace Extensions
{
    public class ArchiveViewer : Control, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty ArchivePathProperty = DependencyProperty.Register("ArchivePath", typeof(string), typeof(ArchiveViewer), new PropertyMetadata(null, Changed));
        public static readonly DependencyPropertyKey ProgressProperty = DependencyProperty.RegisterReadOnly("Progress", typeof(double), typeof(ArchiveViewer), new PropertyMetadata(0d));
        protected ICollectionView cvs;
        private bool disposedValue;

        static ArchiveViewer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ArchiveViewer), new FrameworkPropertyMetadata(typeof(ArchiveViewer))); }

        public ArchiveViewer()
        {
            PropertyChanged += ArchiveViewer_PropertyChanged;
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                Arşivİçerik =
                [
                    new() { DosyaAdı = "DosyaAdı", Oran = 0.4F, Boyut = 100, SıkıştırılmışBoyut = 40, Crc = "FFFFFFFF", DüzenlenmeZamanı = DateTime.Today },
                    new() { DosyaAdı = "DosyaAdı", Oran = 0.6F, Boyut = 100, SıkıştırılmışBoyut = 60, Crc = "FFFFFFFF", DüzenlenmeZamanı = DateTime.Today },
                    new() { DosyaAdı = "DosyaAdı", Oran = 0.8F, Boyut = 100, SıkıştırılmışBoyut = 80, Crc = "FFFFFFFF", DüzenlenmeZamanı = DateTime.Today },
                ];
            }
            ArşivTekDosyaÇıkar = new RelayCommand<object>(
                async parameter =>
                {
                    try
                    {
                        string extractedfile = await ExtractToFileAsync(SelectedFile);
                        _ = Process.Start(extractedfile);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex?.Message);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(ArchivePath));

            ArşivDosyaEkle = new RelayCommand<object>(
                async parameter =>
                {
                    try
                    {
                        string temppath = ArchivePath;
                        await AddFilesToZipAsync(ArchivePath, SelectedFiles);
                        ArchivePath = null;
                        ArchivePath = temppath;
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex?.Message);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(ArchivePath));

            ArşivDosyaSil = new RelayCommand<object>(
                parameter =>
                {
                    try
                    {
                        if (parameter is ArchiveData archiveData)
                        {
                            ExtendedMessageBox extendedmessagebox = new() { NoButton = Visibility.Visible, YesButton = Visibility.Visible, };
                            extendedmessagebox.ShowDialog(
                                Window.GetWindow(this),
                                string.Empty,
                                "DOSYA SİL",
                                () =>
                                {
                                    using FileStream zipToOpen = new(ArchivePath, FileMode.Open);
                                    using ZipArchive archive = new(zipToOpen, ZipArchiveMode.Update);
                                    if (archive is not null)
                                    {
                                        ZipArchiveEntry entry = archive.GetEntry(archiveData.DosyaAdı);
                                        entry?.Delete();
                                        _ = Arşivİçerik?.Remove(archiveData);
                                        ToplamOran = GetCompressedRatio();
                                        TotalFilesCount = GetArchiveFileCount(archive);
                                    }
                                });
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex?.Message);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(ArchivePath) && Arşivİçerik?.Count > 1 && string.Equals(Path.GetExtension(ArchivePath), ".zip", StringComparison.InvariantCultureIgnoreCase));

            TümünüSeç = new RelayCommand<object>(
                parameter =>
                {
                    foreach (ArchiveData item in CollectionViewSource.GetDefaultView(Arşivİçerik))
                    {
                        item.IsChecked = !item.IsChecked;
                    }
                },
                parameter => Arşivİçerik is not null && !CollectionViewSource.GetDefaultView(Arşivİçerik).IsEmpty);

            SeçiliAyıkla = new RelayCommand<object>(
                parameter =>
                {
                    string path = FolderDialog.SelectFolder("Kaydedilecek Klasörü Seçin.", null);
                    if (!string.IsNullOrEmpty(path))
                    {
                        ExtractSelectedFiles(ArchivePath, Arşivİçerik.Where(z => z.IsChecked), path);
                        OpenFolderAndSelectItem(path, string.Empty);
                    }
                },
                parameter => Arşivİçerik is not null && CollectionViewSource.GetDefaultView(Arşivİçerik).OfType<ArchiveData>().Count(z => z.IsChecked) > 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<CheckBoxItem> ArchiveFileTypes
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(ArchiveFileTypes));
                }
            }
        }

        public string ArchivePath { get => (string)GetValue(ArchivePathProperty); set => SetValue(ArchivePathProperty, value); }

        public RelayCommand<object> ArşivDosyaEkle { get; }

        public RelayCommand<object> ArşivDosyaSil { get; }

        public ObservableCollection<ArchiveData> Arşivİçerik
        {
            get;

            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Arşivİçerik));
                }
            }
        }

        public RelayCommand<object> ArşivTekDosyaÇıkar { get; }

        public int CheckedCount
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(CheckedCount));
                }
            }
        }

        public double Progress => (double)GetValue(ProgressProperty.DependencyProperty);

        public string Search
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Search));
                }
            }
        } = string.Empty;

        public RelayCommand<object> SeçiliAyıkla { get; }

        public ArchiveData SelectedFile
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SelectedFile));
                }
            }
        }

        public string[] SelectedFiles
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SelectedFiles));
                }
            }
        }

        public double ToplamOran
        {
            get;

            set
            {
                field = value;
                OnPropertyChanged(nameof(ToplamOran));
            }
        }

        public int TotalFilesCount
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(TotalFilesCount));
                }
            }
        }

        public RelayCommand<object> TümünüSeç { get; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }
                disposedValue = true;
            }
        }

        protected void ExtractSelectedFiles(string archivepath, IEnumerable<ArchiveData> files, string destinationfolder)
        {
            if (string.IsNullOrWhiteSpace(destinationfolder) || !Directory.Exists(destinationfolder))
            {
                throw new ArgumentException("Ayıklanacak Klasörün Yolu Hatalı Veya Klasör Yok");
            }
            using ZipArchive archive = ZipFile.Open(archivepath, ZipArchiveMode.Read) ?? throw new ArgumentException("Arşiv Açılamadı");
            ArchiveData[] archivedata = [.. files];
            for (int i = 0; i < archivedata.Length; i++)
            {
                ArchiveData item = archivedata[i];
                ZipArchiveEntry dosya = archive.Entries?.FirstOrDefault(z => z.Name == Path.GetFileName(item.DosyaAdı));
                dosya?.ExtractToFile(Path.Combine(destinationfolder, Path.GetFileName(item.DosyaAdı)), true);
                SetValue(ProgressProperty, (i + 1) / (double)archivedata.Length);
            }
        }

        protected async Task<string> ExtractToFileAsync(ArchiveData entryname)
        {
            string archivepath = ArchivePath;
            return await Task.Run(
                async () =>
                {
                    using ZipArchive archive = ZipFile.Open(archivepath, ZipArchiveMode.Read);
                    if (archive is not null)
                    {
                        ZipArchiveEntry dosya = archive.GetEntry(entryname.TamYol);
                        string extractpath = $"{Path.GetTempPath()}{dosya?.Name}";
                        if (!File.Exists(extractpath))
                        {
                            _ = await Dispatcher.InvokeAsync(() => entryname.IsIndeterminate = true);
                            dosya?.ExtractToFile(extractpath, true);
                            _ = await Dispatcher.InvokeAsync(() => entryname.IsIndeterminate = false);
                        }
                        return extractpath;
                    }

                    return null;
                });
        }

        protected async void LoadDroppedZipFile(string[] droppedfiles)
        {
            if (droppedfiles.Contains(ArchivePath))
            {
                return;
            }
            if (File.Exists(ArchivePath) && ArşivDosyaEkle.CanExecute(null))
            {
                SelectedFiles = droppedfiles;
                ArşivDosyaEkle.Execute(null);
                return;
            }
            SaveFileDialog saveFileDialog = new() { Filter = "Zip File (*.zip)|*.zip", AddExtension = true, FileName = "File" };
            if (saveFileDialog.ShowDialog() == true)
            {
                if (droppedfiles.Contains(saveFileDialog.FileName))
                {
                    return;
                }
                await AddFilesToZipAsync(saveFileDialog.FileName, droppedfiles);
                ArchivePath = saveFileDialog.FileName;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                Dispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }

        protected virtual async Task<ObservableCollection<ArchiveData>> ReadArchiveContent(string archiveFilePath)
        {
            ObservableCollection<ArchiveData> tempContent = [];
            ObservableCollection<CheckBoxItem> FilterTypes = [];

            double totalCompressed = 0;
            double totalSize = 0;

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(archiveFilePath);
                List<ZipArchiveEntry> entries = archive.Entries?.Where(e => e.Length > 0).ToList() ?? [];

                TotalFilesCount = entries.Count;

                await Task.Run(
                    () =>
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            ZipArchiveEntry entry = entries[i];

                            string fileType = GetFileType(entry.Name, new SHFILEINFO());
                            ArchiveData archiveData = new()
                            {
                                SıkıştırılmışBoyut = entry.CompressedLength,
                                DosyaAdı = entry.Name,
                                TamYol = entry.FullName,
                                Boyut = entry.Length,
                                Oran = (float)entry.CompressedLength / entry.Length,
                                DüzenlenmeZamanı = entry.LastWriteTime.Date,
                                DosyaTipi = fileType,
                                Crc = null
                            };

                            archiveData.PropertyChanged += ArchiveData_PropertyChanged;

                            if (!FilterTypes.Any(t => t.Name == fileType))
                            {
                                CheckBoxItem checkboxitem = new() { Content = archiveData.DosyaAdı, Name = fileType};
                                checkboxitem.PropertyChanged += CheckBoxItem_PropertyChanged;
                                FilterTypes.Add(checkboxitem);
                            }

                            tempContent.Add(archiveData);
                            totalCompressed += entry.CompressedLength;
                            totalSize += entry.Length;
                        }
                    });

                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        Arşivİçerik = tempContent;
                        tempContent = null;
                        ArchiveFileTypes = FilterTypes;
                        FilterTypes = null;
                        ToplamOran = totalCompressed / totalSize * 100;
                        cvs = CollectionViewSource.GetDefaultView(Arşivİçerik);
                    });
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex?.Message);
            }

            return Arşivİçerik;
        }

        private static async void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ArchiveViewer archiveViewer && e.NewValue is string path)
            {
                if (File.Exists(path))
                {
                    _ = await archiveViewer.ReadArchiveContent(path);
                }
                else
                {
                    archiveViewer.Arşivİçerik?.Clear();
                    archiveViewer.ToplamOran = 0;
                }
            }
        }

        private async Task AddFilesToZipAsync(string zipPath, string[] files)
        {
            await Task.Run(
                async () =>
                {
                    if (!string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.InvariantCultureIgnoreCase) || files?.Length is <= 0 or > 65535 || files.Contains(zipPath))
                    {
                        return;
                    }

                    using ZipArchive zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
                    int filescount = files.Length;
                    for (int i = 0; i < filescount; i++)
                    {
                        string file = files[i];
                        FileInfo fileInfo = new(file);
                        _ = zipArchive.CreateEntryFromFile(fileInfo.FullName, fileInfo.Name);
                        await Dispatcher.InvokeAsync(() => SetValue(ProgressProperty, (i + 1) / (double)filescount));
                    }
                });
        }

        private void ArchiveData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "IsChecked")
            {
                CheckedCount = Arşivİçerik?.Count(z => z.IsChecked) ?? 0;
            }
        }

        private void ArchiveViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Search" && cvs is not null)
            {
                cvs.Filter = !string.IsNullOrWhiteSpace(Search)
                             ? (x =>
                                {
                                    ArchiveData archiveData = x as ArchiveData;
                                    return archiveData?.DosyaAdı?.Contains(Search, StringComparison.CurrentCultureIgnoreCase) == true;
                                })
                             : null;
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

        private int GetArchiveFileCount(ZipArchive archive) => archive.Entries?.Count(z => z.Length > 0) ?? 0;

        private double GetCompressedRatio() => (double)Arşivİçerik.Sum(z => z.SıkıştırılmışBoyut) / Arşivİçerik.Sum(z => z.Boyut) * 100;
    }
}
