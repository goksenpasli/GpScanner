using System.Windows;
using System.Windows.Input;

namespace TwainControl;

public class SimplePdfViewer : PdfViewer.PdfViewer
{
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        PdfImportViewerControl pdfImportViewerControl = new();
        string pdffilepath = (string)DataContext;
        pdfImportViewerControl.PdfViewer.PdfFilePath = pdffilepath;
        pdfImportViewerControl.PdfViewer.AddToHistoryList(pdffilepath);
        Window maximizePdfWindow = new()
        {
            Content = pdfImportViewerControl,
            WindowState = WindowState.Maximized,
            ShowInTaskbar = true,
            Title = Application.Current?.MainWindow?.Title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
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