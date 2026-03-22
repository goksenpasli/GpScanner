using Extensions;
using Extensions.Controls;
using Microsoft.Win32;
using Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfViewer;
using SevenZipExtractor;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.Xml.Linq;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.TwainNative;
using TwainWpf.Wpf;
using Xceed.Words.NET;
using static Extensions.ExtensionMethods;
using static TwainControl.DrawControl;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Image = System.Drawing.Image;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = TwainWpf.TwainNative.Orientation;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;
using Viewer = PdfViewer.PdfViewer;

namespace TwainControl;

public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable, IDataErrorInfo
{
    public const double Inch = 2.54d;
    public static readonly string AppName = Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Title;
    public static DispatcherTimer CameraQrCodeTimer;
    public static Task FileSaveTask;
    private readonly object _lockObject = new();
    private readonly SolidColorBrush bluesaveprogresscolor = Brushes.DeepSkyBlue;
    private readonly Brush defaultsaveprogressforegroundcolor = (Brush)new BrushConverter().ConvertFromString("#FF06B025");
    private readonly string[] imagefileextensions = [ ".tiff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".png", ".bmp" ];
    private readonly Stack<DeletedImageEntry> invertundoStack = new();
    private readonly Rectangle selectionbox = new() { Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), StrokeThickness = 2, StrokeDashArray = [ with([ 1 ]) ] };
    private readonly Stack<DeletedImageEntry> undoStack = new();
    private int cropAllMaximumWidth;
    private bool disposedValue;
    private GridLength documentGridLength = new(5, GridUnitType.Star);
    private bool encodeAsJb2;
    private bool encodeAsWebp;
    private CancellationTokenSource fileloadcancellationToken;
    private double height;
    private bool isMouseDown;
    private bool isRightMouseDown;
    private Window maximizedWindow;
    private Point mousedowncoord;
    private GridLength twainGuiControlLength = new(3, GridUnitType.Star);
    private double width;

    public TwainCtrl()
    {
        InitializeComponent();
        DataContext = this;
        Scanner = new Scanner();
        PdfGeneration.Scanner = Scanner;

        Scanner.PropertyChanged += Scanner_PropertyChanged;
        Settings.Default.PropertyChanged += Default_PropertyChanged;
        PropertyChanged += TwainCtrl_PropertyChangedAsync;
        Camera.PropertyChanged += CameraUserControl_PropertyChanged;
        TranslationSource.Instance.PropertyChanged += Language_PropertyChanged;
        SelectedPaper = Settings.Default.LockSelectedPaper ? Papers.FirstOrDefault(z => z.PaperType == Settings.Default.DefaultPaper) : Papers.FirstOrDefault(z => z.PaperType == "A4");
        OnPropertyChanged(nameof(TesseractOrientationFileExists));
        DependencyPropertyDescriptor.FromProperty(MediaViewer.MediaPositionProperty, typeof(MediaViewer))?.AddValueChanged(mediaViewer, OnMediaPositionChanged);
        Loaded += TwainCtrl_Loaded;

        ScanImage = new RelayCommand<object>(
            async parameter =>
            {
                GC.Collect();
                await Task.Delay(TimeSpan.FromSeconds(Settings.Default.ScanDelay));
                Scanner.ArayüzEtkin = false;
                await DefaultScanAsync();
                Twain.ScanningComplete += ScanComplete;
            },
            parameter => !Environment.Is64BitProcess && AnyScannerExist() && !string.IsNullOrWhiteSpace(Settings.Default.SeçiliTarayıcı) && Policy.CheckPolicy(nameof(ScanImage)));

        FastScanImage = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                GC.Collect();
                await Task.Delay(TimeSpan.FromSeconds(Settings.Default.ScanDelay));
                Scanner.ArayüzEtkin = false;
                if (Keyboard.Modifiers != ModifierKeys.Alt)
                {
                    Scanner.Resimler = [];
                }
                await DefaultScanAsync();
                Twain.ScanningComplete += FastScanComplete;
            },
            parameter => !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage) &&
            !Environment.Is64BitProcess &&
            AnyScannerExist() &&
            !string.IsNullOrWhiteSpace(Settings.Default.SeçiliTarayıcı) &&
            Scanner?.AutoSave == true &&
            FileNameValid(Scanner?.FileName) &&
            Policy.CheckPolicy(nameof(FastScanImage)));

        ResimSil = new RelayCommand<object>(
            parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                ScannedImage item = parameter as ScannedImage;
                int index = Scanner.Resimler?.IndexOf(item) ?? -1;
                if (index < 0)
                {
                    return;
                }
                undoStack.Push(new DeletedImageEntry(item, index));
                CanUndoImage = undoStack.Count > 0;

                if (Settings.Default.DirectRemoveImage)
                {
                    RemoveSelectedImage(item);
                    return;
                }

                if (MessageBox.Show(Translation.GetResStringValue("REMOVESELECTED"), AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    RemoveSelectedImage(item);
                }
            },
            parameter => parameter is ScannedImage && Scanner.ArayüzEtkin);

        TekResimSil = new RelayCommand<object>(
            parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                ExtendedMessageBox extendedmessagebox = new()
                {
                    CustomContentVisible = Visibility.Visible,
                    CustomContent = new ShadowedImage() { Source = (parameter as ScannedImage)?.ResimThumb, ShowShadow = true },
                    NoButton = Visibility.Visible,
                    YesButton = Visibility.Visible,
                };
                extendedmessagebox.ShowDialog(
                    Window.GetWindow(this),
                    Translation.GetResStringValue("REMOVESELECTED"),
                    Translation.GetResStringValue("DELETE"),
                    () =>
                    {
                        ScannedImage item = parameter as ScannedImage;
                        int index = Scanner.Resimler?.IndexOf(item) ?? -1;
                        if (index < 0)
                        {
                            return;
                        }
                        undoStack.Push(new DeletedImageEntry(item, index));
                        CanUndoImage = undoStack.Count > 0;
                        RemoveSelectedImage(item);

                    });
            },
            parameter => parameter is ScannedImage && Scanner.ArayüzEtkin);

        ResimSilGeriAl = new RelayCommand<object>(
            parameter =>
            {
                if (undoStack.Count == 0)
                {
                    return;
                }
                DeletedImageEntry entry = undoStack.Pop();
                Scanner.Resimler?.Insert(entry.Index, entry.Image);
                CanUndoImage = undoStack.Count > 0;
            },
            parameter => CanUndoImage);

        InvertImage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not ScannedImage item)
                {
                    return;
                }
                int index = Scanner.Resimler?.IndexOf(item) ?? -1;
                if (index < 0)
                {
                    return;
                }
                invertundoStack.Push(new DeletedImageEntry(new ScannedImage() { Resim = item.Resim }, index));
                using Bitmap bitmap = item.Resim.BitmapSourceToBitmap();
                BitmapFrame processedImage = Keyboard.Modifiers == ModifierKeys.Alt
                                             ? BitmapFrame.Create(bitmap.ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Tiff))
                                             : Keyboard.Modifiers == ModifierKeys.Shift
                                               ? BitmapFrame.Create(bitmap.ConvertBlackAndWhite(Scanner.ToolBarBwThreshold, true).ToBitmapImage(ImageFormat.Jpeg))
                                               : BitmapFrame.Create(item.Resim.InvertBitmap().ToBitmapImage());
                processedImage?.Freeze();
                item.Resim = processedImage;
                processedImage = null;
                GC.Collect();
            },
            parameter => true);

        UndoInvertImage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not ScannedImage item || invertundoStack.Count == 0)
                {
                    return;
                }
                DeletedImageEntry entry = invertundoStack.Pop();
                item.Resim = entry.Image.Resim;
            },
            parameter => true);

        AutoDeskewImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is ScannedImage item &&
                MessageBox.Show($"{Translation.GetResStringValue("Auto")} {Translation.GetResStringValue("DESKEW")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    await CreateAutoDeskewedImage(item);
                }
            },
            parameter => true);

        ManualDeskewImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is ScannedImage item)
                {
                    await CreateAutoDeskewedImage(item, CustomDeskewAngle);
                }
            },
            parameter => CustomDeskewAngle != 0);

        TümünüOtomatikDöndür = new RelayCommand<object>(
            parameter =>
            {
                ExtendedMessageBox extendedmessagebox = new()
                {
                    CheckDescription = Translation.GetResStringValue("DESKEW"),
                    CheckVisibility = Visibility.Visible,
                    CustomContentVisible = Visibility.Visible,
                    CustomContentHeight = 20,
                    NoButton = Visibility.Visible,
                    YesButton = Visibility.Visible,
                };
                NumericUpDown numericUpDown = new() { Minimum = 1, Maximum = Environment.ProcessorCount, Value = 4, IsReadOnly = true };

                extendedmessagebox.CustomContent = numericUpDown;
                extendedmessagebox.ShowDialog(
                    Window.GetWindow(this),
                    Translation.GetResStringValue("LONGTIMEJOB"),
                    $"{Translation.GetResStringValue("ALL")} {Translation.GetResStringValue("AUTOROTATE")}",
                    async () =>
                    {
                        try
                        {
                            AutoRotateIsWorking = true;
                            int parallelcount = (int)numericUpDown.Value;
                            await AutoRotateBasedTextOrientation(Scanner.Resimler, parallelcount);
                            if (extendedmessagebox.IsChecked)
                            {
                                for (int i = 0; i < Scanner.Resimler.Count; i++)
                                {
                                    await CreateAutoDeskewedImage(Scanner.Resimler[i]);
                                    AllRotateProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                                }
                            }
                        }
                        finally
                        {
                            AutoRotateIsWorking = false;
                        }
                    });
                GC.Collect();
            },
            parameter => Scanner?.Resimler?.Any() == true && TesseractOrientationFileExists && !AutoRotateIsWorking);

        ToolBarAutoDeskewImage = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    AutoRotateIsWorking = true;
                    int count = GetSelectedImages().Count;
                    for (int i = 0; i < count; i++)
                    {
                        await CreateAutoDeskewedImage(GetSelectedImages()[i]);
                        AllRotateProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                    }
                }
                finally
                {
                    AutoRotateIsWorking = false;
                }
            },
            parameter => GetSelectedImages().Count > 0 && !AutoRotateIsWorking);

        ToolBarAutoCropImage = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    AutoRotateIsWorking = true;
                    List<ScannedImage> selected = GetSelectedImages();
                    int total = selected.Count;
                    for (int i = 0; i < total; i++)
                    {
                        ScannedImage item = selected[i];
                        item.Resim = BitmapFrame.Create(await item.Resim.AutoCropImage());
                        AllRotateProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                    }
                }
                finally
                {
                    AutoRotateIsWorking = false;
                }
            },
            parameter => GetSelectedImages().Count > 0 && !AutoRotateIsWorking);

        ToolBoxManualDeskewImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is ImageSource item)
                {
                    BitmapFrame bitmapFrame = BitmapFrame.Create(await item.RotateImageAsync(CustomDeskewAngle, Brushes.White));
                    bitmapFrame?.Freeze();
                    Scanner.CroppedImage = bitmapFrame;
                    bitmapFrame = null;
                    GC.Collect();
                }
            },
            parameter => Scanner?.CroppedImage is not null && CustomDeskewAngle != 0);

        InvertSelectedImage = new RelayCommand<object>(
            async parameter =>
            {
                bool bw = Keyboard.Modifiers == ModifierKeys.Alt;
                bool grayscale = Keyboard.Modifiers == ModifierKeys.Shift;

                if (MessageBox.Show($"{Translation.GetResStringValue("LONGTIMEJOB")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    return;
                }

                List<ScannedImage> selected = [ .. GetSelectedImages() ];
                int threshold = Scanner.ToolBarBwThreshold;
                await Task.Run(
                    () =>
                    {
                        for (int i = 0; i < selected.Count; i++)
                        {
                            ScannedImage item = selected[i];
                            using Bitmap bitmap = item.Resim.BitmapSourceToBitmap();
                            using Bitmap processed = bw ? bitmap.ConvertBlackAndWhite(threshold) : grayscale ? bitmap.ConvertBlackAndWhite(threshold, true) : item.Resim.InvertBitmap().BitmapSourceToBitmap();
                            BitmapImage img = bw ? processed.ToBitmapImage(ImageFormat.Tiff) : processed.ToBitmapImage(ImageFormat.Jpeg);
                            img.Freeze();
                            item.Resim = BitmapFrame.Create(img);
                            AllRotateProgressValue = (i + 1) / (double)selected.Count;
                        }
                    });
            },
            parameter => Scanner.Resimler.Count(z => z.Seçili) > 0);

        ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

        OpenHelpDialog = new RelayCommand<object>(parameter => HelpIsOpened = !HelpIsOpened, parameter => true);

        SaveSinglePdfFile = new RelayCommand<object>(
            async parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası (*.pdf)|*.pdf", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    await SavePdfImageAsync([ scannedImage ], saveFileDialog.FileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, false, Settings.Default.ImgLoadResolution);
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleBwPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    if (EncodeAsJb2)
                    {
                        List<ScannedImage> image = [ scannedImage ];
                        File.WriteAllBytes(saveFileDialog.FileName, image.CreateMultipagePdfWithJbig2Images().AddPdfPassword_PdfSharp().ToArray());
                    }
                    else
                    {
                        await SavePdfImageAsync([ scannedImage ], saveFileDialog.FileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, true, Settings.Default.ImgLoadResolution);
                    }
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleJpgFile = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Jpg Dosyası (*.jpg)|*.jpg", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    SaveJpgImage(scannedImage.Resim, saveFileDialog.FileName);
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleXpsFile = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Xps Dosyası (*.xps)|*.xps", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    SaveXpsImage(scannedImage.Resim, saveFileDialog.FileName);
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleTifFile = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Tif Dosyası (*.tif)|*.tif", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    SaveTifImage(scannedImage.Resim, saveFileDialog.FileName);
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleWebpFile = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Webp Dosyası (*.webp)|*.webp", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    SaveWebpImage(scannedImage.Resim, saveFileDialog.FileName);
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleBlackWhiteJb2File = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Jb2 Dosyası (*.jb2)|*.jb2", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    SaveJb2Image(scannedImage.Resim, saveFileDialog.FileName);
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleTxtFile = new RelayCommand<object>(
            async parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Txt Dosyası (*.txt)|*.txt", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    await SaveTxtFileAsync(scannedImage.Resim, saveFileDialog.FileName);
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        Tümünüİşaretle = new RelayCommand<object>(
            parameter =>
            {
                ObservableCollection<ScannedImage> resimler = Scanner.Resimler;
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    for (int i = 1; i < resimler.Count; i += 2)
                    {
                        resimler[i].Seçili = true;
                    }
                    return;
                }
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    for (int i = 0; i < resimler.Count; i += 2)
                    {
                        resimler[i].Seçili = true;
                    }
                    return;
                }
                foreach (ScannedImage item in resimler)
                {
                    item.Seçili = true;
                }
            },
            parameter => Policy.CheckPolicy(nameof(Tümünüİşaretle)) && AnyImageExist());

        PdfImportViewerTümünüİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (PdfData item in PdfPages)
                {
                    item.Selected = true;
                }
            },
            parameter => PdfPages?.Count > 0);

        PdfImportViewerTekİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (PdfData item in PdfPages.Where(z => z.PageNumber % 2 == 1))
                {
                    item.Selected = true;
                }
            },
            parameter => PdfPages?.Count > 0);

        PdfImportViewerÇiftİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (PdfData item in PdfPages.Where(z => z.PageNumber % 2 == 0))
                {
                    item.Selected = true;
                }
            },
            parameter => PdfPages?.Count > 0);

        PdfImportViewerTersiniİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (PdfData item in PdfPages)
                {
                    item.Selected = !item.Selected;
                }
            },
            parameter => PdfPages?.Count > 0);

        TümünüİşaretleDikey = new RelayCommand<object>(
            parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in Scanner.Resimler.Where(item => item.Resim.PixelWidth <= item.Resim.PixelHeight))
                {
                    item.Seçili = true;
                }
            },
            parameter => AnyImageExist());

        TümünüİşaretleYatay = new RelayCommand<object>(
            parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in Scanner.Resimler.Where(item => item.Resim.PixelHeight < item.Resim.PixelWidth))
                {
                    item.Seçili = true;
                }
            },
            parameter => AnyImageExist());

        TümününİşaretiniKaldır = new RelayCommand<object>(
            parameter =>
            {
                SeçiliResim = null;
                foreach (ScannedImage item in Scanner.Resimler)
                {
                    item.Seçili = false;
                }
            },
            parameter => AnyImageExist());

        Tersiniİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler)
                {
                    item.Seçili = !item.Seçili;
                }
            },
            parameter => AnyImageExist());

        KayıtYoluBelirle = new RelayCommand<object>(
            parameter =>
            {
                string path = FolderDialog.SelectFolder(Translation.GetResStringValue("AUTOFOLDER"), null, Settings.Default.AutoFolder);
                string oldpath = Settings.Default.AutoFolder;
                ExtendedMessageBox extendedMessageBox = new();
                if (!string.IsNullOrEmpty(path))
                {
                    DriveInfo driveInfo = new(path);
                    if (driveInfo.DriveType == DriveType.CDRom)
                    {
                        extendedMessageBox.ShowDialog(Window.GetWindow(this), $"{Translation.GetResStringValue("ERROR")}\n{Translation.GetResStringValue("INVALIDFILENAME")}", AppName);
                        return;
                    }
                    Settings.Default.AutoFolder = path;
                    Scanner.LocalizedPath = ShellIcon.GetDisplayName(path);
                }

                if (!string.IsNullOrWhiteSpace(oldpath) && oldpath != Settings.Default.AutoFolder)
                {
                    extendedMessageBox.ShowDialog(Window.GetWindow(this), Translation.GetResStringValue("AUTOFOLDERCHANGE"), AppName);
                }
            },
            parameter => true);

        SaveSelectedFilesPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası (*.pdf)|*.pdf", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true)
                {
                    FileSaveTask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SavePdfImageAsync(seçiliresimler, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, false, Settings.Default.ImgLoadResolution);
                        });
                    await RemoveProcessedImages();
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SaveSelectedFilesJpgFile = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                SaveFileDialog saveFileDialog = new() { Filter = "Jpg Dosyası (*.jpg)|*.jpg", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Scanner.ProgressState = TaskbarItemProgressState.Normal;
                    FileSaveTask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveJpgImageAsync(seçiliresimler, fileName, Settings.Default.WebPJpgFileProcessorCount, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SaveSelectedFilesBwPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                SaveFileDialog saveFileDialog = new() { Filter = "Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Scanner.ProgressState = TaskbarItemProgressState.Normal;
                    FileSaveTask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            if (EncodeAsJb2)
                            {
                                Progress<double> progress = new(percent => Scanner.PdfSaveProgressValue = percent);
                                File.WriteAllBytes(fileName, seçiliresimler.CreateMultipagePdfWithJbig2Images(progress).AddPdfPassword_PdfSharp().ToArray());
                                Scanner.PdfSaveProgressValue = 0;
                            }
                            else
                            {
                                await SavePdfImageAsync(seçiliresimler, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, true, Settings.Default.ImgLoadResolution);
                            }
                        });
                    await RemoveProcessedImages();
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SaveSelectedFilesTifFile = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                SaveFileDialog saveFileDialog = new() { Filter = "Tif Dosyası (*.tif)|*.tif", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true)
                {
                    FileSaveTask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveTifImageAsync(seçiliresimler, fileName);
                        });
                    await RemoveProcessedImages();
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SaveSelectedFilesTxtFile = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                SaveFileDialog saveFileDialog = new() { Filter = "Txt Dosyası (*.txt)|*.txt", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Scanner.ProgressState = TaskbarItemProgressState.Normal;
                    FileSaveTask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveTxtFileAsync(seçiliresimler, fileName, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SaveSelectedFilesWebpFile = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                SaveFileDialog saveFileDialog = new() { Filter = "Webp Dosyası (*.webp)|*.webp", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Scanner.ProgressState = TaskbarItemProgressState.Normal;
                    FileSaveTask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveWebpImageAsync(seçiliresimler, fileName, Settings.Default.WebPJpgFileProcessorCount, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SaveSelectedFilesZipFile = new RelayCommand<object>(
            async parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }

                SaveFileDialog saveFileDialog = new() { Filter = EncodeAsJb2 || EncodeAsWebp ? "JB2 ZIP Dosyası (*.jb2zip)|*.jb2zip" : "Zip Dosyası (*.zip)|*.zip", FileName = Scanner.SaveFileName };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Scanner.ProgressState = TaskbarItemProgressState.Normal;
                    FileSaveTask = Task.Run(
                        () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            SaveZipImage(seçiliresimler, fileName, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
                    Scanner.SaveFileFullPath = saveFileDialog.FileName;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SeçiliDirektPdfKaydet = new RelayCommand<object>(
            parameter => FileSaveTask =
            Task.Run(
                async () =>
                {
                    List<ScannedImage> seçiliresimler = GetSelectedImages();
                    Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
                    DataBaseTextData = [];

                    if (Scanner.ApplyDataBaseOcr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
                    {
                        Scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;

                        int total = seçiliresimler.Count;
                        int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
                        using SemaphoreSlim semaphore = new(maxDegreeOfParallelism);
                        object listLock = new();
                        int completed = 0;
                        IProgress<double> progress = new Progress<double>(value => Scanner.PdfSaveProgressValue = value);
                        List<Task> tasks = [];

                        for (int i = 0; i < total; i++)
                        {
                            int index = i;
                            await semaphore.WaitAsync();

                            tasks.Add(
                                Task.Run(
                                    async () =>
                                    {
                                        try
                                        {
                                            byte[] imgdata = seçiliresimler[index].Resim.ToTiffJpegByteArray(Format.Jpg);
                                            DataBaseQrData = imgdata;

                                            ObservableCollection<OcrData> ocrText = await imgdata.OcrAsync(Scanner.SelectedTtsLanguage);

                                            lock (listLock)
                                            {
                                                DataBaseTextData.Add(ocrText);
                                                completed++;
                                            }

                                            progress.Report(completed / (double)total);
                                        }
                                        finally
                                        {
                                            _ = semaphore.Release();
                                        }
                                    }));
                        }

                        await Task.WhenAll(tasks);
                        Scanner.PdfSaveProgressValue = 0;
                        DataBaseTextDataCompleted = true;
                    }

                    bool applyocr = !Scanner.ApplyDataBaseOcr && Scanner.ApplyPdfSaveOcr;
                    bool isBlackAndWhiteMode = (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite;
                    await SavePdfImageAsync(seçiliresimler, PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, applyocr, isBlackAndWhiteMode, Settings.Default.ImgLoadResolution, DataBaseTextData);
                    OnPropertyChanged(nameof(Scanner.Resimler));
                    DataBaseTextDataCompleted = false;
                    await RemoveProcessedImages();
                }),
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage) && Policy.CheckPolicy(nameof(SeçiliDirektPdfKaydet)) && Scanner?.AutoSave == true && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        ListeTemizle = new RelayCommand<object>(
            parameter =>
            {
                if (!CheckFileSaveProgress())
                {
                    return;
                }
                if (MessageBox.Show(Window.GetWindow(this), Translation.GetResStringValue("LISTREMOVEWARN"), AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                    undoStack.Clear();
                    CanUndoImage = false;
                    ToolBox.ResetCropMargin();
                    GC.Collect();
                }
            },
            parameter => Policy.CheckPolicy(nameof(ListeTemizle)) && AnyImageExist() && Scanner.ArayüzEtkin);

        SeçiliListeTemizle = new RelayCommand<object>(
            parameter =>
            {
                foreach (ScannedImage item in GetSelectedImages())
                {
                    int index = Scanner.Resimler?.IndexOf(item) ?? -1;
                    if (index < 0)
                    {
                        return;
                    }
                    undoStack.Push(new DeletedImageEntry(item, index));
                    CanUndoImage = undoStack.Count > 0;
                    _ = Scanner.Resimler?.Remove(item);
                }
                ToolBox.ResetCropMargin();
                GC.Collect();
            },
            parameter => Policy.CheckPolicy(nameof(SeçiliListeTemizle)) && Scanner?.Resimler?.Any(z => z.Seçili) == true && Scanner.ArayüzEtkin);

        ShowDateFolderHelp = new RelayCommand<object>(
            parameter =>
            {
                StringBuilder sb = new();
                foreach (KeyValuePair<string, int> item in Scanner.FolderDateFormats)
                {
                    _ = sb.Append(item.Key).Append(' ').AppendLine(DateTime.Today.ToString(item.Key, TranslationSource.Instance.CurrentCulture));
                }
                _ = sb.AppendLine().AppendLine(Translation.GetResStringValue("FOLDERFORMAT"));
                ExtendedMessageBox extendedMessageBox = new();
                extendedMessageBox.ShowDialog(Window.GetWindow(this), sb.ToString(), AppName);
            },
            parameter => true);

        SaveProfile = new RelayCommand<object>(
            parameter =>
            {
                string profile = $"{Scanner.ProfileName}|{Settings.Default.Çözünürlük}|{Settings.Default.Adf}|{Settings.Default.Mode}|{Scanner.Duplex}|{Scanner.ShowUi}|false|{Settings.Default.ShowFile}|{Scanner.DetectEmptyPage}|{Scanner.FileName}|{Scanner.InvertImage}|{Scanner.ApplyMedian}|{Settings.Default.SeçiliTarayıcı}|{Settings.Default.AutoCropImage}|{Scanner.UseFilmScanner}";
                _ = Settings.Default.Profile.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
                Scanner.ProfileName = string.Empty;
            },
            parameter => !string.IsNullOrWhiteSpace(Scanner?.ProfileName) &&
            !Settings.Default.Profile.Cast<string>().Select(z => z.Split('|')[0]).Contains(Scanner?.ProfileName) &&
            FileNameValid(Scanner?.FileName) &&
            FileNameValid(Scanner?.ProfileName) &&
            AnyScannerExist() &&
            !string.IsNullOrWhiteSpace(Settings.Default.SeçiliTarayıcı));

        RemoveProfile = new RelayCommand<object>(
            parameter =>
            {
                Settings.Default.Profile.Remove(parameter as string);
                Settings.Default.DefaultProfile = null;
                Settings.Default.UseSelectedProfile = false;
                Settings.Default.Save();
                Settings.Default.Reload();
            },
            parameter => true);

        LoadCroppedImage = new RelayCommand<object>(
            async parameter =>
            {
                bool altpressed = Keyboard.Modifiers == ModifierKeys.Alt;
                if (!altpressed && SeçiliResim is not null)
                {
                    Scanner.CroppedImage = SeçiliResim.Resim;
                    Scanner.CroppedImageIndex = SeçiliResim.Index;
                    Scanner.CopyCroppedImage = Scanner.CroppedImage;
                    return;
                }
                OpenFileDialog openFileDialog = new() { Filter = "Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.bmp;*.heic)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.bmp;*.heic", Multiselect = false };
                if (openFileDialog.ShowDialog() == false)
                {
                    return;
                }
                ILoadFileHandler loadFileHandler = new ImageFileHandler();
                BitmapFrame bitmapframe = await loadFileHandler.LoadImageAsync(openFileDialog.FileName);
                bitmapframe?.Freeze();
                if (bitmapframe is null)
                {
                    return;
                }
                Scanner.CroppedImage = bitmapframe;
                Scanner.CopyCroppedImage = bitmapframe;
                Scanner.CroppedImageIndex = 0;
            },
            parameter => true);

        InsertFileNamePlaceHolder = new RelayCommand<object>(
            parameter =>
            {
                string placeholder = parameter as string;
                Scanner.FileName = $"{Scanner.FileName.Substring(0, Scanner.CaretPosition)}{placeholder}{Scanner.FileName.Substring(Scanner.CaretPosition, Scanner.FileName.Length - Scanner.CaretPosition)}";
            },
            parameter => true);

        WebAdreseGit = new RelayCommand<object>(parameter => GotoPage(parameter as string), parameter => true);

        ExtractNugetPackage = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "NuGet Package (*.nupkg)|*.nupkg", Multiselect = false };
                if (openFileDialog.ShowDialog() == true)
                {
                    string dllpath = $@"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\x86\pdfium.dll";
                    ExtendedMessageBox extendedMessageBox = new();
                    try
                    {
                        ZipExtractSingleFile(openFileDialog.FileName, "runtimes/win-x86/native/pdfium.dll", dllpath);
                        extendedMessageBox.ShowDialog(Window.GetWindow(this), $"{Translation.GetResStringValue("INSTALLED")}\n{Translation.GetResStringValue("RESTARTAPP")}", AppName);
                    }
                    catch (Exception)
                    {
                        if (IsAdministrator)
                        {
                            string sourcedllpath = $"{Path.GetTempPath()}pdfium.dll";
                            ZipExtractSingleFile(openFileDialog.FileName, "runtimes/win-x86/native/pdfium.dll", sourcedllpath);
                            AddPendingFileRenameOperation(sourcedllpath, dllpath);
                            extendedMessageBox.ShowDialog(Window.GetWindow(this), Translation.GetResStringValue("RESTARTCOMP"), AppName);
                            return;
                        }
                        extendedMessageBox.ShowDialog(Window.GetWindow(this), Translation.GetResStringValue("FOLDERACCESS"), AppName);
                    }
                }
            },
            parameter => true);

        LoadImage = new RelayCommand<object>(
            async parameter =>
            {
                if (FileLoadTask?.IsCompleted == false)
                {
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(Window.GetWindow(this), Translation.GetResStringValue("TRANSLATEPENDING"), AppName);
                    return;
                }

                OpenFileDialog openFileDialog = new()
                {
                    Filter =
                    "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp;*.webp;*.jb2;*.j2k;*.docx;*.odt;*.cbz;*.cbr;*.7z;*.arj;*.bzip2;*.cab;*.gzip;*.iso;*.lzh;*.lzma;*.ntfs;*.ppmd;*.rar;*.rar5;*.rpm;*.tar;*.vhd;*.wim;*.xar;*.xz;*.z;*.zip;*.jb2zip;*.gz;*.xls;*.xlsx;*.xlsb;*.csv;*.ods;*.txt)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp;*.webp;*.jb2;*.j2k;*.docx;*.odt;*.cbz;*.cbr;*.7z;*.arj;*.bzip2;*.cab;*.gzip;*.iso;*.lzh;*.lzma;*.ntfs;*.ppmd;*.rar;*.rar5;*.rpm;*.tar;*.vhd;*.wim;*.xar;*.xz;*.z;*.zip;*.jb2zip;*.gz;*.xls;*.xlsx;*.xlsb;*.csv;*.ods;*.txt|" +
                        "Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp;*.webp;*.jb2;*.j2k)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp;*.webp;*.jb2;*.j2k|" +
                        "Pdf Dosyası (*.pdf)|*.pdf|" +
                        "Docx Dosyası (*.docx;*.odt)|*.docx;*.odt|" +
                        "Xps Dosyası (*.xps)|*.xps|" +
                        "Eyp Dosyası (*.eyp)|*.eyp|" +
                        "Çizgi Roman Dosyası (*.cbz;*.cbr)|*.cbz;*.cbr|" +
                        "Webp Dosyası (*.webp)|*.webp|" +
                        "Arşiv Dosyaları (*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.gz; *.jb2zip)|*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.gz; *.jb2zip|" +
                        "Excel Dosyası (*.xls;*.xlsx;*.xlsb;*.csv;*.ods)|*.xls;*.xlsx;*.xlsb;*.csv;*.ods|" +
                        "Belge Liste Dosyası (*.txt)|*.txt",
                    Multiselect = true
                };

                if (ImageFileHandler.CheckWithCurrentOsVersion("10.0.17134"))
                {
                    openFileDialog.Filter += "|Heic Dosyası (*.heic)|*.heic";
                }

                if (openFileDialog.ShowDialog() == true)
                {
                    GC.Collect();
                    fileloadcancellationToken = new CancellationTokenSource();
                    await AddFiles(openFileDialog.FileNames, DecodeHeight, fileloadcancellationToken);
                }
            },
            parameter => Policy.CheckPolicy(nameof(LoadImage)));

        CancelLoadFile = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("FILE")} {Translation.GetResStringValue("STOP")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    fileloadcancellationToken?.Cancel();
                }
            },
            parameter => FileLoadTask?.IsCompleted == false);

        LoadXpsFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Xps Dosyası (*.xps)|*.xps", Multiselect = false };

                if (openFileDialog.ShowDialog() == true && parameter is XpsViewer xpsViewer)
                {
                    xpsViewer.XpsDataFilePath = openFileDialog.FileName;
                }
            },
            parameter => true);

        SplitPdf = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    try
                    {
                        PdfToolBarControlIsEnabled = false;
                        string savefolder = ToolBox.CreateSaveFolder("SPLIT");
                        SplitPdfPageCount(pdfviewer.PdfFilePath, savefolder, PdfSplitCount);
                        WebAdreseGit.Execute(savefolder);
                    }
                    finally
                    {
                        PdfToolBarControlIsEnabled = true;
                    }
                }
            },
            parameter => PdfSplitCount > 0);

        AddFromClipBoard = new RelayCommand<object>(
            async parameter =>
            {
                if (Clipboard.ContainsImage())
                {
                    BitmapSource image = Clipboard.GetImage();
                    if (image is not null)
                    {
                        Scanner?.Resimler?.Add(new ScannedImage { Seçili = true, Resim = BitmapFrame.Create(image) });
                    }
                }
                if (Clipboard.ContainsFileDropList())
                {
                    StringCollection clipboardFiles = Clipboard.GetFileDropList();
                    if (clipboardFiles?.Count > 0)
                    {
                        await AddFiles([ .. clipboardFiles.Cast<string>() ], DecodeHeight);
                    }
                }
                Clipboard.Clear();
            },
            parameter => true);

        DuplicateSelectedImage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not BitmapFrame bitmapFrame)
                {
                    return;
                }
                ScannedImage image = new() { Seçili = false, Resim = bitmapFrame };
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    Scanner?.Resimler?.Insert(SeçiliResim.Index, image);
                }
                else
                {
                    Scanner?.Resimler?.Add(image);
                }
            },
            parameter => parameter is BitmapFrame);

        ReplaceSelectedImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not ScannedImage scannedImage)
                {
                    return;
                }

                if (Clipboard.ContainsImage())
                {
                    BitmapSource image = Clipboard.GetImage();
                    if (image is not null)
                    {
                        scannedImage.Resim = BitmapFrame.Create(image);
                    }
                    return;
                }

                if (!Clipboard.ContainsFileDropList())
                {
                    return;
                }

                StringCollection clipboardFiles = Clipboard.GetFileDropList();
                PdfFileHandler pdfFileHandler = new();
                TiffFileHandler tiffFileHandler = new();
                ImageFileHandler imageFileHandler = new();
                foreach (string filename in clipboardFiles)
                {
                    if (pdfFileHandler.IsValidFile(filename))
                    {
                        for (int i = pdfFileHandler.GetPageCount(filename); i >= 1; i--)
                        {
                            BitmapFrame bitmapFrame = BitmapFrame.Create(await pdfFileHandler.LoadPdfAsync(filename, i));
                            bitmapFrame?.Freeze();
                            Scanner?.Resimler?.Insert(scannedImage.Index, new ScannedImage() { Resim = bitmapFrame });
                        }
                    }
                    else if (tiffFileHandler.IsValidFile(filename))
                    {
                        List<BitmapFrame> list = [ .. await tiffFileHandler.LoadTiffPagesAsync(filename) ];
                        for (int i = list.Count - 1; i >= 0; i--)
                        {
                            BitmapFrame bitmapFrame = list[i];
                            bitmapFrame?.Freeze();
                            Scanner?.Resimler?.Insert(scannedImage.Index, new ScannedImage() { Resim = bitmapFrame });
                        }
                    }
                    else if (imageFileHandler.IsValidFile(filename))
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Alt)
                        {
                            scannedImage.Resim = await imageFileHandler.LoadImageAsync(clipboardFiles[0]);
                        }
                        else
                        {
                            BitmapFrame bitmapFrame = await imageFileHandler.LoadImageAsync(filename);
                            bitmapFrame?.Freeze();
                            Scanner?.Resimler?.Insert(scannedImage.Index, new ScannedImage() { Resim = bitmapFrame });
                        }
                    }
                }
            },
            parameter => true);

        InsertClipBoardImage = new RelayCommand<object>(
            parameter =>
            {
                bool altkeypressed = Keyboard.Modifiers == ModifierKeys.Alt;
                if (AddFromClipBoard.CanExecute(null))
                {
                    AddFromClipBoard.Execute(null);
                }

                if (altkeypressed && SeçiliDirektPdfKaydet.CanExecute(null))
                {
                    SeçiliDirektPdfKaydet.Execute(null);
                }
            },
            parameter => true);

        SaveFileList = new RelayCommand<object>(
            parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Belge Liste Dosyası (*.txt)|*.txt", FileName = $"{Translation.GetResStringValue("FILE")}" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    using StreamWriter file = new(saveFileDialog.FileName);
                    foreach (ScannedImage image in Scanner?.Resimler?.GroupBy(z => z.FilePath).Select(z => z.FirstOrDefault()))
                    {
                        file.WriteLine(image.FilePath);
                    }
                }
            },
            parameter => Scanner?.Resimler?.Count(z => !string.IsNullOrWhiteSpace(z.FilePath)) > 0);

        MergePdfListToCurrentFile = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    PdfToolBarControlIsEnabled = false;
                    string currentfile = PdfImportViewer.PdfViewer.PdfFilePath;
                    ExtendedPdfData data = new() { FileName = currentfile };
                    if (MergePdfFileToFirst)
                    {
                        Scanner.MergePdfFiles.Add(data);
                    }
                    else
                    {
                        Scanner.MergePdfFiles.Insert(0, data);
                    }
                    ObservableCollection<ExtendedPdfData> files = [ .. Scanner.MergePdfFiles.Where(z => string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)) ];
                    using PdfDocument document = files.Select(z => z.FileName).ToArray().MergePdf();
                    document.Save(currentfile);
                    Scanner?.MergePdfFiles?.Clear();
                    PdfImportViewer.PdfViewer.PdfFilePath = null;
                    PdfImportViewer.PdfViewer.PdfFilePath = currentfile;
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => Scanner?.MergePdfFiles?.Count > 0 && File.Exists(PdfImportViewer.PdfViewer.PdfFilePath));

        MergePdfListToFile = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    ObservableCollection<ExtendedPdfData> files = [ .. Scanner.MergePdfFiles.Where(z => string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)) ];
                    SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = $"{Translation.GetResStringValue("MERGE")}" };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        PdfToolBarControlIsEnabled = false;
                        using PdfDocument document = files.OrderBy(z => z.PageNumber).Select(z => z.FileName).ToArray().MergePdf();
                        document.Save(saveFileDialog.FileName);
                        Scanner?.MergePdfFiles?.Clear();
                    }
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter =>
            {
                ObservableCollection<ExtendedPdfData> files = Scanner?.MergePdfFiles;
                return files?.Count > 1 && files.Select(f => f.PageNumber).Distinct().Count() == files.Count;
            });

        MergePdfListRemoveFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ExtendedPdfData extendedPdfData)
                {
                    _ = (Scanner?.MergePdfFiles?.Remove(extendedPdfData));
                }
            },
            parameter => true);

        MergePdfListAddFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Pdf Dosyası (*.pdf)|*.pdf", Multiselect = true };
                if (openFileDialog.ShowDialog() == true)
                {
                    int page = (Scanner?.MergePdfFiles?.Count ?? 0) + 1;
                    foreach (string file in openFileDialog.FileNames.Where(Viewer.IsValidPdfFile))
                    {
                        Scanner?.MergePdfFiles?.Add(new ExtendedPdfData() { FileName = file, PageNumber = page++ });
                    }
                }
            },
            parameter => true);

        int cycleindex = 0;
        CycleSelectedDocuments = new RelayCommand<object>(
            async parameter =>
            {
                ScannedImage scannedImage = GetSelectedImages().ElementAtOrDefault(cycleindex);
                if (parameter is ListBox listBox && scannedImage is not null)
                {
                    listBox.ScrollIntoView(scannedImage);
                    scannedImage.Animate = true;
                    cycleindex++;
                    cycleindex %= GetSelectedImagesCount() ?? 0;
                    await Task.Delay(1000);
                    scannedImage.Animate = false;
                }
            },
            parameter => GetSelectedImagesCount() > 0);

        GotoDocumentIndex = new RelayCommand<object>(
            async parameter =>
            {
                ScannedImage scannedImage = Scanner?.Resimler?.FirstOrDefault(z => z.Index == PageIndex);
                if (parameter is ListBox listBox && scannedImage is not null)
                {
                    listBox.ScrollIntoView(scannedImage);
                    scannedImage.Animate = true;
                    await Task.Delay(500);
                    scannedImage.Animate = false;
                }
            },
            parameter => AnyImageExist());

        PdfWaterMark = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    try
                    {
                        string oldpdfpath = pdfViewer.PdfFilePath;
                        using PdfDocument pdfdocument = PdfReader.Open(oldpdfpath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                        if (pdfdocument is null)
                        {
                            return;
                        }
                        PdfToolBarControlIsEnabled = false;
                        if (Keyboard.Modifiers == ModifierKeys.Alt)
                        {
                            PdfDocument listDocument = null;
                            for (int i = 0; i < pdfdocument.PageCount; i++)
                            {
                                listDocument = pdfdocument.GenerateWatermarkedPdf(i, PdfWatermarkFontAngle, PdfWatermarkColor, PdfWatermarkFontSize, PdfWaterMarkText, PdfWatermarkFont);
                            }
                            listDocument?.Save(oldpdfpath);
                            listDocument?.Dispose();
                        }
                        else
                        {
                            using PdfDocument document = pdfdocument.GenerateWatermarkedPdf(pdfViewer.Sayfa - 1, PdfWatermarkFontAngle, PdfWatermarkColor, PdfWatermarkFontSize, PdfWaterMarkText, PdfWatermarkFont);
                            document.Save(oldpdfpath);
                        }
                    }
                    finally
                    {
                        PdfToolBarControlIsEnabled = true;
                    }
                    pdfViewer.Source = await Viewer.ConvertToImgAsync(pdfViewer.PdfFilePath, pdfViewer.Sayfa, pdfViewer.Dpi);
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath) && !string.IsNullOrWhiteSpace(PdfWaterMarkText));

        MergeSelectedImagesToPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    List<ScannedImage> seçiliresimler = GetSelectedImages();
                    if (parameter is not Viewer pdfviewer ||
                    !File.Exists(pdfviewer.PdfFilePath) ||
                    !seçiliresimler.Any() ||
                    MessageBox.Show($"{seçiliresimler.Count} {Translation.GetResStringValue("DOCUMENT")}\n{Translation.GetResStringValue("SAVESELECTED")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) !=
                    MessageBoxResult.Yes)
                    {
                        return;
                    }

                    PdfToolBarControlIsEnabled = false;
                    string pdfFilePath = pdfviewer.PdfFilePath;
                    string temporarypdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                    string[] processedfiles = Keyboard.Modifiers == ModifierKeys.Alt ? [ pdfFilePath, temporarypdf ] : [ temporarypdf, pdfFilePath ];
                    await Task.Run(
                        async () =>
                        {
                            using PdfDocument pdfDocument = await seçiliresimler.GeneratePdfAsync(Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, null, Settings.Default.ImgLoadResolution, progress => Scanner.PdfSaveProgressValue = progress);
                            pdfDocument.Save(temporarypdf);
                            processedfiles.MergePdf().Save(pdfFilePath);
                        });
                    pdfviewer.Sayfa = 1;
                    NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                    ClosedPdfFilePath = pdfFilePath;
                    RefreshDocumentList = true;
                    await RemoveProcessedImages();
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        PasteFileToPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is not Viewer pdfviewer || !File.Exists(pdfviewer.PdfFilePath))
                    {
                        return;
                    }
                    IDataObject clipboardData = Clipboard.GetDataObject();
                    if (clipboardData is null)
                    {
                        return;
                    }
                    string pdfFilePath = pdfviewer.PdfFilePath;
                    string temporaryPdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                    string[] processedFiles = Keyboard.Modifiers == ModifierKeys.Alt ? [ pdfFilePath, temporaryPdf ] : [ temporaryPdf, pdfFilePath ];
                    PdfToolBarControlIsEnabled = false;
                    if (Clipboard.ContainsFileDropList())
                    {
                        await ProcessDropFileList(pdfFilePath, temporaryPdf, processedFiles);
                    }

                    if (Clipboard.ContainsImage())
                    {
                        await ProcessImageFile(pdfFilePath, temporaryPdf, processedFiles);
                    }
                    pdfviewer.Sayfa = 1;
                    NotifyPdfChange(pdfviewer, temporaryPdf, pdfFilePath);
                    ClosedPdfFilePath = pdfFilePath;
                    RefreshDocumentList = true;
                    Clipboard.Clear();
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ReadPdfTag = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not string filepath || !File.Exists(filepath))
                {
                    return;
                }
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    WebAdreseGit.Execute(filepath);
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    ExploreFile.Execute(filepath);
                    return;
                }

                using PdfDocument reader = PdfReader.Open(filepath, PdfDocumentOpenMode.InformationOnly);
                StringBuilder stringBuilder = new();
                _ = stringBuilder.AppendLine(filepath)
                .AppendLine("PDF ")
                .Append((reader.Version / 10d).ToString("n1", CultureInfo.InvariantCulture))
                .AppendLine(reader.Info.Title)
                .Append(Translation.GetResStringValue("PAGENUMBER"))
                .Append(": ")
                .Append(reader.PageCount)
                .AppendLine()
                .AppendLine(reader.Info.Producer)
                .AppendLine(reader.Info.Creator)
                .AppendLine(reader.Info.Author)
                .Append(reader.Info.CreationDate.AddHours(DateTimeOffset.Now.Offset.Hours))
                .AppendLine()
                .Append($"{reader.FileSize / 1048576d:F}")
                .AppendLine(" MB");
                ExtendedMessageBox extendedMessageBox = new();
                extendedMessageBox.ShowDialog(Window.GetWindow(this), stringBuilder.ToString(), AppName);
            },
            parameter => parameter is string filepath && File.Exists(filepath));

        AddAllFileToControlPanel = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not Viewer pdfviewer || !File.Exists(pdfviewer.PdfFilePath))
                {
                    return;
                }
                if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                {
                    PdfImportViewer.PdfViewer.PdfFilePath = pdfviewer.PdfFilePath;
                    SelectedTabIndex = 3;
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    await AddFiles([ pdfviewer.PdfFilePath ], DecodeHeight);
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (SayfaBaşlangıç <= SayfaBitiş)
                    {
                        string savefilename = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                        await PdfPageRangeSaveFileAsync(pdfviewer.PdfFilePath, savefilename, SayfaBaşlangıç, SayfaBitiş);
                        await AddFiles([ savefilename ], DecodeHeight);
                    }

                    return;
                }

                byte[] filedata = await Viewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                using MemoryStream ms = await Viewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, Settings.Default.ImgLoadResolution);
                if (ms is not null)
                {
                    BitmapFrame bitmapFrame = ms.GenerateBitmapFrameFromMemoryStream();
                    bitmapFrame?.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    Scanner?.Resimler.Add(scannedImage);
                }

                filedata = null;
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        RotateSelectedPage = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is not Viewer pdfviewer || !File.Exists(pdfviewer.PdfFilePath))
                    {
                        return;
                    }
                    using PdfDocument pdfdocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                    if ((pdfdocument is null) || pdfviewer.Sayfa < 1 || pdfviewer.Sayfa > pdfdocument.PageCount)
                    {
                        return;
                    }
                    PdfToolBarControlIsEnabled = false;
                    string path = pdfviewer.PdfFilePath;
                    int angle = Keyboard.Modifiers == ModifierKeys.Alt ? -90 : 90;
                    int index = pdfviewer.Sayfa - 1;
                    await Task.Run(() => SavePageRotated(path, pdfdocument, angle, index));
                    pdfviewer.Source = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ReversePdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("SAVEPDF")} {Translation.GetResStringValue("REVERSE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    try
                    {
                        PdfToolBarControlIsEnabled = false;
                        string oldpdfpath = pdfviewer.PdfFilePath;
                        await ReverseFileAsync(pdfviewer.PdfFilePath, pdfviewer.PdfFilePath);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = oldpdfpath;
                    }
                    finally
                    {
                        PdfToolBarControlIsEnabled = true;
                    }
                }
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath) && pdfviewer.ToplamSayfa > 1);

        AddPdfAttachmentFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("ADDDOC")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    OpenFileDialog openFileDialog = new() { Filter = "Tüm Dosyalar (*.*)|*.*", Multiselect = true };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        try
                        {
                            PdfToolBarControlIsEnabled = false;
                            string oldpdfpath = pdfviewer.PdfFilePath;
                            await AddAttachmentFileAsync(openFileDialog.FileNames, pdfviewer.PdfFilePath, pdfviewer.PdfFilePath);
                            pdfviewer.PdfFilePath = null;
                            pdfviewer.PdfFilePath = oldpdfpath;
                        }
                        finally
                        {
                            PdfToolBarControlIsEnabled = true;
                        }
                    }
                }
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        LoadArchiveFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter =
                    "Arşiv Dosyaları (*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.jb2zip; *.gz)|*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.jb2zip; *.gz",
                    Multiselect = false
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    ArchiveVwr.ArchivePath = openFileDialog.FileName;
                }
            },
            parameter => true);

        LoadXlsFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Excel Dosyası(*.xls; *.xlsx; *.xlsb; *.csv; *.ods) | *.xls; *.xlsx; *.xlsb; *.csv; *.ods", Multiselect = false };
                if (openFileDialog.ShowDialog() == true)
                {
                    xlsxViewer.XlsxDataFilePath = openFileDialog.FileName;
                }
            },
            parameter => true);

        LoadDocxFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Word Dosyası(*.docx; *.txt; *.xml; *.xsl; *.xslt; *.xaml; *.log; *.odt) | *.docx; *.txt; *.xml; *.xsl; *.xslt; *.xaml; *.log; *.odt", Multiselect = false };
                if (openFileDialog.ShowDialog() == true)
                {
                    docxViewer.DocxDataFilePath = openFileDialog.FileName;
                }
            },
            parameter => true);

        LoadJb2ZipFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Jb2Zip Dosyası(*.jb2zip) | *.jb2zip", Multiselect = false };
                if (openFileDialog.ShowDialog() == true)
                {
                    jb2zipViewer.ImageFilePath = openFileDialog.FileName;
                }
            },
            parameter => true);

        ClosePdfFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is EypPdfViewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("CLOSEFILE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    pdfviewer.EypAttachments = null;
                    pdfviewer.EypNonSuportedAttachments = null;
                    ClosedPdfFilePath = pdfviewer.PdfFilePath;
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.Source = null;
                    pdfviewer.Sayfa = 1;
                    pdfviewer.ToplamSayfa = 0;
                    SayfaBaşlangıç = 1;
                    SayfaBitiş = 1;
                    RefreshDocumentList = true;
                }
            },
            parameter => parameter is EypPdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ReverseData = new RelayCommand<object>(
            parameter =>
            {
                List<ScannedImage> scannedImages = [ .. Scanner.Resimler.Reverse() ];
                Scanner.Resimler = [ .. scannedImages ];
            },
            parameter => Scanner?.Resimler?.Count > 1);

        FirstLastGroup = new RelayCommand<object>(
            parameter =>
            {
                List<ScannedImage> scannedImages = [ .. Scanner.Resimler ];
                Scanner.Resimler = [ .. GroupByFirstLastList(scannedImages, GroupSplitCount) ];
            },
            parameter => Scanner?.Resimler?.Count > 1);

        ShuffleData = new RelayCommand<object>(
            parameter =>
            {
                bool altkeypressed = Keyboard.Modifiers == ModifierKeys.Alt;
                if (MessageBox.Show($"{Translation.GetResStringValue("RANDOM")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Random random = new();
                    Scanner.Resimler = altkeypressed ? Shuffle(GetSelectedImages(), random) : Shuffle(Scanner.Resimler, random);
                }
            },
            parameter => Scanner?.Resimler?.Count > 1);

        ShufflePdfPages = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("RANDOM")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Random random = new();
                    PdfPages = Shuffle(PdfPages, random);
                }
            },
            parameter => PdfPages?.Count > 1);

        FirstLastSortSequenceData = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    Scanner.Resimler = FirstLastReverseSequence([ .. Scanner.Resimler ], item => item.Index);
                    return;
                }
                Scanner.Resimler = FirstLastSequence(Scanner.Resimler);
            },
            parameter => Scanner?.Resimler?.Count > 1);

        ReverseDataHorizontal = new RelayCommand<object>(
            parameter =>
            {
                int start = Scanner.Resimler.IndexOf(Scanner?.Resimler.FirstOrDefault(z => z.Seçili));
                int end = Scanner.Resimler.IndexOf(Scanner?.Resimler.LastOrDefault(z => z.Seçili));
                if (GetSelectedImagesCount() == end - start + 1)
                {
                    List<ScannedImage> scannedImages = [ .. Scanner.Resimler ];
                    scannedImages.Reverse(start, end - start + 1);
                    Scanner.Resimler = [ .. scannedImages ];
                }
            },
            parameter =>
            {
                List<ScannedImage> selected = GetSelectedImages();
                int start = Scanner?.Resimler?.IndexOf(selected?.FirstOrDefault()) ?? 0;
                int end = Scanner?.Resimler?.IndexOf(selected?.LastOrDefault()) ?? 0;
                return GetSelectedImagesCount() > 1 && selected?.Count() == end - start + 1;
            });

        LoadPdfExtractFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    PdfPages = [];
                    int count = Viewer.PdfPageCount(pdfViewer.PdfFilePath);
                    for (int i = 1; i <= count; i++)
                    {
                        PdfData data = new() { PageNumber = i };
                        data.PropertyChanged -= PdfData_PropertyChanged;
                        data.PropertyChanged += PdfData_PropertyChanged;
                        PdfPages.Add(data);
                    }
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        CopyPdfBitmapFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not Viewer pdfViewer || !File.Exists(pdfViewer.PdfFilePath))
                {
                    return;
                }
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    Clipboard.SetImage((BitmapSource)pdfViewer.Source);
                    return;
                }
                byte[] filedata = await Viewer.ReadAllFileAsync(pdfViewer.PdfFilePath);
                using MemoryStream ms = await Viewer.ConvertToImgStreamAsync(filedata, pdfViewer.Sayfa, Settings.Default.ImgLoadResolution);
                filedata = null;
                using Image image = Image.FromStream(ms);
                Clipboard.SetImage(image.ToBitmapImage(ImageFormat.Jpeg));
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        CopyCurrentImageToClipBoard = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ScannedImage scannedImage && scannedImage?.Resim is not null)
                {
                    Clipboard.SetImage(scannedImage.Resim);
                    ExtendedMessageBox extendedMessageBox = new();
                    extendedMessageBox.ShowDialog(Window.GetWindow(this), Translation.GetResStringValue("COPYCLIPBOARD"), AppName);
                }
            },
            parameter => true);

        CopyCurrentImageToImageEditor = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ScannedImage scannedImage && scannedImage?.Resim is not null)
                {
                    SelectedTabIndex = 1;
                    drawControl.TemporaryImage = scannedImage.Resim;
                    drawControl.Ink.CurrentZoom = ActualHeight / scannedImage.Resim.PixelHeight;
                    TümününİşaretiniKaldır?.Execute(null);
                    scannedImage.Seçili = true;
                }
            },
            parameter => true);

        ApplyPdfMedianFilter = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is not Viewer pdfViewer || !File.Exists(pdfViewer.PdfFilePath))
                    {
                        return;
                    }

                    byte[] filedata = await Viewer.ReadAllFileAsync(pdfViewer.PdfFilePath);
                    using MemoryStream ms = await Viewer.ConvertToImgStreamAsync(filedata, PdfImportViewer.PdfViewer.Sayfa, Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = ms.GenerateBitmapFrameFromMemoryStream();
                    if (bitmapFrame is not null)
                    {
                        filedata = null;
                        using PdfDocument document = bitmapFrame.MedianFilterBitmap(PdfMedianValue).GeneratePdf(null, Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution);
                        SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = $"{Translation.GetResStringValue("PAGENUMBER")} {pdfViewer.Sayfa}.pdf" };
                        if (saveFileDialog.ShowDialog() != true)
                        {
                            return;
                        }
                        PdfToolBarControlIsEnabled = false;
                        document.Save(saveFileDialog.FileName);
                        PdfMedianValue = 0;
                    }
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => PdfMedianValue > 0 && parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        ExtractMultiplePdfFile = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is not Viewer pdfViewer || !Viewer.IsValidPdfFile(pdfViewer.PdfFilePath))
                    {
                        return;
                    }
                    string savefolder = ToolBox.CreateSaveFolder("SPLIT");
                    List<string> files = [];
                    List<PdfData> currentpages = PdfPages?.Where(currentpage => currentpage.Selected).ToList();
                    double pagecount = currentpages.Count;
                    PdfToolBarControlIsEnabled = false;
                    for (int i = 0; i < pagecount; i++)
                    {
                        PdfData currentpage = currentpages[i];
                        string savefilename = $@"{savefolder}\{Path.GetFileNameWithoutExtension(pdfViewer.PdfFilePath)} {currentpage.PageNumber}.pdf";
                        await PdfPageRangeSaveFileAsync(pdfViewer.PdfFilePath, savefilename, currentpage.PageNumber, currentpage.PageNumber);
                        files.Add(savefilename);
                        Scanner.PdfSaveProgressValue = (i + 1) / pagecount;
                    }
                    if (currentpages.Count > 1 && MessageBox.Show($"{Translation.GetResStringValue("MERGEPDF")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        using PdfDocument mergedPdf = files.ToArray().MergePdf();
                        mergedPdf.Save($@"{savefolder}\{Path.GetFileNameWithoutExtension(pdfViewer.PdfFilePath)} {Translation.GetResStringValue("MERGE")}.pdf");
                    }
                    Scanner.PdfSaveProgressValue = 0;
                    WebAdreseGit.Execute(savefolder);
                    files = null;
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => PdfPages?.Any(z => z.Selected) == true);

        LoadArrangedPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is not Viewer pdfViewer || !Viewer.IsValidPdfFile(pdfViewer.PdfFilePath))
                    {
                        return;
                    }
                    string oldpdfpath = pdfViewer.PdfFilePath;
                    string savefolder = Path.GetTempPath();
                    List<string> files = [];
                    List<PdfData> currentpages = PdfPages?.ToList();
                    double pagecount = currentpages.Count;
                    PdfToolBarControlIsEnabled = false;
                    for (int i = 0; i < pagecount; i++)
                    {
                        PdfData currentpage = currentpages[i];
                        string savefilename = $@"{savefolder}\{Path.GetFileNameWithoutExtension(pdfViewer.PdfFilePath)} {currentpage.PageNumber}.pdf";
                        await PdfPageRangeSaveFileAsync(pdfViewer.PdfFilePath, savefilename, currentpage.PageNumber, currentpage.PageNumber);
                        files.Add(savefilename);
                        Scanner.PdfSaveProgressValue = (i + 1) / pagecount;
                    }
                    using PdfDocument mergedPdf = files.ToArray().MergePdf();
                    mergedPdf.Save(pdfViewer.PdfFilePath);
                    Scanner.PdfSaveProgressValue = 0;
                    pdfViewer.PdfFilePath = null;
                    pdfViewer.PdfFilePath = oldpdfpath;
                    pdfViewer.Sayfa = 1;
                    LoadPdfExtractFile?.Execute(pdfViewer);
                    files.Where(z => File.Exists(z)).ToList().ForEach(z => File.Delete(z));
                    files = null;
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => PdfPages?.Count > 1);

        RemoveArrangedPdfFile = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is Viewer pdfViewer &&
                    Viewer.IsValidPdfFile(pdfViewer.PdfFilePath) &&
                    MessageBox.Show($"{Translation.GetResStringValue("REMOVESELECTED")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        string oldpdfpath = pdfViewer.PdfFilePath;
                        using PdfDocument inputDocument = PdfReader.Open(pdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                        foreach (PdfData item in PdfPages?.Where(z => z.Selected)?.OrderByDescending(z => z.PageNumber))
                        {
                            inputDocument.Pages.RemoveAt(item.PageNumber - 1);
                        }
                        PdfToolBarControlIsEnabled = false;
                        inputDocument.Save(pdfViewer.PdfFilePath);
                        pdfViewer.PdfFilePath = null;
                        pdfViewer.PdfFilePath = oldpdfpath;
                        pdfViewer.Sayfa = 1;
                        LoadPdfExtractFile?.Execute(pdfViewer);
                    }
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => PdfPages?.Count(z => z.Selected) > 0 && PdfPages?.All(z => z.Selected) == false);

        RemoveCurrentPdfPage = new RelayCommand<object>(
            parameter =>
            {
                try
                {
                    if (parameter is Viewer pdfViewer &&
                    Viewer.IsValidPdfFile(pdfViewer.PdfFilePath) &&
                    MessageBox.Show($"{Translation.GetResStringValue("REMOVESELECTED")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        string oldpdfpath = pdfViewer.PdfFilePath;
                        using PdfDocument inputDocument = PdfReader.Open(pdfViewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                        inputDocument.Pages.RemoveAt(pdfViewer.Sayfa - 1);
                        PdfToolBarControlIsEnabled = false;
                        inputDocument.Save(pdfViewer.PdfFilePath);
                        pdfViewer.PdfFilePath = null;
                        pdfViewer.PdfFilePath = oldpdfpath;
                        pdfViewer.Sayfa = 1;
                    }
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfViewer && pdfViewer.ToplamSayfa > 1 && PdfToolBarControlIsEnabled);

        AddPageNumber = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is not Viewer pdfviewer)
                    {
                        return;
                    }
                    PdfToolBarControlIsEnabled = false;
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    using PdfDocument pdfdocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                    if (pdfdocument is null)
                    {
                        return;
                    }
                    XFont font = new(PdfWatermarkFont, Scanner.PdfPageNumberSize);
                    XBrush brush = new XSolidBrush(XColor.FromKnownColor(Scanner.PdfPageNumberAlignTextColor));
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        for (int i = 0; i < pdfdocument.PageCount; i++)
                        {
                            PdfPage pageall = pdfdocument.Pages[i];
                            using XGraphics gfxall = XGraphics.FromPdfPage(pageall, XGraphicsPdfPageOptions.Append);
                            double textallwidth = gfxall.MeasureString(GetPdfBatchNumberString(i), font).Width;
                            if (i % 2 == 0 && IsOdd)
                            {
                                gfxall.DrawText(brush, GetPdfBatchNumberString(i), PdfWatermarkFont, PdfGeneration.GetPdfTextLayout(pageall, textallwidth)[0], PdfGeneration.GetPdfTextLayout(pageall, textallwidth)[1], Scanner.PdfPageNumberSize);
                            }
                            if (i % 2 == 1 && IsEven)
                            {
                                gfxall.DrawText(brush, GetPdfBatchNumberString(i), PdfWatermarkFont, PdfGeneration.GetPdfTextLayout(pageall, textallwidth)[0], PdfGeneration.GetPdfTextLayout(pageall, textallwidth)[1], Scanner.PdfPageNumberSize);
                            }
                        }
                        pdfdocument.Save(pdfviewer.PdfFilePath);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = oldpdfpath;
                        pdfviewer.Sayfa = 1;
                        return;
                    }

                    PdfPage page = pdfdocument.Pages[pdfviewer.Sayfa - 1];
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    double textwidth = gfx.MeasureString(GetPdfBatchNumberString(pdfviewer.Sayfa), font).Width;
                    gfx.DrawText(brush, GetPdfBatchNumberString(pdfviewer.Sayfa - 1), PdfWatermarkFont, PdfGeneration.GetPdfTextLayout(page, textwidth)[0], PdfGeneration.GetPdfTextLayout(page, textwidth)[1], Scanner.PdfPageNumberSize);
                    pdfdocument.Save(pdfviewer.PdfFilePath);
                    pdfviewer.Source = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath) && (IsEven || IsOdd));

        FlipPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not Viewer pdfviewer)
                {
                    return;
                }

                try
                {
                    PdfToolBarControlIsEnabled = false;
                    int currentpage = pdfviewer.Sayfa;
                    using PdfDocument pdfdocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                    if (pdfdocument is null)
                    {
                        return;
                    }

                    BitmapImage bitmapImage = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, currentpage, pdfviewer.Dpi);
                    PdfPage page = pdfdocument.Pages[currentpage - 1];
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Replace);
                    XPoint center = new(page.Width / 2, page.Height / 2);
                    gfx.ScaleAtTransform(Keyboard.Modifiers == ModifierKeys.Alt ? 1 : -1, Keyboard.Modifiers == ModifierKeys.Alt ? -1 : 1, center);
                    using XImage image = XImage.FromBitmapSource(bitmapImage);
                    gfx.DrawImage(image, 0, 0, page.Width, page.Height);
                    pdfdocument.Save(pdfviewer.PdfFilePath);
                    pdfviewer.Source = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                    bitmapImage = null;
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath) && pdfViewer.Source?.Width < pdfViewer.Source?.Height);

        BlackAndWhitePdfPage = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is Viewer pdfviewer && MessageBox.Show($"{Translation.GetResStringValue("BW")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        PdfToolBarControlIsEnabled = false;
                        BitmapImage bitmapImage = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                        using Bitmap img = bitmapImage.BitmapSourceToBitmap();
                        BitmapImage image = img.ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Tiff);
                        using PdfDocument pdfdocument = RenderPdfPage(pdfviewer, image, pdfviewer.Sayfa);
                        pdfdocument.Save(pdfviewer.PdfFilePath);
                        pdfviewer.Source = image;
                        image = null;
                        bitmapImage = null;
                    }
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        InvertPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    if (parameter is Viewer pdfviewer && MessageBox.Show($"{Translation.GetResStringValue("INVERTCOLOR")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        PdfToolBarControlIsEnabled = false;
                        BitmapImage bitmapImage = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                        BitmapImage image = bitmapImage.InvertBitmap().ToBitmapImage();
                        using PdfDocument pdfdocument = RenderPdfPage(pdfviewer, image, pdfviewer.Sayfa);
                        pdfdocument.Save(pdfviewer.PdfFilePath);
                        pdfviewer.Source = image;
                        image = null;
                        bitmapImage = null;
                    }
                }
                finally
                {
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        ClearPdfHistory = new RelayCommand<object>(
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("CLEARLIST")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Settings.Default.PdfLoadHistory.Clear();
                    Settings.Default.Save();
                    Settings.Default.Reload();
                }
            },
            parameter => Settings.Default.PdfLoadHistory.Count > 0);

        ResetCrop = new RelayCommand<object>(
            parameter =>
            {
                Settings.Default.Left = 0;
                Settings.Default.Top = 0;
                Settings.Default.Bottom = PageHeight;
                Settings.Default.Right = PageWidth;
                Settings.Default.Save();
            },
            parameter => true);

        CompressPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfviewer &&
                MessageBox.Show($"{Translation.GetResStringValue("FILE")} {Translation.GetResStringValue("COMPRESS")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    try
                    {
                        string filepath = pdfviewer.PdfFilePath;
                        long oldSize = new FileInfo(filepath).Length;
                        PdfCompressor pdfcompressor = new() { EncodeAsJb2File = EncodeAsJb2, UseMozJpeg = UseMozJpeg, Dpi = PdfCompressDpi, Quality = PdfQuality, };
                        pdfcompressor.ProgressChanged += (_, e) => Dispatcher.Invoke(() => PdfImportControlProgressValue = e);
                        PdfToolBarControlIsEnabled = false;
                        using PdfDocument pdfdocument = await pdfcompressor.Compress(filepath);
                        if (pdfdocument is null)
                        {
                            return;
                        }
                        string originalPath = filepath;
                        string tempPath = Path.Combine(Path.GetDirectoryName(originalPath), $"{Path.GetFileNameWithoutExtension(originalPath)}_temp.pdf");
                        pdfdocument.Save(tempPath);
                        long newSize = new FileInfo(tempPath).Length;
                        if (newSize >= oldSize)
                        {
                            try
                            {
                                File.Delete(tempPath);
                            }
                            catch
                            {
                            }
                            ExtendedMessageBox errormessagebox = new() { YesIconType = IconType.Error };
                            errormessagebox.ShowDialog(
                                Window.GetWindow(this),
                                $"{Translation.GetResStringValue("ERROR")}\n{Translation.GetResStringValue("ORİGİNAL")}:{oldSize / 1048576d:F} MB\n{Translation.GetResStringValue("DOCUMENT")} {Translation.GetResStringValue("BIG")}:{newSize / 1048576d:F} MB",
                                AppName);
                            return;
                        }
                        File.Delete(originalPath);
                        File.Move(tempPath, originalPath);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = originalPath;
                        double compressionratio = (double)newSize / oldSize;
                        ExtendedMessageBox extendedMessageBox = new();
                        extendedMessageBox.ShowDialog(Window.GetWindow(this), $"{Translation.GetResStringValue("SUCCESS")}\n{compressionratio:P2} {newSize / 1048576d:F} MB", AppName);
                    }
                    finally
                    {
                        PdfToolBarControlIsEnabled = true;
                    }
                }
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ResetPreviewSize = new RelayCommand<object>(parameter => Settings.Default.PreviewWidth = 155, parameter => Settings.Default?.PreviewWidth != 155);

        ApplyCropCurrentImage = new RelayCommand<object>(
            parameter =>
            {
                bool altkeypressed = Keyboard.Modifiers == ModifierKeys.Alt;
                BitmapFrame bitmapframe = BitmapFrame.Create(GenerateCroppedImage(SeçiliResim.Resim, Settings.Default.Top, Settings.Default.Left, Settings.Default.Bottom, Settings.Default.Right));
                bitmapframe.Freeze();
                if (altkeypressed)
                {
                    SeçiliResim.Resim = bitmapframe;
                    return;
                }
                Scanner?.Resimler?.Add(new ScannedImage() { Resim = bitmapframe });
            },
            parameter => SeçiliResim is not null && PageWidth == SeçiliResim.Resim.PixelWidth && PageHeight == SeçiliResim.Resim.PixelHeight && Settings.Default.Left != Settings.Default.Right && Settings.Default.Top != Settings.Default.Bottom);

        ApplyCropAllImages = new RelayCommand<object>(
            parameter =>
            {
                foreach (ScannedImage item in GetSelectedImages())
                {
                    int index = Scanner.Resimler?.IndexOf(item) ?? -1;
                    if (index < 0)
                    {
                        return;
                    }
                    bool process = TrimPage switch
                    {
                        0 => index % 2 == 0,
                        1 => index % 2 == 1,
                        _ => true
                    };
                    if (!process)
                    {
                        continue;
                    }

                    undoStack.Push(new DeletedImageEntry(new ScannedImage() { Resim = item.Resim }, index));
                    CanUndoImage = undoStack.Count > 0;
                    BitmapFrame bitmapframe = BitmapFrame.Create(GenerateCroppedImage(item.Resim, Settings.Default.Top, Settings.Default.Left, Settings.Default.Bottom, Settings.Default.Right));
                    bitmapframe.Freeze();
                    item.Resim = bitmapframe;
                }
                if (PrepareCropCurrentImage.CanExecute(null))
                {
                    PrepareCropCurrentImage.Execute(null);
                }
            },
            parameter =>
            {
                List<ScannedImage> distinct = GetSelectedImages()?.Distinct(new ImageWidthHeightComparer()).ToList();
                DistinctImages = $"{string.Join(",", distinct?.Select(z => z.Index))} {Translation.GetResStringValue("DOCUMENT")}";
                SelectedImageWidthHeightIsEqual = IgnoreImageWidthHeight || distinct?.Count() == 1;
                return SeçiliResim is not null &&
                Scanner.Resimler.Count(z => z.Seçili) > 1 &&
                SelectedImageWidthHeightIsEqual &&
                PageWidth == SeçiliResim.Resim.PixelWidth &&
                PageHeight == SeçiliResim.Resim.PixelHeight &&
                Settings.Default.Left != Settings.Default.Right &&
                Settings.Default.Top != Settings.Default.Bottom;
            });

        PrepareCropCurrentImage = new RelayCommand<object>(
            parameter =>
            {
                PageWidth = SeçiliResim.Resim.PixelWidth;
                PageHeight = SeçiliResim.Resim.PixelHeight;
                ResetCrop.Execute(null);
            },
            parameter => SeçiliResim is not null);

        AddSplitListsIndex = new RelayCommand<object>(
            parameter =>
            {
                if (SeçiliResim is null)
                {
                    return;
                }
                if (ImagesSplitLists?.Contains(SeçiliResim.Index) == false)
                {
                    ImagesSplitLists?.Add(SeçiliResim.Index);
                }
            },
            parameter => AnyImageExist());

        SplitImagesByIndex = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt && ImagesSplitLists.Count > 0)
                {
                    SplittedIndexImages = SplitArray(Scanner.Resimler.ToArray(), [ .. ImagesSplitLists ]);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(TextSplitList))
                {
                    SplittedIndexImages = SplitArray(Scanner.Resimler.ToArray(), [ .. TextSplitList.Split(',').Select(z => int.TryParse(z, out int result) ? result : 0) ]);
                }
            },
            parameter => Scanner?.Resimler?.Count > 1);

        SelectSplittedIndexImages = new RelayCommand<object>(
            parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in parameter as ScannedImage[])
                {
                    item.Seçili = true;
                }
            },
            parameter => AnyImageExist() && parameter is ScannedImage[] scannedimages && scannedimages?.Length > 0);

        RemoveSplitListsIndex = new RelayCommand<object>(parameter => ImagesSplitLists?.Remove((int)parameter), parameter => true);

        DetectEmptyPages = new RelayCommand<object>(
            parameter => Parallel.ForEach(
                Scanner.Resimler,
                item =>
                {
                    using Bitmap bmp = item.Resim.Resize(0.1).BitmapSourceToBitmap();
                    item.Seçili = bmp.IsEmptyPage(Settings.Default.EmptyThreshold);
                }),
            parameter => Scanner?.Resimler?.Any() == true);

        AddActiveVisibleContentImage = new RelayCommand<object>(
            parameter =>
            {
                ScrollViewer scrollviewer = ImgViewer?.FindVisualChildren<ScrollViewer>()?.First();
                if (scrollviewer is not null)
                {
                    System.Windows.Controls.Image image = scrollviewer.Content as System.Windows.Controls.Image;
                    BitmapFrame bitmapFrame = BitmapFrame.Create(image?.ToRenderTargetBitmap(scrollviewer.ViewportWidth, scrollviewer.ViewportHeight));
                    bitmapFrame?.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    Scanner?.Resimler?.Insert(SeçiliResim.Index, scannedImage);
                }
            },
            parameter => SeçiliResim is not null);

        GridSplitterMouseDoubleClick = new RelayCommand<object>(
            parameter =>
            {
                TwainGuiControlLength = new GridLength(3, GridUnitType.Star);
                DocumentGridLength = new GridLength(5, GridUnitType.Star);
            },
            parameter => true);

        GridSplitterMouseRightButtonDown = new RelayCommand<object>(
            parameter =>
            {
                TwainGuiControlLength = new GridLength(1, GridUnitType.Star);
                DocumentGridLength = new GridLength(0, GridUnitType.Star);
            },
            parameter => true);

        LoadMiniDraw = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is object[] obj && obj[0] is DrawControl drawControl && obj[1] is BitmapFrame bitmapFrame)
                {
                    drawControl.TemporaryImage = bitmapFrame;
                    drawControl.FitImage.Execute(null);
                }
            },
            parameter => true);

        MoveToNextTabCommand = new RelayCommand<object>(parameter => SelectedTabIndex = (SelectedTabIndex + 1) % TbCtrl.Items.Count, parameter => true);

        MoveToPreviousTabCommand = new RelayCommand<object>(parameter => SelectedTabIndex = SelectedTabIndex > 0 ? SelectedTabIndex - 1 : TbCtrl.Items.Count - 1, parameter => true);

        PdfViewerFullScreen = new RelayCommand<object>(
            parameter =>
            {
                string file = parameter as string;
                if (!File.Exists(file))
                {
                    return;
                }
                PdfImportViewerControl pdfImportViewerControl = new();
                if (Path.GetExtension(file.ToLowerInvariant()) == ".pdf")
                {
                    pdfImportViewerControl.PdfViewer.PdfFilePath = file;
                }
                if (Path.GetExtension(file.ToLowerInvariant()) == ".eyp")
                {
                    pdfImportViewerControl.PdfViewer.EypFilePath = file;
                }
                pdfImportViewerControl.DataContext = this;
                maximizedWindow = new()
                {
                    Owner = Window.GetWindow(this),
                    WindowState = WindowState.Maximized,
                    ShowInTaskbar = true,
                    Title = file,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    UseLayoutRounding = true,
                    Icon = ShellIcon.GetFileIconBySize(file, 0)
                };
                maximizedWindow.Closed += (s, e) =>
                                          {
                                              maximizedWindow = null;
                                              pdfImportViewerControl.PdfViewer.Source = null;
                                              ClosedPdfFilePath = pdfImportViewerControl.PdfViewer.PdfFilePath;
                                              pdfImportViewerControl.PdfViewer.PdfFilePath = null;
                                              pdfImportViewerControl.PdfViewer.EypAttachments = null;
                                              pdfImportViewerControl.PdfViewer.EypNonSuportedAttachments = null;
                                              pdfImportViewerControl.PdfViewer.ToplamSayfa = 0;
                                              SayfaBaşlangıç = 1;
                                              SayfaBitiş = 1;
                                              RefreshDocumentList = true;
                                          };
                maximizedWindow.Content = pdfImportViewerControl;
                _ = maximizedWindow.ShowDialog();
            },
            parameter => true);

        ImageViewerFullScreen = new RelayCommand<object>(
            parameter =>
            {
                ImageViewer imageViewer = new() { PanoramaButtonVisibility = Visibility.Collapsed, PrintButtonVisibility = Visibility.Visible, ImageFilePath = parameter as string };
                maximizedWindow = new()
                {
                    Owner = Window.GetWindow(this),
                    WindowState = WindowState.Maximized,
                    ShowInTaskbar = true,
                    Title = AppName,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    UseLayoutRounding = true,
                    Icon = ShellIcon.GetFileIconBySize(imageViewer.ImageFilePath, 0)
                };
                maximizedWindow.Closed += (s, e) =>
                                          {
                                              maximizedWindow = null;
                                              imageViewer?.Dispose();
                                              imageViewer.ImageFilePath = null;
                                          };
                maximizedWindow.Content = imageViewer;
                _ = maximizedWindow.ShowDialog();
            },
            parameter => true);

        XmlViewerFullScreen = new RelayCommand<object>(
            parameter =>
            {
                XmlViewerControl xmlViewerControl = new();
                XmlViewerControlModel.SetXmlContent(xmlViewerControl, parameter as string);
                maximizedWindow = new()
                {
                    Owner = Window.GetWindow(this),
                    WindowState = WindowState.Maximized,
                    ShowInTaskbar = true,
                    Title = AppName,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    UseLayoutRounding = true,
                    Icon = ShellIcon.GetFileIconBySize(parameter as string, 0)
                };
                maximizedWindow.Closed += (s, e) =>
                                          {
                                              maximizedWindow = null;
                                              XmlViewerControlModel.SetXmlContent(xmlViewerControl, null);
                                          };
                maximizedWindow.Content = xmlViewerControl;
                _ = maximizedWindow.ShowDialog();
            },
            parameter => true);

        VideoViewerFullScreen = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not Grid grid)
                {
                    return;
                }
                mediaViewer.ContextMenuEnabled = true;
                mediaViewer.ControlVisible = Visibility.Collapsed;
                grid.Children.Remove(mediaViewer);
                maximizedWindow = new()
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    ResizeMode = ResizeMode.NoResize,
                    WindowState = WindowState.Maximized,
                    ShowInTaskbar = false,
                    Title = AppName,
                    Owner = Window.GetWindow(this),
                    UseLayoutRounding = true,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                maximizedWindow.KeyDown += (s, e) =>
                                           {
                                               if (e.Key == Key.Escape)
                                               {
                                                   maximizedWindow?.Close();
                                               }
                                           };
                maximizedWindow.Closed += (s, e) =>
                                          {
                                              maximizedWindow.Content = null;
                                              mediaViewer.ContextMenuEnabled = false;
                                              mediaViewer.ControlVisible = Visibility.Visible;
                                              mediaViewer.SliderControlVisible = Visibility.Visible;
                                              _ = grid.Children.Add(mediaViewer);
                                          };
                maximizedWindow.Content = mediaViewer;
                _ = maximizedWindow.ShowDialog();
            },
            parameter => true);

        VideodanResimYükle = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is MediaViewer mediaViewer && mediaViewer.FindName("grid") is Grid grid)
                {
                    using MemoryStream ms = new(grid.ToRenderTargetBitmap().ToTiffJpegByteArray(Format.Jpg));
                    BitmapFrame bitmapFrame = ms.GenerateBitmapFrameFromMemoryStream();
                    bitmapFrame.Freeze();
                    Scanner?.Resimler?.Add(new ScannedImage { Resim = bitmapFrame });
                }
            },
            parameter => parameter is MediaViewer mediaViewer && File.Exists(mediaViewer.MediaDataFilePath));

        XpsDosyasındanResimYükle = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is XpsViewer xpsViewer)
                {
                    XpsFileHandler xpsFileHandler = new();
                    BitmapFrame bitmapFrame = await xpsFileHandler.LoadXpsSinglePagesAsync(xpsViewer.XpsDataFilePath, xpsViewer.PageNumber - 1);
                    bitmapFrame?.Freeze();
                    Scanner?.Resimler?.Add(new ScannedImage { Resim = bitmapFrame });
                }
            },
            parameter => parameter is XpsViewer xpsViewer && File.Exists(xpsViewer.XpsDataFilePath));

        Jb2DosyasındanResimYükle = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is Jb2ZipImageViewer jb2ZipViewer && jb2ZipViewer.Source is BitmapFrame bitmapFrame)
                {
                    bitmapFrame?.Freeze();
                    Scanner?.Resimler?.Add(new ScannedImage { Resim = bitmapFrame });
                }
            },
            parameter => parameter is Jb2ZipImageViewer jb2ZipViewer && File.Exists(jb2ZipViewer.ImageFilePath));

        SaveDocxFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is ScannedImage scannedImage && scannedImage is not null)
                {
                    SaveFileDialog saveFileDialog = new() { Filter = "Docx Dosyası (*.docx)|*.docx", FileName = $"{Scanner.SaveFileName}{scannedImage.Index}" };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        ObservableCollection<OcrData> ocrtext = ConvertPdfCharacterToOcrData(scannedImage.Resim.PixelHeight, scannedImage.Resim.PixelWidth, scannedImage.GetPdfCharacterInformations, Settings.Default.ImgLoadResolution, true);
                        using DocX document = DocX.Create(saveFileDialog.FileName);
                        Xceed.Document.NET.Paragraph p = document.InsertParagraph();
                        foreach (OcrData item in ocrtext)
                        {
                            _ = p.Append(item.Text.Replace("\uFFFE", ""));
                            _ = p.FontSize(Math.Round(item.FontSize));
                        }
                        document.Save();
                    }
                }
            },
            parameter => true);

        PrintSelectedDocuments = new RelayCommand<object>(
            parameter =>
            {
                PrintDialog printdialog = new() { PageRangeSelection = PageRangeSelection.AllPages, UserPageRangeEnabled = false, MaxPage = (uint)GetSelectedImagesCount(), MinPage = 1 };
                if (printdialog.ShowDialog() == true)
                {
                    FixedDocument fixedDocument = ImageViewer.PrintMultipleFixedDocumentPages(printdialog, 0, (int)(GetSelectedImagesCount() - 1), GetSelectedImages().Select(z => z.Resim), PrintDpi);
                    XpsDocumentWriter xpsWriter = PrintQueue.CreateXpsDocumentWriter(printdialog.PrintQueue);
                    xpsWriter.WriteAsync(fixedDocument, printdialog.PrintTicket);
                    fixedDocument = null;
                }
            },
            parameter => GetSelectedImagesCount() > 0);

        PrintEypPackageSelectedDocuments = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not string filepath)
                {
                    return;
                }

                PrintDialog printdialog = null;
                LocalPrintServer localPrintServer = new();
                string extension = Path.GetExtension(filepath)?.ToLowerInvariant();
                if (imagefileextensions.Contains(extension))
                {
                    printdialog = new() { PageRangeSelection = PageRangeSelection.AllPages, UserPageRangeEnabled = false, MaxPage = 1, MinPage = 1, PrintQueue = localPrintServer.GetPrintQueue(SelectedPrinter) };
                    BitmapFrame bitmapframe = BitmapFrame.Create(new Uri(filepath));
                    bitmapframe?.Freeze();
                    FixedDocument fixedDocument = ImageViewer.PrintMultipleFixedDocumentPages(printdialog, 0, 0, [ bitmapframe ], PrintDpi);
                    XpsDocumentWriter xpsWriter = PrintQueue.CreateXpsDocumentWriter(printdialog.PrintQueue);
                    xpsWriter.WriteAsync(fixedDocument, printdialog.PrintTicket);
                    fixedDocument = null;
                    bitmapframe = null;
                    return;
                }
                if (extension == ".eyp")
                {
                    List<string> eypfilelist = EypFileExtract(filepath);
                    filepath = eypfilelist?.FirstOrDefault(z => Path.GetExtension(z.ToLowerInvariant()) == ".pdf");
                }
                if (!Viewer.IsValidPdfFile(filepath))
                {
                    return;
                }

                using PdfiumViewer.PdfDocument pdfDocument = PdfiumViewer.PdfDocument.Load(filepath);
                printdialog = new() { PageRangeSelection = PageRangeSelection.AllPages, UserPageRangeEnabled = false, MaxPage = (uint)pdfDocument.PageCount, MinPage = 1, PrintQueue = localPrintServer.GetPrintQueue(SelectedPrinter) };
                await Viewer.GenerateDocument(printdialog, pdfDocument, (int)printdialog.MinPage, (int)printdialog.MaxPage, PrintDpi);
            },
            parameter => true);

        RemoveVerticalLines = new RelayCommand<object>(
            async parameter =>
            {
                try
                {
                    int count = GetSelectedImages().Count;
                    IProgress<double> progress = new Progress<double>(p => AllRotateProgressValue = p);
                    RemoveVerticalLinesIsRunning = true;
                    await Task.Run(
                        () =>
                        {
                            for (int i = 0; i < count; i++)
                            {
                                ScannedImage item = GetSelectedImages()[i];
                                WriteableBitmap wbmp = new(item.Resim);
                                WriteableBitmap bitmapWithoutVerticalLines = wbmp.RemoveVerticalLines(Scanner.VerticalLineThreshold);
                                bitmapWithoutVerticalLines.Freeze();
                                BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapWithoutVerticalLines.ToBitmapImage());
                                bitmapFrame.Freeze();
                                item.Resim = bitmapFrame;
                                bitmapFrame = null;
                                wbmp = null;
                                bitmapWithoutVerticalLines = null;
                                progress.Report((i + 1) / (double)count);
                            }
                        });
                }
                finally
                {
                    RemoveVerticalLinesIsRunning = false;
                }
            },
            parameter => GetSelectedImages().Count > 0 && !RemoveVerticalLinesIsRunning);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static Cursor DragCursor { get; set; }

    public RelayCommand<object> AddActiveVisibleContentImage { get; }

    public ICommand AddAllFileToControlPanel { get; }

    public ICommand AddFromClipBoard { get; }

    public ICommand AddPageNumber { get; }

    public ICommand AddPdfAttachmentFile { get; }

    public RelayCommand<object> AddSplitListsIndex { get; }

    public double AllImageRotationAngle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AllImageRotationAngle));
            }
        }
    }

    public double AllRotateProgressValue
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AllRotateProgressValue));
            }
        }
    }

    public RelayCommand<object> ApplyCropAllImages { get; }

    public RelayCommand<object> ApplyCropCurrentImage { get; }

    public ICommand ApplyPdfMedianFilter { get; }

    public RelayCommand<object> AutoDeskewImage { get; }

    public bool AutoRotateIsWorking
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AutoRotateIsWorking));
            }
        }
    }

    public RelayCommand<object> BlackAndWhitePdfPage { get; }

    public byte[] CameraQRCodeData
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CameraQRCodeData));
            }
        }
    }

    public RelayCommand<object> CancelLoadFile { get; }

    public RelayCommand<object> CancelScan { get; }

    public bool CanUndoImage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CanUndoImage));
            }
        }
    }

    public ICommand ClearPdfHistory { get; }

    public string ClosedPdfFilePath
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ClosedPdfFilePath));
            }
        }
    }

    public ICommand ClosePdfFile { get; }

    public List<Tuple<string, int, double, bool, double>> CompressionProfiles => [ new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Low, false, (double)Quality.Low), new Tuple<string, int, double, bool, double>(
        Translation.GetResStringValue("COLOR"),
        2,
        (double)Resolution.Low,
        true,
        (double)Quality.Low), new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Medium, false, (double)Quality.Medium), new Tuple<string, int, double, bool, double>(
        Translation.GetResStringValue("COLOR"),
        2,
        (double)Resolution.Medium,
        true,
        (double)Quality.Medium), new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Standard, false, (double)Quality.Standard), new Tuple<string, int, double, bool, double>(
        Translation.GetResStringValue("COLOR"),
        2,
        (double)Resolution.Standard,
        true,
        (double)Quality.Standard), new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.High, false, (double)Quality.High), new Tuple<string, int, double, bool, double>(
        Translation.GetResStringValue("COLOR"),
        2,
        (double)Resolution.High,
        true,
        (double)Quality.High), new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Ultra, false, (double)Quality.Ultra), new Tuple<string, int, double, bool, double>(
        Translation.GetResStringValue("COLOR"),
        2,
        (double)Resolution.Ultra,
        true,
        (double)Quality.Ultra), new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Low, false, (double)Quality.Low), new Tuple<string, int, double, bool, double>(
        Translation.GetResStringValue("COLOR"),
        2,
        (double)Resolution.Medium,
        false,
        (double)Quality.Medium), new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Standard, false, (double)Quality.Standard), new Tuple<string, int, double, bool, double>(
        Translation.GetResStringValue("COLOR"),
        2,
        (double)Resolution.High,
        false,
        (double)Quality.High), new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Ultra, false, (double)Quality.Ultra) ];

    public bool CompressorDpiSnap
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CompressorDpiSnap));
            }
        }
    } = true;

    public RelayCommand<object> CompressPdfFile { get; }

    public RelayCommand<object> CopyCurrentImageToClipBoard { get; }

    public RelayCommand<object> CopyCurrentImageToImageEditor { get; }

    public ICommand CopyPdfBitmapFile { get; }

    public int CropAllMargin
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropAllMargin));
            }
        }
    }

    public int CropAllMaximumWidth
    {
        get => Math.Min(PageHeight, PageWidth) / 2;
        set
        {
            if (cropAllMaximumWidth != value)
            {
                cropAllMaximumWidth = value;
                OnPropertyChanged(nameof(CropAllMaximumWidth));
            }
        }
    }

    public bool CropAutoCropChecked
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropAutoCropChecked));
            }
        }
    } = Settings.Default.AutoCropImage || Settings.Default.CropScan;

    public int CropBottomMargin
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropBottomMargin));
            }
        }
    }

    public CroppedBitmap CroppedOcrBitmap
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CroppedOcrBitmap));
            }
        }
    }

    public int CropRightMargin
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropRightMargin));
            }
        }
    }

    public double CustomDeskewAngle
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CustomDeskewAngle));
            }
        }
    }

    public ICommand CycleSelectedDocuments { get; }

    public byte[] DataBaseQrData
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DataBaseQrData));
            }
        }
    }

    public List<ObservableCollection<OcrData>> DataBaseTextData
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DataBaseTextData));
            }
        }
    }

    public bool DataBaseTextDataCompleted
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DataBaseTextDataCompleted));
            }
        }
    }

    public int DecodeHeight
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DecodeHeight));
            }
        }
    }

    public RelayCommand<object> DetectEmptyPages { get; }

    public string DistinctImages
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DistinctImages));
            }
        }
    }

    public GridLength DocumentGridLength
    {
        get => documentGridLength;

        set
        {
            if (documentGridLength != value)
            {
                documentGridLength = value;
                OnPropertyChanged(nameof(DocumentGridLength));
            }
        }
    }

    public bool DocumentPreviewIsExpanded
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DocumentPreviewIsExpanded));
            }
        }
    } = true;

    public bool DpiSnap
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DpiSnap));
            }
        }
    } = true;

    public bool DragMoveStarted
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DragMoveStarted));
            }
        }
    }

    public RelayCommand<object> DuplicateSelectedImage { get; }

    public bool EncodeAsJb2
    {
        get => encodeAsJb2;
        set
        {
            if (encodeAsJb2 != value)
            {
                encodeAsJb2 = value;
                if (value)
                {
                    encodeAsWebp = false;
                    OnPropertyChanged(nameof(EncodeAsWebp));
                }
                OnPropertyChanged(nameof(EncodeAsJb2));
            }
        }
    }

    public bool EncodeAsWebp
    {
        get => encodeAsWebp;
        set
        {
            if (encodeAsWebp != value)
            {
                encodeAsWebp = value;
                if (value)
                {
                    encodeAsJb2 = false;
                    OnPropertyChanged(nameof(EncodeAsJb2));
                }
                OnPropertyChanged(nameof(EncodeAsWebp));
            }
        }
    }

    public string Error => string.Empty;

    public ICommand ExploreFile { get; }

    public ICommand ExtractMultiplePdfFile { get; }

    public RelayCommand<object> ExtractNugetPackage { get; }

    public ICommand FastScanImage { get; }

    public Task FileLoadTask
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileLoadTask));
            }
        }
    }

    public RelayCommand<object> FirstLastGroup { get; }

    public RelayCommand<object> FirstLastSortSequenceData { get; }

    public RelayCommand<object> FlipPdfPage { get; }

    public RelayCommand<object> GotoDocumentIndex { get; }

    public RelayCommand<object> GridSplitterMouseDoubleClick { get; }

    public RelayCommand<object> GridSplitterMouseRightButtonDown { get; }

    public int GroupSplitCount
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(GroupSplitCount));
            }
        }
    } = 2;

    public bool HelpIsOpened
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(HelpIsOpened));
            }
        }
    }

    public bool IgnoreImageWidthHeight
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IgnoreImageWidthHeight));
            }
        }
    }

    public ObservableCollection<int> ImagesSplitLists { get; set; } = [];

    public RelayCommand<object> ImageViewerFullScreen { get; }

    public byte[] ImgData
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ImgData));
            }
        }
    }

    public ICommand InsertClipBoardImage { get; }

    public ICommand InsertFileNamePlaceHolder { get; }

    public RelayCommand<object> InvertImage { get; }

    public RelayCommand<object> InvertPdfPage { get; }

    public RelayCommand<object> InvertSelectedImage { get; }

    public bool IsAdministrator
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            field = principal.IsInRole(WindowsBuiltInRole.Administrator);
            return field;
        }
    }

    public bool IsEven
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsEven));
            }
        }
    } = true;

    public bool IsOdd
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsOdd));
            }
        }
    } = true;

    public RelayCommand<object> Jb2DosyasındanResimYükle { get; }

    public IEnumerable<int> Jb2SaturationValues { get; private set; } = Enumerable.Range(3, 49).Where(n => n % 2 != 0);

    public IEnumerable<int> Jb2ThresholdValues { get; private set; } = Enumerable.Range(0, 51);

    public ICommand KayıtYoluBelirle { get; }

    public ICommand ListeTemizle { get; }

    public RelayCommand<object> LoadArchiveFile { get; }

    public RelayCommand<object> LoadArrangedPdfFile { get; }

    public ICommand LoadCroppedImage { get; }

    public RelayCommand<object> LoadDocxFile { get; }

    public ICommand LoadImage { get; }

    public RelayCommand<object> LoadJb2ZipFile { get; }

    public RelayCommand<object> LoadMiniDraw { get; }

    public ICommand LoadPdfExtractFile { get; }

    public RelayCommand<object> LoadXlsFile { get; }

    public ICommand LoadXpsFile { get; }

    public RelayCommand<object> ManualDeskewImage { get; }

    public int MaxPreviewWidth
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MaxPreviewWidth));
            }
        }
    } = 512;

    public bool MergePdfFileToFirst
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MergePdfFileToFirst));
            }
        }
    }

    public ICommand MergePdfListAddFile { get; }

    public ICommand MergePdfListRemoveFile { get; }

    public RelayCommand<object> MergePdfListToCurrentFile { get; }

    public RelayCommand<object> MergePdfListToFile { get; }

    public ICommand MergeSelectedImagesToPdfFile { get; }

    public int MinPreviewWidth
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MinPreviewWidth));
            }
        }
    } = 96;

    public RelayCommand<object> MoveToNextTabCommand { get; }

    public RelayCommand<object> MoveToPreviousTabCommand { get; }

    public RelayCommand<object> OpenHelpDialog { get; }

    public int PageHeight
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PageHeight));
                OnPropertyChanged(nameof(CropAllMaximumWidth));
            }
        }
    }

    public int PageIndex
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PageIndex));
            }
        }
    } = 1;

    public int PageWidth
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PageWidth));
                OnPropertyChanged(nameof(CropAllMaximumWidth));
            }
        }
    }

    public ObservableCollection<Paper> Papers
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Papers));
            }
        }
    } = [ new Paper { Category = "A", Height = 118.9, PaperType = "A0", Width = 84.1 }, new Paper { Category = "A", Height = 84.1, PaperType = "A1", Width = 59.4 }, new Paper { Category = "A", Height = 59.4, PaperType = "A2", Width = 42 }, new Paper
    {
        Category = "A",
        Height = 42,
        PaperType = "A3",
        Width = 29.7
    }, new Paper { Category = "A", Height = 29.7, PaperType = "A4", Width = 21, WidespreadPaper = Visibility.Visible }, new Paper { Category = "A", Height = 21, PaperType = "A5", Width = 14.8 }, new Paper
    {
        Category = "B",
        Height = 141.4,
        PaperType = "B0",
        Width = 100
    }, new Paper { Category = "B", Height = 100, PaperType = "B1", Width = 70.7 }, new Paper { Category = "B", Height = 70.7, PaperType = "B2", Width = 50 }, new Paper { Category = "B", Height = 50, PaperType = "B3", Width = 35.3 }, new Paper
    {
        Category = "B",
        Height = 35.3,
        PaperType = "B4",
        Width = 25
    }, new Paper { Category = "B", Height = 25, PaperType = "B5", Width = 17.6 }, new Paper { Height = 27.94, PaperType = "Letter", Width = 21.59, WidespreadPaper = Visibility.Visible }, new Paper { Height = 35.56, PaperType = "Legal", Width = 21.59 }, new Paper
    {
        Height = 26.67,
        PaperType = "Executive",
        Width = 18.415
    }, new Paper { Category = string.Empty, Height = 0, PaperType = "Original", Width = 0 }, new Paper { Category = string.Empty, Height = Settings.Default.CustomPaperHeight, PaperType = "Custom", Width = Settings.Default.CustomPaperWidth }, ];

    public ICommand PasteFileToPdfFile { get; }

    public int PdfCompressDpi
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfCompressDpi));
            }
        }
    } = 150;

    public double PdfImportControlProgressValue
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfImportControlProgressValue));
            }
        }
    }

    public RelayCommand<object> PdfImportViewerÇiftİşaretle { get; }

    public RelayCommand<object> PdfImportViewerTekİşaretle { get; }

    public RelayCommand<object> PdfImportViewerTersiniİşaretle { get; }

    public RelayCommand<object> PdfImportViewerTümünüİşaretle { get; }

    public double PdfLoadProgressValue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfLoadProgressValue));
            }
        }
    }

    public int PdfMedianValue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfMedianValue));
            }
        }
    }

    public ObservableCollection<PdfData> PdfPages
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPages));
            }
        }
    }

    public int PdfQuality
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfQuality));
            }
        }
    } = 70;

    public int PdfSplitCount
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfSplitCount));
            }
        }
    }

    public bool PdfToolBarControlIsEnabled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfToolBarControlIsEnabled));
            }
        }
    } = true;

    public RelayCommand<object> PdfViewerFullScreen { get; }

    public ICommand PdfWaterMark { get; }

    public SolidColorBrush PdfWatermarkColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfWatermarkColor));
            }
        }
    } = Brushes.Red;

    public string PdfWatermarkFont
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfWatermarkFont));
            }
        }
    } = "Arial";

    public double PdfWatermarkFontAngle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfWatermarkFontAngle));
            }
        }
    } = 315d;

    public double PdfWatermarkFontSize
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfWatermarkFontSize));
            }
        }
    } = 72d;

    public string PdfWaterMarkText
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfWaterMarkText));
            }
        }
    }

    public bool PolicyApplied { get; } = Policy.AnyPolicyExsist();

    public RelayCommand<object> PrepareCropCurrentImage { get; }

    public int PrintDpi
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PrintDpi));
            }
        }
    } = 300;

    public RelayCommand<object> PrintEypPackageSelectedDocuments { get; }

    public RelayCommand<object> PrintSelectedDocuments { get; }

    public ICommand ReadPdfTag { get; }

    public bool RefreshDocumentList
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(RefreshDocumentList));
            }
        }
    }

    public RelayCommand<object> RemoveArrangedPdfFile { get; }

    public RelayCommand<object> RemoveCurrentPdfPage { get; }

    public ICommand RemoveProfile { get; }

    public RelayCommand<object> RemoveSplitListsIndex { get; }

    public RelayCommand<object> RemoveVerticalLines { get; }

    public bool RemoveVerticalLinesIsRunning
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(RemoveVerticalLinesIsRunning));
            }
        }
    }

    public RelayCommand<object> ReplaceSelectedImage { get; }

    public ICommand ResetCrop { get; }

    public RelayCommand<object> ResetPreviewSize { get; }

    public ICommand ResimSil { get; }

    public ICommand ResimSilGeriAl { get; }

    public ICommand ReverseData { get; }

    public ICommand ReverseDataHorizontal { get; }

    public ICommand ReversePdfFile { get; }

    public ICommand RotateSelectedPage { get; }

    public RelayCommand<object> SaveDocxFile { get; }

    public ICommand SaveFileList { get; }

    public ICommand SaveProfile { get; }

    public RelayCommand<object> SaveSelectedFilesBwPdfFile { get; }

    public RelayCommand<object> SaveSelectedFilesJpgFile { get; }

    public RelayCommand<object> SaveSelectedFilesPdfFile { get; }

    public RelayCommand<object> SaveSelectedFilesTifFile { get; }

    public RelayCommand<object> SaveSelectedFilesTxtFile { get; }

    public RelayCommand<object> SaveSelectedFilesWebpFile { get; }

    public RelayCommand<object> SaveSelectedFilesZipFile { get; }

    public RelayCommand<object> SaveSingleBlackWhiteJb2File { get; }

    public RelayCommand<object> SaveSingleBwPdfFile { get; }

    public RelayCommand<object> SaveSingleJpgFile { get; }

    public RelayCommand<object> SaveSinglePdfFile { get; }

    public RelayCommand<object> SaveSingleTifFile { get; }

    public RelayCommand<object> SaveSingleTxtFile { get; }

    public RelayCommand<object> SaveSingleWebpFile { get; }

    public RelayCommand<object> SaveSingleXpsFile { get; }

    public int SayfaBaşlangıç
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SayfaBaşlangıç));
            }
        }
    } = 1;

    public int SayfaBitiş
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SayfaBitiş));
            }
        }
    } = 1;

    public ICommand ScanImage { get; }

    public Scanner Scanner
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Scanner));
            }
        }
    }

    public ICommand SeçiliDirektPdfKaydet { get; }

    public ICommand SeçiliKaydet { get; }

    public ICommand SeçiliListeTemizle { get; }

    public ScannedImage SeçiliResim
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SeçiliResim));
            }
        }
    }

    public int SeekIndex
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SeekIndex));
            }
        }
    } = -1;

    public Tuple<string, int, double, bool, double> SelectedCompressionProfile
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedCompressionProfile));
            }
        }
    }

    public PageFlip SelectedFlip
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedFlip));
            }
        }
    } = PageFlip.NONE;

    public bool SelectedImageWidthHeightIsEqual
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedImageWidthHeightIsEqual));
            }
        }
    }

    public Orientation SelectedOrientation
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedOrientation));
            }
        }
    } = Orientation.Default;

    public Paper SelectedPaper
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedPaper));
            }
        }
    }

    public string SelectedPrinter
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedPrinter));
            }
        }
    } = GetDefaultPrinterName();

    public PageRotation SelectedRotation
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedRotation));
            }
        }
    } = PageRotation.NONE;

    public int SelectedTabIndex
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedTabIndex));
            }
        }
    }

    public RelayCommand<object> SelectSplittedIndexImages { get; }

    public bool SetShutdown
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SetShutdown));
            }
        }
    }

    public ICommand ShowDateFolderHelp { get; }

    public RelayCommand<object> ShuffleData { get; }

    public RelayCommand<object> ShufflePdfPages { get; }

    public RelayCommand<object> SplitImagesByIndex { get; }

    public ICommand SplitPdf { get; }

    public List<ScannedImage[]> SplittedIndexImages
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SplittedIndexImages));
            }
        }
    }

    public RelayCommand<object> TekResimSil { get; }

    public ICommand Tersiniİşaretle { get; }

    public bool TesseractOrientationFileExists
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TesseractOrientationFileExists));
            }
        }
    } = File.Exists($@"{Ocr.Ocr.TesseractPath}\osd.traineddata");

    public string TextSplitList
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TextSplitList));
            }
        }
    }

    public RelayCommand<object> ToolBarAutoCropImage { get; }

    public RelayCommand<object> ToolBarAutoDeskewImage { get; }

    public RelayCommand<object> ToolBoxManualDeskewImage { get; }

    public int TotalPageCount
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TotalPageCount));
            }
        }
    }

    public int TrimPage
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TrimPage));
            }
        }
    } = -1;

    public ICommand Tümünüİşaretle { get; }

    public ICommand TümünüİşaretleDikey { get; }

    public ICommand TümünüİşaretleYatay { get; }

    public ICommand TümününİşaretiniKaldır { get; }

    public RelayCommand<object> TümünüOtomatikDöndür { get; }

    public Twain Twain
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Twain));
            }
        }
    }

    public GridLength TwainGuiControlLength
    {
        get => twainGuiControlLength;

        set
        {
            if (twainGuiControlLength != value)
            {
                twainGuiControlLength = value;
                OnPropertyChanged(nameof(TwainGuiControlLength));
            }
        }
    }

    public RelayCommand<object> UndoInvertImage { get; }

    public bool UseMozJpeg
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UseMozJpeg));
            }
        }
    }

    public FileVersionInfo Version => FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess()?.MainModule?.FileName);

    public RelayCommand<object> VideodanResimYükle { get; }

    public RelayCommand<object> VideoViewerFullScreen { get; }

    public ICommand WebAdreseGit { get; }

    public RelayCommand<object> XmlViewerFullScreen { get; }

    public RelayCommand<object> XpsDosyasındanResimYükle { get; }

    public string this[string columnName] => columnName switch
    {
        "CropAutoCropChecked" when Settings.Default.AutoCropImage || Settings.Default.CropScan => "TRIMIMAGE",
        _ => null
    };

    public static async Task ArrangeFileAsync(string loadfilename, string savefilename, int start, int end)
    {
        await Task.Run(
            () =>
            {
                using PdfDocument outputDocument = loadfilename.ArrangePdfPages(start, end);
                if (outputDocument is not null)
                {
                    outputDocument.ApplyDefaultPdfCompression();
                    outputDocument.Save(savefilename);
                }
            });
    }

    public static List<List<T>> ChunkBy<T>(IEnumerable<T> source, int chunkSize) => [ .. source.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / chunkSize).Select(x => x.Select(v => v.Value).ToList()) ];

    public static ObservableCollection<OcrData> ConvertPdfCharacterToOcrData(int imageheight, int imagewidth, IEnumerable<PdfiumViewer.PdfCharacterInformation> pdfCharacterInformations = null, int imageLoadResolution = 200, bool docxformat = false)
    {
        if (pdfCharacterInformations is null)
        {
            return null;
        }
        ObservableCollection<OcrData> ocrDatas = [];
        const double PointsPerInch = 72.0;
        double pageheight = (double)imageheight / imageLoadResolution * PointsPerInch;
        double pagewidth = (double)imagewidth / imageLoadResolution * PointsPerInch;
        double widthScale = imagewidth / pagewidth;
        double heightScale = imageheight / pageheight;
        foreach (PdfCharacterInformation character in MergeCharactersToWords(pdfCharacterInformations, docxformat))
        {
            double x = character.Bounds.X * widthScale;
            double y = (pageheight - character.Bounds.Y) * heightScale;
            double width = Math.Abs(character.Bounds.Width) * widthScale;
            double height = Math.Abs(character.Bounds.Height) * heightScale;
            Rect rect = new(x, y, width, height);
            ocrDatas.Add(new OcrData() { FontSize = character.FontSize, Rect = rect, Text = character.Word });
        }
        return ocrDatas;
    }

    public static void ExtractAndHandleFiles(string zipFilePath, string extractPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string destinationFile = Path.Combine(extractPath, entry.FullName);
            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                entry.ExtractToFile(destinationFile, true);
            }
            catch (IOException) when (IsFileLocked(destinationFile))
            {
                ScheduleFileReplacement(entry, destinationFile);
            }
        }
    }

    public static List<string> EypFileExtract(string eypfilepath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eypfilepath) || !string.Equals(Path.GetExtension(eypfilepath), ".eyp", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            using ZipArchive archive = ZipFile.Open(eypfilepath, ZipArchiveMode.Read);
            if (archive is not null)
            {
                List<string> data = [];
                ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "NihaiOzet.xml");
                string source = $"{Path.GetTempPath()}{Guid.NewGuid()}.xml";
                üstveri?.ExtractToFile(source, true);
                XDocument xdoc = XDocument.Load(source);
                if (xdoc is not null)
                {
                    foreach (string file in xdoc.Descendants().Select(z => Path.GetFileName((string)z.Attribute("URI"))).Where(z => !string.IsNullOrEmpty(z)))
                    {
                        ZipArchiveEntry zipArchiveEntry = archive.Entries.FirstOrDefault(entry => entry.Name == file);
                        if (zipArchiveEntry is not null)
                        {
                            string destinationFileName = $"{Path.GetTempPath()}{Guid.NewGuid()}{Path.GetExtension(file.ToLowerInvariant())}";
                            zipArchiveEntry.ExtractToFile(destinationFileName, true);
                            data.Add(destinationFileName);
                        }
                    }
                }

                return data;
            }
            return null;
        }
        catch
        {
        }
        return null;
    }

    public static bool FileNameValid(string filename) => !string.IsNullOrWhiteSpace(filename) && filename.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetDefaultPrinter(StringBuilder pszBuffer, ref int pcchBuffer);

    public static void GotoPage(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                _ = Process.Start(path);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex?.Message);
            }
        }
    }

    public static void NotifyPdfChange(Viewer pdfviewer, string temporarypdf, string pdfFilePath)
    {
        File.Delete(temporarypdf);
        pdfviewer.PdfFilePath = null;
        pdfviewer.PdfFilePath = pdfFilePath;
    }

    public static void PlayNotificationSound(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                using SoundPlayer player = new(file);
                player.Play();
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
        }
    }

    public static async Task RemovePdfPageAsync(string pdffilepath, int start, int end)
    {
        await Task.Run(
            () =>
            {
                PdfDocument inputDocument = PdfReader.Open(pdffilepath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                if (inputDocument is null)
                {
                    return;
                }
                for (int i = end; i >= start; i--)
                {
                    inputDocument.Pages.RemoveAt(i - 1);
                }
                if (inputDocument.PageCount > 0)
                {
                    inputDocument.Save(pdffilepath);
                }
            });
    }

    public static PdfDocument RenderPdfPage(Viewer pdfviewer, BitmapImage image, int page)
    {
        PdfDocument document = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider)?.GenerateFromBitmapSourcePdf(page - 1, image);
        document.ApplyDefaultPdfCompression();
        return document;
    }

    public static void SavePageRotated(string savepath, PdfDocument inputDocument, int angle, int pageindex)
    {
        PdfPage page = inputDocument?.Pages[pageindex];
        if (page?.Rotate is > 360 or < -360)
        {
            page.Rotate = 0;
        }
        page.Rotate += angle;
        inputDocument.Save(savepath);
    }

    public Task AddFiles(string[] filenames, int decodeheight, CancellationTokenSource cancellationTokenSource = null)
    {
        if (cancellationTokenSource?.IsCancellationRequested == true)
        {
            filenames = null;
            PdfLoadProgressValue = 0;
            return Task.CompletedTask;
        }
        FileLoadTask = Task.Run(
            async () =>
            {
                foreach (string filename in filenames)
                {
                    try
                    {
                        ILoadFileHandler fileHandler;
                        switch (Path.GetExtension(filename.ToLowerInvariant()))
                        {
                            case ".pdf":
                                if (Settings.Default.UsePdfJpgFiles)
                                {
                                    string[] jpgoutputfiles = await PdfGeneration.WritePdfToJpgFileAsync(filename, Settings.Default.ImgLoadResolution, progress => Dispatcher.InvokeAsync(() => PdfLoadProgressValue = progress));
                                    if (jpgoutputfiles is not null)
                                    {
                                        await AddFiles(jpgoutputfiles, decodeheight);
                                    }
                                }
                                else
                                {
                                    fileHandler = new PdfFileHandler();
                                    await AddFilesAsync(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                }

                                break;

                            case ".eyp":
                                await AddFiles([ .. EypFileExtract(filename) ], decodeheight);
                                break;

                            case ".txt":
                                await AddFiles(File.ReadAllLines(filename), decodeheight);
                                break;

                            case ".jpg":
                            case ".jpeg":
                            case ".jfif":
                            case ".jpe":
                            case ".png":
                            case ".gif":
                            case ".bmp":
                            case ".heic":
                                fileHandler = new ImageFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                break;

                            case ".jb2":
                                fileHandler = new Jb2FileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                break;

                            case ".j2k":
                                fileHandler = new J2kFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                break;

                            case ".cbz":
                            case ".cbr":
                                fileHandler = new ImageFileHandler();
                                await CbzCbrFileExtract(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                break;

                            case ".zip":
                            case ".7z":
                            case ".arj":
                            case ".bzip2":
                            case ".cab":
                            case ".gzip":
                            case ".iso":
                            case ".lzh":
                            case ".lzma":
                            case ".ntfs":
                            case ".ppmd":
                            case ".rar":
                            case ".rar5":
                            case ".rpm":
                            case ".tar":
                            case ".vhd":
                            case ".wim":
                            case ".xar":
                            case ".xz":
                            case ".z":
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        SelectedTabIndex = 2;
                                        ArchiveVwr.ArchivePath = filename;
                                    });
                                break;

                            case ".mp4":
                            case ".3gp":
                            case ".mpg":
                            case ".mpeg":
                            case ".avi":
                            case ".m2ts":
                            case ".ts":
                            case ".m4v":
                            case ".mkv":
                            case ".mpv4":
                            case ".mov":
                            case ".wmv":
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        SelectedTabIndex = 5;
                                        mediaViewer.MediaDataFilePath = filename;
                                    });
                                break;

                            case ".xls":
                            case ".xlsx":
                            case ".xlsb":
                            case ".csv":
                            case ".ods":
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        SelectedTabIndex = 6;
                                        xlsxViewer.XlsxDataFilePath = filename;
                                    });
                                break;

                            case ".docx":
                            case ".odt":
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        SelectedTabIndex = 8;
                                        docxViewer.DocxDataFilePath = filename;
                                    });
                                break;

                            case ".jb2zip":
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        SelectedTabIndex = 9;
                                        jb2zipViewer.ImageFilePath = filename;
                                    });
                                break;

                            case ".webp":
                                fileHandler = new WebpFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                break;

                            case ".tiff" or ".tif":
                                fileHandler = new TiffFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                break;

                            case ".xps":
                                fileHandler = new XpsFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight, cancellationTokenSource);
                                break;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                    finally
                    {
                        filenames = null;
                    }
                }
            });
        return Task.CompletedTask;
    }

    public async Task CbzCbrFileExtract(string filepath, ILoadFileHandler fileHandler, int decodeHeight = 0, CancellationTokenSource cancellationTokenSource = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filepath) || fileHandler is null)
            {
                return;
            }
            await Task.Run(
                async () =>
                {
                    using ArchiveFile archiveFile = new(filepath);
                    if (archiveFile is not null)
                    {
                        foreach (Entry entry in archiveFile.Entries)
                        {
                            if (entry is null)
                            {
                                continue;
                            }
                            string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(entry.FileName));
                            entry.Extract(tempFilePath, true);
                            await AddFilesAsync(tempFilePath, fileHandler, decodeHeight, cancellationTokenSource);
                        }
                    }
                });
        }
        catch
        {
        }
    }

    public void CreateBuiltInScanProfiles()
    {
        if (AnyScannerExist())
        {
            string[] profiles = new string[6];
            string[] dpiValues = [ "96", "200", "300" ];
            string[] colorModes = [ "BW", "COLOR" ];
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < dpiValues.Length; j++)
                {
                    profiles[(i * dpiValues.Length) + j] = $"{Translation.GetResStringValue(colorModes[i])} {dpiValues[j]} Dpi|{dpiValues[j]}|{Settings.Default.Adf}|{i * 2}|{Scanner.Duplex}|{Scanner.ShowUi}|false|{Settings.Default.ShowFile}|{Scanner.DetectEmptyPage}|{Scanner.FileName}|{Scanner.InvertImage}|{Scanner.ApplyMedian}|{Scanner.Tarayıcılar[0]}|{Settings.Default.AutoCropImage}|{Scanner.UseFilmScanner}";
                }
            }
            foreach (string profile in profiles)
            {
                _ = Settings.Default.Profile.Add(profile);
            }
            Settings.Default.Save();
        }
    }

    public void Dispose() => Dispose(true);

    public void DropFile(object sender, DragEventArgs e)
    {
        if (sender is not StackPanel stackpanel || e.Data.GetData(typeof(ScannedImage)) is not ScannedImage droppedData || stackpanel.DataContext is not ScannedImage target)
        {
            return;
        }

        OrganizeDroppedData(droppedData, target);
    }

    public async Task ListBoxDropFileAsync(DragEventArgs e)
    {
        if (FileLoadTask?.IsCompleted == false)
        {
            ExtendedMessageBox extendedMessageBox = new();
            extendedMessageBox.ShowDialog(Window.GetWindow(this), Translation.GetResStringValue("TRANSLATEPENDING"), AppName);
            return;
        }
        fileloadcancellationToken = new CancellationTokenSource();
        if (e.Data.GetData(typeof(Scanner)) is Scanner droppedData)
        {
            await Task.Run(() => AddFiles([ droppedData.FileName ], DecodeHeight));
            return;
        }

        if ((e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
        {
            foreach (string file in droppedfiles)
            {
                try
                {
                    if (File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                    {
                        string folder = file;
                        await Task.Run(() => AddFiles(Directory.GetFiles(folder), DecodeHeight, fileloadcancellationToken));
                    }
                }
                catch
                {
                }
            }

            await Task.Run(() => AddFiles(droppedfiles, DecodeHeight, fileloadcancellationToken));
        }
    }

    public void SplitPdfPageCount(string pdfpath, string savefolder, int pagecount)
    {
        using PdfDocument inputDocument = PdfReader.Open(pdfpath, PdfDocumentOpenMode.Import, PdfGeneration.PasswordProvider);
        if (inputDocument is null)
        {
            return;
        }
        foreach (List<int> item in ChunkBy(Enumerable.Range(0, inputDocument.PageCount).ToList(), pagecount))
        {
            using PdfDocument outputDocument = new();
            foreach (int pagenumber in item)
            {
                _ = outputDocument.AddPage(inputDocument.Pages[pagenumber]);
            }

            outputDocument.Save(savefolder.SetUniqueFile(Translation.GetResStringValue("SPLIT"), "pdf"));
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Scanner.Resimler = null;
                Twain = null;
                Scanner.CroppedImage = null;
                Scanner.CopyCroppedImage = null;
            }

            disposedValue = true;
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher;
        if (dispatcher?.CheckAccess() == true)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            _ = dispatcher?.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    private static async Task CreateAutoDeskewedImage(ScannedImage item, double? angle = null)
    {
        BitmapFrame bitmapFrame = BitmapFrame.Create(await item.Resim.RotateImageAsync(angle ?? Deskew.GetDeskewAngle(item.Resim), Brushes.White));
        bitmapFrame?.Freeze();
        item.Resim = bitmapFrame;
        item.DeskewAngle = Deskew.GetDeskewAngle(item.Resim) + 90;
    }

    private static PdfCharacterInformation CreateWordFromCharacters(List<PdfCharacterInformation> characters)
    {
        if (characters?.Any() == false)
        {
            throw new ArgumentException("Characters list cannot be empty");
        }

        PdfCharacterInformation firstChar = characters[0];
        PdfCharacterInformation lastChar = characters.Last();

        RectangleF bounds = new(firstChar.Bounds.Left, firstChar.Bounds.Top, lastChar.Bounds.Right - firstChar.Bounds.Left, firstChar.Bounds.Height);

        return new PdfCharacterInformation { FontSize = firstChar.FontSize, Word = string.Concat(characters.Select(c => c.Character)), Bounds = bounds };
    }

    private static string GetDefaultPrinterName()
    {
        int bufferSize = 256;
        StringBuilder printerNameBuffer = new(bufferSize);
        return GetDefaultPrinter(printerNameBuffer, ref bufferSize) ? (printerNameBuffer?.ToString()) : null;
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            _ = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static IEnumerable<PdfCharacterInformation> MergeCharactersToWords(IEnumerable<PdfiumViewer.PdfCharacterInformation> characters, bool docxformat = false)
    {
        List<PdfCharacterInformation> words = [];
        List<PdfCharacterInformation> currentWord = [];
        foreach (PdfiumViewer.PdfCharacterInformation character in characters)
        {
            if (character.Character is ' ' or '\n' or '\r')
            {
                if (currentWord.Any())
                {
                    if (docxformat)
                    {
                        currentWord.Add(new PdfCharacterInformation() { Character = character.Character });
                    }
                    words.Add(CreateWordFromCharacters(currentWord));
                    currentWord.Clear();
                }
            }
            else
            {
                currentWord.Add(new PdfCharacterInformation() { Bounds = character.Bounds, Character = character.Character, FontSize = character.FontSize, });
            }
        }

        if (currentWord.Any())
        {
            words.Add(CreateWordFromCharacters(currentWord));
        }

        return words;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, MoveFileFlags dwFlags);

    private static void ScheduleFileReplacement(ZipArchiveEntry entry, string destinationFile)
    {
        string tempFile = Path.GetTempFileName();
        entry.ExtractToFile(tempFile, overwrite: true);
        _ = MoveFileEx(tempFile, destinationFile, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT);
    }

    private async Task AddAttachmentFileAsync(string[] files, string loadfilename, string savefilename)
    {
        await Task.Run(
            () =>
            {
                using PdfDocument pdfdocument = PdfReader.Open(loadfilename, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                if (pdfdocument is null)
                {
                    return;
                }
                foreach (string item in files)
                {
                    pdfdocument.AddEmbeddedFile(Path.GetFileNameWithoutExtension(item), item);
                }

                pdfdocument.Save(savefilename);
            });
    }

    private async Task AddFilesAsync(string filename, ILoadFileHandler fileHandler, int decodeHeight = 0, CancellationTokenSource cancellationTokenSource = null)
    {
        if (!fileHandler.IsValidFile(filename))
        {
            return;
        }

        TotalPageCount = fileHandler.GetPageCount(filename);
        BitmapFrame bitmapFrame;
        for (int i = 1; i <= TotalPageCount; i++)
        {
            if (cancellationTokenSource?.IsCancellationRequested == true)
            {
                _ = await Dispatcher.InvokeAsync(() => PdfLoadProgressValue = 0);
                return;
            }
            switch (fileHandler)
            {
                case PdfFileHandler:
                    bitmapFrame = BitmapFrame.Create(await fileHandler.LoadPdfAsync(filename, i));
                    _ = fileHandler.GetPdfCharacters();
                    break;

                case WebpFileHandler:
                    bitmapFrame = await fileHandler.LoadWebpImage(decodeHeight, filename);
                    break;

                case XpsFileHandler:
                    await HandleTifXpsFileAsync(fileHandler.LoadXpsPagesAsync, filename, TotalPageCount);
                    return;

                case TiffFileHandler:
                case Jb2FileHandler:
                    await HandleTifXpsFileAsync(fileHandler.LoadTiffPagesAsync, filename, TotalPageCount);
                    return;

                default:
                    bitmapFrame = await fileHandler.LoadImageAsync(filename);
                    break;
            }

            bitmapFrame.Freeze();
            double deskewAngle = Settings.Default.UseDeskewAngle ? Deskew.GetDeskewAngle(bitmapFrame) + 90 : 90;
            await Dispatcher.InvokeAsync(
                () =>
                {
                    ScannedImage item = new() { GetPdfCharacterInformations = fileHandler.GetPdfCharacters(), Resim = bitmapFrame, FilePath = filename, DeskewAngle = deskewAngle };
                    Scanner?.Resimler.Add(item);
                    PdfLoadProgressValue = (double)i / TotalPageCount;
                });
            bitmapFrame = null;
        }
        _ = await Dispatcher.InvokeAsync(() => PdfLoadProgressValue = 0);
    }

    private void AddPendingFileRenameOperation(string sourceFilePath, string targetFilePath)
    {
        const string registryKeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager";
        const string valueName = "PendingFileRenameOperations";

        using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKeyPath, true);
        if (key is not null)
        {
            string[] newValue = key.GetValue(valueName) is string[] currentValue ? (string[])currentValue.Clone() : [];
            Array.Resize(ref newValue, newValue.Length + 2);
            newValue[newValue.Length - 2] = sourceFilePath;
            newValue[newValue.Length - 1] = targetFilePath;
            key.SetValue(valueName, newValue, RegistryValueKind.MultiString);
        }
        else
        {
            throw new Exception("Registry key not found.");
        }
    }

    private bool AnyImageExist() => Scanner?.Resimler?.Count > 0;

    private bool AnyScannerExist() => Scanner?.Tarayıcılar?.Count > 0;

    private async Task AutoRotateBasedTextOrientation(ObservableCollection<ScannedImage> scannedImages, int parallelcount)
    {
        int i = 0;
        await Task.Run(
            () =>
            {
                ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = parallelcount };
                _ = Parallel.ForEach(
                    scannedImages,
                    parallelOptions,
                    async image =>
                    {
                        try
                        {
                            int orientation = image.Resim.ToTiffJpegByteArray(Format.Jpg).GetImageOrientation();
                            if (orientation == 1)
                            {
                                image.Resim = await RotateImage(image, -1);
                            }
                            if (orientation == 2)
                            {
                                image.Resim = await RotateImage(image, -2);
                            }
                            if (orientation == 3)
                            {
                                image.Resim = await RotateImage(image, 1);
                            }
                            AllRotateProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                            i++;
                        }
                        catch
                        {
                        }
                    });
            });
    }

    private void ButtonedTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Scanner.CaretPosition = (sender as ButtonedTextBox)?.CaretIndex ?? 0;

    private void CameraUserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not CameraUserControl cameraUserControl)
        {
            return;
        }

        if (e.PropertyName is "ResimData" && cameraUserControl.ResimData is not null)
        {
            Scanner?.Resimler?.Add(new ScannedImage { Resim = cameraUserControl.ResimData });
        }

        if (e.PropertyName is not "DetectQRCode")
        {
            return;
        }

        if (cameraUserControl.DetectQRCode)
        {
            CameraQrCodeTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
            QrCode.QrCode qrcode = new();
            CameraQrCodeTimer.Tick += (s, f2) =>
                                      {
                                          Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(cameraUserControl.CameraEncodeBitmapImage());
                                          if (!string.IsNullOrWhiteSpace(Scanner.BarcodeContent))
                                          {
                                              OnPropertyChanged(nameof(CameraQRCodeData));
                                          }
                                      };
            CameraQrCodeTimer?.Start();
            return;
        }

        CameraQrCodeTimer?.Stop();
    }

    private bool CheckFileSaveProgress()
    {
        if (FileSaveTask?.IsCompleted == false)
        {
            ExtendedMessageBox extendedMessageBox = new();
            extendedMessageBox.ShowDialog(Window.GetWindow(this), Translation.GetResStringValue("TASKSRUNNING"), AppName);
            return false;
        }
        return true;
    }

    private Int32Rect CropPreviewImage(ImageSource imageSource)
    {
        if (imageSource is not BitmapSource bitmapSource)
        {
            return default;
        }

        int height = bitmapSource.PixelHeight - (int)Scanner.CropBottom - (int)Scanner.CropTop;
        int width = bitmapSource.PixelWidth - (int)Scanner.CropRight - (int)Scanner.CropLeft;
        return width < 0 || height < 0 ? default : new Int32Rect((int)Scanner.CropLeft, (int)Scanner.CropTop, width, height);
    }

    private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "ImgLoadResolution")
        {
            DecodeHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.ImgLoadResolution);
        }

        if (e.PropertyName is "AutoFolder")
        {
            Scanner.AutoSave = Directory.Exists(Settings.Default.AutoFolder);
        }

        if (e.PropertyName is "Adf" && !Settings.Default.Adf)
        {
            Scanner.DetectEmptyPage = false;
            Scanner.Duplex = false;
        }

        if (e.PropertyName is "Mode")
        {
            Settings.Default.BackMode = Settings.Default.Mode;
        }

        if (e.PropertyName is "Çözünürlük")
        {
            SetCropPageResolution();
        }

        if (Settings.Default.UseSelectedProfile)
        {
            Scanner.SelectedProfile = Settings.Default.DefaultProfile;
        }

        if (e.PropertyName is "CustomPaperWidth" or "CustomPaperHeight")
        {
            Paper paper = Papers.FirstOrDefault(z => z.PaperType == "Custom");
            paper.Width = Settings.Default.CustomPaperWidth;
            paper.Height = Settings.Default.CustomPaperHeight;
        }

        if (e.PropertyName is "Right" or "Bottom")
        {
            CropRightMargin = PageWidth - Settings.Default.Right;
            CropBottomMargin = PageHeight - Settings.Default.Bottom;
        }

        if (e.PropertyName is "CropScan" && Settings.Default.CropScan)
        {
            Settings.Default.AutoCropImage = false;
        }
        else if (e.PropertyName is "AutoCropImage" && Settings.Default.AutoCropImage)
        {
            Settings.Default.CropScan = false;
        }

        if (e.PropertyName is "CropScan" or "AutoCropImage")
        {
            CropAutoCropChecked = Settings.Default.AutoCropImage || Settings.Default.CropScan;
        }

        if (e.PropertyName is "AutoRotateBasedText" && Settings.Default.AutoRotateBasedText && !TesseractOrientationFileExists)
        {
            Settings.Default.AutoRotateBasedText = false;
        }

        if (e.PropertyName is "UsePdfInternalTextData" && Settings.Default.UsePdfInternalTextData)
        {
            Scanner.ApplyPdfSaveOcr = false;
        }
    }

    private async Task DefaultScanAsync()
    {
        if (Settings.Default.SeçiliTarayıcı.Contains("|http"))
        {
            BitmapImage bitmapimage = await ESCLScanner.ScanDocumentAsync(Settings.Default.SeçiliTarayıcı.Split('|')[1], (int)Settings.Default.Çözünürlük);
            if (bitmapimage is not null)
            {
                bitmapimage.Freeze();
                Scanner.Resimler.Add(new ScannedImage() { Resim = BitmapFrame.Create(bitmapimage) });
            }
        }
        else
        {
            Twain.SelectSource(Settings.Default.SeçiliTarayıcı);
            Twain.StartScanning(DefaultScanSettings());
        }
    }

    private ScanSettings DefaultScanSettings()
    {
        ScanSettings scansettings = new()
        {
            UseAutoScanCache = true,
            UseDocumentFeeder = Settings.Default.Adf,
            ShowTwainUi = Scanner.ShowUi,
            ShowProgressIndicatorUi = Scanner.ShowProgress,
            UseDuplex = Scanner.Duplex,
            ShouldTransferAllPages = true,
            UseFilmScanner = Scanner.UseFilmScanner,
            Resolution = new ResolutionSettings { Dpi = (int)Settings.Default.Çözünürlük, ColourSetting = IsBlackAndWhiteMode() ? ColourSetting.BlackAndWhite : ColourSetting.Colour },
            Page = new PageSettings { Orientation = SelectedOrientation },
            Rotation = new RotationSettings { AutomaticBorderDetection = true, AutomaticRotate = true, AutomaticDeskew = true },
        };
        scansettings.Page.Size = SelectedPaper.PaperType switch
        {
            "A0" => PageType.A0,
            "A1" => PageType.A1,
            "A2" => PageType.A2,
            "A3" => PageType.A3,
            "A4" => PageType.A4,
            "A5" => PageType.A5,
            "B0" => PageType.ISOB0,
            "B1" => PageType.ISOB1,
            "B2" => PageType.ISOB2,
            "B3" => PageType.ISOB3,
            "B4" => PageType.ISOB4,
            "B5" => PageType.ISOB5,
            "Letter" => PageType.UsLetter,
            "Legal" => PageType.UsLegal,
            "Executive" => PageType.UsExecutive,
            _ => scansettings.Page.Size
        };
        return scansettings;
    }

    private BitmapSource EvrakOluştur(Bitmap bitmap, ColourSetting color, int decodepixelheight)
    {
        return color switch
        {
            ColourSetting.BlackAndWhite => bitmap.ConvertBlackAndWhite(Settings.Default.BwThreshold).ToBitmapImage(ImageFormat.Tiff, decodepixelheight),
            _ => color switch
            {
                ColourSetting.GreyScale => bitmap.ConvertBlackAndWhite(Settings.Default.BwThreshold, true).ToBitmapImage(ImageFormat.Jpeg, decodepixelheight),
                _ => color switch
                {
                    ColourSetting.Colour => bitmap.ToBitmapImage(ImageFormat.Jpeg, decodepixelheight),
                    _ => null
                }
            }
        };
    }

    private async void FastScanComplete(object sender, ScanningCompleteEventArgs e)
    {
        try
        {
            Scanner.ArayüzEtkin = false;
            QrCode.QrCode qrcode = new();
            Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(Scanner?.Resimler?.LastOrDefault()?.Resim);
            OnPropertyChanged(nameof(Scanner.DetectPageSeperator));
            Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
            DataBaseTextData = [];
            if (Scanner.ApplyDataBaseOcr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
            {
                Scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
                for (int i = 0; i < Scanner.Resimler.Count; i++)
                {
                    ScannedImage scannedimage = Scanner.Resimler[i];
                    Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(scannedimage.Resim);
                    DataBaseTextData.Add(await scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage));
                    Scanner.PdfSaveProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                }
                DataBaseTextDataCompleted = true;
            }

            Format fileFormat = (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite ? Format.Tiff : Format.Jpg;
            (await Scanner.Resimler.ToList().GeneratePdfAsync(fileFormat, SelectedPaper, Settings.Default.JpegQuality, DataBaseTextData, (int)Settings.Default.Çözünürlük, progress => Scanner.PdfSaveProgressValue = progress)).Save(Scanner.PdfFilePath);

            if (Settings.Default.ShowFile)
            {
                ExploreFile.Execute(Scanner.PdfFilePath);
            }

            if (Settings.Default.PlayNotificationAudio)
            {
                PlayNotificationSound(Settings.Default.AudioFilePath);
            }

            OnPropertyChanged(nameof(Scanner.Resimler));
            Scanner.Resimler.Clear();
            DataBaseTextData = null;
            DataBaseTextDataCompleted = false;
            Twain.ScanningComplete -= FastScanComplete;
            Scanner.ArayüzEtkin = true;
        }
        finally
        {
            if (SetShutdown)
            {
                Shutdown.DoExitWin(Shutdown.EWX_SHUTDOWN);
            }
        }
    }

    private IndexedObservableCollection<T> FirstLastReverseSequence<T>(List<T> items, Func<T, int> indexSelector) where T : IIndexable
    {
        items.Sort((a, b) => indexSelector(a) % 2 != indexSelector(b) % 2 ? indexSelector(a) % 2 == 1 ? -1 : 1 : indexSelector(a) % 2 == 0 ? indexSelector(b).CompareTo(indexSelector(a)) : indexSelector(a).CompareTo(indexSelector(b)));

        return[ .. items ];
    }

    private IndexedObservableCollection<T> FirstLastSequence<T>(ObservableCollection<T> images) where T : IIndexable
    {
        IndexedObservableCollection<T> result = [];
        int startIndex = 0;
        int endIndex = images.Count - 1;

        while (startIndex <= endIndex)
        {
            result.Add(images[startIndex++]);
            if (startIndex > endIndex)
            {
                break;
            }
            result.Add(images[endIndex--]);
        }

        return result;
    }

    private CroppedBitmap GenerateCroppedImage(BitmapSource evrak, int top, int left, int bottom, int right)
    {
        int height = bottom - top;
        int width = right - left;
        Int32Rect sourceRect = new(left, top, Math.Abs(width), Math.Abs(height));
        if (sourceRect.HasArea)
        {
            CroppedBitmap croppedbitmap = new(evrak, sourceRect);
            croppedbitmap.Freeze();
            return croppedbitmap;
        }
        return null;
    }

    private async Task<ObservableCollection<OcrData>> GetImageOcrData(ScannedImage item)
    {
        if (Scanner.ApplyDataBaseOcr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            Scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
            return await item.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage);
        }
        else
        {
            return null;
        }
    }

    private string GetPdfBatchNumberString(int i) => Scanner.PdfBatchNumberIsFirst ? $"{i + 1} {Scanner.PdfBatchNumberText}" : $"{Scanner.PdfBatchNumberText} {i + 1}";

    private List<ScannedImage> GetSelectedImages() => Scanner?.Resimler?.Where(z => z.Seçili).ToList();

    private int? GetSelectedImagesCount() => Scanner?.Resimler?.Count(z => z.Seçili);

    private List<T> GroupByFirstLastList<T>(List<T> scannedImages, int splitCount = 2)
    {
        int splitIndex = scannedImages.Count / splitCount;
        List<List<T>> splitLists = [];
        for (int i = 0; i < splitCount; i++)
        {
            splitLists.Add([ .. scannedImages.Skip(i * splitIndex).Take(splitIndex) ]);
        }
        return MixLists([ .. splitLists ]);
    }

    private async Task HandleTifXpsFileAsync(Func<string, Task<IEnumerable<BitmapFrame>>> loadPagesAsync, string filename, int totalPageCount)
    {
        IEnumerable<BitmapFrame> frames = await loadPagesAsync(filename);
        List<BitmapFrame> list = [ .. frames ];
        for (int i = 0; i < list.Count; i++)
        {
            BitmapFrame frame = list[i];
            if (frame is not null)
            {
                frame.Freeze();
                double deskewAngle = Settings.Default.UseDeskewAngle ? Deskew.GetDeskewAngle(frame) + 90 : 90;
                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        ScannedImage item = new() { DeskewAngle = deskewAngle, Resim = frame, FilePath = filename };
                        Scanner?.Resimler.Add(item);
                        PdfLoadProgressValue = (i + 1) / (double)totalPageCount;
                    });
            }
        }
        _ = await Dispatcher.InvokeAsync(() => PdfLoadProgressValue = 0);
    }

    private void ImgViewer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Image img || img.Parent is not ScrollViewer scrollviewer)
        {
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed && (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift))
        {
            isMouseDown = true;
            Cursor = Cursors.Cross;
        }

        if (e.RightButton == MouseButtonState.Pressed)
        {
            isRightMouseDown = true;
            Cursor = Cursors.Cross;
        }

        mousedowncoord = e.GetPosition(scrollviewer);
    }

    private void ImgViewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Image img || img.Parent is not ScrollViewer scrollviewer)
        {
            return;
        }
        if (isRightMouseDown && SeçiliResim.Resim is not null)
        {
            Point mousemovecoord = e.GetPosition(scrollviewer);
            double x1 = Math.Min(mousedowncoord.X, mousemovecoord.X);
            double y1 = Math.Min(mousedowncoord.Y, mousemovecoord.Y);
            double coordx = x1 + scrollviewer.HorizontalOffset;
            double coordy = y1 + scrollviewer.VerticalOffset;
            double widthmultiply = SeçiliResim.Resim.PixelWidth / scrollviewer.ExtentWidth;
            double heightmultiply = SeçiliResim.Resim.PixelHeight / scrollviewer.ExtentHeight;
            if (scrollviewer.ExtentWidth < scrollviewer.ViewportWidth)
            {
                coordx -= (scrollviewer.ViewportWidth - scrollviewer.ExtentWidth) / 2;
            }
            if (scrollviewer.ExtentHeight < scrollviewer.ViewportHeight)
            {
                coordy -= (scrollviewer.ViewportHeight - scrollviewer.ExtentHeight) / 2;
            }
            Int32Rect sourceRect = new((int)(coordx * widthmultiply), (int)(coordy * heightmultiply), 1, 1);
            if (sourceRect.X < SeçiliResim.Resim.PixelWidth && sourceRect.Y < SeçiliResim.Resim.PixelHeight)
            {
                CroppedBitmap croppedbitmap = new(SeçiliResim.Resim, sourceRect);
                byte[] pixels = new byte[4];
                croppedbitmap.CopyPixels(pixels, 4, 0);
                croppedbitmap.Freeze();
                Scanner.SourceColor = Color.FromRgb(pixels[2], pixels[1], pixels[0]).ToString();
                Scanner.AutoCropColor = Color.FromRgb(pixels[2], pixels[1], pixels[0]).ToString();
            }

            if (e.RightButton == MouseButtonState.Released)
            {
                isRightMouseDown = false;
                Cursor = Cursors.Arrow;
            }
        }

        if (isMouseDown)
        {
            Point mousemovecoord = e.GetPosition(scrollviewer);
            Grid grid = scrollviewer.FindVisualParent<ImageViewer>().FindVisualParent<Grid>();
            Canvas cnv = grid.GetFirstVisualChild<Canvas>();
            if (!cnv.Children.Contains(selectionbox))
            {
                _ = cnv.Children.Add(selectionbox);
            }

            double x1 = Math.Min(mousedowncoord.X, mousemovecoord.X);
            double x2 = Math.Max(mousedowncoord.X, mousemovecoord.X);
            double y1 = Math.Min(mousedowncoord.Y, mousemovecoord.Y);
            double y2 = Math.Max(mousedowncoord.Y, mousemovecoord.Y);
            Canvas.SetLeft(selectionbox, x1);
            Canvas.SetTop(selectionbox, y1);
            selectionbox.Width = x2 - x1;
            selectionbox.Height = y2 - y1;

            if (e.LeftButton != MouseButtonState.Released)
            {
                return;
            }
            cnv.Children.Remove(selectionbox);
            width = Math.Abs(x2 - x1);
            height = Math.Abs(y2 - y1);
            double coordx = x1 + scrollviewer.HorizontalOffset;
            double coordy = y1 + scrollviewer.VerticalOffset;
            ImgData = BitmapFrame.Create((BitmapSource)img.Source).CaptureScreen(coordx, coordy, width, height, scrollviewer);

            if (Keyboard.Modifiers == ModifierKeys.Shift && ImgData is not null)
            {
                using MemoryStream ms = new(ImgData);
                BitmapFrame bitmapframe = ms.GenerateBitmapFrameFromMemoryStream();
                bitmapframe.Freeze();
                ScannedImage item = new() { Resim = bitmapframe };
                Scanner.Resimler.Add(item);
            }

            mousedowncoord.X = mousedowncoord.Y = 0;
            isMouseDown = false;
            Cursor = Cursors.Arrow;
            ImgData = null;
        }
    }

    private void ImgViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && sender is ImageViewer imageViewer)
        {
            imageViewer.Zoom = e.Delta > 0 ? imageViewer.Zoom + .05 : imageViewer.Zoom - .05;
            imageViewer.Zoom = Math.Max(imageViewer.MinZoom, Math.Min(imageViewer.MaxZoom, imageViewer.Zoom));
        }
    }

    private void InitializeEsclScannersControl()
    {
        if (Settings.Default?.EsclScanners?.Count > 0)
        {
            foreach (string item in Settings.Default.EsclScanners)
            {
                Scanner.Tarayıcılar.Add(item);
            }
        }
    }

    private void InitializeTwainControl()
    {
        try
        {
            Twain = new Twain(new WindowMessageHook(Window.GetWindow(Parent)));
            if (Twain?.SourceNames?.Count == 0)
            {
                return;
            }
            Scanner.Tarayıcılar = Twain.SourceNames;
            Twain.TransferImage += Twain_TransferImage;
            Twain.ScanningComplete += Twain_ScanningComplete;
            switch (Scanner.Tarayıcılar.Count)
            {
                case 0:
                    Settings.Default.SeçiliTarayıcı = string.Empty;
                    return;

                case 1:
                    Settings.Default.SeçiliTarayıcı = Twain.DefaultSourceName;
                    break;
            }
        }
        catch (Exception)
        {
            Scanner.ArayüzEtkin = false;
        }
    }

    private bool IsBlackAndWhiteMode() => Settings.Default.BackMode == (int)ColourSetting.BlackAndWhite && Settings.Default.Mode == (int)ColourSetting.BlackAndWhite;

    private void Language_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is TranslationSource translationSource)
        {
            Scanner.UiLanguage = translationSource.CurrentCulture;
        }
        if (!Settings.Default.UseSelectedProfile)
        {
            Scanner.FileName = Translation.GetResStringValue("DEFAULTSCANNAME");
        }
    }

    private async void ListBox_DropAsync(object sender, DragEventArgs e) => await ListBoxDropFileAsync(e);

    private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }
        Settings.Default.PreviewWidth += e.Delta > 0 ? 10 : -10;
        Settings.Default.PreviewWidth = Math.Max(MinPreviewWidth, Math.Min(MaxPreviewWidth, Settings.Default.PreviewWidth));
    }

    private List<T> MixLists<T>(List<T>[] lists)
    {
        int maxLength = lists.Max(list => list.Count);
        List<T> mixedList = [];
        for (int i = 0; i < maxLength; i++)
        {
            foreach (List<T> list in lists)
            {
                if (i < list.Count)
                {
                    mixedList.Add(list[i]);
                }
            }
        }
        return mixedList;
    }

    private void OnMediaPositionChanged(object sender, EventArgs e)
    {
        Scanner.ProgressState = TaskbarItemProgressState.Normal;
        Scanner.PdfSaveProgressValue = (double)mediaViewer.MediaPosition.Ticks / mediaViewer.EndTimeSpan.Ticks;
    }

    private void OrganizeDroppedData(ScannedImage droppedData, ScannedImage target)
    {
        IndexedObservableCollection<ScannedImage> resimler = Scanner.Resimler;

        int removedIdx = resimler.IndexOf(droppedData);
        int targetIdx = resimler.IndexOf(target);

        if (removedIdx == -1 || targetIdx == -1 || droppedData == target)
        {
            return;
        }

        resimler.RemoveAt(removedIdx);
        resimler.Insert(targetIdx, droppedData);
    }

    private void PdfData_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "PageNumber")
        {
            foreach (PdfData page in PdfPages)
            {
                page.BorderBrush = null;
            }
            foreach (PdfData item in PdfPages?.GroupBy(x => x.PageNumber).Where(g => g.Count() > 1).SelectMany(g => g))
            {
                item.BorderBrush = Brushes.Red;
            }
        }
    }

    private void PdfImportViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Delta > 0)
            {
                PdfImportViewer?.PdfViewer?.ZoomIncrease?.Execute(null);
            }
            else
            {
                PdfImportViewer?.PdfViewer?.ZoomDecrease?.Execute(null);
            }
        }
    }

    private async Task PdfPageRangeSaveFileAsync(string loadfilename, string savefilename, int start, int end)
    {
        await Task.Run(
            () =>
            {
                using PdfDocument outputDocument = loadfilename.ExtractPdfPages(start, end);
                if (outputDocument is null)
                {
                    return;
                }
                outputDocument.ApplyDefaultPdfCompression();
                outputDocument.Save(savefilename);
            });
    }

    private async Task ProcessDropFileList(string pdfFilePath, string temporaryPdf, string[] processedFiles)
    {
        StringCollection clipboardFiles = Clipboard.GetFileDropList();
        List<string> clipboardPdfFiles = [ .. clipboardFiles.Cast<string>().Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)) ];
        List<string> clipboardImageFiles = [ .. clipboardFiles.Cast<string>().Where(z => imagefileextensions.Contains(Path.GetExtension(z).ToLowerInvariant())) ];
        if (clipboardPdfFiles.Any() || clipboardImageFiles.Any())
        {
            await Task.Run(
                () =>
                {
                    if (clipboardPdfFiles.Any())
                    {
                        clipboardPdfFiles.Add(pdfFilePath);
                        clipboardPdfFiles.ToArray().MergePdf().Save(pdfFilePath);
                    }

                    if (clipboardImageFiles.Any())
                    {
                        using (PdfDocument document = clipboardImageFiles.GeneratePdf(SelectedPaper, progress => Scanner.PdfSaveProgressValue = progress))
                        {
                            document.Save(temporaryPdf);
                        }

                        processedFiles.MergePdf().Save(pdfFilePath);
                    }
                });
        }
    }

    private async Task ProcessImageFile(string pdfFilePath, string temporaryPdf, string[] processedFiles)
    {
        BitmapSource image = Clipboard.GetImage();
        if (image is not null)
        {
            BitmapFrame bitmapFrame = BitmapFrame.Create(image);
            await Task.Run(
                () =>
                {
                    using (PdfDocument pdfDocument = bitmapFrame.GeneratePdf(null, Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution))
                    {
                        pdfDocument.Save(temporaryPdf);
                    }
                    processedFiles.MergePdf().Save(pdfFilePath);
                });
        }
    }

    private async Task RemoveProcessedImages()
    {
        await Dispatcher.InvokeAsync(
            () =>
            {
                if (Settings.Default.RemoveProcessedImage)
                {
                    SeçiliListeTemizle.Execute(null);
                }
            });
    }

    private void RemoveSelectedImage(ScannedImage item)
    {
        _ = Scanner.Resimler?.Remove(item);
        ToolBox.ResetCropMargin();
        GC.Collect();
    }

    private async Task ReverseFileAsync(string loadfilename, string savefilename)
    {
        await Task.Run(
            () =>
            {
                using PdfDocument inputDocument = PdfReader.Open(loadfilename, PdfDocumentOpenMode.Import, PdfGeneration.PasswordProvider);
                if (inputDocument is null)
                {
                    return;
                }
                using PdfDocument outputdocument = new();
                if (outputdocument is null)
                {
                    return;
                }
                for (int i = inputDocument.PageCount - 1; i >= 0; i--)
                {
                    _ = outputdocument.AddPage(inputDocument.Pages[i]);
                }
                outputdocument.Save(savefilename);
            });
    }

    private async Task<BitmapFrame> RotateImage(ScannedImage image, double angle)
    {
        BitmapFrame bitmapframe = BitmapFrame.Create(await image.Resim.RotateImageAsync(angle));
        bitmapframe.Freeze();
        return bitmapframe;
    }

    private void Run_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Run run)
        {
            TextBlock textBlock = run.Parent as TextBlock;
            if (textBlock?.Parent is not StackPanel stackPanel)
            {
                return;
            }

            DragMoveStarted = true;
            using Bitmap img = stackPanel.ToRenderTargetBitmap().BitmapSourceToBitmap();
            IntPtr hIcon = img.GetHicon();
            try
            {
                using Icon icon = Icon.FromHandle(hIcon);
                DragCursor = CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
                _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
                e.Handled = true;
            }
            finally
            {
                _ = ShellIcon.Win32.DestroyIcon(hIcon);
                DragMoveStarted = false;
            }
        }
    }

    private void SaveJb2Image(BitmapFrame scannedImage, string filename)
    {
        Dispatcher.Invoke(
            () =>
            {
                byte[] data;
                using Bitmap bmp = scannedImage.BwAdaptiveThreshold(Settings.Default.Jb2Saturation, Settings.Default.Jb2Threshold).BitmapSourceToBitmap();
                data = JBig2Encoder.Encode(bmp, false);
                File.WriteAllBytes(filename, data);
                data = null;
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private void SaveJpgImage(BitmapFrame scannedImage, string filename)
    {
        Dispatcher.Invoke(
            () =>
            {
                File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg, Settings.Default.JpegQuality));
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private async Task SaveJpgImageAsync(List<ScannedImage> images, string filename, int parallelDegree = 1, Action<double> progressCallback = null)
    {
        string directory = Path.GetDirectoryName(filename);
        await Task.Run(
            () =>
            {
                ParallelOptions options = new() { MaxDegreeOfParallelism = parallelDegree };

                _ = Parallel.For(
                    0,
                    images.Count,
                    options,
                    i =>
                    {
                        ScannedImage scannedimage = images[i];
                        byte[] bytes = scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg, Settings.Default.JpegQuality);
                        lock (_lockObject)
                        {
                            string uniqueFilename = directory.SetUniqueFile(Path.GetFileNameWithoutExtension(filename), "jpg");
                            File.WriteAllBytes(uniqueFilename, bytes);
                            bytes = null;
                        }
                        progressCallback?.Invoke((i + 1) / (double)images.Count);
                    });
            });
        Dispatcher.Invoke(
            () =>
            {
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private async Task SavePdfImageAsync(List<ScannedImage> images, string filename, Scanner scanner, Paper paper, bool applyocr, bool blackwhite = false, int dpi = 120, List<ObservableCollection<OcrData>> scannedtext = null)
    {
        if (applyocr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
            scannedtext ??= [];
            scanner.ProgressState = TaskbarItemProgressState.Normal;
            for (int i = 0; i < images.Count; i++)
            {
                ScannedImage image = images[i];
                await Dispatcher.Invoke(
                    async () =>
                    {
                        ObservableCollection<OcrData> item = await image.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage);
                        scannedtext.Add(item);
                    });
                scanner.PdfSaveProgressValue = (i + 1) / (double)images.Count;
            }
            Dispatcher.Invoke(
                () =>
                {
                    Scanner.SaveFileFullPath = filename;
                    OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
                });
            scanner.PdfSaveProgressValue = 0;
        }

        scanner.SaveProgressBarForegroundBrush = defaultsaveprogressforegroundcolor;
        Format fileFormat = blackwhite ? Format.Tiff : Format.Jpg;
        using PdfDocument pdfdocument = await images.GeneratePdfAsync(fileFormat, paper, Settings.Default.JpegQuality, scannedtext, dpi, progress => Scanner.PdfSaveProgressValue = progress);
        pdfdocument.Save(filename);
        Dispatcher.Invoke(
            () =>
            {
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private void SaveTifImage(BitmapFrame scannedImage, string filename)
    {
        Format format = (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite ? Format.Tiff : Format.TiffRenkli;
        Dispatcher.Invoke(
            () =>
            {
                File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(format));
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private async Task SaveTifImageAsync(List<ScannedImage> images, string filename)
    {
        await Task.Run(
            () =>
            {
                TiffBitmapEncoder tifccittencoder = new() { Compression = (ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale ? TiffCompressOption.Zip : TiffCompressOption.Ccitt4 };
                for (int i = 0; i < images.Count; i++)
                {
                    ScannedImage scannedimage = images[i];
                    tifccittencoder.Frames.Add(scannedimage.Resim);
                }

                using FileStream stream = new(filename, FileMode.Create);
                tifccittencoder.Save(stream);
            });
        Dispatcher.Invoke(
            () =>
            {
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private async Task SaveTxtFileAsync(BitmapFrame bitmapFrame, string fileName)
    {
        if (bitmapFrame is null || string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            return;
        }
        await Dispatcher.Invoke(
            async () =>
            {
                ObservableCollection<OcrData> ocrtext = await bitmapFrame.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage);
                File.WriteAllText(fileName, string.Join(" ", ocrtext.Select(z => z.Text)));
                Scanner.SaveFileFullPath = fileName;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private async Task SaveTxtFileAsync(List<ScannedImage> images, string fileName, Action<double> progressCallback = null)
    {
        if (images == null || string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            return;
        }

        int total = images.Count;
        int completed = 0;
        string directory = Path.GetDirectoryName(fileName);
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        int maxParallel = Math.Max(1, Environment.ProcessorCount / 3);
        SemaphoreSlim semaphore = new(maxParallel);
        List<Task> tasks = [];
        object lockObj = new();

        for (int i = 0; i < total; i++)
        {
            int index = i;
            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            byte[] imgBytes = images[index].Resim.ToTiffJpegByteArray(Format.Jpg);
                            ObservableCollection<OcrData> ocrText = await imgBytes.OcrAsync(Scanner.SelectedTtsLanguage);

                            string outputPath = Path.Combine(directory, $"{baseName}{index}.txt");
                            File.WriteAllText(outputPath, string.Join(" ", ocrText.Select(z => z.Text)));

                            lock (lockObj)
                            {
                                completed++;
                            }
                            await Application.Current.Dispatcher.InvokeAsync(() => progressCallback?.Invoke(completed / (double)total));
                        }
                        finally
                        {
                            _ = semaphore.Release();
                        }
                    }));
        }

        await Task.WhenAll(tasks);

        await Application.Current.Dispatcher
        .InvokeAsync(
            () =>
            {
                Scanner.SaveFileFullPath = fileName;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private void SaveWebpImage(BitmapFrame scannedImage, string filename)
    {
        Dispatcher.Invoke(
            () =>
            {
                File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg).WebpEncode(Settings.Default.WebpQuality));
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private async Task SaveWebpImageAsync(List<ScannedImage> images, string filename, int parallelDegree = 1, Action<double> progressCallback = null)
    {
        string directory = Path.GetDirectoryName(filename);
        await Task.Run(
            () =>
            {
                ParallelOptions options = new() { MaxDegreeOfParallelism = parallelDegree };

                _ = Parallel.For(
                    0,
                    images.Count,
                    options,
                    i =>
                    {
                        ScannedImage scannedimage = images[i];
                        byte[] bytes = scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg).WebpEncode(Settings.Default.WebpQuality);
                        lock (_lockObject)
                        {
                            string uniqueFilename = directory.SetUniqueFile(Path.GetFileNameWithoutExtension(filename), "webp");
                            File.WriteAllBytes(uniqueFilename, bytes);
                            bytes = null;
                        }
                        progressCallback?.Invoke((i + 1) / (double)images.Count);
                    });
            });
        Dispatcher.Invoke(
            () =>
            {
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private void SaveXpsImage(BitmapFrame scannedImage, string filename)
    {
        _ = Dispatcher.Invoke(
            async () =>
            {
                FixedDocument fixedDoc = new();
                PageContent pageContent = new();
                double width = SelectedPaper.Width * 96 / Inch;
                double height = SelectedPaper.Height * 96 / Inch;
                double Width = width == 0 ? scannedImage.PixelWidth : width;
                double Height = height == 0 ? scannedImage.PixelHeight : height;
                FixedPage fixedPage = new() { Width = Width, Height = Height };
                System.Windows.Controls.Image image = new();
                image.BeginInit();
                image.Source = scannedImage.PixelWidth > scannedImage.PixelHeight ? await scannedImage.RotateImageAsync(-1) : scannedImage;
                image.Width = fixedPage.Width;
                image.Stretch = Stretch.UniformToFill;
                image.Height = fixedPage.Height;
                image.EndInit();
                _ = fixedPage.Children.Add(image);
                ((IAddChild)pageContent).AddChild(fixedPage);
                _ = fixedDoc.Pages.Add(pageContent);
                using XpsDocument xpsd = new(filename, FileAccess.Write);
                XpsDocumentWriter xw = XpsDocument.CreateXpsDocumentWriter(xpsd);
                xw.Write(fixedDoc);
                image = null;
                Scanner.SaveFileFullPath = filename;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private void SaveZipImage(List<ScannedImage> selectedImages, string fileName, Action<double> progressCallback = null)
    {
        using FileStream zipStream = File.OpenWrite(fileName);
        using IWriter zipWriter = WriterFactory.Open(zipStream, SharpCompress.Common.ArchiveType.Zip, new ZipWriterOptions(SharpCompress.Common.CompressionType.None) { UseZip64 = true });
        int count = selectedImages.Count;

        for (int i = 0; i < count; i++)
        {
            ScannedImage image = selectedImages[i];
            byte[] buffer;
            string fileNameInZip;

            if (EncodeAsWebp)
            {
                buffer = image.Resim.ToTiffJpegByteArray(Format.Jpg).WebpEncode(70);
                fileNameInZip = $"{image.Index}.webp";
            }
            else if (EncodeAsJb2)
            {
                using Bitmap bitmap = image.Resim.BwAdaptiveThreshold(Settings.Default.Jb2Saturation, Settings.Default.Jb2Threshold).BitmapSourceToBitmap();
                buffer = JBig2Encoder.Encode(bitmap, false);
                fileNameInZip = $"{image.Index}.jb2";
            }
            else
            {
                buffer = image.Resim.ToTiffJpegByteArray(Format.Jpg);
                fileNameInZip = $"{image.Index}.jpg";
            }
            using MemoryStream ms = new(buffer, false);
            zipWriter.Write(fileNameInZip, ms);
            progressCallback?.Invoke((i + 1) / (double)count);
        }

        Dispatcher.Invoke(
            () =>
            {
                Scanner.SaveFileFullPath = fileName;
                OnPropertyChanged(nameof(Scanner.SaveFileFullPath));
            });
    }

    private async void ScanComplete(object sender, ScanningCompleteEventArgs e)
    {
        try
        {
            if (Scanner.ScanSeperate)
            {
                DataBaseTextData = [];
                QrCode.QrCode qrcode = Scanner.UsePageSeperator ? new QrCode.QrCode() : null;
                for (int i = 0; i < Scanner.Resimler.Count; i++)
                {
                    Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
                    ScannedImage scannedImage = Scanner.Resimler[i];
                    DataBaseTextDataCompleted = false;

                    if (Settings.Default.AutoRotateBasedText && TesseractOrientationFileExists)
                    {
                        await AutoRotateBasedTextOrientation([ scannedImage ], 1);
                    }

                    if (qrcode is not null)
                    {
                        Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(scannedImage.Resim);
                        OnPropertyChanged(nameof(Scanner.DetectPageSeperator));
                    }

                    DataBaseTextData.Clear();
                    DataBaseTextData.Add(await GetImageOcrData(scannedImage));
                    await SavePdfImageAsync([ scannedImage ], Scanner.PdfFilePath, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, false, Settings.Default.ImgLoadResolution, DataBaseTextData);
                    Scanner.PdfSaveProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                    DataBaseTextDataCompleted = true;
                    OnPropertyChanged(nameof(Scanner.Resimler));
                }
            }

            if (Settings.Default.PlayNotificationAudio)
            {
                PlayNotificationSound(Settings.Default.AudioFilePath);
            }
        }
        finally
        {
            DataBaseTextData = null;
            DataBaseTextDataCompleted = false;
            Scanner.PdfSaveProgressValue = 0;
            Twain.ScanningComplete -= ScanComplete;
        }
    }

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "CropLeft" or "CropTop" or "CropRight" or "CropBottom" && SeçiliResim is not null)
        {
            Int32Rect sourceRect = CropPreviewImage(SeçiliResim.Resim);
            if (sourceRect.HasArea)
            {
                Scanner.CroppedImage = new CroppedBitmap(SeçiliResim.Resim, sourceRect);
                Scanner.CroppedImage.Freeze();
                Scanner.CopyCroppedImage = Scanner.CroppedImage;
                Scanner.CopyCroppedImage.Freeze();
                Scanner.CropDialogExpanded = true;
            }
        }

        if (e.PropertyName is "SelectedProfile" && !string.IsNullOrWhiteSpace(Scanner.SelectedProfile))
        {
            string[] selectedprofile = Scanner.SelectedProfile.Split('|');
            Settings.Default.Çözünürlük = double.Parse(selectedprofile[1]);
            Settings.Default.Adf = bool.Parse(selectedprofile[2]);
            Settings.Default.Mode = int.Parse(selectedprofile[3]);
            Scanner.Duplex = bool.Parse(selectedprofile[4]);
            Scanner.ShowUi = bool.Parse(selectedprofile[5]);
            Settings.Default.ShowFile = bool.Parse(selectedprofile[7]);
            Scanner.DetectEmptyPage = bool.Parse(selectedprofile[8]);
            Scanner.FileName = selectedprofile[9];
            Scanner.InvertImage = bool.Parse(selectedprofile[10]);
            Scanner.ApplyMedian = bool.Parse(selectedprofile[11]);
            Settings.Default.SeçiliTarayıcı = selectedprofile[12];
            Settings.Default.AutoCropImage = bool.Parse(selectedprofile[13]);
            Scanner.UseFilmScanner = bool.Parse(selectedprofile[14]);
            Settings.Default.DefaultProfile = Scanner.SelectedProfile;
            Settings.Default.Save();
        }

        if (e.PropertyName is "UsePageSeperator")
        {
            if (!Settings.Default.UseSelectedProfile && !Scanner.UsePageSeperator)
            {
                Scanner.FileName = Translation.GetResStringValue("DEFAULTSCANNAME");
            }
            OnPropertyChanged(nameof(Scanner.UsePageSeperator));
        }

        if (e.PropertyName is "Duplex" && !Scanner.Duplex)
        {
            Scanner.PaperBackScan = false;
        }

        if (e.PropertyName is "ApplyPdfSaveOcr" && Scanner.ApplyPdfSaveOcr && Settings.Default.UsePdfInternalTextData)
        {
            Scanner.ApplyPdfSaveOcr = false;
            MessageBox.Show($"{Translation.GetResStringValue("PDFINTERNAL")}\n{Translation.GetResStringValue("RESET")}", AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
    }

    private void SetCropPageResolution()
    {
        PageHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.Çözünürlük);
        PageWidth = (int)(SelectedPaper.Width / Inch * Settings.Default.Çözünürlük);
        Settings.Default.Bottom = PageHeight;
        Settings.Default.Right = PageWidth;
    }

    private IndexedObservableCollection<T> Shuffle<T>(IList<T> collection, Random random) where T : IIndexable
    {
        for (int i = collection.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (collection[j], collection[i]) = (collection[i], collection[j]);
        }
        return[ .. collection ];
    }

    private List<T[]> SplitArray<T>(T[] array, params int[] indices)
    {
        if (indices.Length == 0)
        {
            throw new ArgumentException("At least one split index is required.");
        }
        Array.Sort(indices);
        List<T[]> parts = [ with(indices.Length + 1) ];
        for (int i = 0; i < indices.Length; i++)
        {
            int startIndex = i == 0 ? 0 : indices[i - 1];
            int length = i == 0 ? indices[i] : indices[i] - indices[i - 1];
            parts.Add([ .. array.Skip(startIndex).Take(length) ]);
        }
        parts.Add([ .. array.Skip(indices[indices.Length - 1]) ]);
        return parts;
    }

    private void StackPanel_Drop(object sender, DragEventArgs e) => DropFile(sender, e);

    private void StackPanel_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (e.Effects == DragDropEffects.Move)
        {
            if (DragCursor is not null)
            {
                e.UseDefaultCursors = false;
                _ = Mouse.SetCursor(DragCursor);
            }
        }
        else
        {
            e.UseDefaultCursors = true;
        }
        e.Handled = true;
    }

    private void Twain_ScanningComplete(object sender, ScanningCompleteEventArgs e) => Scanner.ArayüzEtkin = true;

    private async void Twain_TransferImage(object sender, TransferImageEventArgs e)
    {
        if (e.Image is null)
        {
            return;
        }
        await Task.Delay(TimeSpan.FromSeconds(Settings.Default.ScanBetweenDelay));
        using Bitmap bitmap = e.Image;
        if (Scanner.DetectEmptyPage && bitmap.IsEmptyPage(Settings.Default.EmptyThreshold))
        {
            return;
        }

        BitmapSource evrak = Scanner?.Resimler.Count % 2 == 0
                             ? EvrakOluştur(bitmap, (ColourSetting)Settings.Default.Mode, PageHeight)
                             : Scanner?.PaperBackScan == true ? EvrakOluştur(bitmap, (ColourSetting)Settings.Default.BackMode, PageHeight) : EvrakOluştur(bitmap, (ColourSetting)Settings.Default.Mode, PageHeight);

        if (Scanner.ApplyMedian)
        {
            evrak = evrak.MedianFilterBitmap(Settings.Default.MedianValue).ToBitmapImage();
        }

        if (Settings.Default.CropScan)
        {
            evrak = GenerateCroppedImage(evrak, Settings.Default.Top, Settings.Default.Left, Settings.Default.Bottom, Settings.Default.Right);
        }

        if (Settings.Default.AutoCropImage)
        {
            evrak = await evrak.AutoCropImage(Settings.Default.AutoCropThreshold);
        }

        if (Scanner.InvertImage)
        {
            evrak = evrak.InvertBitmap().ToBitmapImage();
        }

        if (Settings.Default.ApplyVerticalLineRemove)
        {
            WriteableBitmap wbmp = new(evrak);
            evrak = wbmp.RemoveVerticalLines(Settings.Default.VerticalLineThreshold);
            wbmp = null;
        }

        evrak.Freeze();
        BitmapFrame bitmapFrame = BitmapFrame.Create(evrak);
        bitmapFrame.Freeze();
        evrak = null;
        ScannedImage item = new() { Resim = bitmapFrame, RotationAngle = (double)SelectedRotation, FlipAngle = (double)SelectedFlip };
        item.DeskewAngle = Deskew.GetDeskewAngle(item.Resim) + 90;
        if (Settings.Default.AutoRotateBasedText && TesseractOrientationFileExists)
        {
            _ = AutoRotateBasedTextOrientation([ item ], 1).ConfigureAwait(true).GetAwaiter();
        }
        Scanner?.Resimler?.Add(item);
    }

    private void TwainCtrl_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeTwainControl();
        InitializeEsclScannersControl();

    }

    private async void TwainCtrl_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SelectedCompressionProfile" && SelectedCompressionProfile is not null)
        {
            Settings.Default.Mode = SelectedCompressionProfile.Item2;
            Settings.Default.Çözünürlük = SelectedCompressionProfile.Item3;
            Settings.Default.ImgLoadResolution = (int)SelectedCompressionProfile.Item3;
            Settings.Default.JpegQuality = (int)SelectedCompressionProfile.Item5;
            Scanner.UseMozJpegEncoding = SelectedCompressionProfile.Item4 && MozJpeg.MozJpeg.MozJpegDllExists;
        }

        if (e.PropertyName is "SelectedPaper" && SelectedPaper is not null)
        {
            ToolBox.Paper = SelectedPaper;
            DecodeHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.Çözünürlük);
            SetCropPageResolution();
            Settings.Default.DefaultPaper = SelectedPaper.PaperType;
        }

        if (e.PropertyName is "SeekIndex" && SeekIndex >= 0 && SeekIndex < Scanner.Resimler.Count)
        {
            Lb?.ScrollIntoView(Lb.Items[SeekIndex]);
        }

        if (e.PropertyName is "AllImageRotationAngle" && AllImageRotationAngle != 0)
        {
            bool shiftpressed = Keyboard.Modifiers == ModifierKeys.Shift;
            bool altpressed = Keyboard.Modifiers == ModifierKeys.Alt;
            if (Scanner.Resimler.Count > 0 && MessageBox.Show($"{Translation.GetResStringValue("LONGTIMEJOB")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
            {
                AllImageRotationAngle = 0;
                return;
            }

            List<ScannedImage> selectedimages = GetSelectedImages();
            if (shiftpressed)
            {
                for (int i = 0; i < selectedimages.Count; i++)
                {
                    ScannedImage image = selectedimages[i];
                    BitmapFrame bitmapframe = BitmapFrame.Create(await image.Resim.FlipImageAsync(AllImageRotationAngle));
                    bitmapframe.Freeze();
                    image.Resim = bitmapframe;
                    AllRotateProgressValue = (i + 1) / (double)selectedimages.Count;
                }
                GC.Collect();
                AllRotateProgressValue = 0;
                AllImageRotationAngle = 0;
                return;
            }

            if (altpressed)
            {
                for (int i = 0; i < selectedimages.Count; i++)
                {
                    ScannedImage image = selectedimages[i];
                    image.Resim = await RotateImage(image, AllImageRotationAngle);
                    AllRotateProgressValue = (i + 1) / (double)selectedimages.Count;
                }
                GC.Collect();
                AllRotateProgressValue = 0;
                AllImageRotationAngle = 0;
                return;
            }

            for (int i = 0; i < Scanner.Resimler.Count; i++)
            {
                ScannedImage image = Scanner.Resimler[i];
                image.Resim = await RotateImage(image, AllImageRotationAngle);
                AllRotateProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
            }
            GC.Collect();
            AllRotateProgressValue = 0;
            AllImageRotationAngle = 0;
        }

        if (e.PropertyName is "SeçiliResim" && SeçiliResim is null)
        {
            Scanner.CropDialogExpanded = false;
        }

        if (e.PropertyName is "CropAllMargin")
        {
            Settings.Default.Top = Settings.Default.Left = CropAllMargin;
            Settings.Default.Bottom = PageHeight - CropAllMargin;
            Settings.Default.Right = PageWidth - CropAllMargin;
        }

        if (e.PropertyName is "TesseractOrientationFileExists" && !TesseractOrientationFileExists)
        {
            Settings.Default.AutoRotateBasedText = false;
        }
    }

    private void ZipExtractSingleFile(string zipfileName, string zipcontentfilename, string destinationfilename)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipfileName);
        archive.Entries?.FirstOrDefault(z => z.FullName == zipcontentfilename)?.ExtractToFile(destinationfilename, true);
    }

    private record DeletedImageEntry
    {
        public DeletedImageEntry(ScannedImage image, int index)
        {
            Image = image;
            Index = index;
        }

        public ScannedImage Image { get; }

        public int Index { get; }
    }
}