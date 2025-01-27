using Extensions;
using SevenZipExtractor;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TwainControl;

public class CbrImageViewer : ImageViewer
{
    private ObservableCollection<ArchiveData> cbrFilecontents = [];

    public CbrImageViewer()
    {
        PropertyChanged += CbrImageViewer_PropertyChanged;
        ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => IsCbrFile(ImageFilePath) && Sayfa < Pages?.Count());
        ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => IsCbrFile(ImageFilePath) && Sayfa > 1 && Sayfa <= Pages?.Count());
    }

    public override RelayCommand<object> ViewerBack { get; set; }

    public override RelayCommand<object> ViewerNext { get; set; }

    protected override async Task LoadImageAsync(string filepath, ImageViewer imageViewer)
    {
        if (filepath is not null && File.Exists(filepath))
        {
            switch (Path.GetExtension(filepath).ToLowerInvariant())
            {
                case ".cbr" or ".cbz":
                    imageViewer.Sayfa = 1;
                    CbrViewer cbrViewer = new() { ArchivePath = filepath };
                    cbrFilecontents = await cbrViewer.ReadArchive(cbrViewer.ArchivePath);
                    imageViewer.TifNavigasyonButtonEtkin = Visibility.Visible;
                    imageViewer.Pages = Enumerable.Range(1, cbrFilecontents.Count);
                    OnPropertyChanged(nameof(Sayfa));
                    return;
            }
        }
    }

    private void CbrImageViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Sayfa")
        {
            if (!IsCbrFile(ImageFilePath))
            {
                return;
            }
            if (cbrFilecontents is not { Count: > 0 } || Sayfa > cbrFilecontents.Count)
            {
                return;
            }
            CbrViewer cbrViewer = new() { ArchivePath = ImageFilePath };
            using ArchiveFile archiveFile = new(cbrViewer.ArchivePath);
            Entry entry = archiveFile?.Entries?.FirstOrDefault(z => z.FileName == cbrFilecontents[Sayfa - 1].DosyaAdı);
            string extractpath = $"{Path.GetTempPath()}{cbrFilecontents[Sayfa - 1].DosyaAdı}";
            if (!File.Exists(extractpath))
            {
                entry?.Extract(extractpath);
            }
            Source = BitmapFrame.Create(new Uri(extractpath), BitmapCreateOptions.None, BitmapCacheOption.None);
            if (Resize?.CanExecute(null) == true)
            {
                Resize.Execute(null);
            }
        }
    }

    private bool IsCbrFile(string filepath) => string.Equals(Path.GetExtension(filepath), ".cbr", StringComparison.InvariantCultureIgnoreCase) || string.Equals(Path.GetExtension(filepath), ".cbz", StringComparison.InvariantCultureIgnoreCase);
}
