using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Twainsettings = TwainControl.Properties;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly CollectionViewSource cvs;

        private DateTime? seçiliGün;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadData();
            cvs = TryFindResource("Veriler") as CollectionViewSource;
            SeçiliGün = DateTime.Today;
            PropertyChanged += MainWindow_PropertyChanged;
            TwainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<string> Dosyalar { get; set; } = new ObservableCollection<string>();

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

        private void LoadData()
        {
            if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
            {
                Dosyalar = new ObservableCollection<string>(Directory.EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*").Where(s => (new string[] { ".pdf", ".tif", ".jpg" }).Any(ext => ext == Path.GetExtension(s))));
            }
        }

        private void MainWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeçiliGün")
            {
                cvs.Filter += (s, x) => x.Accepted = new FileInfo(x.Item as string).Name?.StartsWith(SeçiliGün.Value.ToShortDateString()) == true;
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