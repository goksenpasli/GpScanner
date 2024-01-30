using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Extensions
{
    public class ArchiveViewer : Control, INotifyPropertyChanged, IDisposable
    {
        public static readonly DependencyProperty ArchivePathProperty = DependencyProperty.Register("ArchivePath", typeof(string), typeof(ArchiveViewer), new PropertyMetadata(null, Changed));
        protected ICollectionView cvs;
        private ObservableCollection<ArchiveData> arşivİçerik;
        private bool disposedValue;
        private string search = string.Empty;
        private ArchiveData selectedFile;
        private string[] selectedFiles;
        private double toplamOran;
        private int totalFilesCount;

        static ArchiveViewer() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ArchiveViewer), new FrameworkPropertyMetadata(typeof(ArchiveViewer))); }

        public ArchiveViewer()
        {
            PropertyChanged += ArchiveViewer_PropertyChanged;
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                Arşivİçerik =
                [
                    new() { DosyaAdı = "DosyaAdı", Oran = 0.5F, Boyut = 100, SıkıştırılmışBoyut = 80, Crc = "FFFFFFFF", DüzenlenmeZamanı = DateTime.Today },
                    new() { DosyaAdı = "DosyaAdı", Oran = 0.5F, Boyut = 100, SıkıştırılmışBoyut = 80, Crc = "FFFFFFFF", DüzenlenmeZamanı = DateTime.Today },
                    new() { DosyaAdı = "DosyaAdı", Oran = 0.5F, Boyut = 100, SıkıştırılmışBoyut = 80, Crc = "FFFFFFFF", DüzenlenmeZamanı = DateTime.Today },
                ];
            }
            ArşivTekDosyaÇıkar = new RelayCommand<object>(
                parameter =>
                {
                    try
                    {
                        string extractedfile = ExtractToFile(parameter as string);
                        _ = Process.Start(extractedfile);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex.Message);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(ArchivePath));

            ArşivDosyaEkle = new RelayCommand<object>(
                parameter =>
                {
                    try
                    {
                        AddFilesToZip(ArchivePath, SelectedFiles);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException(ex.Message);
                    }
                },
                parameter => !string.IsNullOrWhiteSpace(ArchivePath));

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
                    System.Windows.Forms.FolderBrowserDialog dialog = new() { Description = "Kaydedilecek Klasörü Seçin.", SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        ExtractSelectedFiles(ArchivePath, Arşivİçerik.Where(z => z.IsChecked), dialog.SelectedPath);
                    }
                },
                parameter => Arşivİçerik is not null && CollectionViewSource.GetDefaultView(Arşivİçerik).OfType<ArchiveData>().Count(z => z.IsChecked) > 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ArchivePath { get => (string)GetValue(ArchivePathProperty); set => SetValue(ArchivePathProperty, value); }

        public RelayCommand<object> ArşivDosyaEkle { get; }

        public ObservableCollection<ArchiveData> Arşivİçerik
        {
            get => arşivİçerik;

            set
            {
                if (arşivİçerik != value)
                {
                    arşivİçerik = value;
                    OnPropertyChanged(nameof(Arşivİçerik));
                }
            }
        }

        public RelayCommand<object> ArşivTekDosyaÇıkar { get; }

        public string Search
        {
            get => search;
            set
            {
                if (search != value)
                {
                    search = value;
                    OnPropertyChanged(nameof(Search));
                }
            }
        }

        public RelayCommand<object> SeçiliAyıkla { get; }

        public ArchiveData SelectedFile
        {
            get => selectedFile;
            set
            {
                if (selectedFile != value)
                {
                    selectedFile = value;
                    OnPropertyChanged(nameof(SelectedFile));
                }
            }
        }

        public string[] SelectedFiles
        {
            get => selectedFiles;
            set
            {
                if (selectedFiles != value)
                {
                    selectedFiles = value;
                    OnPropertyChanged(nameof(SelectedFiles));
                }
            }
        }

        public double ToplamOran
        {
            get => toplamOran;

            set
            {
                toplamOran = value;
                OnPropertyChanged(nameof(ToplamOran));
            }
        }

        public int TotalFilesCount
        {
            get => totalFilesCount;
            set
            {
                if (totalFilesCount != value)
                {
                    totalFilesCount = value;
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
            foreach (ArchiveData item in files)
            {
                ZipArchiveEntry dosya = archive.Entries?.FirstOrDefault(z => z.Name == Path.GetFileName(item.DosyaAdı));
                dosya?.ExtractToFile(Path.Combine(destinationfolder, Path.GetFileName(item.DosyaAdı)), true);
            }
        }

        protected string ExtractToFile(string entryname)
        {
            using ZipArchive archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Read);
            if (archive != null)
            {
                ZipArchiveEntry dosya = archive.GetEntry(entryname);
                string extractpath = $"{Path.GetTempPath()}{Guid.NewGuid()}{Path.GetExtension(dosya.Name)}";
                dosya?.ExtractToFile(extractpath, true);
                return extractpath;
            }

            return null;
        }

        protected void LoadDroppedZipFile(string[] droppedfiles)
        {
            if (droppedfiles.Contains(ArchivePath))
            {
                return;
            }
            if (File.Exists(ArchivePath) && ArşivDosyaEkle.CanExecute(null))
            {
                string temppath = ArchivePath;
                SelectedFiles = droppedfiles;
                ArşivDosyaEkle.Execute(null);
                ArchivePath = null;
                ArchivePath = temppath;
                return;
            }
            SaveFileDialog saveFileDialog = new() { Filter = "Zip File (*.zip)|*.zip", AddExtension = true, FileName = "File" };
            if (saveFileDialog.ShowDialog() == true)
            {
                if (droppedfiles.Contains(saveFileDialog.FileName))
                {
                    return;
                }
                using (ZipArchive archive = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Update))
                {
                    foreach (string path in droppedfiles)
                    {
                        _ = archive.CreateEntryFromFile(path, Path.GetFileName(path));
                    }
                }
                ArchivePath = saveFileDialog.FileName;
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if ((e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
            {
                LoadDroppedZipFile(droppedfiles);
            }
        }

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected virtual async void ReadArchiveContent(string ArchiveFilePath, ArchiveViewer archiveViewer)
        {
            archiveViewer.Arşivİçerik = [];
            await Task.Run(
                async () =>
                {
                    try
                    {
                        using ZipArchive archive = ZipFile.Open(ArchiveFilePath, ZipArchiveMode.Read);
                        if (archive != null)
                        {
                            archiveViewer.TotalFilesCount = archive.Entries.Count;
                            foreach (ZipArchiveEntry item in archive.Entries)
                            {
                                ArchiveData archiveData = new()
                                {
                                    SıkıştırılmışBoyut = item.CompressedLength,
                                    DosyaAdı = item.Name,
                                    TamYol = item.FullName,
                                    Boyut = item.Length,
                                    Oran = (float)item.CompressedLength / item.Length,
                                    DüzenlenmeZamanı = item.LastWriteTime.Date,
                                    Crc = null
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

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ArchiveViewer archiveViewer && e.NewValue is string path)
            {
                if (File.Exists(path))
                {
                    archiveViewer.ReadArchiveContent(path, archiveViewer);
                }
                else
                {
                    archiveViewer.Arşivİçerik?.Clear();
                    archiveViewer.ToplamOran = 0;
                }
            }
        }

        private void AddFilesToZip(string zipPath, string[] files)
        {
            if (Path.GetExtension(zipPath) != ".zip" || files?.Length == 0 || files.Contains(zipPath))
            {
                return;
            }

            using ZipArchive zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
            foreach (string file in files)
            {
                FileInfo fileInfo = new(file);
                _ = zipArchive.CreateEntryFromFile(fileInfo.FullName, fileInfo.Name);
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
    }
}
