using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Extensions.Controls;

/// <summary>
/// Interaction logic for ArchiveViewer.xaml
/// </summary>
public partial class ArchiveViewer : UserControl, INotifyPropertyChanged
{
    public ArchiveViewer()
    {
        InitializeComponent();
        DataContext = this;

        ArşivTekDosyaÇıkar = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    using ZipArchive archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Read);
                    ZipArchiveEntry dosya = archive.GetEntry(parameter as string);
                    string extractpath = Path.Combine(Path.GetTempPath(), dosya.Name);
                    dosya?.ExtractToFile(extractpath, true);
                    _ = Process.Start(extractpath);
                }
                catch(Exception ex)
                {
                    throw new ArgumentException(ArchivePath, ex);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(ArchivePath));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string ArchivePath { get { return (string)GetValue(ArchivePathProperty); } set { SetValue(ArchivePathProperty, value); } }

    public ObservableCollection<ArchiveData> Arşivİçerik
    {
        get { return arşivİçerik; }

        set
        {
            if(arşivİçerik != value)
            {
                arşivİçerik = value;
                OnPropertyChanged(nameof(Arşivİçerik));
            }
        }
    }

    public ICommand ArşivTekDosyaÇıkar { get; }

    public double ToplamOran
    {
        get { return toplamOran; }

        set
        {
            toplamOran = value;
            OnPropertyChanged(nameof(ToplamOran));
        }
    }

    protected virtual void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if(d is ArchiveViewer archiveViewer && e.NewValue is not null)
        {
            archiveViewer.Arşivİçerik = new ObservableCollection<ArchiveData>();
            using(ZipArchive archive = ZipFile.Open((string)e.NewValue, ZipArchiveMode.Read))
            {
                foreach(ZipArchiveEntry item in archive.Entries.Where(z => z.Length > 0))
                {
                    ArchiveData archiveData = new()
                    {
                        SıkıştırılmışBoyut = item.CompressedLength,
                        DosyaAdı = item.Name,
                        TamYol = item.FullName,
                        Boyut = item.Length,
                        Oran = (double)item.CompressedLength / item.Length,
                        DüzenlenmeZamanı = item.LastWriteTime.Date
                    };
                    archiveViewer.Arşivİçerik.Add(archiveData);
                }
            }

            archiveViewer.ToplamOran = (double)archiveViewer.Arşivİçerik.Sum(z => z.SıkıştırılmışBoyut) / archiveViewer.Arşivİçerik.Sum(z => z.Boyut) * 100;
        }
    }

    private static double toplamOran;
    public static readonly DependencyProperty ArchivePathProperty = DependencyProperty.Register(
        "ArchivePath",
        typeof(string),
        typeof(ArchiveViewer),
        new PropertyMetadata(null, Changed));

    private ObservableCollection<ArchiveData> arşivİçerik;
}