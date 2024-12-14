using Extensions;
using GpScanner.ViewModel;
using PdfCompressor;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TwainControl;

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
            DeselectAllFile = new RelayCommand<object>(
                parameter =>
                {
                    foreach (BatchPdfData item in Compressor.BatchPdfList)
                    {
                        item.IsChecked = false;
                    }
                },
                parameter => Compressor?.BatchPdfList?.Count > 0);
            SelectAllFile = new RelayCommand<object>(
                parameter =>
                {
                    foreach (BatchPdfData item in Compressor.BatchPdfList)
                    {
                        item.IsChecked = true;
                    }
                },
                parameter => Compressor?.BatchPdfList?.Count > 0);
            InverseSelectFile = new RelayCommand<object>(
                parameter =>
                {
                    foreach (BatchPdfData item in Compressor.BatchPdfList)
                    {
                        item.IsChecked = !item.IsChecked;
                    }
                },
                parameter => Compressor?.BatchPdfList?.Count > 0);
        }

        public RelayCommand<object> DeselectAllFile { get; }

        public RelayCommand<object> InverseSelectFile { get; }

        public double ProgressValue => (double)GetValue(ProgressValueProperty.DependencyProperty);

        public RelayCommand<object> SelectAllFile { get; }

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
                foreach (BatchPdfData item in Compressor.BatchPdfList.Where(z => z.IsChecked))
                {
                    FileInfo fi = new(item.Filename);
                    Scanner scanner = new()
                    {
                        FileName = Path.Combine(Path.GetDirectoryName(item.Filename), $"{Path.GetFileNameWithoutExtension(item.Filename)}_Compressed.pdf"),
                        FolderName = fi?.Directory?.Name,
                        FileSize = (float)(fi.Length / 1048576F * item.CompressionRatio / 100)
                    };
                    gpScannerViewModel.Dosyalar?.Add(scanner);
                }
            }
        }

        private void Compressor_ProgressChanged(object sender, double e) => Dispatcher.BeginInvoke(() => SetValue(ProgressValueProperty, e));
    }
}
