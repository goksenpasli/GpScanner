using System;
using System.IO;
using System.Linq;
using System.Windows;
using Extensions;
using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for PdfImportViewerControl.xaml
    /// </summary>
    ///
    public class EypPdfViewer : PdfViewer.PdfViewer
    {
        public EypPdfViewer()
        {
            DosyaAç = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Doküman (*.pdf;*.eyp)|*.pdf;*.eyp" };
                openFileDialog.Multiselect = false;
                if (openFileDialog.ShowDialog() == true)
                {
                    if (Path.GetExtension(openFileDialog.FileName.ToLower()) == ".eyp")
                    {
                        string eyppath = ExtractEypFilesToPdf(openFileDialog.FileName);
                        if (PdfReader.TestPdfFile(eyppath) != 0)
                        {
                            PdfFilePath = eyppath;
                        }
                        return;
                    }
                    if (PdfReader.TestPdfFile(openFileDialog.FileName) != 0)
                    {
                        PdfFilePath = openFileDialog.FileName;
                    }
                }
            });
        }

        public new RelayCommand<object> DosyaAç { get; }

        public string ExtractEypFilesToPdf(string filename)
        {
            using PdfDocument document = TwainCtrl.EypFileExtract(filename).Where(z => Path.GetExtension(z.ToLower()) == ".pdf").ToArray().MergePdf();
            string source = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
            document.Save(source);
            return source;
        }

        protected override void OnDrop(DragEventArgs e)
        {
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                if (Path.GetExtension(droppedfiles[0]) == ".eyp")
                {
                    string eyppath = ExtractEypFilesToPdf(droppedfiles[0]);
                    if (PdfReader.TestPdfFile(eyppath) != 0)
                    {
                        PdfFilePath = eyppath;
                    }
                    return;
                }
                if (PdfReader.TestPdfFile(droppedfiles[0]) != 0)
                {
                    PdfFilePath = droppedfiles[0];
                }
            }
        }
    }
}