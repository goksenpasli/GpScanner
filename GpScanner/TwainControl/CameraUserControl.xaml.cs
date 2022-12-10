using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatenaLogic.Windows.Presentation.WebcamPlayer;
using Extensions;
using Microsoft.Win32;

namespace TwainControl
{
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
            KameradanResimYükle = new RelayCommand<object>(parameter =>
            {
                using MemoryStream ms = new();
                EncodeBitmapImage(ms);
                ResimData = ms.ToArray();
            }, parameter => SeçiliKamera is not null);

            Durdur = new RelayCommand<object>(parameter => Device?.Stop(), parameter => SeçiliKamera is not null && Device?.IsRunning == true);

            Oynat = new RelayCommand<object>(parameter => Device?.Start(), parameter => SeçiliKamera is not null && Device?.IsRunning == false);

            Kaydet = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Jpg Dosyası (*.jpg)|*.jpg", AddExtension = true, Title = "Kaydet" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    using FileStream ms = new(saveFileDialog.FileName, FileMode.Create, FileAccess.Write);
                    EncodeBitmapImage(ms);
                }
            }, parameter => SeçiliKamera is not null);

            PropertyChanged += CameraUserControl_PropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

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

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private CapDevice device;

        private FilterInfo[] liste = CapDevice.DeviceMonikers;

        private byte[] resimData;

        private double rotation = 180;

        private FilterInfo seçiliKamera;

        private void CameraUserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeçiliKamera")
            {
                Device = new CapDevice(SeçiliKamera.MonikerString)
                {
                    MaxHeightInPixels = 1080
                };
            }
        }

        private void CameraUserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Device?.Stop();
        }

        private void EncodeBitmapImage(Stream ms)
        {
            JpegBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(Device.BitmapSource, new RotateTransform(Rotation))));
            encoder.QualityLevel = 90;
            encoder.Save(ms);
        }
    }
}