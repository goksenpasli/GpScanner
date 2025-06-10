using Extensions;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

    public EypPdfViewer()
    {
        DosyaAç = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Multiselect = false, Filter = "Doküman (*.pdf;*.eyp)|*.pdf;*.eyp" };
                openFileDialog.Multiselect = false;
                if (openFileDialog?.ShowDialog() == true)
                {
                    if (Path.GetExtension(openFileDialog.FileName.ToLowerInvariant()) == ".eyp")
                    {
                        string eypfile = ExtractEypFilesToPdf(openFileDialog.FileName);
                        if (!IsValidPdfFile(eypfile))
                        {
                            return;
                        }

                        PdfFilePath = eypfile;
                    }

                    if (Path.GetExtension(openFileDialog.FileName.ToLowerInvariant()) == ".pdf")
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
            parameter =>
            {
                if (parameter is int sayfa && DataContext is TwainCtrl twainCtrl)
                {
                    string path = PdfFilePath;
                    using PdfDocument inputDocument = PdfReader.Open(PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                    if (inputDocument is not null)
                    {
                        twainCtrl.PdfToolBarControlIsEnabled = false;
                        TwainCtrl.SavePageRotated(path, inputDocument, Keyboard.Modifiers == ModifierKeys.Alt ? -90 : 90, sayfa - 1);
                        twainCtrl.PdfToolBarControlIsEnabled = true;
                        PdfFilePath = null;
                        PdfFilePath = path;
                    }
                }
            },
            parameter => true);

        AddAllFileToControlPanel = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int sayfa && DataContext is TwainCtrl twainCtrl)
                {
                    byte[] filedata = await ReadAllFileAsync(PdfFilePath);
                    using MemoryStream ms = await ConvertToImgStreamAsync(filedata, sayfa, Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = ms.GenerateBitmapFrameFromMemoryStream();
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    twainCtrl?.Scanner?.Resimler?.Add(scannedImage);
                    filedata = null;
                }
            },
            parameter => true);

        InvertSelectedPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int currentpage &&
                DataContext is TwainCtrl twainCtrl &&
                MessageBox.Show($"{Translation.GetResStringValue("INVERTCOLOR")}", TwainCtrl.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string oldpdfpath = PdfFilePath;
                    BitmapImage bitmapImage = await ConvertToImgAsync(PdfFilePath, currentpage, Dpi);
                    BitmapImage image = bitmapImage.InvertBitmap().ToBitmapImage();
                    using PdfDocument pdfdocument = TwainCtrl.RenderPdfPage(this, image);
                    twainCtrl.PdfToolBarControlIsEnabled = false;
                    pdfdocument.Save(PdfFilePath);
                    twainCtrl.PdfToolBarControlIsEnabled = true;
                    image = null;
                    bitmapImage = null;
                    PdfFilePath = null;
                    PdfFilePath = oldpdfpath;
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
                    filedata = null;
                    using Image image = Image.FromStream(ms);
                    Clipboard.SetImage(image.ToBitmapImage(ImageFormat.Jpeg));
                    ExtendedMessageBox extendedMessageBox = new();
                    Window window = Window.GetWindow(this);
                    extendedMessageBox.ShowDialog(window, Translation.GetResStringValue("COPYCLIPBOARD"), window.Title);
                }
            },
            parameter => true);

        FlipPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is int currentpage && DataContext is TwainCtrl twainCtrl)
                {
                    string oldpdfpath = PdfFilePath;
                    using PdfDocument document = PdfReader.Open(PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                    if (document is not null)
                    {
                        PdfPage page = document.Pages?[currentpage - 1];
                        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Replace);
                        XPoint center = new(page.Width / 2, page.Height / 2);
                        gfx?.ScaleAtTransform(Keyboard.Modifiers == ModifierKeys.Alt ? 1 : -1, Keyboard.Modifiers == ModifierKeys.Alt ? -1 : 1, center);
                        BitmapImage bitmapImage = await ConvertToImgAsync(PdfFilePath, currentpage);
                        XImage image = XImage.FromBitmapSource(bitmapImage);
                        gfx?.DrawImage(image, 0, 0);
                        twainCtrl.PdfToolBarControlIsEnabled = false;
                        document.Save(PdfFilePath);
                        twainCtrl.PdfToolBarControlIsEnabled = true;
                        image = null;
                        bitmapImage = null;
                        PdfFilePath = null;
                        PdfFilePath = oldpdfpath;
                    }
                }
            },
            parameter => true);
    }

    public RelayCommand<object> AddAllFileToControlPanel { get; }

    public RelayCommand<object> CopyPdfBitmapFile { get; }

    public new RelayCommand<object> DosyaAç { get; }

    public ObservableCollection<string> EypAttachments
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(EypAttachments));
            }
        }
    }

    public string EypFilePath { get => (string)GetValue(EypFilePathProperty); set => SetValue(EypFilePathProperty, value); }

    public ObservableCollection<string> EypNonSuportedAttachments
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(EypNonSuportedAttachments));
            }
        }
    }

    public RelayCommand<object> FlipPdfPage { get; }

    public RelayCommand<object> InvertSelectedPage { get; }

    public RelayCommand<object> RotateSelectedPage { get; }

    public void AddToHistoryList(string pdffilepath)
    {
        if (Settings.Default?.PdfLoadHistory?.Contains(PdfFilePath) == false)
        {
            if (Settings.Default.PdfLoadHistory.Count >= Settings.Default.PdfLoadHistoryCount)
            {
                Settings.Default.PdfLoadHistory.RemoveAt(Settings.Default.PdfLoadHistory.Count - 1);
            }
            Settings.Default?.PdfLoadHistory?.Insert(0, pdffilepath);
            Settings.Default.Save();
            Settings.Default.Reload();
        }
    }

    public string ExtractEypFilesToPdf(string filename)
    {
        List<string> files = TwainCtrl.EypFileExtract(filename);
        if (files is not null)
        {
            string[] eypcontentfilesextension = [".pdf", ".eyp", ".tiff", ".tif", ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg", ".xls", ".xlsx", ".7z", ".arj", ".bzip2", ".cab", ".gzip", ".iso", ".lzh", ".lzma", ".ntfs", ".ppmd", ".rar", ".rar5", ".rpm", ".tar", ".vhd", ".wim", ".xar", ".xz", ".z", ".zip"];
            EypAttachments = [.. files?.Where(z => eypcontentfilesextension.Contains(Path.GetExtension(z)?.ToLowerInvariant()))];
            EypNonSuportedAttachments = [.. files?.Where(z => !eypcontentfilesextension.Contains(Path.GetExtension(z)?.ToLowerInvariant()))];
            using PdfDocument document = PdfReader.Open(files?.First(z => Path.GetExtension(z.ToLowerInvariant()) == ".pdf"), PdfDocumentOpenMode.Import, PdfGeneration.PasswordProvider);
            return document?.FullPath;
        }
        return null;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        if (OpenButtonVisibility is Visibility.Hidden or Visibility.Collapsed)
        {
            e.Handled = true;
            return;
        }

        if (e?.Data?.GetData(typeof(Scanner)) is Scanner droppedData && IsValidPdfFile(droppedData.FileName))
        {
            PdfFilePath = droppedData.FileName;
            AddToHistoryList(PdfFilePath);

            return;
        }

        if ((e?.Data?.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
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

        if (e?.Data?.GetData(typeof(ScannedImage)) is ScannedImage scannedImage && DataContext is TwainCtrl twainCtrl)
        {
            string currentfile = PdfFilePath;
            string temppdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
            using PdfDocument pdffile = scannedImage?.Resim?.GeneratePdf(null, ExtensionMethods.Format.Jpg, twainCtrl.SelectedPaper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution);
            pdffile?.Save(temppdf);
            if (!IsValidPdfFile(temppdf))
            {
                return;
            }
            string[] files = Keyboard.Modifiers == ModifierKeys.Alt ? [temppdf, currentfile] : [currentfile, temppdf];
            files.MergePdf().Save(currentfile);
            PdfFilePath = null;
            PdfFilePath = currentfile;
            if (File.Exists(temppdf))
            {
                File.Delete(temppdf);
            }
        }
    }

    protected override void OnPreviewDragOver(DragEventArgs e)
    {
        if (e?.Data?.GetData(typeof(ScannedImage)) is not ScannedImage || !IsValidPdfFile(PdfFilePath))
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;
        }
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EypPdfViewer eypPdfViewer && e.NewValue is not null)
        {
            try
            {
                string eypfile = eypPdfViewer.ExtractEypFilesToPdf((string)e.NewValue);
                if (IsValidPdfFile(eypfile))
                {
                    eypPdfViewer.PdfFilePath = eypfile;
                }
            }
            catch (Exception)
            {
            }
        }
    }
}