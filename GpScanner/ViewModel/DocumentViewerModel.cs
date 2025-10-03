using Extensions;
using GpScanner.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private string title;

    public DocumentViewerModel(IScannerService scannerService, IFileService fileService)
    {
        PropertyChanged += DocumentViewerModel_PropertyChanged;
        List<string> files = fileService.GetFileNames();
        files?.Sort(new StrCmpLogicalComparer());
        DirectoryAllPdfFiles = [ .. files ];
        Index = Array.IndexOf([ .. DirectoryAllPdfFiles ], FilePath);
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
                    BitmapFrame bitmapFrame = ms.GenerateBitmapFrameFromMemoryStream();
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, FilePath = FilePath, Resim = bitmapFrame };
                    scannerService.GetScanner()?.Resimler?.Add(scannedImage);
                    bitmapFrame = null;
                    scannedImage = null;
                }
            },
            parameter => true);
    }

    public ICommand AddFileToControlPanel { get; }

    public ICommand Back { get; }

    public ObservableCollection<string> DirectoryAllPdfFiles
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DirectoryAllPdfFiles));
            }
        }
    }

    public string FilePath
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FilePath));
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public ICommand Forward { get; }

    public int Index
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }

    public string PdfFileContent
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfFileContent));
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
                () =>
                {
                    using AppDbContext context = new();
                    return string.Join(" ", context?.Data?.AsNoTracking()?.Where(z => z.FileName == FilePath)?.Select(z => z.FileContent));
                });

            if (!string.IsNullOrWhiteSpace(PdfFileContent))
            {
                Settings.Default.DocumentViewerPanelIsExpanded = true;
            }
        }
    }
}