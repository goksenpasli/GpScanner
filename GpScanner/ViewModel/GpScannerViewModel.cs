using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TwainControl;
using static Extensions.GraphControl;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class GpScannerViewModel : InpcBase
    {
        public GpScannerViewModel()
        {
            Dosyalar = GetScannerFileData();
            ChartData = GetChartsData();
            SeçiliGün = DateTime.Today;
            SeçiliDil = Settings.Default.DefaultLang;

            TesseractViewModel = new TesseractViewModel();

            ResetFilter = new RelayCommand<object>(parameter => MainWindow.cvs.View.Filter = null, parameter => MainWindow.cvs.View is not null);

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
                        TwainCtrl.MergePdf(Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName).ToArray()).Save(saveFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }, parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase));
                return CheckedPdfCount > 1;
            });

            OcrPage = new RelayCommand<object>(parameter =>
            {
                byte[] imgdata;
                switch (parameter)
                {
                    case Scanner scanner:
                        {
                            imgdata = scanner.SeçiliResim.Resim.ToTiffJpegByteArray(ExtensionMethods.Format.Png);
                            Ocr(imgdata);
                            return;
                        }

                    case ImageSource croppedimage:
                        {
                            imgdata = croppedimage.ToTiffJpegByteArray(ExtensionMethods.Format.Png);
                            Ocr(imgdata);
                            return;
                        }
                }
            }, parameter => (!string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is Scanner scanner && scanner.SeçiliResim is not null) || (parameter is ImageSource ımageSource && ımageSource is not null));

            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += GpScannerViewModel_PropertyChanged;
            OnPropertyChanged(nameof(SeçiliDil));
        }

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

        public ObservableCollection<Chart> ChartData
        {
            get => chartData;

            set
            {
                if (chartData != value)
                {
                    chartData = value;
                    OnPropertyChanged(nameof(ChartData));
                }
            }
        }

        public int? CheckedPdfCount
        {
            get => checkedPdfCount;

            set
            {
                if (checkedPdfCount != value)
                {
                    checkedPdfCount = value;
                    OnPropertyChanged(nameof(CheckedPdfCount));
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

        public bool IsBusy
        {
            get => ısBusy;

            set
            {
                if (ısBusy != value)
                {
                    ısBusy = value;
                    OnPropertyChanged(nameof(IsBusy));
                }
            }
        }

        public ICommand OcrPage { get; }

        public ICommand PdfBirleştir { get; }

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

        public bool ScannedTextWindowOpen
        {
            get => scannedTextWindowOpen;

            set
            {
                if (scannedTextWindowOpen != value)
                {
                    scannedTextWindowOpen = value;
                    OnPropertyChanged(nameof(ScannedTextWindowOpen));
                }
            }
        }

        public string SeçiliDil
        {
            get => seçiliDil;

            set
            {
                if (seçiliDil != value)
                {
                    seçiliDil = value;
                    OnPropertyChanged(nameof(SeçiliDil));
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

        public TesseractViewModel TesseractViewModel
        {
            get => tesseractViewModel;

            set
            {
                if (tesseractViewModel != value)
                {
                    tesseractViewModel = value;
                    OnPropertyChanged(nameof(TesseractViewModel));
                }
            }
        }

        public ObservableCollection<Chart> GetChartsData()
        {
            ObservableCollection<Chart> list = new();
            try
            {
                foreach (IGrouping<int, Scanner> chart in Dosyalar.GroupBy(z => DateTime.Parse(Directory.GetParent(z.FileName).Name).Day))
                {
                    list.Add(new Chart() { Description = chart.Key.ToString(), ChartBrush = RandomColor(), ChartValue = chart.Count() });
                }
                return list;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public ObservableCollection<Scanner> GetScannerFileData()
        {
            if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
            {
                ObservableCollection<Scanner> list = new();
                foreach (string dosya in Directory.EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*", SearchOption.AllDirectories).Where(s => (new string[] { ".pdf", ".tif", ".jpg", ".zip" }).Any(ext => ext == Path.GetExtension(s).ToLower())))
                {
                    list.Add(new Scanner() { FileName = dosya, Seçili = false });
                }
                return list;
            }
            return null;
        }

        public void Ocr(byte[] imgdata)
        {
            if (imgdata is not null)
            {
                _ = Task.Run(() =>
                {
                    ScannedText = null;
                    IsBusy = true;
                    ScannedText = imgdata.OcrYap(Settings.Default.DefaultTtsLang);
                    IsBusy = false;
                    if (!string.IsNullOrWhiteSpace(ScannedText))
                    {
                        ScannedTextWindowOpen = true;
                    }
                    imgdata = null;
                });
            }
        }

        private string aramaMetni;

        private ObservableCollection<Chart> chartData;

        private int? checkedPdfCount = 0;

        private ObservableCollection<Scanner> dosyalar;

        private bool ısBusy;

        private string scannedText;

        private bool scannedTextWindowOpen;

        private string seçiliDil;

        private DateTime? seçiliGün;

        private bool showPdfPreview;

        private TesseractViewModel tesseractViewModel;

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Settings.Default.Save();
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
            if (e.PropertyName is "SeçiliDil")
            {
                switch (SeçiliDil)
                {
                    case "TÜRKÇE":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                        break;

                    case "ENGLISH":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("en-EN");
                        break;
                }
                Settings.Default.DefaultLang = SeçiliDil;
            }
        }

        private Brush RandomColor()
        {
            Random rand = new(Guid.NewGuid().GetHashCode());
            return new SolidColorBrush(Color.FromRgb((byte)rand.Next(0, 256), (byte)rand.Next(0, 256), (byte)rand.Next(0, 256)));
        }
    }
}