using Extensions;
using Extensions.Controls;
using Microsoft.Win32;
using Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfViewer;
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
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
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
using System.Xml.Serialization;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.TwainNative;
using TwainWpf.Wpf;
using static Extensions.ExtensionMethods;
using static TwainControl.DrawControl;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
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

public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable
{
    public const double Inch = 2.54d;
    public static readonly string AppName = Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Title;
    public static DispatcherTimer CameraQrCodeTimer;
    public static Task Filesavetask;
    private readonly object _lockObject = new();
    private readonly SolidColorBrush bluesaveprogresscolor = Brushes.DeepSkyBlue;
    private readonly Brush defaultsaveprogressforegroundcolor = (Brush)new BrushConverter().ConvertFromString("#FF06B025");
    private readonly string[] imagefileextensions = [".tiff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".png", ".bmp"];
    private readonly Rectangle selectionbox = new() { Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), StrokeThickness = 2, StrokeDashArray = new DoubleCollection([1]) };
    private int cropAllMaximumWidth;
    private bool disposedValue;
    private GridLength documentGridLength = new(5, GridUnitType.Star);
    private Task fileloadtask;
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
                Twain.SelectSource(Settings.Default.SeçiliTarayıcı);
                Twain.StartScanning(DefaultScanSettings());
                Twain.ScanningComplete += ScanComplete;
            },
            parameter => !Environment.Is64BitProcess && AnyScannerExist() && !string.IsNullOrWhiteSpace(Settings.Default.SeçiliTarayıcı) && Policy.CheckPolicy(nameof(ScanImage)));

        FastScanImage = new RelayCommand<object>(
            async parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }
                GC.Collect();
                await Task.Delay(TimeSpan.FromSeconds(Settings.Default.ScanDelay));
                Scanner.ArayüzEtkin = false;
                Scanner.Resimler = [];
                Scanner.Resimler.CollectionChanged -= Scanner.Resimler_CollectionChanged;
                Scanner.Resimler.CollectionChanged += Scanner.Resimler_CollectionChanged;
                Twain.SelectSource(Settings.Default.SeçiliTarayıcı);
                Twain.StartScanning(DefaultScanSettings());
                Twain.ScanningComplete += FastScanComplete;
            },
            parameter => !Environment.Is64BitProcess && AnyScannerExist() && !string.IsNullOrWhiteSpace(Settings.Default.SeçiliTarayıcı) && Scanner?.AutoSave == true && FileNameValid(Scanner?.FileName) && Policy.CheckPolicy(nameof(FastScanImage)));

        ResimSil = new RelayCommand<object>(
            parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }

                ScannedImage item = parameter as ScannedImage;
                UndoImageIndex = Scanner.Resimler?.IndexOf(item);
                UndoImage = item;
                CanUndoImage = true;
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
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
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
                        UndoImageIndex = Scanner.Resimler?.IndexOf(item);
                        UndoImage = item;
                        CanUndoImage = true;
                        RemoveSelectedImage(item);
                        SeekIndex = UndoImageIndex ?? 0;
                    });
            },
            parameter => parameter is ScannedImage && Scanner.ArayüzEtkin);

        ResimSilGeriAl = new RelayCommand<object>(
            parameter =>
            {
                Scanner.Resimler?.Insert((int)UndoImageIndex, UndoImage);
                CanUndoImage = false;
                UndoImage = null;
                UndoImageIndex = null;
            },
            parameter => CanUndoImage && UndoImage is not null);

        InvertImage = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is not ScannedImage item)
                {
                    return;
                }
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    BitmapFrame bitmapframe = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Jpeg));
                    bitmapframe?.Freeze();
                    item.Resim = bitmapframe;
                    bitmapframe = null;
                    GC.Collect();
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    BitmapFrame bitmapframe = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold, true).ToBitmapImage(ImageFormat.Jpeg));
                    bitmapframe?.Freeze();
                    item.Resim = bitmapframe;
                    bitmapframe = null;
                    GC.Collect();
                    return;
                }

                BitmapFrame bitmapFrame = BitmapFrame.Create(item.Resim.InvertBitmap().ToBitmapImage());
                bitmapFrame?.Freeze();
                item.Resim = bitmapFrame;
                bitmapFrame = null;
                GC.Collect();
            },
            parameter => true);

        AutoDeskewImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is ScannedImage item &&
                MessageBox.Show($"{Translation.GetResStringValue("DESKEW")} {Translation.GetResStringValue("APPLY")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    double deskewAngle = Deskew.GetDeskewAngle(item.Resim);
                    BitmapFrame bitmapFrame = BitmapFrame.Create(await item.Resim.RotateImageAsync(deskewAngle, Brushes.White));
                    bitmapFrame?.Freeze();
                    item.Resim = bitmapFrame;
                    bitmapFrame = null;
                    GC.Collect();
                }
            },
            parameter => true);

        ManualDeskewImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is ScannedImage item)
                {
                    BitmapFrame bitmapFrame = BitmapFrame.Create(await item.Resim.RotateImageAsync(CustomDeskewAngle, Brushes.White));
                    bitmapFrame?.Freeze();
                    item.Resim = bitmapFrame;
                    bitmapFrame = null;
                    GC.Collect();
                }
            },
            parameter => CustomDeskewAngle != 0);

        TümünüOtomatikDöndür = new RelayCommand<object>(
            parameter =>
            {
                ExtendedMessageBox extendedmessagebox = new() { CustomContentVisible = Visibility.Visible, CustomContentHeight = 20, NoButton = Visibility.Visible, YesButton = Visibility.Visible, };
                NumericUpDown numericUpDown = new() { Minimum = 1, Maximum = Environment.ProcessorCount, Value = 4, IsReadOnly = true };
                int parallelcount = (int)numericUpDown.Value;
                extendedmessagebox.CustomContent = numericUpDown;
                extendedmessagebox.ShowDialog(
                    Window.GetWindow(this),
                    Translation.GetResStringValue("LONGTIMEJOB"),
                    $"{Translation.GetResStringValue("ALL")} {Translation.GetResStringValue("AUTOROTATE")}",
                    async () => await AutoRotateBasedTextOrientation(Scanner.Resimler, parallelcount));
                GC.Collect();
            },
            parameter => Scanner?.Resimler?.Any() == true && TesseractOrientationFileExists);

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
            parameter =>
            {
                if (MessageBox.Show($"{Translation.GetResStringValue("LONGTIMEJOB")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    return;
                }
                bool bw = Keyboard.Modifiers == ModifierKeys.Alt;
                bool grayscale = Keyboard.Modifiers == ModifierKeys.Shift;
                foreach (ScannedImage item in GetSelectedImages())
                {
                    if (bw)
                    {
                        BitmapFrame blackandwhiteimage = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Jpeg));
                        blackandwhiteimage?.Freeze();
                        item.Resim = blackandwhiteimage;
                        blackandwhiteimage = null;
                        GC.Collect();
                        continue;
                    }

                    if (grayscale)
                    {
                        BitmapFrame grayimage = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold, true).ToBitmapImage(ImageFormat.Jpeg));
                        grayimage?.Freeze();
                        item.Resim = grayimage;
                        grayimage = null;
                        GC.Collect();
                        continue;
                    }

                    BitmapFrame bitmapFrame = BitmapFrame.Create(item.Resim.InvertBitmap().ToBitmapImage());
                    bitmapFrame?.Freeze();
                    item.Resim = bitmapFrame;
                    bitmapFrame = null;
                    GC.Collect();
                }
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
                    await SavePdfImageAsync(scannedImage.Resim, saveFileDialog.FileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr);
                }
            },
            parameter => FileNameValid(Scanner?.FileName));

        SaveSingleBwPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                SaveFileDialog saveFileDialog = new() { Filter = "Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true && parameter is ScannedImage scannedImage)
                {
                    await SavePdfImageAsync(scannedImage.Resim, saveFileDialog.FileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, true);
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
                string path = FolderDialog.SelectFolder(Translation.GetResStringValue("AUTOFOLDER"), new WindowInteropHelper(Window.GetWindow(this)).Handle, Settings.Default.AutoFolder);
                string oldpath = Settings.Default.AutoFolder;
                if (!string.IsNullOrEmpty(path))
                {
                    DriveInfo driveInfo = new(path);
                    if (driveInfo.DriveType == DriveType.CDRom)
                    {
                        _ = MessageBox.Show($"{Translation.GetResStringValue("ERROR")}\n{Translation.GetResStringValue("INVALIDFILENAME")}", AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    Settings.Default.AutoFolder = path;
                    Scanner.LocalizedPath = ShellIcon.GetDisplayName(path);
                }

                if (!string.IsNullOrWhiteSpace(oldpath) && oldpath != Settings.Default.AutoFolder)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("AUTOFOLDERCHANGE"), AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
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
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        int i = 0;
                        foreach (ScannedImage resimlerItem in GetSelectedImages())
                        {
                            ScannedImage item = resimlerItem;
                            Scanner.PdfFilePath = Path.GetDirectoryName(saveFileDialog.FileName).SetUniqueFile(Scanner.SaveFileName, "pdf");
                            await SavePdfImageAsync(item.Resim, Scanner.PdfFilePath, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr);
                            Scanner.PdfSaveProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                            i++;
                        }
                    }
                    return;
                }
                if (saveFileDialog.ShowDialog() == true)
                {
                    Filesavetask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SavePdfImageAsync(seçiliresimler, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, false, Settings.Default.ImgLoadResolution);
                        });
                    await RemoveProcessedImages();
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
                    Filesavetask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveJpgImageAsync(seçiliresimler, fileName, Settings.Default.WebPJpgFileProcessorCount, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
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
                    Filesavetask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SavePdfImageAsync(seçiliresimler, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, true, Settings.Default.ImgLoadResolution);
                        });
                    await RemoveProcessedImages();
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
                    Filesavetask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveTifImageAsync(seçiliresimler, fileName);
                        });
                    await RemoveProcessedImages();
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
                    Filesavetask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveTxtFileAsync(seçiliresimler, fileName, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
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
                    Filesavetask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            await SaveWebpImageAsync(seçiliresimler, fileName, Settings.Default.WebPJpgFileProcessorCount, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
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
                SaveFileDialog saveFileDialog = new() { Filter = "Zip Dosyası (*.zip)|*.zip", FileName = Scanner.SaveFileName, };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Filesavetask = Task.Run(
                        () =>
                        {
                            List<ScannedImage> seçiliresimler = GetSelectedImages();
                            string fileName = saveFileDialog.FileName;
                            SaveZipImage(seçiliresimler, fileName, progress => Scanner.PdfSaveProgressValue = progress);
                            Scanner.PdfSaveProgressValue = 0;
                        });
                    await RemoveProcessedImages();
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliKaydet)) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SeçiliDirektPdfKaydet = new RelayCommand<object>(
            parameter =>
            {
                bool altkeypressed = Keyboard.Modifiers == ModifierKeys.Alt;
                Filesavetask =
                Task.Run(
                    async () =>
                    {
                        List<ScannedImage> seçiliresimler = GetSelectedImages();
                        if (Scanner.ApplyDataBaseOcr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
                        {
                            Scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
                            Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
                            for (int i = 0; i < seçiliresimler.Count; i++)
                            {
                                byte[] imgdata = null;
                                _ = await Dispatcher.InvokeAsync(() => imgdata = seçiliresimler[i].Resim.ToTiffJpegByteArray(Format.Jpg));
                                ObservableCollection<OcrData> ocrdata = await imgdata.OcrAsync(Scanner.SelectedTtsLanguage);
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        DataBaseQrData = imgdata;
                                        DataBaseTextData = ocrdata;
                                    });
                                Scanner.PdfSaveProgressValue = (i + 1) / (double)seçiliresimler.Count;
                            }
                        }

                        bool isBlackAndWhiteMode = (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite;
                        bool isColourOrGreyscaleMode = (ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale;

                        if (isBlackAndWhiteMode || isColourOrGreyscaleMode)
                        {
                            if (altkeypressed)
                            {
                                for (int i = 0; i < seçiliresimler.Count; i++)
                                {
                                    ScannedImage item = seçiliresimler[i];
                                    await SavePdfImageAsync(item.Resim, PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, isBlackAndWhiteMode);
                                    Scanner.PdfSaveProgressValue = (i + 1) / (double)seçiliresimler.Count;
                                }
                            }
                            else
                            {
                                await SavePdfImageAsync(seçiliresimler, PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, isBlackAndWhiteMode, Settings.Default.ImgLoadResolution);
                            }
                        }
                        await RemoveProcessedImages(true);
                    });
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = GetSelectedImagesCount() ?? 0;
                return Policy.CheckPolicy(nameof(SeçiliDirektPdfKaydet)) && Scanner?.AutoSave == true && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        ListeTemizle = new RelayCommand<object>(
            parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Window.GetWindow(this), Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }

                if (MessageBox.Show(Window.GetWindow(this), Translation.GetResStringValue("LISTREMOVEWARN"), AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                    UndoImage = null;
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
                    _ = Scanner.Resimler?.Remove(item);
                }

                UndoImage = null;
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
                _ = MessageBox.Show(sb.ToString(), AppName);
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
            parameter =>
            {
                Scanner.CroppedImage = SeçiliResim.Resim;
                Scanner.CroppedImageIndex = SeçiliResim.Index;
                Scanner.CopyCroppedImage = Scanner.CroppedImage;
            },
            parameter => SeçiliResim is not null);

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
                    try
                    {
                        ZipExtractSingleFile(openFileDialog.FileName, "runtimes/win-x86/native/pdfium.dll", dllpath);
                        _ = MessageBox.Show($"{Translation.GetResStringValue("INSTALLED")}\n{Translation.GetResStringValue("RESTARTAPP")}", AppName);
                    }
                    catch (Exception)
                    {
                        if (IsAdministrator)
                        {
                            string sourcedllpath = $"{Path.GetTempPath()}pdfium.dll";
                            ZipExtractSingleFile(openFileDialog.FileName, "runtimes/win-x86/native/pdfium.dll", sourcedllpath);
                            AddPendingFileRenameOperation(sourcedllpath, dllpath);
                            _ = MessageBox.Show($"{Translation.GetResStringValue("RESTARTCOMP")}", AppName);
                            return;
                        }
                        _ = MessageBox.Show($"{Translation.GetResStringValue("FOLDERACCESS")}", AppName);
                    }
                }
            },
            parameter => true);

        LoadImage = new RelayCommand<object>(
            async parameter =>
            {
                if (fileloadtask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TRANSLATEPENDING"), AppName);
                    return;
                }

                OpenFileDialog openFileDialog = new()
                {
                    Filter =
                    "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp;*.webp;*.jb2)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp;*.webp;*.jb2|" +
                        "Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.webp;*.jb2)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.webp;*.jb2|" +
                        "Pdf Dosyası (*.pdf)|*.pdf|" +
                        "Xps Dosyası (*.xps)|*.xps|" +
                        "Eyp Dosyası (*.eyp)|*.eyp|" +
                        "Webp Dosyası (*.webp)|*.webp|" +
                        "Arşiv Dosyaları (*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.gz)|*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.gz|" +
                        "Excel Dosyası (*.xls;*.xlsx;*.xlsb;*.csv)|*.xls;*.xlsx;*.xlsb;*.csv|" +
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
                    await AddFiles(openFileDialog.FileNames, DecodeHeight);
                }
            },
            parameter => Policy.CheckPolicy(nameof(LoadImage)));

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
                    PdfToolBarControlIsEnabled = false;
                    string savefolder = ToolBox.CreateSaveFolder("SPLIT");
                    SplitPdfPageCount(pdfviewer.PdfFilePath, savefolder, PdfSplitCount);
                    PdfToolBarControlIsEnabled = true;
                    WebAdreseGit.Execute(savefolder);
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
                        Scanner?.Resimler?.Add(new ScannedImage { Seçili = true, Resim = GenerateBitmapFrame(image.ToBitmapImage()) });
                    }
                }
                if (Clipboard.ContainsFileDropList())
                {
                    StringCollection clipboardFiles = Clipboard.GetFileDropList();
                    if (clipboardFiles?.Count > 0)
                    {
                        await AddFiles(clipboardFiles.Cast<string>().ToArray(), DecodeHeight);
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
                        scannedImage.Resim = GenerateBitmapFrame(image.ToBitmapImage());
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
                            BitmapFrame bitmapFrame = BitmapFrame.Create(await pdfFileHandler.LoadImageAsync(filename, i));
                            bitmapFrame?.Freeze();
                            Scanner?.Resimler?.Insert(scannedImage.Index, new ScannedImage() { Resim = bitmapFrame });
                        }
                    }
                    else if (tiffFileHandler.IsValidFile(filename))
                    {
                        List<BitmapFrame> list = (await tiffFileHandler.LoadTiffPagesAsync(filename)).ToList();
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
                Scanner.RefreshIndexNumbers();
            },
            parameter => true);

        InsertClipBoardImage = new RelayCommand<object>(
            parameter =>
            {
                if (AddFromClipBoard.CanExecute(null))
                {
                    AddFromClipBoard.Execute(null);
                }

                if (Keyboard.Modifiers == ModifierKeys.Alt && SeçiliDirektPdfKaydet.CanExecute(null))
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
                string currentfile = PdfImportViewer.PdfViewer.PdfFilePath;
                if (MergePdfFileToFirst)
                {
                    Scanner.MergePdfFiles.Add(currentfile);
                }
                else
                {
                    Scanner.MergePdfFiles.Insert(0, currentfile);
                }
                string[] files = Scanner.MergePdfFiles.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
                PdfToolBarControlIsEnabled = false;
                files.MergePdf().Save(currentfile);
                Scanner?.MergePdfFiles?.Clear();
                PdfToolBarControlIsEnabled = true;
                PdfImportViewer.PdfViewer.PdfFilePath = null;
                PdfImportViewer.PdfViewer.PdfFilePath = currentfile;
            },
            parameter => Scanner?.MergePdfFiles?.Count > 0 && File.Exists(PdfImportViewer.PdfViewer.PdfFilePath));

        MergePdfListToFile = new RelayCommand<object>(
            parameter =>
            {
                string[] files = Scanner.MergePdfFiles.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
                SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = $"{Translation.GetResStringValue("MERGE")}" };
                if (saveFileDialog.ShowDialog() == true)
                {
                    PdfToolBarControlIsEnabled = false;
                    files.MergePdf().Save(saveFileDialog.FileName);
                    Scanner?.MergePdfFiles?.Clear();
                    PdfToolBarControlIsEnabled = true;
                }
            },
            parameter => Scanner?.MergePdfFiles?.Count > 1);

        MergePdfListRemoveFile = new RelayCommand<object>(parameter => Scanner?.MergePdfFiles?.Remove(parameter as string), parameter => true);

        MergePdfListAddFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Pdf Dosyası (*.pdf)|*.pdf", Multiselect = true };
                if (openFileDialog.ShowDialog() == true)
                {
                    string[] files = openFileDialog.FileNames;
                    foreach (string item in files?.Where(z => Viewer.IsValidPdfFile(z)))
                    {
                        Scanner?.MergePdfFiles?.Add(item);
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

        PdfWaterMark = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    string oldpdfpath = pdfViewer.PdfFilePath;
                    using (PdfDocument pdfdocument = PdfReader.Open(oldpdfpath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider))
                    {
                        if (pdfdocument is null)
                        {
                            return;
                        }
                        if (Keyboard.Modifiers == ModifierKeys.Alt)
                        {
                            PdfDocument listDocument = null;
                            for (int i = 0; i < pdfdocument.PageCount; i++)
                            {
                                listDocument = pdfdocument.GenerateWatermarkedPdf(i, PdfWatermarkFontAngle, PdfWatermarkColor, PdfWatermarkFontSize, PdfWaterMarkText, PdfWatermarkFont);
                            }
                            PdfToolBarControlIsEnabled = false;
                            listDocument?.Save(oldpdfpath);
                            listDocument?.Dispose();
                            PdfToolBarControlIsEnabled = true;
                        }
                        else
                        {
                            PdfToolBarControlIsEnabled = false;
                            using PdfDocument document = pdfdocument.GenerateWatermarkedPdf(pdfViewer.Sayfa - 1, PdfWatermarkFontAngle, PdfWatermarkColor, PdfWatermarkFontSize, PdfWaterMarkText, PdfWatermarkFont);
                            document.Save(oldpdfpath);
                            PdfToolBarControlIsEnabled = true;
                        }
                    }
                    pdfViewer.Source = await Viewer.ConvertToImgAsync(pdfViewer.PdfFilePath, pdfViewer.Sayfa, pdfViewer.Dpi);
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath) && !string.IsNullOrWhiteSpace(PdfWaterMarkText));

        MergeSelectedImagesToPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                List<ScannedImage> seçiliresimler = GetSelectedImages();
                if (parameter is not Viewer pdfviewer ||
                !File.Exists(pdfviewer.PdfFilePath) ||
                !seçiliresimler.Any() ||
                MessageBox.Show($"{seçiliresimler.Count} {Translation.GetResStringValue("DOCUMENT")}\n{Translation.GetResStringValue("SAVESELECTED")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    return;
                }

                PdfToolBarControlIsEnabled = false;
                string pdfFilePath = pdfviewer.PdfFilePath;
                string temporarypdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                string[] processedfiles = Keyboard.Modifiers == ModifierKeys.Alt ? [pdfFilePath, temporarypdf] : [temporarypdf, pdfFilePath];
                await Task.Run(
                    async () =>
                    {
                        using PdfDocument pdfDocument = await seçiliresimler.GeneratePdfAsync(Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, null, Settings.Default.ImgLoadResolution, progress => Scanner.PdfSaveProgressValue = progress);
                        pdfDocument.Save(temporarypdf);
                        processedfiles.MergePdf().Save(pdfFilePath);
                    });
                PdfToolBarControlIsEnabled = true;
                pdfviewer.Sayfa = 1;
                NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                if (!Settings.Default.RemoveProcessedImage)
                {
                    return;
                }

                SeçiliListeTemizle.Execute(null);
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        PasteFileToPdfFile = new RelayCommand<object>(
            async parameter =>
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
                string[] processedFiles = Keyboard.Modifiers == ModifierKeys.Alt ? [pdfFilePath, temporaryPdf] : [temporaryPdf, pdfFilePath];
                if (Clipboard.ContainsFileDropList())
                {
                    StringCollection clipboardFiles = Clipboard.GetFileDropList();
                    List<string> clipboardPdfFiles = clipboardFiles.Cast<string>().Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
                    List<string> clipboardImageFiles = clipboardFiles.Cast<string>().Where(z => imagefileextensions.Contains(Path.GetExtension(z).ToLowerInvariant())).ToList();
                    if (clipboardPdfFiles.Any() || clipboardImageFiles.Any())
                    {
                        PdfToolBarControlIsEnabled = false;
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
                                    using (PdfDocument document = clipboardImageFiles.GeneratePdf(SelectedPaper, null, progress => Scanner.PdfSaveProgressValue = progress))
                                    {
                                        document.Save(temporaryPdf);
                                    }

                                    processedFiles.MergePdf().Save(pdfFilePath);
                                }
                            });
                        PdfToolBarControlIsEnabled = true;
                        pdfviewer.Sayfa = 1;
                        NotifyPdfChange(pdfviewer, temporaryPdf, pdfFilePath);
                    }
                }

                if (Clipboard.ContainsImage())
                {
                    BitmapSource image = Clipboard.GetImage();
                    if (image is not null)
                    {
                        BitmapFrame bitmapFrame = GenerateBitmapFrame(image);
                        PdfToolBarControlIsEnabled = false;
                        await Task.Run(
                            () =>
                            {
                                using (PdfDocument pdfDocument = bitmapFrame.GeneratePdf(null, Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution))
                                {
                                    pdfDocument.Save(temporaryPdf);
                                }
                                processedFiles.MergePdf().Save(pdfFilePath);
                            });
                        PdfToolBarControlIsEnabled = true;
                        pdfviewer.Sayfa = 1;
                        NotifyPdfChange(pdfviewer, temporaryPdf, pdfFilePath);
                    }
                }
                Clipboard.Clear();
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
                _ = MessageBox.Show(stringBuilder.ToString(), AppName);
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
                    await AddFiles([pdfviewer.PdfFilePath], DecodeHeight);
                    return;
                }

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (SayfaBaşlangıç <= SayfaBitiş)
                    {
                        string savefilename = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                        await PdfPageRangeSaveFileAsync(pdfviewer.PdfFilePath, savefilename, SayfaBaşlangıç, SayfaBitiş);
                        await AddFiles([savefilename], DecodeHeight);
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
                if (parameter is not Viewer pdfviewer || !File.Exists(pdfviewer.PdfFilePath))
                {
                    return;
                }
                string path = pdfviewer.PdfFilePath;
                using PdfDocument pdfdocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                if (pdfdocument is null)
                {
                    return;
                }
                PdfToolBarControlIsEnabled = false;
                if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        SavePageRotated(path, pdfdocument, -90);
                        PdfToolBarControlIsEnabled = true;
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = path;
                        return;
                    }

                    SavePageRotated(path, pdfdocument, 90);
                    PdfToolBarControlIsEnabled = true;
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = path;
                    return;
                }

                SavePageRotated(path, pdfdocument, Keyboard.Modifiers == ModifierKeys.Alt ? -90 : 90, pdfviewer.Sayfa - 1);
                PdfToolBarControlIsEnabled = true;
                pdfviewer.Source = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ReversePdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("SAVEPDF")} {Translation.GetResStringValue("REVERSE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    PdfToolBarControlIsEnabled = false;
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    await ReverseFileAsync(pdfviewer.PdfFilePath, pdfviewer.PdfFilePath);
                    PdfToolBarControlIsEnabled = true;
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
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
                        PdfToolBarControlIsEnabled = false;
                        string oldpdfpath = pdfviewer.PdfFilePath;
                        await AddAttachmentFileAsync(openFileDialog.FileNames, pdfviewer.PdfFilePath, pdfviewer.PdfFilePath);
                        PdfToolBarControlIsEnabled = true;
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = oldpdfpath;
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
                    "Arşiv Dosyaları (*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.gz)|*.7z; *.arj; *.bzip2; *.cab; *.gzip; *.iso; *.lzh; *.lzma; *.ntfs; *.ppmd; *.rar; *.rar5; *.rpm; *.tar; *.vhd; *.wim; *.xar; *.xz; *.z; *.zip; *.gz",
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
                OpenFileDialog openFileDialog = new() { Filter = "Excel Dosyası(*.xls; *.xlsx; *.xlsb; *.csv) | *.xls; *.xlsx; *.xlsb; *.csv", Multiselect = false };
                if (openFileDialog.ShowDialog() == true)
                {
                    xlsxViewer.XlsxDataFilePath = openFileDialog.FileName;
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
                List<ScannedImage> scannedImages = Scanner.Resimler.Reverse().ToList();
                Scanner.Resimler = [.. scannedImages];
                Scanner.RefreshIndexNumbers();
            },
            parameter => Scanner?.Resimler?.Count > 1);

        FirstLastGroup = new RelayCommand<object>(
            parameter =>
            {
                List<ScannedImage> scannedImages = [.. Scanner.Resimler];
                Scanner.Resimler = [.. GroupByFirstLastList(scannedImages, GroupSplitCount)];
                Scanner.RefreshIndexNumbers();
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
                    Scanner.RefreshIndexNumbers();
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
                    Scanner.Resimler = FirstLastReverseSequence([.. Scanner.Resimler], item => item.Index);
                    Scanner.RefreshIndexNumbers();
                    return;
                }
                Scanner.Resimler = FirstLastSequence(Scanner.Resimler);
                Scanner.RefreshIndexNumbers();
            },
            parameter => Scanner?.Resimler?.Count > 1);

        ReverseDataHorizontal = new RelayCommand<object>(
            parameter =>
            {
                int start = Scanner.Resimler.IndexOf(Scanner?.Resimler.FirstOrDefault(z => z.Seçili));
                int end = Scanner.Resimler.IndexOf(Scanner?.Resimler.LastOrDefault(z => z.Seçili));
                if (GetSelectedImagesCount() == end - start + 1)
                {
                    List<ScannedImage> scannedImages = [.. Scanner.Resimler];
                    scannedImages.Reverse(start, end - start + 1);
                    Scanner.Resimler = [.. scannedImages];
                    Scanner.RefreshIndexNumbers();
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
                    for (int i = 1; i <= pdfViewer.ToplamSayfa; i++)
                    {
                        PdfPages.Add(new PdfData { PageNumber = i });
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
                    Clipboard.SetImage(scannedImage.Resim.ToBitmapImage());
                    _ = MessageBox.Show(Translation.GetResStringValue("COPYCLIPBOARD"), AppName, MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (parameter is not Viewer pdfViewer || !File.Exists(pdfViewer.PdfFilePath))
                {
                    return;
                }

                byte[] filedata = await Viewer.ReadAllFileAsync(pdfViewer.PdfFilePath);
                using MemoryStream ms = await Viewer.ConvertToImgStreamAsync(filedata, PdfImportViewer.PdfViewer.Sayfa, Settings.Default.ImgLoadResolution);
                BitmapFrame bitmapFrame = ms.GenerateBitmapFrameFromMemoryStream();
                filedata = null;
                using PdfDocument document = bitmapFrame.MedianFilterBitmap(PdfMedianValue).GeneratePdf(null, Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution);
                SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = $"{Translation.GetResStringValue("PAGENUMBER")} {pdfViewer.Sayfa}.pdf" };
                if (saveFileDialog.ShowDialog() != true)
                {
                    return;
                }
                PdfToolBarControlIsEnabled = false;
                document.Save(saveFileDialog.FileName);
                PdfToolBarControlIsEnabled = true;
                PdfMedianValue = 0;
            },
            parameter => PdfMedianValue > 0 && parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        ExtractMultiplePdfFile = new RelayCommand<object>(
            async parameter =>
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
                PdfToolBarControlIsEnabled = true;
                Scanner.PdfSaveProgressValue = 0;
                WebAdreseGit.Execute(savefolder);
                files = null;
            },
            parameter => PdfPages?.Any(z => z.Selected) == true);

        LoadArrangedPdfFile = new RelayCommand<object>(
            async parameter =>
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
                PdfToolBarControlIsEnabled = true;
                Scanner.PdfSaveProgressValue = 0;
                pdfViewer.PdfFilePath = null;
                pdfViewer.PdfFilePath = oldpdfpath;
                pdfViewer.Sayfa = 1;
                LoadPdfExtractFile?.Execute(pdfViewer);
                files.Where(z => File.Exists(z)).ToList().ForEach(z => File.Delete(z));
                files = null;
            },
            parameter => PdfPages?.Count > 1);

        RemoveArrangedPdfFile = new RelayCommand<object>(
            parameter =>
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
                    PdfToolBarControlIsEnabled = true;
                    pdfViewer.PdfFilePath = null;
                    pdfViewer.PdfFilePath = oldpdfpath;
                    pdfViewer.Sayfa = 1;
                    LoadPdfExtractFile?.Execute(pdfViewer);
                }
            },
            parameter => PdfPages?.Count(z => z.Selected) > 0 && PdfPages?.All(z => z.Selected) == false);

        RemoveCurrentPdfPage = new RelayCommand<object>(
            parameter =>
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
                    PdfToolBarControlIsEnabled = true;
                    pdfViewer.PdfFilePath = null;
                    pdfViewer.PdfFilePath = oldpdfpath;
                    pdfViewer.Sayfa = 1;
                }
            },
            parameter => parameter is Viewer pdfViewer && pdfViewer.ToplamSayfa > 1 && PdfToolBarControlIsEnabled);

        AddPageNumber = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not Viewer pdfviewer)
                {
                    return;
                }
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
                    PdfToolBarControlIsEnabled = false;
                    pdfdocument.Save(pdfviewer.PdfFilePath);
                    PdfToolBarControlIsEnabled = true;
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                    pdfviewer.Sayfa = 1;
                    return;
                }

                PdfPage page = pdfdocument.Pages[pdfviewer.Sayfa - 1];
                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                double textwidth = gfx.MeasureString(GetPdfBatchNumberString(pdfviewer.Sayfa), font).Width;
                gfx.DrawText(brush, GetPdfBatchNumberString(pdfviewer.Sayfa - 1), PdfWatermarkFont, PdfGeneration.GetPdfTextLayout(page, textwidth)[0], PdfGeneration.GetPdfTextLayout(page, textwidth)[1], Scanner.PdfPageNumberSize);
                PdfToolBarControlIsEnabled = false;
                pdfdocument.Save(pdfviewer.PdfFilePath);
                PdfToolBarControlIsEnabled = true;
                pdfviewer.Source = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath) && (IsEven || IsOdd));

        FlipPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is not Viewer pdfviewer)
                {
                    return;
                }

                int currentpage = pdfviewer.Sayfa;
                using PdfDocument pdfdocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider);
                if (pdfdocument is null)
                {
                    return;
                }

                PdfPage page = pdfdocument.Pages[currentpage - 1];
                using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Replace);
                XPoint center = new(page.Width / 2, page.Height / 2);
                gfx.ScaleAtTransform(Keyboard.Modifiers == ModifierKeys.Alt ? 1 : -1, Keyboard.Modifiers == ModifierKeys.Alt ? -1 : 1, center);
                BitmapImage bitmapImage = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, currentpage, pdfviewer.Dpi);
                XImage image = XImage.FromBitmapSource(bitmapImage);
                gfx.DrawImage(image, 0, 0);
                PdfToolBarControlIsEnabled = false;
                pdfdocument.Save(pdfviewer.PdfFilePath);
                PdfToolBarControlIsEnabled = true;
                pdfviewer.Source = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                image = null;
                bitmapImage = null;
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        BlackAndWhitePdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfviewer && MessageBox.Show($"{Translation.GetResStringValue("BW")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    BitmapImage bitmapImage = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                    BitmapImage image = bitmapImage.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Tiff);
                    using PdfDocument pdfdocument = RenderPdfPage(pdfviewer, image);
                    PdfToolBarControlIsEnabled = false;
                    pdfdocument.Save(pdfviewer.PdfFilePath);
                    PdfToolBarControlIsEnabled = true;
                    pdfviewer.Source = image;
                    image = null;
                    bitmapImage = null;
                }
            },
            parameter => parameter is Viewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        InvertPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Viewer pdfviewer && MessageBox.Show($"{Translation.GetResStringValue("INVERTCOLOR")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    BitmapImage bitmapImage = await Viewer.ConvertToImgAsync(pdfviewer.PdfFilePath, pdfviewer.Sayfa, pdfviewer.Dpi);
                    BitmapImage image = bitmapImage.InvertBitmap().ToBitmapImage();
                    using PdfDocument pdfdocument = RenderPdfPage(pdfviewer, image);
                    PdfToolBarControlIsEnabled = false;
                    pdfdocument.Save(pdfviewer.PdfFilePath);
                    PdfToolBarControlIsEnabled = true;
                    pdfviewer.Source = image;
                    image = null;
                    bitmapImage = null;
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
                    string filepath = pdfviewer.PdfFilePath;
                    double oldsize = new FileInfo(filepath).Length;
                    PdfCompressor pdfcompressor = new() { UseMozJpeg = UseMozJpeg, Dpi = PdfCompressDpi, Quality = PdfQuality, };
                    pdfcompressor.ProgressChanged += (_, e) => Dispatcher.Invoke(() => PdfImportControlProgressValue = e);
                    PdfToolBarControlIsEnabled = false;
                    using PdfDocument pdfdocument = await pdfcompressor.Compress(filepath);
                    if (pdfdocument is null)
                    {
                        return;
                    }
                    pdfdocument.Save(filepath);
                    PdfToolBarControlIsEnabled = true;
                    double newsize = new FileInfo(filepath).Length;
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = filepath;
                    double compressionratio = newsize / oldsize;
                    _ = MessageBox.Show($"{Translation.GetResStringValue("SUCCESS")}\n{compressionratio:P2}", AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            },
            parameter => parameter is Viewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ResetPreviewSize = new RelayCommand<object>(parameter => Settings.Default.PreviewWidth = 155, parameter => true);

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
                bool altkeypressed = Keyboard.Modifiers == ModifierKeys.Alt;
                foreach (ScannedImage item in GetSelectedImages())
                {
                    BitmapFrame bitmapframe = BitmapFrame.Create(GenerateCroppedImage(item.Resim, Settings.Default.Top, Settings.Default.Left, Settings.Default.Bottom, Settings.Default.Right));
                    bitmapframe.Freeze();
                    if (altkeypressed)
                    {
                        item.Resim = bitmapframe;
                        continue;
                    }
                    Scanner?.Resimler?.Add(new ScannedImage() { Resim = bitmapframe });
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
            parameter => true);

        SplitImagesByIndex = new RelayCommand<object>(
            parameter =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt && ImagesSplitLists.Count > 0)
                {
                    SplittedIndexImages = SplitArray(Scanner.Resimler.ToArray(), [.. ImagesSplitLists]);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(TextSplitList))
                {
                    SplittedIndexImages = SplitArray(Scanner.Resimler.ToArray(), TextSplitList.Split(',').Select(z => int.TryParse(z, out int result) ? result : 0).ToArray());
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
            parameter => Parallel.ForEach(Scanner.Resimler, item => item.Seçili = item.Resim.Resize(0.1).BitmapSourceToBitmap().IsEmptyPage(Settings.Default.EmptyThreshold)),
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
                maximizedWindow = new() { Owner = Window.GetWindow(this), WindowState = WindowState.Maximized, ShowInTaskbar = true, Title = file, WindowStartupLocation = WindowStartupLocation.CenterOwner, UseLayoutRounding = true };
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
                maximizedWindow = new() { Owner = Window.GetWindow(this), WindowState = WindowState.Maximized, ShowInTaskbar = true, Title = AppName, WindowStartupLocation = WindowStartupLocation.CenterOwner, UseLayoutRounding = true };
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
                maximizedWindow = new() { Owner = Window.GetWindow(this), WindowState = WindowState.Maximized, ShowInTaskbar = true, Title = AppName, WindowStartupLocation = WindowStartupLocation.CenterOwner, UseLayoutRounding = true };
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
            parameter =>
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
                    FixedDocument fixedDocument = ImageViewer.PrintMultipleFixedDocumentPages(printdialog, 0, 0, [bitmapframe], PrintDpi);
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
                Viewer.GenerateDocument(printdialog, pdfDocument, (int)printdialog.MinPage, (int)printdialog.MaxPage, PrintDpi);
            },
            parameter => true);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static Cursor DragCursor { get; set; }

    public static bool IsAdministrator
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            field = principal.IsInRole(WindowsBuiltInRole.Administrator);
            return field;
        }
    }

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

    public List<Tuple<string, int, double, bool, double>> CompressionProfiles => [
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Low, false, (double)Quality.Low),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Low, true, (double)Quality.Low),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Medium, false, (double)Quality.Medium),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Medium, true, (double)Quality.Medium),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Standard, false, (double)Quality.Standard),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Standard, true, (double)Quality.Standard),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.High, false, (double)Quality.High),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.High, true, (double)Quality.High),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("BW"), 0, (double)Resolution.Ultra, false, (double)Quality.Ultra),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Ultra, true, (double)Quality.Ultra),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Low, false, (double)Quality.Low),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Medium, false, (double)Quality.Medium),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Standard, false, (double)Quality.Standard),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.High, false, (double)Quality.High),
        new Tuple<string, int, double, bool, double>(Translation.GetResStringValue("COLOR"), 2, (double)Resolution.Ultra, false, (double)Quality.Ultra)
    ];

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

    public ObservableCollection<OcrData> DataBaseTextData
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

    public ICommand ExploreFile { get; }

    public ICommand ExtractMultiplePdfFile { get; }

    public RelayCommand<object> ExtractNugetPackage { get; }

    public ICommand FastScanImage { get; }

    public RelayCommand<object> FirstLastGroup { get; }

    public RelayCommand<object> FirstLastSortSequenceData { get; }

    public RelayCommand<object> FlipPdfPage { get; }

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

    public ICommand KayıtYoluBelirle { get; }

    public ICommand ListeTemizle { get; }

    public RelayCommand<object> LoadArchiveFile { get; }

    public RelayCommand<object> LoadArrangedPdfFile { get; }

    public ICommand LoadCroppedImage { get; }

    public ICommand LoadImage { get; }

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
    } = 400;

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
    } = 85;

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
    } = [
        new Paper { Category = "A", Height = 118.9, PaperType = "A0", Width = 84.1 },
        new Paper { Category = "A", Height = 84.1, PaperType = "A1", Width = 59.4 },
        new Paper { Category = "A", Height = 59.4, PaperType = "A2", Width = 42 },
        new Paper { Category = "A", Height = 42, PaperType = "A3", Width = 29.7 },
        new Paper { Category = "A", Height = 29.7, PaperType = "A4", Width = 21, WidespreadPaper = Visibility.Visible },
        new Paper { Category = "A", Height = 21, PaperType = "A5", Width = 14.8 },
        new Paper { Category = "B", Height = 141.4, PaperType = "B0", Width = 100 },
        new Paper { Category = "B", Height = 100, PaperType = "B1", Width = 70.7 },
        new Paper { Category = "B", Height = 70.7, PaperType = "B2", Width = 50 },
        new Paper { Category = "B", Height = 50, PaperType = "B3", Width = 35.3 },
        new Paper { Category = "B", Height = 35.3, PaperType = "B4", Width = 25 },
        new Paper { Category = "B", Height = 25, PaperType = "B5", Width = 17.6 },
        new Paper { Height = 27.94, PaperType = "Letter", Width = 21.59, WidespreadPaper = Visibility.Visible },
        new Paper { Height = 35.56, PaperType = "Legal", Width = 21.59 },
        new Paper { Height = 26.67, PaperType = "Executive", Width = 18.415 },
        new Paper { Category = string.Empty, Height = 0, PaperType = "Original", Width = 0 },
        new Paper { Category = string.Empty, Height = Settings.Default.CustomPaperHeight, PaperType = "Custom", Width = Settings.Default.CustomPaperWidth },
    ];

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

    public RelayCommand<object> ReplaceSelectedImage { get; }

    public ICommand ResetCrop { get; }

    public RelayCommand<object> ResetPreviewSize { get; }

    public ICommand ResimSil { get; }

    public ICommand ResimSilGeriAl { get; }

    public ICommand ReverseData { get; }

    public ICommand ReverseDataHorizontal { get; }

    public ICommand ReversePdfFile { get; }

    public ICommand RotateSelectedPage { get; }

    public ICommand SaveFileList { get; }

    public ICommand SaveProfile { get; }

    public RelayCommand<object> SaveSelectedFilesBwPdfFile { get; }

    public RelayCommand<object> SaveSelectedFilesJpgFile { get; }

    public RelayCommand<object> SaveSelectedFilesPdfFile { get; }

    public RelayCommand<object> SaveSelectedFilesTifFile { get; }

    public RelayCommand<object> SaveSelectedFilesTxtFile { get; }

    public RelayCommand<object> SaveSelectedFilesWebpFile { get; }

    public RelayCommand<object> SaveSelectedFilesZipFile { get; }

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

    public RelayCommand<object> ToolBoxManualDeskewImage { get; }

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

    public ScannedImage UndoImage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UndoImage));
            }
        }
    }

    public int? UndoImageIndex
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UndoImageIndex));
            }
        }
    }

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

    public static List<List<T>> ChunkBy<T>(IEnumerable<T> source, int chunkSize) => source.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / chunkSize).Select(x => x.Select(v => v.Value).ToList()).ToList();

    public static List<string> EypFileExtract(string eypfilepath)
    {
        try
        {
            if (eypfilepath is null || !string.Equals(Path.GetExtension(eypfilepath), ".eyp", StringComparison.OrdinalIgnoreCase))
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
        catch (Exception)
        {
        }
        return null;
    }

    public static bool FileNameValid(string filename) => !string.IsNullOrWhiteSpace(filename) && filename.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    public static BitmapFrame GenerateBitmapFrame(BitmapSource bitmapSource)
    {
        bitmapSource.Freeze();
        BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapSource.ToBitmapImage());
        bitmapFrame.Freeze();
        return bitmapFrame;
    }

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

    public static PdfDocument RenderPdfPage(Viewer pdfviewer, BitmapImage image)
    {
        PdfDocument document = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify, PdfGeneration.PasswordProvider)?.GenerateFromBitmapSourcePdf(pdfviewer.Sayfa - 1, image);
        document.ApplyDefaultPdfCompression();
        return document;
    }

    public static void SavePageRotated(string savepath, PdfDocument inputDocument, int angle)
    {
        foreach (PdfPage page in inputDocument?.Pages)
        {
            if (page?.Rotate is > 360 or < (-360))
            {
                page.Rotate = 0;
            }
            page.Rotate += angle;
        }

        inputDocument.Save(savepath);
    }

    public static void SavePageRotated(string savepath, PdfDocument inputDocument, int angle, int pageindex)
    {
        PdfPage page = inputDocument?.Pages[pageindex];
        if (page?.Rotate is > 360 or < (-360))
        {
            page.Rotate = 0;
        }
        page.Rotate += angle;
        inputDocument.Save(savepath);
    }

    public Task AddFiles(string[] filenames, int decodeheight)
    {
        fileloadtask = Task.Run(
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
                                    await AddFilesAsync(filename, fileHandler, DecodeHeight);
                                }

                                break;

                            case ".eyp":
                                await AddFiles([.. (EypFileExtract(filename))], decodeheight);
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
                                await AddFilesAsync(filename, fileHandler, DecodeHeight);
                                break;

                            case ".jb2":
                                fileHandler = new Jb2FileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight);
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
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        SelectedTabIndex = 6;
                                        xlsxViewer.XlsxDataFilePath = filename;
                                    });
                                break;

                            case ".webp":
                                fileHandler = new WebpFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight);
                                break;

                            case ".tiff" or ".tif":
                                fileHandler = new TiffFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight);
                                break;

                            case ".xps":
                                fileHandler = new XpsFileHandler();
                                await AddFilesAsync(filename, fileHandler, DecodeHeight);
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

    public void CreateBuiltInScanProfiles()
    {
        if (AnyScannerExist())
        {
            string[] profiles = new string[6];
            string[] dpiValues = ["96", "200", "300"];
            string[] colorModes = ["BW", "COLOR"];
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
        if (fileloadtask?.IsCompleted == false)
        {
            _ = MessageBox.Show(Window.GetWindow(this), Translation.GetResStringValue("TRANSLATEPENDING"), AppName);
            return;
        }

        if (e.Data.GetData(typeof(Scanner)) is Scanner droppedData)
        {
            await Task.Run(() => AddFiles([droppedData.FileName], DecodeHeight));
            return;
        }

        if ((e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
        {
            foreach (string folder in from string file in droppedfiles where File.GetAttributes(file).HasFlag(FileAttributes.Directory) select file)
            {
                await Task.Run(() => AddFiles(Directory.GetFiles(folder), DecodeHeight));
            }
            await Task.Run(() => AddFiles(droppedfiles, DecodeHeight));
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

    internal static T DeSerialize<T>(string xmldatapath) where T : class, new()
    {
        try
        {
            XmlSerializer serializer = new(typeof(T));
            using StreamReader stream = new(xmldatapath);
            return serializer.Deserialize(stream) as T;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
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

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static bool CheckFileSaveProgress()
    {
        if (Filesavetask?.IsCompleted == false)
        {
            _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
            return false;
        }
        return true;
    }

    private static string GetDefaultPrinterName()
    {
        int bufferSize = 256;
        StringBuilder printerNameBuffer = new(bufferSize);
        return GetDefaultPrinter(printerNameBuffer, ref bufferSize) ? (printerNameBuffer?.ToString()) : null;
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

    private async Task AddFilesAsync(string filename, ILoadFileHandler fileHandler, int decodeHeight = 0)
    {
        if (!fileHandler.IsValidFile(filename))
        {
            return;
        }

        int totalPageCount = fileHandler.GetPageCount(filename);
        MemoryStream ms;
        BitmapFrame bitmapFrame;
        for (int i = 1; i <= totalPageCount; i++)
        {
            switch (fileHandler)
            {
                case PdfFileHandler:
                    byte[] fileData = await Viewer.ReadAllFileAsync(filename);
                    ms = await fileHandler.ConvertToImageStreamAsync(fileData, i);
                    bitmapFrame = ms.GenerateBitmapFrameFromMemoryStream();
                    break;

                case WebpFileHandler:
                    bitmapFrame = fileHandler.LoadWebpImage(decodeHeight, filename);
                    break;

                case XpsFileHandler:
                    await HandleTifXpsFileAsync(fileHandler.LoadXpsPagesAsync, filename, i, totalPageCount);
                    return;

                case TiffFileHandler:
                    await HandleTifXpsFileAsync(fileHandler.LoadTiffPagesAsync, filename, i, totalPageCount);
                    return;

                default:
                    bitmapFrame = await fileHandler.LoadImageAsync(filename);
                    break;
            }

            bitmapFrame.Freeze();
            await Dispatcher.InvokeAsync(
                () =>
                {
                    Scanner?.Resimler.Add(new ScannedImage { Resim = bitmapFrame, FilePath = filename });
                    PdfLoadProgressValue = (double)i / totalPageCount;
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
                            _ = Dispatcher.Invoke(() => AllRotateProgressValue = (i + 1) / (double)Scanner.Resimler.Count);
                            i++;
                        }
                        catch (Exception)
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

        if (e.PropertyName is "AutoRotateBasedText" && Settings.Default.AutoRotateBasedText && !TesseractOrientationFileExists)
        {
            Settings.Default.AutoRotateBasedText = false;
        }

        if (Settings.Default.ShowFileGroupIndicator)
        {
            Scanner.Resimler.CollectionChanged -= Resimler_CollectionChanged;
            Scanner.Resimler.CollectionChanged += Resimler_CollectionChanged;
        }
        else
        {
            Scanner.Resimler.CollectionChanged -= Resimler_CollectionChanged;
        }

        Settings.Default.Save();
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
        Scanner.ArayüzEtkin = false;
        QrCode.QrCode qrcode = new();
        Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(Scanner?.Resimler?.LastOrDefault()?.Resim);
        OnPropertyChanged(nameof(Scanner.DetectPageSeperator));
        Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
        List<ObservableCollection<OcrData>> PdfFileOcrData = null;
        if (Scanner.ApplyDataBaseOcr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            PdfFileOcrData = [];
            Scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
            for (int i = 0; i < Scanner.Resimler.Count; i++)
            {
                ScannedImage scannedimage = Scanner.Resimler[i];
                Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(scannedimage.Resim);
                DataBaseTextData = await scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage);
                PdfFileOcrData.Add(DataBaseTextData);
                Scanner.PdfSaveProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
            }
        }

        if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
        {
            (await Scanner.Resimler.ToList().GeneratePdfAsync(Format.Tiff, SelectedPaper, Settings.Default.JpegQuality, PdfFileOcrData, (int)Settings.Default.Çözünürlük, progress => Scanner.PdfSaveProgressValue = progress)).Save(Scanner.PdfFilePath);
        }

        if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
        {
            (await Scanner.Resimler.ToList().GeneratePdfAsync(Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, PdfFileOcrData, (int)Settings.Default.Çözünürlük, progress => Scanner.PdfSaveProgressValue = progress)).Save(Scanner.PdfFilePath);
        }

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
        PdfFileOcrData = null;
        Twain.ScanningComplete -= FastScanComplete;
        Scanner.ArayüzEtkin = true;
    }

    private ObservableCollection<T> FirstLastReverseSequence<T>(List<T> items, Func<T, int> indexSelector)
    {
        items.Sort((a, b) => indexSelector(a) % 2 != indexSelector(b) % 2 ? indexSelector(a) % 2 == 1 ? -1 : 1 : indexSelector(a) % 2 == 0 ? indexSelector(b).CompareTo(indexSelector(a)) : indexSelector(a).CompareTo(indexSelector(b)));

        return [.. items];
    }

    private ObservableCollection<T> FirstLastSequence<T>(ObservableCollection<T> images)
    {
        ObservableCollection<T> result = [];
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
            splitLists.Add(scannedImages.Skip(i * splitIndex).Take(splitIndex).ToList());
        }
        return MixLists([.. splitLists]);
    }

    private async Task HandleTifXpsFileAsync(Func<string, Task<IEnumerable<BitmapFrame>>> loadPagesAsync, string filename, int i, int totalPageCount)
    {
        IEnumerable<BitmapFrame> frames = await loadPagesAsync(filename);
        foreach (BitmapFrame frame in frames)
        {
            frame.Freeze();
            await Dispatcher.InvokeAsync(
                () =>
                {
                    Scanner?.Resimler.Add(new ScannedImage { Resim = frame, FilePath = filename });
                    PdfLoadProgressValue = (double)i / totalPageCount;
                });
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
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            ImgViewer.Zoom = e.Delta > 0 ? ImgViewer.Zoom + .05 : ImgViewer.Zoom + -.05;
            ImgViewer.Zoom = Math.Max(ImgViewer.MinZoom, Math.Min(ImgViewer.MaxZoom, ImgViewer.Zoom));
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
        int removedIdx = Scanner.Resimler.IndexOf(droppedData);
        int targetIdx = Scanner.Resimler.IndexOf(target);

        if (removedIdx < targetIdx)
        {
            Scanner.Resimler.Insert(targetIdx + 1, droppedData);
            Scanner.Resimler.RemoveAt(removedIdx);
            Scanner.RefreshIndexNumbers();
            return;
        }

        int remIdx = removedIdx + 1;
        if (Scanner.Resimler.Count + 1 <= remIdx)
        {
            return;
        }

        Scanner.Resimler.Insert(targetIdx, droppedData);
        Scanner.Resimler.RemoveAt(remIdx);
        Scanner.RefreshIndexNumbers();
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

    private async Task RemoveProcessedImages(bool notifyimage = false)
    {
        await Dispatcher.InvokeAsync(
            () =>
            {
                if (Settings.Default.RemoveProcessedImage)
                {
                    SeçiliListeTemizle.Execute(null);
                }
                if (notifyimage)
                {
                    OnPropertyChanged(nameof(Scanner.Resimler));
                }
            });
    }

    private void RemoveSelectedImage(ScannedImage item)
    {
        _ = Scanner.Resimler?.Remove(item);
        ToolBox.ResetCropMargin();
        GC.Collect();
    }

    private void Resimler_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        Dictionary<string, SolidColorBrush> colorMap = [];
        Random random = new();

        foreach (IGrouping<string, ScannedImage> group in Scanner.Resimler?.GroupBy(z => z.FilePath))
        {
            if (group.Key is null)
            {
                continue;
            }

            if (!colorMap.TryGetValue(group.Key, out SolidColorBrush solidColorBrush))
            {
                solidColorBrush = new SolidColorBrush(Color.FromArgb(128, (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
                solidColorBrush.Freeze();
                colorMap[group.Key] = solidColorBrush;
            }

            foreach (ScannedImage image in group)
            {
                image.FileGroupColor = solidColorBrush;
            }
        }
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
            DragMoveStarted = true;
            StackPanel stackPanel = (run.Parent as TextBlock)?.Parent as StackPanel;
            using Icon icon = Icon.FromHandle(stackPanel.ToRenderTargetBitmap().BitmapSourceToBitmap().GetHicon());
            DragCursor = CursorInteropHelper.Create(new SafeIconHandle(icon.Handle));
            _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
            DragMoveStarted = false;
            e.Handled = true;
        }
    }

    private void SaveJpgImage(BitmapFrame scannedImage, string filename) => Dispatcher.Invoke(() => File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg, Settings.Default.JpegQuality)));

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
                        if (Settings.Default.RemoveProcessedImage)
                        {
                            scannedimage.Resim = null;
                        }
                        progressCallback?.Invoke((i + 1) / (double)images.Count);
                    });
            });
    }

    private async Task SavePdfImageAsync(BitmapFrame scannedImage, string filename, Scanner scanner, Paper paper, bool applyocr, bool blackwhite = false)
    {
        ObservableCollection<OcrData> ocrtext = null;
        if (applyocr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
            _ = await Dispatcher.Invoke(async () => ocrtext = await scannedImage.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage));
        }

        scanner.SaveProgressBarForegroundBrush = defaultsaveprogressforegroundcolor;
        if (blackwhite)
        {
            scannedImage.GeneratePdf(ocrtext, Format.Tiff, paper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution).Save(filename);
            return;
        }

        scannedImage.GeneratePdf(ocrtext, Format.Jpg, paper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution).Save(filename);
    }

    private async Task SavePdfImageAsync(List<ScannedImage> images, string filename, Scanner scanner, Paper paper, bool applyocr, bool blackwhite = false, int dpi = 120)
    {
        List<ObservableCollection<OcrData>> scannedtext = null;
        if (applyocr && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
            scannedtext = [];
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

            scanner.PdfSaveProgressValue = 0;
        }

        scanner.SaveProgressBarForegroundBrush = defaultsaveprogressforegroundcolor;
        if (blackwhite)
        {
            (await images.GeneratePdfAsync(Format.Tiff, paper, Settings.Default.JpegQuality, scannedtext, dpi, progress => Scanner.PdfSaveProgressValue = progress)).Save(filename);
            return;
        }

        (await images.GeneratePdfAsync(Format.Jpg, paper, Settings.Default.JpegQuality, scannedtext, dpi, progress => Scanner.PdfSaveProgressValue = progress)).Save(filename);
    }

    private void SaveTifImage(BitmapFrame scannedImage, string filename)
    {
        if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
        {
            Dispatcher.Invoke(() => File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Tiff)));
            return;
        }

        if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
        {
            Dispatcher.Invoke(() => File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.TiffRenkli)));
        }
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
            });
    }

    private async Task SaveTxtFileAsync(List<ScannedImage> images, string fileName, Action<double> progressCallback = null)
    {
        if (images is null || string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            return;
        }
        for (int i = 0; i < images.Count; i++)
        {
            await Dispatcher.Invoke(
                async () =>
                {
                    ObservableCollection<OcrData> ocrtext = await images[i].Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage);
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(fileName), $"{Path.GetFileNameWithoutExtension(fileName)}{i}.txt"), string.Join(" ", ocrtext.Select(z => z.Text)));
                    progressCallback?.Invoke((i + 1) / (double)images.Count);
                });
        }
    }

    private void SaveWebpImage(BitmapFrame scannedImage, string filename) => Dispatcher.Invoke(() => File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg).WebpEncode(Settings.Default.WebpQuality)));

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
                        if (Settings.Default.RemoveProcessedImage)
                        {
                            scannedimage.Resim = null;
                        }
                        progressCallback?.Invoke((i + 1) / (double)images.Count);
                    });
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
            });
    }

    private void SaveZipImage(List<ScannedImage> seçiliresimler, string fileName, Action<double> progressCallback = null)
    {
        using ZipArchive archive = ZipFile.Open(fileName, ZipArchiveMode.Update);
        for (int i = 0; i < seçiliresimler.Count; i++)
        {
            string fPath = Path.Combine(Path.GetTempPath(), $"{seçiliresimler[i].Index}.jpg");
            File.WriteAllBytes(fPath, seçiliresimler[i].Resim.ToTiffJpegByteArray(Format.Jpg));
            _ = archive.CreateEntryFromFile(fPath, Path.GetFileName(fPath));
            File.Delete(fPath);
            progressCallback?.Invoke((i + 1) / (double)seçiliresimler.Count);
        }
    }

    private async void ScanComplete(object sender, ScanningCompleteEventArgs e)
    {
        if (Scanner.ScanSeperate)
        {
            if (!Scanner.UsePageSeperator)
            {
                for (int i = 0; i < Scanner.Resimler.Count; i++)
                {
                    ScannedImage item = Scanner.Resimler[i];
                    if (Settings.Default.AutoRotateBasedText && TesseractOrientationFileExists)
                    {
                        await AutoRotateBasedTextOrientation([item], 1);
                    }
                    Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
                    DataBaseTextData = await GetImageOcrData(item);
                    await SavePdfImageAsync(item.Resim, Scanner.PdfFilePath, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr);
                    Scanner.PdfSaveProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                }
            }
            else
            {
                QrCode.QrCode qrcode = new();
                for (int i = 0; i < Scanner.Resimler.Count; i++)
                {
                    ScannedImage item = Scanner.Resimler[i];
                    if (Settings.Default.AutoRotateBasedText && TesseractOrientationFileExists)
                    {
                        await AutoRotateBasedTextOrientation([item], 1);
                    }
                    Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(item.Resim);
                    OnPropertyChanged(nameof(Scanner.DetectPageSeperator));
                    Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
                    DataBaseTextData = await GetImageOcrData(item);
                    await SavePdfImageAsync(item.Resim, Scanner.PdfFilePath, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr);
                    Scanner.PdfSaveProgressValue = (i + 1) / (double)Scanner.Resimler.Count;
                }
            }
            OnPropertyChanged(nameof(Scanner.Resimler));
        }

        if (Settings.Default.PlayNotificationAudio)
        {
            PlayNotificationSound(Settings.Default.AudioFilePath);
        }
        DataBaseTextData = null;
        Twain.ScanningComplete -= ScanComplete;
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
    }

    private void SetCropPageResolution()
    {
        PageHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.Çözünürlük);
        PageWidth = (int)(SelectedPaper.Width / Inch * Settings.Default.Çözünürlük);
        Settings.Default.Bottom = PageHeight;
        Settings.Default.Right = PageWidth;
    }

    private ObservableCollection<T> Shuffle<T>(IList<T> collection, Random random)
    {
        for (int i = collection.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            T temp = collection[i];
            collection[i] = collection[j];
            collection[j] = temp;
        }
        return [.. collection];
    }

    private List<T[]> SplitArray<T>(T[] array, params int[] indices)
    {
        if (indices.Length == 0)
        {
            throw new ArgumentException("At least one split index is required.");
        }
        Array.Sort(indices);
        List<T[]> parts = new(indices.Length + 1);
        for (int i = 0; i < indices.Length; i++)
        {
            int startIndex = i == 0 ? 0 : indices[i - 1];
            int length = i == 0 ? indices[i] : indices[i] - indices[i - 1];
            parts.Add(array.Skip(startIndex).Take(length).ToArray());
        }
        parts.Add(array.Skip(indices[indices.Length - 1]).ToArray());
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
            Color color = (Color)ColorConverter.ConvertFromString(Settings.Default.AutoCropColor);
            evrak = evrak.AutoCropImage(color);
        }

        if (Scanner.InvertImage)
        {
            evrak = evrak.InvertBitmap().ToBitmapImage();
        }

        evrak.Freeze();
        BitmapFrame bitmapFrame = BitmapFrame.Create(evrak);
        bitmapFrame.Freeze();
        evrak = null;
        ScannedImage item = new() { Resim = bitmapFrame, RotationAngle = (double)SelectedRotation, FlipAngle = (double)SelectedFlip };
        if (Settings.Default.AutoRotateBasedText && TesseractOrientationFileExists)
        {
            _ = AutoRotateBasedTextOrientation([item], 1).ConfigureAwait(true).GetAwaiter();
        }
        Scanner?.Resimler?.Add(item);
    }

    private void TwainCtrl_Loaded(object sender, RoutedEventArgs e) => InitializeTwainControl();

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
            DecodeHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.ImgLoadResolution);
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
}