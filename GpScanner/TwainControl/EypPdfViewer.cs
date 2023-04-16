using System;
using System.IO;
using System.Linq;
using Extensions;
using Microsoft.Win32;
using PdfSharp.Pdf;

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
                        using PdfDocument document = TwainCtrl.EypFileExtract(openFileDialog.FileName).Where(z => Path.GetExtension(z.ToLower()) == ".pdf").ToArray().MergePdf();
                        string source = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                        document.Save(source);
                        PdfFilePath = source;
                        return;
                    }
                    PdfFilePath = openFileDialog.FileName;
                }
            });
        }

        public new RelayCommand<object> DosyaAç { get; }
    }
}