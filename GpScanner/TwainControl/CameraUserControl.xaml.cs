using CatenaLogic.Windows.Presentation.WebcamPlayer;
using Extensions;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TwainControl;

/// <summary>
/// Interaction logic for CameraUserControl.xaml
/// </summary>
public partial class CameraUserControl : UserControl, INotifyPropertyChanged
{
    public CameraUserControl()
    {
        InitializeComponent();
        DataContext = this;
        Unloaded += CameraUserControl_Unloaded;
        PropertyChanged += CameraUserControl_PropertyChanged;

        KameradanResimYükle = new RelayCommand<object>(parameter => ResimData = CameraEncodeBitmapImage().ToArray(), parameter => SeçiliKamera is not null && Device?.BitmapSource is not null);

        Durdur = new RelayCommand<object>(parameter => Device?.Stop(), parameter => SeçiliKamera is not null && Device?.IsRunning == true);

        Oynat = new RelayCommand<object>(parameter => Device?.Start(), parameter => SeçiliKamera is not null && Device?.IsRunning == false);

        Kaydet = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Jpg Dosyası (*.jpg)|*.jpg", AddExtension = true, Title = "Kaydet" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(saveFileDialog.FileName, CameraEncodeBitmapImage().ToArray());
                }
            },
            parameter => SeçiliKamera is not null && Device?.BitmapSource is not null);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public bool DetectQRCode
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DetectQRCode));
            }
        }
    }

    public CapDevice Device
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Device));
            }
        }
    }

    public ICommand Durdur { get; }

    public ICommand KameradanResimYükle { get; }

    public ICommand Kaydet { get; }

    public FilterInfo[] Liste
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Liste));
            }
        }
    } = CapDevice.DeviceMonikers;

    public ICommand Oynat { get; }

    public byte[] ResimData
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ResimData));
            }
        }
    }

    public double Rotation
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Rotation));
            }
        }
    } = 180;

    public FilterInfo SeçiliKamera
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SeçiliKamera));
            }
        }
    }

    public MemoryStream CameraEncodeBitmapImage()
    {
        using MemoryStream stream = new();
        JpegBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(Device?.BitmapSource, new RotateTransform(Rotation))));
        encoder.QualityLevel = 90;
        encoder?.Save(stream);
        return stream;
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void CameraUserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SeçiliKamera")
        {
            Device = new CapDevice(SeçiliKamera?.MonikerString) { MaxHeightInPixels = 1080 };
        }
    }

    private void CameraUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        Device?.Stop();
        DetectQRCode = false;
    }
}