﻿using Extensions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwainControl;

namespace GpScanner.ViewModel;

public class DocumentViewerModel : InpcBase
{
    private IEnumerable<string> directoryAllPdfFiles;
    private string filePath;
    private int ındex;
    private string ocrText;
    private string pdfFileContent;
    private Scanner scanner;
    private string title;

    public DocumentViewerModel()
    {
        PropertyChanged += DocumentViewerModel_PropertyChanged;
        Back = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    Index--;
                    FilePath = DirectoryAllPdfFiles?.ElementAtOrDefault(Index);
                    listBox.ScrollIntoView(FilePath);
                }
            },
            parameter => Index > 0);

        Forward = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    Index++;
                    FilePath = DirectoryAllPdfFiles?.ElementAtOrDefault(Index);
                    listBox.ScrollIntoView(FilePath);
                }
            },
            parameter => Index < DirectoryAllPdfFiles?.Count() - 1);

        AddFileToControlPanel = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ImageSource imageSource)
                {
                    using MemoryStream ms = new(imageSource.ToTiffJpegByteArray(ExtensionMethods.Format.Jpg));
                    BitmapFrame bitmapFrame = BitmapMethods.GenerateImageDocumentBitmapFrame(ms);
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, FilePath = FilePath, Resim = bitmapFrame };
                    Scanner?.Resimler?.Add(scannedImage);
                    bitmapFrame = null;
                    scannedImage = null;
                }
            },
            parameter => true);
    }

    public ICommand AddFileToControlPanel { get; }

    public ICommand Back { get; }

    public IEnumerable<string> DirectoryAllPdfFiles
    {
        get => directoryAllPdfFiles;

        set
        {
            if (directoryAllPdfFiles != value)
            {
                directoryAllPdfFiles = value;
                OnPropertyChanged(nameof(DirectoryAllPdfFiles));
            }
        }
    }

    public string FilePath
    {
        get => filePath;

        set
        {
            if (filePath != value)
            {
                filePath = value;
                OnPropertyChanged(nameof(FilePath));
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public ICommand Forward { get; }

    public int Index
    {
        get => ındex;

        set
        {
            if (ındex != value)
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
            if (ocrText != value)
            {
                ocrText = value;
                OnPropertyChanged(nameof(OcrText));
            }
        }
    }

    public string PdfFileContent
    {
        get => pdfFileContent;

        set
        {
            if (pdfFileContent != value)
            {
                pdfFileContent = value;
                OnPropertyChanged(nameof(PdfFileContent));
            }
        }
    }

    public Scanner Scanner
    {
        get => scanner;

        set
        {
            if (scanner != value)
            {
                scanner = value;
                OnPropertyChanged(nameof(Scanner));
            }
        }
    }

    public string Title
    {
        get => Path.GetFileName(FilePath);

        set
        {
            if (title != value)
            {
                title = value;
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    private async void DocumentViewerModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "FilePath")
        {
            PdfFileContent = await Task.Run(
                async () =>
                {
                    using AppDbContext context = new();
                    return string.Join(" ", (await context?.Data?.AsNoTracking().ToListAsync())?.Where(z => z.FileName == FilePath)?.Select(z => z.FileContent));
                });
        }
    }
}