using Extensions;
using GpScanner.ViewModel;
using System;
using System.Windows;
using System.Windows.Input;
using Viewer = PdfViewer.PdfViewer;

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
                        if (Cnt.GetFirstVisualChild<Viewer>() is Viewer pdfvwr)
                        {
                            pdfvwr.PdfFilePath = null;
                            pdfvwr.Source = null;
                        }
                    };
    }

    private void Cnt_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }
        if (Cnt.GetFirstVisualChild<Viewer>() is Viewer pdfvwr)
        {
            if (e.Delta > 0)
            {
                pdfvwr.ZoomIncrease?.Execute(null);
            }
            else
            {
                pdfvwr.ZoomDecrease?.Execute(null);
            }
            return;
        }
        if (Cnt.GetFirstVisualChild<ImageViewer>() is ImageViewer ImgViewer)
        {
            ImgViewer.Zoom = e.Delta > 0 ? ImgViewer.Zoom + .05 : ImgViewer.Zoom + -.05;
            ImgViewer.Zoom = Math.Max(ImgViewer.MinZoom, Math.Min(ImgViewer.MaxZoom, ImgViewer.Zoom));
        }
    }
}