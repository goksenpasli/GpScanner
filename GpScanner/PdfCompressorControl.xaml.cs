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

        private void CompressFinishedButton_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Source is Button button && button.IsEnabled && button.DataContext is GpScannerViewModel gpScannerViewModel)
            {
                gpScannerViewModel.ReloadFileDatas(false);
            }
        }
    }
}
