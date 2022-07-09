using Extensions;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using TwainControl;
using Twainsettings = TwainControl.Properties;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadData();
            cvs = TryFindResource("Veriler") as CollectionViewSource;
            SeçiliGün = DateTime.Today;

            ResetFilter = new RelayCommand<object>(parameter => cvs.View.Filter = null, parameter => cvs.View is not null);

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

            PropertyChanged += MainWindow_PropertyChanged;
            TwainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

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

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly CollectionViewSource cvs;

        private ObservableCollection<Scanner> dosyalar;

        private DateTime? seçiliGün;

        private void Calendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured is CalendarItem)
            {
                _ = Mouse.Capture(null);
            }
        }

        private void LoadData()
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

        private void MainWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeçiliGün")
            {
                cvs.Filter += (s, x) => x.Accepted = Path.GetFileName((x.Item as Scanner)?.FileName).StartsWith(SeçiliGün.Value.ToShortDateString()) == true;
            }
        }

        private void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Tarandı")
            {
                LoadData();
            }
        }
    }
}