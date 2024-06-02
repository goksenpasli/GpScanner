using GpScanner.ViewModel;
using PdfCompressor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for PdfCompressorControl.xaml
    /// </summary>
    public partial class PdfCompressorControl : UserControl
    {
        public static readonly DependencyPropertyKey ProgressValueProperty = DependencyProperty.RegisterReadOnly("ProgressValue", typeof(double), typeof(PdfCompressorControl), new PropertyMetadata(0d));

        public PdfCompressorControl()
        {
            InitializeComponent();
            Compressor.ProgressChanged += Compressor_ProgressChanged;
        }

        public double ProgressValue => (double)GetValue(ProgressValueProperty.DependencyProperty);

        private void ComboBox_CompressorListSourceUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Source is ComboBox comboBox && comboBox.DataContext is GpScannerViewModel gpScannerViewModel)
            {
                Compressor.Dpi = (int)gpScannerViewModel.SelectedCompressorProfile.Width;
                Compressor.Quality = (int)gpScannerViewModel.SelectedCompressorProfile.Height;
            }
        }

        private void CompressFinishedButton_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Source is Button button && button.IsEnabled && button.DataContext is GpScannerViewModel gpScannerViewModel)
            {
                gpScannerViewModel.ReloadFileDatas(false);
            }
        }

        private void Compressor_ProgressChanged(object sender, double e) => Dispatcher.BeginInvoke(() => SetValue(ProgressValueProperty, e));
    }
}
