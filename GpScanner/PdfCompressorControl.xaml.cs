using GpScanner.ViewModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace GpScanner
{
    /// <summary>
    /// Interaction logic for PdfCompressorControl.xaml
    /// </summary>
    public partial class PdfCompressorControl : UserControl
    {
        public PdfCompressorControl() { InitializeComponent(); }

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
    }
}
