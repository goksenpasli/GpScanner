using Extensions;
using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using TwainControl.Properties;

namespace TwainControl;

/// <summary>
/// Interaction logic for PdfImportViewerControl.xaml
/// </summary>
public class EypPdfViewer : PdfViewer.PdfViewer
{
    public static readonly DependencyProperty EypFilePathProperty = DependencyProperty.Register("EypFilePath", typeof(string), typeof(EypPdfViewer), new PropertyMetadata(null, Changed));

    public EypPdfViewer()
    {
        DosyaAç = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Doküman (*.pdf;*.eyp)|*.pdf;*.eyp" };
                openFileDialog.Multiselect = false;
                if (openFileDialog.ShowDialog() == true)
                {
                    if (Path.GetExtension(openFileDialog.FileName.ToLower()) == ".eyp")
                    {
                        string eypfile = ExtractEypFilesToPdf(openFileDialog.FileName);
                        if (PdfReader.TestPdfFile(eypfile) == 0)
                        {
                            return;
                        }

                        PdfFilePath = eypfile;
                        AddToHistoryList(PdfFilePath);
                    }

                    if (Path.GetExtension(openFileDialog.FileName.ToLower()) == ".pdf")
                    {
                        if (PdfReader.TestPdfFile(openFileDialog.FileName) == 0)
                        {
                            return;
                        }

                        PdfFilePath = openFileDialog.FileName;
                        AddToHistoryList(PdfFilePath);
                    }
                }
            });
    }

    public new RelayCommand<object> DosyaAç { get; }

    public string EypFilePath { get => (string)GetValue(EypFilePathProperty); set => SetValue(EypFilePathProperty, value); }

    public void AddToHistoryList(string pdffilepath)
    {
        if (!Settings.Default.PdfLoadHistory.Contains(PdfFilePath))
        {
            _ = Settings.Default.PdfLoadHistory.Add(pdffilepath);
            Settings.Default.Save();
            Settings.Default.Reload();
        }
    }

    public string ExtractEypFilesToPdf(string filename)
    {
        using PdfDocument document = TwainCtrl.EypFileExtract(filename).Where(z => Path.GetExtension(z.ToLower()) == ".pdf").ToArray().MergePdf();
        string source = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
        document.Save(source);
        return source;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Scanner)) is Scanner droppedData && IsValidPdfFile(droppedData.FileName))
        {
            PdfFilePath = droppedData.FileName;
            AddToHistoryList(PdfFilePath);

            return;
        }

        if ((e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
        {
            if (string.Equals(Path.GetExtension(droppedfiles[0]), ".eyp", StringComparison.OrdinalIgnoreCase))
            {
                PdfFilePath = ExtractEypFilesToPdf(droppedfiles[0]);
                AddToHistoryList(PdfFilePath);

                return;
            }
            if (IsValidPdfFile(droppedfiles[0]))
            {
                PdfFilePath = droppedfiles[0];
                AddToHistoryList(PdfFilePath);
            }
        }
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EypPdfViewer eypPdfViewer && e.NewValue is not null)
        {
            string eypfile = eypPdfViewer.ExtractEypFilesToPdf((string)e.NewValue);
            if (PdfReader.TestPdfFile(eypfile) != 0)
            {
                eypPdfViewer.PdfFilePath = eypfile;
            }
        }
    }
}