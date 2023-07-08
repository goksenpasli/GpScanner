using Extensions;
using GpScanner.ViewModel;
using System;
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
        DataContext = new DocumentViewerModel();
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        if(cnt.GetFirstVisualChild<PdfViewer.PdfViewer>() is PdfViewer.PdfViewer pdfvwr)
        {
            pdfvwr.PdfFilePath = null;
            pdfvwr.Source = null;
            GC.Collect();
        }
    }
}