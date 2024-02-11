using Extensions;
using System;
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
            }
        }
    }

    public BitmapSource ResimThumb
    {
        get => resimThumb;

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
            ResimThumb = await Resim.ResizeAsync(Settings.Default.DefaultThumbPictureResizeRatio / 100d);
        }
    }
}