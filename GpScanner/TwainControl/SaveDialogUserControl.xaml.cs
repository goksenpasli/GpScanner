using Extensions;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl;

/// <summary>
/// Interaction logic for SaveDialogUserControl.xaml
/// </summary>
public partial class SaveDialogUserControl : UserControl, INotifyPropertyChanged
{
    private BitmapSource previewImage;
    private TwainCtrl twainCtrl;

    public SaveDialogUserControl() { InitializeComponent(); }

    public event PropertyChangedEventHandler PropertyChanged;

    public BitmapSource PreviewImage
    {
        get => previewImage;

        set
        {
            if (previewImage != value)
            {
                previewImage = value;
                OnPropertyChanged(nameof(PreviewImage));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "BwThreshold")
        {
            GenerateImage();
        }
    }

    private void GenerateImage() => PreviewImage =
    twainCtrl.SaveIndex == 3
    ? twainCtrl?.SeçiliResim?.Resim?.Resize(512, 512).BitmapSourceToBitmap().ConvertBlackAndWhite(Settings.Default.BwThreshold).ToBitmapImage(ImageFormat.Jpeg)
    : null;

    private void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e) => GenerateImage();

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        twainCtrl = DataContext as TwainCtrl;
        if (twainCtrl is not null)
        {
            twainCtrl.PropertyChanged += TwainCtrl_PropertyChanged;
            Settings.Default.PropertyChanged += Default_PropertyChanged;
        }
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (PreviewImage is not null)
        {
            PreviewImage = null;
        }
    }
}