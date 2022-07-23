using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TwainControl;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class GpScannerViewModel : InpcBase
    {
        public GpScannerViewModel()
        {
            LoadData();
            SeçiliGün = DateTime.Today;
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
                        TwainCtrl.MergePdf(Dosyalar.Where(z => z.Seçili).Select(z => z.FileName).ToArray()).Save(saveFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }, parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili);
                return CheckedPdfCount > 1;
            });

            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += GpScannerViewModel_PropertyChanged;
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

        public ICommand PdfBirleştir { get; }

        public ICommand ResetFilter { get; }

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

        private string aramaMetni;

        private int? checkedPdfCount = 0;

        private ObservableCollection<Scanner> dosyalar;

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
        }
    }
}