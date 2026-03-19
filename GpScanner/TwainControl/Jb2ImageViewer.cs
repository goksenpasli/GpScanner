using Extensions;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl;

public class Jb2ImageViewer : ImageViewer
{
    public Jb2ImageViewer()
    {
        PropertyChanged += Jb2ImageViewer_PropertyChanged;
        ViewerNext = new RelayCommand<object>(parameter => Sayfa++, parameter => IsJb2File(ImageFilePath) && Sayfa < Pages?.Count());
        ViewerBack = new RelayCommand<object>(parameter => Sayfa--, parameter => IsJb2File(ImageFilePath) && Sayfa > 1 && Sayfa <= Pages?.Count());
    }

    public override RelayCommand<object> ViewerBack { get; set; }

    public override RelayCommand<object> ViewerNext { get; set; }

    public static bool IsJb2File(string filepath) => string.Equals(Path.GetExtension(filepath), ".jb2", StringComparison.InvariantCultureIgnoreCase);

    protected override async Task LoadImageAsync(string filepath, ImageViewer imageViewer)
    {
        if (filepath is not null && File.Exists(filepath))
        {
            switch (Path.GetExtension(filepath).ToLowerInvariant())
            {
                case ".jb2":
                    imageViewer.Sayfa = 1;
                    Jb2FileHandler jb2FileHandler = new();
                    int pageCount = jb2FileHandler.GetPageCount(filepath);
                    imageViewer.TifNavigasyonButtonEtkin = Visibility.Visible;
                    imageViewer.Pages = Enumerable.Range(1, pageCount);
                    OnPropertyChanged(nameof(Sayfa));
                    return;
            }
        }
    }

    private void Jb2ImageViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Sayfa")
        {
            if (!IsJb2File(ImageFilePath) || !File.Exists(ImageFilePath))
            {
                return;
            }
            try
            {
                PBoxJBig2 pBoxJBig2 = new(File.ReadAllBytes(ImageFilePath), null);
                using Image image = pBoxJBig2.decodeImage(Sayfa);
                BitmapImage bitmapimage = image.ToBitmapImage(ImageFormat.Tiff);
                BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(bitmapimage.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(bitmapimage);
                bitmapFrame.Freeze();
                Source = bitmapFrame;
                if (Resize?.CanExecute(null) == true)
                {
                    Resize.Execute(null);
                }
            }
            catch
            {
                Source = null;
            }
        }
    }
}