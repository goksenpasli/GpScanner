using Extensions;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl;

/// <summary>
/// Interaction logic for PdfImportViewerControl.xaml
/// </summary>
public class EypPdfViewer : PdfViewer.PdfViewer
{
    public static readonly DependencyProperty EypFilePathProperty = DependencyProperty.Register("EypFilePath", typeof(string), typeof(EypPdfViewer), new PropertyMetadata(null, Changed));
    private readonly string[] eypcontentfilesextension = [".pdf", ".eyp", ".tıff", ".tıf", ".tiff", ".tif", ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg", ".xls", ".xlsx", ".7z", ".arj", ".bzip2", ".cab", ".gzip", ".iso", ".lzh", ".lzma", ".ntfs", ".ppmd", ".rar", ".rar5", ".rpm", ".tar", ".vhd", ".wim", ".xar", ".xz", ".z", ".zip"];
    private ObservableCollection<string> eypAttachments;
    private ObservableCollection<string> eypNonSuportedAttachments;

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
                        if (!IsValidPdfFile(eypfile))
                        {
                            return;
                        }

                        PdfFilePath = eypfile;
                    }

                    if (Path.GetExtension(openFileDialog.FileName.ToLower()) == ".pdf")
                    {
                        if (!IsValidPdfFile(openFileDialog.FileName))
                        {
                            return;
                        }

                        PdfFilePath = openFileDialog.FileName;
                        AddToHistoryList(PdfFilePath);
                    }
                }
            });

        RotateSelectedPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int sayfa)
                {
                    string path = PdfFilePath;
                    using PdfDocument inputDocument = PdfReader.Open(PdfFilePath, PdfDocumentOpenMode.Import);
                    TwainCtrl.SavePageRotated(path, inputDocument, Keyboard.Modifiers == ModifierKeys.Alt ? -90 : 90, sayfa - 1);
                    await Task.Delay(1000);
                    PdfFilePath = null;
                    PdfFilePath = path;
                }
            },
            parameter => true);

        RemoveSelectedPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int sayfa)
                {
                    string path = PdfFilePath;
                    await TwainCtrl.RemovePdfPageAsync(path, sayfa, sayfa);
                    await Task.Delay(1000);
                    PdfFilePath = null;
                    PdfFilePath = path;
                }
            },
            parameter => ToplamSayfa > 1);

        AddAllFileToControlPanel = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int sayfa && DataContext is TwainCtrl twainCtrl)
                {
                    byte[] filedata = await ReadAllFileAsync(PdfFilePath);
                    MemoryStream ms = await ConvertToImgStreamAsync(filedata, sayfa, Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(ms);
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    twainCtrl?.Scanner?.Resimler.Add(scannedImage);
                    ms = null;
                }
            },
            parameter => true);

        CopyPdfBitmapFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int sayfa)
                {
                    byte[] filedata = await ReadAllFileAsync(PdfFilePath);
                    using MemoryStream ms = await ConvertToImgStreamAsync(filedata, sayfa, Settings.Default.ImgLoadResolution);
                    using Image image = Image.FromStream(ms);
                    System.Windows.Forms.Clipboard.SetImage(image);
                }
            },
            parameter => true);

        FlipPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int currentpage)
                {
                    string oldpdfpath = PdfFilePath;
                    using PdfDocument document = PdfReader.Open(PdfFilePath, PdfDocumentOpenMode.Modify);
                    PdfPage page = document.Pages[currentpage - 1];
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Replace);
                    XPoint center = new(page.Width / 2, page.Height / 2);
                    gfx.ScaleAtTransform(Keyboard.Modifiers == ModifierKeys.Alt ? 1 : -1, Keyboard.Modifiers == ModifierKeys.Alt ? -1 : 1, center);
                    BitmapImage bitmapImage = await ConvertToImgAsync(PdfFilePath, currentpage);
                    XImage image = XImage.FromBitmapSource(bitmapImage);
                    gfx.DrawImage(image, 0, 0);
                    document.Save(PdfFilePath);
                    image = null;
                    bitmapImage = null;
                    await Task.Delay(1000);
                    PdfFilePath = null;
                    PdfFilePath = oldpdfpath;
                    Sayfa = currentpage;
                }
            },
            parameter => true);
    }

    public RelayCommand<object> AddAllFileToControlPanel { get; }

    public RelayCommand<object> CopyPdfBitmapFile { get; }

    public new RelayCommand<object> DosyaAç { get; }

    public ObservableCollection<string> EypAttachments
    {
        get => eypAttachments;
        set
        {
            if (eypAttachments != value)
            {
                eypAttachments = value;
                OnPropertyChanged(nameof(EypAttachments));
            }
        }
    }

    public ObservableCollection<string> EypNonSuportedAttachments { get => eypNonSuportedAttachments;
        set {
            if (eypNonSuportedAttachments != value)
            {
                eypNonSuportedAttachments = value;
                OnPropertyChanged(nameof(EypNonSuportedAttachments));
            }
        }
    }
    public string EypFilePath { get => (string)GetValue(EypFilePathProperty); set => SetValue(EypFilePathProperty, value); }

    public RelayCommand<object> FlipPdfPage { get; }

    public RelayCommand<object> RemoveSelectedPage { get; }

    public RelayCommand<object> RotateSelectedPage { get; }

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
        List<string> files = TwainCtrl.EypFileExtract(filename);
        EypAttachments = new ObservableCollection<string>(files?.Where(z => eypcontentfilesextension.Contains(Path.GetExtension(z).ToLower())));
        EypNonSuportedAttachments = new ObservableCollection<string>(files?.Where(z => !eypcontentfilesextension.Contains(Path.GetExtension(z).ToLower())));
        using PdfDocument document = PdfReader.Open(files?.First(z => Path.GetExtension(z.ToLower()) == ".pdf"), PdfDocumentOpenMode.Import);
        return document?.FullPath;
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
            if (IsValidPdfFile(eypfile))
            {
                eypPdfViewer.PdfFilePath = eypfile;
            }
        }
    }
}