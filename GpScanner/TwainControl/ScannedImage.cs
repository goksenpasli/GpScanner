using Extensions;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl;

public class ScannedImage : InpcBase
{
    private bool animate;
    private string filePath;
    private double flipAngle;
    private int ındex;
    private BitmapFrame resim;
    private BitmapSource resimThumb;
    private double rotationAngle;
    private bool seçili;

    public ScannedImage() { PropertyChanged += ScannedImage_PropertyChangedAsync; }

    public bool Animate
    {
        get => animate;

        set
        {
            if (animate != value)
            {
                animate = value;
                OnPropertyChanged(nameof(Animate));
            }
        }
    }

    public string FilePath
    {
        get => filePath;

        set
        {
            if (filePath != value)
            {
                filePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }
    }

    public double FlipAngle
    {
        get => flipAngle;
        set
        {
            if (flipAngle != value)
            {
                flipAngle = value;
                OnPropertyChanged(nameof(FlipAngle));
            }
        }
    }

    public int Index
    {
        get => ındex;

        set
        {
            if (ındex == value)
            {
                return;
            }

            ındex = value;
            OnPropertyChanged(nameof(Index));
        }
    }

    public BitmapFrame Resim
    {
        get => resim;

        set
        {
            if (resim != value)
            {
                resim = value;
                OnPropertyChanged(nameof(Resim));
                OnPropertyChanged(nameof(ResimThumb));
            }
        }
    }

    public BitmapSource ResimThumb
    {
        get => Resim?.Resize(Settings.Default.DefaultThumbPictureResizeRatio / 100d);

        set
        {
            if (resimThumb != value)
            {
                resimThumb = value;
                OnPropertyChanged(nameof(ResimThumb));
            }
        }
    }

    public double RotationAngle
    {
        get => rotationAngle;

        set
        {
            if (rotationAngle != value)
            {
                rotationAngle = value;
                OnPropertyChanged(nameof(RotationAngle));
            }
        }
    }

    public bool Seçili
    {
        get => seçili;

        set
        {
            if (seçili != value)
            {
                seçili = value;
                OnPropertyChanged(nameof(Seçili));
            }
        }
    }

    private async void ScannedImage_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "RotationAngle" && RotationAngle != 0)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                Resim = await Resim.FlipImageAsync(RotationAngle);
                RotationAngle = 0;
                return;
            }

            Resim = await Resim.RotateImageAsync(RotationAngle);
            RotationAngle = 0;
        }
        if (e.PropertyName is "FlipAngle" && FlipAngle != 0)
        {
            Resim = await Resim.FlipImageAsync(FlipAngle);
            FlipAngle = 0;
        }
    }
}