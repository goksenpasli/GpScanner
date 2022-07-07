using Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
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

            ResetFilter = new RelayCommand<object>(parameter => cvs.View.Filter = null, parameter => cvs is not null);

            PropertyChanged += MainWindow_PropertyChanged;
            TwainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IEnumerable<string> Dosyalar { get; set; }

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

        private DateTime? seçiliGün;

        private void LoadData()
        {
            if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
            {
                Dosyalar = Directory.EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*", SearchOption.AllDirectories).Where(s => (new string[] { ".pdf", ".tif", ".jpg" }).Any(ext => ext == Path.GetExtension(s).ToLower()));
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

        private void Calendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured is CalendarItem)
            {
                _ = Mouse.Capture(null);
            }
        }
    }
}