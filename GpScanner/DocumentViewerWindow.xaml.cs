using Extensions;
using GpScanner.ViewModel;
using System.Windows;

namespace GpScanner;

/// <summary>
/// Interaction logic for DocumentViewerWindow.xaml
/// </summary>
public partial class DocumentViewerWindow : Window
{
    public DocumentViewerWindow()
    {
        InitializeComponent();
        IWindowService windowService = new WindowService();
        IScannerService scannerService = new ScannerService();
        IFileService fileService = new FileService();
        DataContext = new DocumentViewerModel(scannerService, fileService);
        Owner = windowService.GetFirstWindow();
        Unloaded += (sender, e) =>
                    {
                        if (cnt.GetFirstVisualChild<PdfViewer.PdfViewer>() is PdfViewer.PdfViewer pdfvwr)
                        {
                            pdfvwr.PdfFilePath = null;
                            pdfvwr.Source = null;
                        }
                    };
    }
}