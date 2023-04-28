using System.Windows;
using System.Windows.Input;

namespace TwainControl
{
    public class SimplePdfViewer : PdfViewer.PdfViewer
    {
        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            PdfImportViewerControl pdfImportViewerControl = new();
            pdfImportViewerControl.PdfViewer.PdfFilePath = (string)DataContext;
            Window maximizePdfWindow = new()
            {
                Content = pdfImportViewerControl,
                WindowState = WindowState.Maximized,
                ShowInTaskbar = true,
                Title = "GPSCANNER",
                DataContext = Tag,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            _ = maximizePdfWindow.ShowDialog();
            maximizePdfWindow.Closed += (s, e) =>
            {
                pdfImportViewerControl?.PdfViewer?.Dispose();
                maximizePdfWindow = null;
            };
            base.OnMouseDoubleClick(e);
        }
    }
}