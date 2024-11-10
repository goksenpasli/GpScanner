using Extensions;
using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl;

public class ScannedImage : InpcBase
{
    public ScannedImage() { PropertyChanged += ScannedImage_PropertyChangedAsync; }

    public bool Animate
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Animate));
            }
        }
    }

    public Brush FileGroupColor
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileGroupColor));
            }
        }
    } = Brushes.Black;

    public string FilePath
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }
    }

    public double FlipAngle
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FlipAngle));
            }
        }
    }

    public int Index
    {
        get;

        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(nameof(Index));
        }
    }

    public BitmapFrame Resim
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Resim));
            }
        }
    }

    public BitmapSource ResimThumb
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ResimThumb));
            }
        }
    }

    public double RotationAngle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(RotationAngle));
            }
        }
    }

    public SolidColorBrush ScannedImageNotifyBrush
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ScannedImageNotifyBrush));
            }
        }
    }

    public bool Seçili
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Seçili));
            }
        }
    }

    private async void ScannedImage_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "RotationAngle" && RotationAngle != 0)
        {
            Resim.Freeze();
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                BitmapImage flippedimage = await Resim.FlipImageAsync(RotationAngle);
                flippedimage?.Freeze();
                BitmapFrame bf = BitmapFrame.Create(flippedimage);
                bf.Freeze();
                Resim = bf;
                RotationAngle = 0;
                GC.Collect();
                return;
            }
            BitmapFrame bitmapframe = BitmapFrame.Create(await Resim.RotateImageAsync(RotationAngle));
            bitmapframe.Freeze();
            Resim = bitmapframe;
            RotationAngle = 0;
            GC.Collect();
        }
        if (e.PropertyName is "FlipAngle" && FlipAngle != 0)
        {
            Resim.Freeze();
            BitmapImage flippedimage = await Resim.FlipImageAsync(FlipAngle);
            flippedimage?.Freeze();
            BitmapFrame bf = BitmapFrame.Create(flippedimage);
            bf.Freeze();
            Resim = bf;
            FlipAngle = 0;
            GC.Collect();
        }
        if (e.PropertyName is "Resim" && Resim is not null)
        {
            Resim.Freeze();
            if (Settings.Default.DefaultThumbPictureAutoResize)
            {
                double resizeratio = Math.Min(256d / Resim.PixelWidth, 256d / Resim.PixelHeight);
                ResimThumb = Resim.Resize(resizeratio);
                return;
            }
            ResimThumb = Resim.Resize(Settings.Default.DefaultThumbPictureResizeRatio / 100d);
        }
    }
}