using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Input;
using TwainControl;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class GpScannerViewModel : INotifyPropertyChanged
    {
        public GpScannerViewModel()
        {
            LoadData();
            SeçiliGün = DateTime.Today;
            string tessdatafolder = Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName) + @"\tessdata";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            TesseractFiles = GetTesseractFiles(tessdatafolder);

            ResetFilter = new RelayCommand<object>(parameter => MainWindow.cvs.View.Filter = null, parameter => MainWindow.cvs.View is not null);

            TesseractDataFilesDownloadLink = new RelayCommand<object>(parameter => _ = Process.Start(parameter as string), parameter => true);

            TesseractDownload = new RelayCommand<object>(async parameter =>
            {
                string filename = parameter as string;
                using WebClient client = new();
                client.DownloadProgressChanged += (s, e) => ProgressValue = e.ProgressPercentage;
                client.DownloadFileCompleted += (s, e) => TesseractFiles = GetTesseractFiles(tessdatafolder);
                await client.DownloadFileTaskAsync(new Uri($"https://github.com/tesseract-ocr/tessdata/raw/main/{filename}"), $@"{tessdatafolder}\{filename}");
            }, parameter => true);

            OcrPage = new RelayCommand<object>(parameter =>
            {
                if (parameter is Scanner scanner)
                {
                    ScannedText = scanner.SeçiliResim.Resim.ToTiffJpegByteArray(ExtensionMethods.Format.Png).OcrYap(Settings.Default.DefaultTtsLang);
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is Scanner scanner && scanner.SeçiliResim is not null);

            PdfBirleştir = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                    FileName = "Birleştirilmiş"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        TwainCtrl.MergePdf(Dosyalar.Where(z => z.Seçili).Select(z => z.FileName).ToArray()).Save(saveFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }, parameter => Dosyalar?.Count(z => z.Seçili) > 0);

            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += GpScannerViewModel_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string AramaMetni
        {
            get => aramaMetni;

            set
            {
                if (aramaMetni != value)
                {
                    aramaMetni = value;
                    OnPropertyChanged(nameof(AramaMetni));
                }
            }
        }

        public ObservableCollection<Scanner> Dosyalar
        {
            get => dosyalar;

            set
            {
                if (dosyalar != value)
                {
                    dosyalar = value;
                    OnPropertyChanged(nameof(Dosyalar));
                }
            }
        }

        public ICommand OcrPage { get; }

        public ICommand PdfBirleştir { get; }

        public double ProgressValue
        {
            get => progressValue;

            set
            {
                if (progressValue != value)
                {
                    progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        public ICommand ResetFilter { get; }

        public string ScannedText
        {
            get => scannedText;

            set
            {
                if (scannedText != value)
                {
                    scannedText = value;
                    OnPropertyChanged(nameof(ScannedText));
                }
            }
        }

        public DateTime? SeçiliGün
        {
            get => seçiliGün; set

            {
                if (seçiliGün != value)
                {
                    seçiliGün = value;
                    OnPropertyChanged(nameof(SeçiliGün));
                }
            }
        }

        public bool ShowPdfPreview
        {
            get => showPdfPreview;

            set
            {
                if (showPdfPreview != value)
                {
                    showPdfPreview = value;
                    OnPropertyChanged(nameof(ShowPdfPreview));
                }
            }
        }

        public ICommand TesseractDataFilesDownloadLink { get; }

        public ICommand TesseractDownload { get; }

        public ObservableCollection<string> TesseractFiles
        {
            get => tesseractFiles;

            set
            {
                if (tesseractFiles != value)
                {
                    tesseractFiles = value;
                    OnPropertyChanged(nameof(TesseractFiles));
                }
            }
        }

        public void LoadData()
        {
            if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
            {
                Dosyalar = new ObservableCollection<Scanner>();
                foreach (string dosya in Directory.EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*", SearchOption.AllDirectories).Where(s => (new string[] { ".pdf", ".tif", ".jpg" }).Any(ext => ext == Path.GetExtension(s).ToLower())))
                {
                    Dosyalar.Add(new Scanner() { FileName = dosya, Seçili = false });
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string aramaMetni;

        private ObservableCollection<Scanner> dosyalar;

        private double progressValue;

        private string scannedText;

        private DateTime? seçiliGün;

        private bool showPdfPreview;

        private ObservableCollection<string> tesseractFiles = new();

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Settings.Default.Save();
        }

        private ObservableCollection<string> GetTesseractFiles(string tesseractfolder)
        {
            return Directory.Exists(tesseractfolder) ? new ObservableCollection<string>(Directory.EnumerateFiles(tesseractfolder, "*.traineddata").Select(Path.GetFileNameWithoutExtension)) : null;
        }

        private void GpScannerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeçiliGün")
            {
                MainWindow.cvs.Filter += (s, x) => x.Accepted = Directory.GetParent((x.Item as Scanner)?.FileName).Name.StartsWith(SeçiliGün.Value.ToShortDateString());
            }
            if (e.PropertyName is "AramaMetni")
            {
                MainWindow.cvs.Filter += (s, x) => x.Accepted = Path.GetFileNameWithoutExtension((x.Item as Scanner)?.FileName).Contains(AramaMetni, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}