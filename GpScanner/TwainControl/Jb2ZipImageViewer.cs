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

public class Jb2ZipImageViewer : ImageViewer
{
    private ObservableCollection<ArchiveData> cbrFilecontents = [];

    public Jb2ZipImageViewer()
    {
        PropertyChanged += Jb2ZipImageViewer_PropertyChanged;
        ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => IsJb2ZipFile(ImageFilePath) && Sayfa < Pages?.Count());
        ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => IsJb2ZipFile(ImageFilePath) && Sayfa > 1 && Sayfa <= Pages?.Count());
    }

    public override RelayCommand<object> ViewerBack { get; set; }

    public override RelayCommand<object> ViewerNext { get; set; }

    public static bool IsJb2ZipFile(string filepath) => string.Equals(Path.GetExtension(filepath), ".jb2zip", StringComparison.InvariantCultureIgnoreCase);

    protected override async Task LoadImageAsync(string filepath, ImageViewer imageViewer)
    {
        if (filepath is not null && File.Exists(filepath))
        {
            switch (Path.GetExtension(filepath).ToLowerInvariant())
            {
                case ".jb2zip":
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

    private async void Jb2ZipImageViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Sayfa")
        {
            if (!IsJb2ZipFile(ImageFilePath) || !File.Exists(ImageFilePath))
            {
                return;
            }
            if (cbrFilecontents is not { Count: > 0 } || Sayfa > cbrFilecontents.Count)
            {
                return;
            }
            using ArchiveFile archiveFile = new(ImageFilePath);
            Entry entry = archiveFile?.Entries?.FirstOrDefault(z => z.FileName == cbrFilecontents[Sayfa - 1].DosyaAdı);
            string extractpath = $"{Path.GetTempPath()}{cbrFilecontents[Sayfa - 1].DosyaAdı}";
            if (!File.Exists(extractpath) || (Crc32.ComputeFile(extractpath) != entry?.CRC))
            {
                entry?.Extract(extractpath);
            }

            ILoadFileHandler loadFileHandler = null;
            switch (Path.GetExtension(extractpath.ToLowerInvariant()))
            {
                case ".webp":
                    loadFileHandler = new WebpFileHandler();
                    break;
                case ".jb2":
                    loadFileHandler = new Jb2FileHandler();
                    break;
                case ".j2k":
                    loadFileHandler = new J2kFileHandler();
                    break;
            }
            if (!loadFileHandler.IsValidFile(extractpath))
            {
                return;
            }
            BitmapFrame bitmapFrame = await loadFileHandler.LoadImageAsync(extractpath);
            bitmapFrame.Freeze();
            Source = bitmapFrame;
            if (Resize?.CanExecute(null) == true)
            {
                Resize.Execute(null);
            }
        }
    }
}
