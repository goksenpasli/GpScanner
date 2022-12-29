using System;
using System.IO;
using System.Linq;
using System.Windows;
using GpScanner.ViewModel;

namespace GpScanner
{
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

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (DataContext is DocumentViewerModel documentViewerModel)
            {
                documentViewerModel.DirectoryAllPdfFiles = Directory.EnumerateFiles(Path.GetDirectoryName(documentViewerModel.PdfFilePath), "*.pdf");
                if (documentViewerModel.DirectoryAllPdfFiles?.Count() > 0)
                {
                    documentViewerModel.Index = Array.IndexOf(documentViewerModel.DirectoryAllPdfFiles.ToArray(), documentViewerModel.PdfFilePath);
                }
            }
        }
    }
}