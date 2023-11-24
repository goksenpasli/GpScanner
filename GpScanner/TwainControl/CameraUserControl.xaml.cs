using CatenaLogic.Windows.Presentation.WebcamPlayer;
using Extensions;
using Extensions.Controls;
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
    private bool detectQRCode;
    private CapDevice device;
    private FilterInfo[] liste = CapDevice.DeviceMonikers;
    private byte[] resimData;
    private double rotation = 180;
    private FilterInfo seçiliKamera;

    public CameraUserControl()
    {
        InitializeComponent();
        DataContext = this;
        Unloaded += CameraUserControl_Unloaded;
        KameradanResimYükle = new RelayCommand<object>(parameter => ResimData = CameraEncodeBitmapImage().ToArray(), parameter => SeçiliKamera is not null);

        VideodanResimYükle = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is MediaViewer mediaViewer && mediaViewer.FindName("grid") is Grid grid)
                {
                    ResimData = grid.ToRenderTargetBitmap().ToTiffJpegByteArray(ExtensionMethods.Format.Jpg);
                }
            },
            parameter => parameter is MediaViewer mediaViewer && !string.IsNullOrWhiteSpace(mediaViewer.MediaDataFilePath));

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

        PropertyChanged += CameraUserControl_PropertyChanged;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public bool DetectQRCode
    {
        get => detectQRCode;

        set
        {
            if (detectQRCode != value)
            {
                detectQRCode = value;
                OnPropertyChanged(nameof(DetectQRCode));
            }
        }
    }

    public CapDevice Device
    {
        get => device;

        set
        {
            if (device != value)
            {
                device = value;
                OnPropertyChanged(nameof(Device));
            }
        }
    }

    public ICommand Durdur { get; }

    public ICommand KameradanResimYükle { get; }

    public ICommand Kaydet { get; }

    public FilterInfo[] Liste
    {
        get => liste;

        set
        {
            if (liste != value)
            {
                liste = value;
                OnPropertyChanged(nameof(Liste));
            }
        }
    }

    public ICommand Oynat { get; }

    public byte[] ResimData
    {
        get => resimData;

        set
        {
            if (resimData != value)
            {
                resimData = value;
                OnPropertyChanged(nameof(ResimData));
            }
        }
    }

    public double Rotation
    {
        get => rotation;

        set
        {
            if (rotation != value)
            {
                rotation = value;
                OnPropertyChanged(nameof(Rotation));
            }
        }
    }

    public FilterInfo SeçiliKamera
    {
        get => seçiliKamera;

        set
        {
            if (seçiliKamera != value)
            {
                seçiliKamera = value;
                OnPropertyChanged(nameof(SeçiliKamera));
            }
        }
    }

    public RelayCommand<object> VideodanResimYükle { get; }

    public MemoryStream CameraEncodeBitmapImage()
    {
        using MemoryStream stream = new();
        JpegBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(Device.BitmapSource, new RotateTransform(Rotation))));
        encoder.QualityLevel = 90;
        encoder.Save(stream);
        return stream;
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void CameraUserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SeçiliKamera")
        {
            Device = new CapDevice(SeçiliKamera.MonikerString) { MaxHeightInPixels = 1080 };
        }
    }

    private void CameraUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        Device?.Stop();
        DetectQRCode = false;
    }
}