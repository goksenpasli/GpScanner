using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwainControl;

namespace GpScanner.ViewModel;

public class DocumentViewerModel : InpcBase
{
    public DocumentViewerModel()
    {
        PropertyChanged += DocumentViewerModel_PropertyChangedAsync;
        Back = new RelayCommand<object>(
            parameter =>
            {
                Index--;
                PdfFilePath = DirectoryAllPdfFiles?.ElementAtOrDefault(Index);
            },
            parameter => Index > 0);

        Forward = new RelayCommand<object>(
            parameter =>
            {
                Index++;
                PdfFilePath = DirectoryAllPdfFiles?.ElementAtOrDefault(Index);
            },
            parameter => Index < DirectoryAllPdfFiles?.Count() - 1);

        AddFileToControlPanel = new RelayCommand<object>(
            async parameter =>
            {
                if(parameter is ImageSource imageSource)
                {
                    MemoryStream ms = new(imageSource.ToTiffJpegByteArray(ExtensionMethods.Format.Jpg));
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(ms, ToolBox.Paper);
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, FilePath = PdfFilePath, Resim = bitmapFrame };
                    Scanner?.Resimler.Add(scannedImage);
                    bitmapFrame = null;
                    scannedImage = null;
                    ms = null;
                    GC.Collect();
                }
            },
            parameter => true);

        SaveImageAsPdfFile = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası (*.pdf)|*.pdf", FileName = "File.pdf" };
                if(saveFileDialog.ShowDialog() == true)
                {
                    using PdfDocument document = new();
                    PdfPage page = document.AddPage();
                    XImage img = XImage.FromFile(PdfFilePath);
                    page.Width = img.PixelWidth;
                    page.Height = img.PixelHeight;
                    using XGraphics gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                    document.Save(saveFileDialog.FileName);
                }
            },
            parameter => Path.GetExtension(PdfFilePath?.ToLower()) is not ".pdf" and not ".zip" and not ".xps");
    }

    public ICommand AddFileToControlPanel { get; }

    public ICommand Back { get; }

    public IEnumerable<string> DirectoryAllPdfFiles
    {
        get => directoryAllPdfFiles;

        set
        {
            if(directoryAllPdfFiles != value)
            {
                directoryAllPdfFiles = value;
                OnPropertyChanged(nameof(DirectoryAllPdfFiles));
            }
        }
    }

    public ICommand Forward { get; }

    public byte[] ImgData
    {
        get => ımgData;

        set
        {
            if(ımgData != value)
            {
                ımgData = value;
                OnPropertyChanged(nameof(ImgData));
            }
        }
    }

    public int Index
    {
        get => ındex;

        set
        {
            if(ındex != value)
            {
                ındex = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }

    public string OcrText
    {
        get => ocrText;

        set
        {
            if(ocrText != value)
            {
                ocrText = value;
                OnPropertyChanged(nameof(OcrText));
            }
        }
    }

    public string PdfFileContent
    {
        get => string.Join(" ", GpScannerViewModel.DataYükle()?.Where(z => z.FileName == PdfFilePath).Select(z => z.FileContent));

        set
        {
            if(pdfFileContent != value)
            {
                pdfFileContent = value;
                OnPropertyChanged(nameof(PdfFileContent));
            }
        }
    }

    public string PdfFilePath
    {
        get => pdfFilePath;

        set
        {
            if(pdfFilePath != value)
            {
                pdfFilePath = value;
                OnPropertyChanged(nameof(PdfFilePath));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(PdfFileContent));
            }
        }
    }

    public ICommand SaveImageAsPdfFile { get; }

    public Scanner Scanner
    {
        get => scanner;

        set
        {
            if(scanner != value)
            {
                scanner = value;
                OnPropertyChanged(nameof(Scanner));
            }
        }
    }

    public string Title
    {
        get => Path.GetFileName(PdfFilePath);

        set
        {
            if(title != value)
            {
                title = value;
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    private async void DocumentViewerModel_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName is "ImgData" && ImgData is not null && !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang))
        {
            ObservableCollection<Ocr.OcrData> ocrtext = await Ocr.Ocr.OcrAsync(ImgData, Settings.Default.DefaultTtsLang);
            OcrText = string.Join(" ", ocrtext?.Select(z => z.Text));
        }
    }

    private IEnumerable<string> directoryAllPdfFiles;

    private byte[] ımgData;

    private int ındex;

    private string ocrText;

    private string pdfFileContent;

    private string pdfFilePath;

    private Scanner scanner;

    private string title;
}