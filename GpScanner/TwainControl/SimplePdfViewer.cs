using System;
using System.Windows;
using System.Windows.Input;

namespace TwainControl;

public class SimplePdfViewer : PdfViewer.PdfViewer
{
    private Window maximizePdfWindow;
    private PdfImportViewerControl pdfImportViewerControl;

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        if (pdfImportViewerControl == null)
        {
            pdfImportViewerControl = new PdfImportViewerControl { DataContext = Tag };
            string pdffilepath = (string)DataContext;
            pdfImportViewerControl.PdfViewer.PdfFilePath = pdffilepath;
            pdfImportViewerControl.PdfViewer.AddToHistoryList(pdffilepath);
        }

        if (maximizePdfWindow == null)
        {
            maximizePdfWindow = new Window
            {
                WindowState = WindowState.Maximized,
                ShowInTaskbar = true,
                Title = Application.Current?.MainWindow?.Title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            maximizePdfWindow.Closed += MaximizePdfWindow_Closed;
        }

        maximizePdfWindow.Content = pdfImportViewerControl;
        _ = maximizePdfWindow.ShowDialog();

        base.OnMouseDoubleClick(e);
    }

    private void MaximizePdfWindow_Closed(object sender, EventArgs e)
    {
        pdfImportViewerControl?.PdfViewer?.Dispose();
        maximizePdfWindow.Closed -= MaximizePdfWindow_Closed;
        maximizePdfWindow = null;
        pdfImportViewerControl = null;
    }
}