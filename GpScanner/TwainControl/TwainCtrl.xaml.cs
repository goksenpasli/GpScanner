﻿using Extensions;
using Extensions.Controls;
using Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
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
using UdfParser;
using static Extensions.ExtensionMethods;
using static TwainControl.DrawControl;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Clipboard = System.Windows.Forms.Clipboard;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.Forms.DataFormats;
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

namespace TwainControl;

public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable
{
    public const double Inch = 2.54d;
    public static DispatcherTimer CameraQrCodeTimer;
    public static Task Filesavetask;
    private static readonly string AppName = Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Title;
    private readonly object _lockObject = new();
    private readonly SolidColorBrush bluesaveprogresscolor = Brushes.DeepSkyBlue;
    private readonly Brush defaultsaveprogressforegroundcolor = (Brush)new BrushConverter().ConvertFromString("#FF06B025");
    private readonly string[] imagefileextensions = [".tiff", ".tıf", ".tıff", ".tif", ".jpg", ".jpe", ".gif", ".jpeg", ".jfif", ".jfıf", ".png", ".bmp"];
    private readonly Rectangle selectionbox = new() { Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), Fill = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), StrokeThickness = 2, StrokeDashArray = new DoubleCollection(new double[] { 1 }) };
    private ScanSettings _settings;
    private double allImageRotationAngle;
    private byte[] cameraQRCodeData;
    private bool canUndoImage;
    private CroppedBitmap croppedOcrBitmap;
    private byte[] dataBaseQrData;
    private ObservableCollection<OcrData> dataBaseTextData;
    private int decodeHeight;
    private bool disposedValue;
    private string distinctImages;
    private GridLength documentGridLength = new(5, GridUnitType.Star);
    private bool documentPreviewIsExpanded = true;
    private bool dragMoveStarted;
    private Task fileloadtask;
    private int groupSplitCount = 2;
    private double height;
    private bool helpIsOpened;
    private bool ıgnoreImageWidthHeight;
    private byte[] ımgData;
    private bool isMouseDown;
    private bool isRightMouseDown;
    private Window maximizedWindow;
    private Point mousedowncoord;
    private int pageHeight;
    private int pageWidth;
    private ObservableCollection<Paper> papers =
    [
        new Paper { Category = "A", Height = 118.9, PaperType = "A0", Width = 84.1 },
        new Paper { Category = "A", Height = 84.1, PaperType = "A1", Width = 59.4 },
        new Paper { Category = "A", Height = 59.4, PaperType = "A2", Width = 42 },
        new Paper { Category = "A", Height = 42, PaperType = "A3", Width = 29.7 },
        new Paper { Category = "A", Height = 29.7, PaperType = "A4", Width = 21 },
        new Paper { Category = "A", Height = 21, PaperType = "A5", Width = 14.8 },
        new Paper { Category = "B", Height = 141.4, PaperType = "B0", Width = 100 },
        new Paper { Category = "B", Height = 100, PaperType = "B1", Width = 70.7 },
        new Paper { Category = "B", Height = 70.7, PaperType = "B2", Width = 50 },
        new Paper { Category = "B", Height = 50, PaperType = "B3", Width = 35.3 },
        new Paper { Category = "B", Height = 35.3, PaperType = "B4", Width = 25 },
        new Paper { Category = "B", Height = 25, PaperType = "B5", Width = 17.6 },
        new Paper { Height = 27.94, PaperType = "Letter", Width = 21.59 },
        new Paper { Height = 35.56, PaperType = "Legal", Width = 21.59 },
        new Paper { Height = 26.67, PaperType = "Executive", Width = 18.415 },
        new Paper { Category = string.Empty, Height = 0, PaperType = "Original", Width = 0 },
        new Paper { Category = string.Empty, Height = Settings.Default.CustomPaperHeight, PaperType = "Custom", Width = Settings.Default.CustomPaperWidth },
    ];
    private double pdfLoadProgressValue;
    private int pdfMedianValue;
    private ObservableCollection<PdfData> pdfPages;
    private int pdfSplitCount;
    private SolidColorBrush pdfWatermarkColor = Brushes.Red;
    private string pdfWatermarkFont = "Arial";
    private double pdfWatermarkFontAngle = 315d;
    private double pdfWatermarkFontSize = 72d;
    private string pdfWaterMarkText;
    private bool refreshDocumentList;
    private int saveIndex = 2;
    private int sayfaBaşlangıç = 1;
    private int sayfaBitiş = 1;
    private Scanner scanner;
    private ScannedImage seçiliResim;
    private int seekIndex = -1;
    private Tuple<string, int, double, bool, double> selectedCompressionProfile;
    private PageFlip selectedFlip = PageFlip.NONE;
    private bool selectedImageWidthHeightIsEqual;
    private Orientation selectedOrientation = Orientation.Default;
    private Paper selectedPaper;
    private PageRotation selectedRotation = PageRotation.NONE;
    private int selectedTabIndex = 0;
    private Twain twain;
    private GridLength twainGuiControlLength = new(3, GridUnitType.Star);
    private ScannedImage undoImage;
    private int? undoImageIndex;
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
        Camera.PropertyChanged += CameraUserControl_PropertyChangedAsync;
        TranslationSource.Instance.PropertyChanged += Language_PropertyChanged;
        SelectedPaper = Papers.FirstOrDefault(z => z.PaperType == "A4");

        ScanImage = new RelayCommand<object>(
            async parameter =>
            {
                GC.Collect();
                await Task.Delay(TimeSpan.FromSeconds(Settings.Default.ScanDelay));
                ScanCommonSettings();
                twain.SelectSource(Settings.Default.SeçiliTarayıcı);
                twain.StartScanning(_settings);
                twain.ScanningComplete += ScanComplete;
            },
            parameter => !Environment.Is64BitProcess && Scanner?.Tarayıcılar?.Count > 0 && !string.IsNullOrWhiteSpace(Settings.Default.SeçiliTarayıcı) && Policy.CheckPolicy("ScanImage"));

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
                ScanCommonSettings();
                Scanner.Resimler = [];
                Scanner.Resimler.CollectionChanged -= Scanner.Resimler_CollectionChanged;
                Scanner.Resimler.CollectionChanged += Scanner.Resimler_CollectionChanged;
                twain.SelectSource(Settings.Default.SeçiliTarayıcı);
                twain.StartScanning(_settings);
                twain.ScanningComplete += FastScanComplete;
            },
            parameter => !Environment.Is64BitProcess &&
            Scanner?.Tarayıcılar?.Count > 0 &&
            !string.IsNullOrWhiteSpace(Settings.Default.SeçiliTarayıcı) &&
            Scanner?.AutoSave == true &&
            !string.IsNullOrWhiteSpace(Scanner?.FileName) &&
            FileNameValid(Scanner?.FileName) &&
            Policy.CheckPolicy("FastScanImage"));

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
            parameter => Scanner.ArayüzEtkin);

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
                if (parameter is ScannedImage item)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        item.Resim = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Jpeg));
                        return;
                    }

                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        item.Resim = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold, true).ToBitmapImage(ImageFormat.Jpeg));
                        return;
                    }

                    BitmapFrame bitmapFrame = BitmapFrame.Create(item.Resim.InvertBitmap());
                    bitmapFrame.Freeze();
                    item.Resim = bitmapFrame;
                }
            },
            parameter => Scanner.ArayüzEtkin);

        AutoDeskewImage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is ScannedImage item &&
                MessageBox.Show(
                    Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault(),
                    $"{Translation.GetResStringValue("DESKEW")} {Translation.GetResStringValue("APPLY")}",
                    AppName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No) ==
                MessageBoxResult.Yes)
                {
                    double deskewAngle = Deskew.GetDeskewAngle(item.Resim);
                    item.Resim = BitmapFrame.Create(await ((BitmapSource)item.Resim).RotateImageAsync(deskewAngle));
                }
            },
            parameter => Scanner.ArayüzEtkin);

        InvertSelectedImage = new RelayCommand<object>(
            parameter =>
            {
                bool bw = Keyboard.Modifiers == ModifierKeys.Alt;
                bool grayscale = Keyboard.Modifiers == ModifierKeys.Shift;
                foreach (ScannedImage item in Scanner?.Resimler?.Where(z => z.Seçili))
                {
                    if (bw)
                    {
                        item.Resim = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold).ToBitmapImage(ImageFormat.Jpeg));
                        continue;
                    }

                    if (grayscale)
                    {
                        item.Resim = BitmapFrame.Create(item.Resim.BitmapSourceToBitmap().ConvertBlackAndWhite(Scanner.ToolBarBwThreshold, true).ToBitmapImage(ImageFormat.Jpeg));
                        continue;
                    }

                    BitmapFrame bitmapFrame = BitmapFrame.Create(item.Resim.InvertBitmap());
                    bitmapFrame.Freeze();
                    item.Resim = bitmapFrame;
                }
            },
            parameter => Scanner.Resimler.Count(z => z.Seçili) > 0);

        ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

        OpenHelpDialog = new RelayCommand<object>(parameter => HelpIsOpened = !HelpIsOpened, parameter => true);

        Kaydet = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is BitmapFrame bitmapFrame)
                {
                    if (Filesavetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
                        return;
                    }

                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi (*.jpg)|*.jpg|Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps|Txt Dosyası (*.txt)|*.txt|Webp Dosyası (*.webp)|*.webp",
                        FileName = Scanner.SaveFileName,
                        FilterIndex = SaveIndex + 1
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        Filesavetask = Task.Run(
                            async () =>
                            {
                                string fileName = saveFileDialog.FileName;
                                switch (saveFileDialog.FilterIndex)
                                {
                                    case 1:
                                        SaveTifImage(bitmapFrame, fileName);
                                        break;

                                    case 2:
                                        SaveJpgImage(bitmapFrame, fileName);
                                        break;

                                    case 3:
                                        await SavePdfImageAsync(bitmapFrame, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr);
                                        break;

                                    case 4:
                                        await SavePdfImageAsync(bitmapFrame, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, true);
                                        break;

                                    case 5:
                                        SaveXpsImage(bitmapFrame, fileName);
                                        break;

                                    case 6:
                                        await SaveTxtFileAsync(bitmapFrame, fileName);
                                        break;

                                    case 7:
                                        SaveWebpImage(bitmapFrame, fileName);
                                        break;
                                }
                            });
                    }
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Scanner?.FileName) && FileNameValid(Scanner?.FileName));

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
                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    for (int i = 0; i < resimler.Count; i += 2)
                    {
                        resimler[i].Seçili = true;
                    }
                }
                else
                {
                    foreach (ScannedImage item in resimler)
                    {
                        item.Seçili = true;
                    }
                }
            },
            parameter => Policy.CheckPolicy("Tümünüİşaretle") && Scanner?.Resimler?.Count > 0);

        PdfImportViewerTümünüİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (PdfData item in PdfPages)
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
            parameter => Scanner?.Resimler?.Count > 0);

        TümünüİşaretleYatay = new RelayCommand<object>(
            parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in Scanner.Resimler.Where(item => item.Resim.PixelHeight < item.Resim.PixelWidth))
                {
                    item.Seçili = true;
                }
            },
            parameter => Scanner?.Resimler?.Count > 0);

        TümününİşaretiniKaldır = new RelayCommand<object>(
            parameter =>
            {
                SeçiliResim = null;
                foreach (ScannedImage item in Scanner.Resimler)
                {
                    item.Seçili = false;
                }
            },
            parameter => Scanner?.Resimler?.Count > 0);

        Tersiniİşaretle = new RelayCommand<object>(
            parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler)
                {
                    item.Seçili = !item.Seçili;
                }
            },
            parameter => Scanner?.Resimler?.Count > 0);

        KayıtYoluBelirle = new RelayCommand<object>(
            parameter =>
            {
                FolderBrowserDialog dialog = new() { Description = $"{Translation.GetResStringValue("FASTSCAN")}\n{Translation.GetResStringValue("AUTOFOLDER")}", SelectedPath = Settings.Default.AutoFolder };
                string oldpath = Settings.Default.AutoFolder;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.AutoFolder = dialog.SelectedPath;
                    Scanner.LocalizedPath = ShellIcon.GetDisplayName(dialog.SelectedPath);
                }

                if (!string.IsNullOrWhiteSpace(oldpath) && oldpath != Settings.Default.AutoFolder)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("AUTOFOLDERCHANGE"), AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            },
            parameter => true);

        SeçiliKaydet = new RelayCommand<object>(
            parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }

                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Jpg Resmi (*.jpg)|*.jpg|Tif Resmi (*.tif)|*.tif|Txt Dosyası (*.txt)|*.txt|Webp Dosyası (*.webp)|*.webp|Zip Dosyası (*.zip)|*.zip",
                    FileName = Scanner.SaveFileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Filesavetask = Task.Run(
                        async () =>
                        {
                            List<ScannedImage> seçiliresimler = Scanner?.Resimler?.Where(z => z.Seçili).ToList();
                            string fileName = saveFileDialog.FileName;
                            switch (saveFileDialog.FilterIndex)
                            {
                                case 1:
                                    await SavePdfImageAsync(seçiliresimler, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, false, Settings.Default.ImgLoadResolution);
                                    break;

                                case 2:
                                    await SavePdfImageAsync(seçiliresimler, fileName, Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, true, Settings.Default.ImgLoadResolution);
                                    break;

                                case 3:
                                    await SaveJpgImageAsync(seçiliresimler, fileName, Settings.Default.WebPJpgFileProcessorCount);
                                    break;

                                case 4:
                                    await SaveTifImageAsync(seçiliresimler, fileName);
                                    break;

                                case 5:
                                    await SaveTxtFileAsync(seçiliresimler, fileName);
                                    break;

                                case 6:
                                    await SaveWebpImageAsync(seçiliresimler, fileName, Settings.Default.WebPJpgFileProcessorCount);
                                    break;

                                case 7:
                                    SaveZipImage(seçiliresimler, fileName);
                                    break;
                            }

                            await Dispatcher.InvokeAsync(
                                () =>
                                {
                                    if (Settings.Default.RemoveProcessedImage)
                                    {
                                        SeçiliListeTemizle.Execute(null);
                                    }
                                });
                        });
                }
            },
            parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner?.Resimler.Count(z => z.Seçili) ?? 0;
                return Policy.CheckPolicy("SeçiliKaydet") && !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        SeçiliDirektPdfKaydet = new RelayCommand<object>(
            parameter => Filesavetask =
            Task.Run(
                async () =>
                {
                    List<ScannedImage> seçiliresimler = Scanner.Resimler.Where(z => z.Seçili).ToList();
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
                            Scanner.PdfSaveProgressValue = i / (double)seçiliresimler.Count;
                        }
                    }

                    bool isBlackAndWhiteMode = (ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite;
                    bool isColourOrGreyscaleMode = (ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale;
                    if (isBlackAndWhiteMode || isColourOrGreyscaleMode)
                    {
                        await SavePdfImageAsync(seçiliresimler, PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, Scanner.ApplyPdfSaveOcr, isBlackAndWhiteMode, Settings.Default.ImgLoadResolution);
                    }

                    await Dispatcher.InvokeAsync(
                        () =>
                        {
                            OnPropertyChanged(nameof(Scanner.Resimler));
                            if (Settings.Default.RemoveProcessedImage)
                            {
                                SeçiliListeTemizle.Execute(null);
                            }
                        });
                }),
            parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner?.Resimler.Count(z => z.Seçili) ?? 0;
                return Policy.CheckPolicy("SeçiliDirektPdfKaydet") && !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.AutoSave == true && Scanner?.SeçiliResimSayısı > 0 && FileNameValid(Scanner?.FileName);
            });

        ListeTemizle = new RelayCommand<object>(
            parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault(), Translation.GetResStringValue("TASKSRUNNING"), AppName);
                    return;
                }

                if (MessageBox.Show(Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault(), Translation.GetResStringValue("LISTREMOVEWARN"), AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                    UndoImage = null;
                    ToolBox.ResetCropMargin();
                    GC.Collect();
                }
            },
            parameter => Policy.CheckPolicy("ListeTemizle") && Scanner?.Resimler?.Count > 0 && Scanner.ArayüzEtkin);

        SeçiliListeTemizle = new RelayCommand<object>(
            parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.Where(z => z.Seçili).ToList())
                {
                    _ = Scanner.Resimler?.Remove(item);
                }

                UndoImage = null;
                ToolBox.ResetCropMargin();
                GC.Collect();
            },
            parameter => Policy.CheckPolicy("SeçiliListeTemizle") && Scanner?.Resimler?.Any(z => z.Seçili) == true && Scanner.ArayüzEtkin);

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
            Scanner?.Tarayıcılar?.Count > 0 &&
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
                    try
                    {
                        string dllpath = $@"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\x86\pdfium.dll";
                        ZipExtractSingleFile(openFileDialog.FileName, "runtimes/win-x86/native/pdfium.dll", dllpath);
                        _ = MessageBox.Show($"{Translation.GetResStringValue("INSTALLED")}\n{Translation.GetResStringValue("RESTARTAPP")}", AppName);
                    }
                    catch (Exception)
                    {
                        _ = MessageBox.Show(Translation.GetResStringValue("FILEINUSE"), AppName, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp;*.webp|Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.webp)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.webp|Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps|Eyp Dosyası (*.eyp)|*.eyp|Webp Dosyası (*.webp)|*.webp|Zip Dosyası (*.zip)|*.zip",
                    Multiselect = true
                };

                if (CheckWithCurrentOsVersion("10.0.17134"))
                {
                    openFileDialog.Filter += "|Heic Dosyası (*.heic)|*.heic";
                }

                if (openFileDialog.ShowDialog() == true)
                {
                    GC.Collect();
                    await AddFiles(openFileDialog.FileNames, DecodeHeight);
                }
            },
            parameter => Policy.CheckPolicy("LoadImage"));

        LoadSingleUdfFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Uyap Dokuman Formatı (*.udf)|*.udf|Xps Dosyası (*.xps)|*.xps", Multiselect = false };

                if (openFileDialog.ShowDialog() == true && parameter is XpsViewer xpsViewer)
                {
                    switch (Path.GetExtension(openFileDialog.FileName))
                    {
                        case ".udf":
                            xpsViewer.XpsDataFilePath = LoadUdfFile(openFileDialog.FileName);
                            return;
                        case ".xps":
                            xpsViewer.XpsDataFilePath = openFileDialog.FileName;
                            break;
                    }
                }
            },
            parameter => true);

        SplitPdf = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string savefolder = ToolBox.CreateSaveFolder("SPLIT");
                    SplitPdfPageCount(pdfviewer.PdfFilePath, savefolder, PdfSplitCount);
                    WebAdreseGit.Execute(savefolder);
                }
            },
            parameter => PdfSplitCount > 0);

        AddFromClipBoard = new RelayCommand<object>(
            parameter =>
            {
                System.Windows.Forms.IDataObject clipboardData = Clipboard.GetDataObject();
                if (clipboardData?.GetDataPresent(DataFormats.Bitmap) == true)
                {
                    Scanner?.Resimler?.Add(new ScannedImage { Seçili = true, Resim = CreateBitmapFromClipBoard(clipboardData) });
                    clipboardData = null;
                    Clipboard.Clear();
                }
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
                SaveFileDialog saveFileDialog = new() { Filter = "Txt Dosyası (*.txt)|*.txt", FileName = "Filedata.txt" };
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

        LoadFileList = new RelayCommand<object>(
            async parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Txt Dosyası (*.txt)|*.txt" };
                if (openFileDialog.ShowDialog() == true)
                {
                    await AddFiles(File.ReadAllLines(openFileDialog.FileName), DecodeHeight);
                }
            },
            parameter => true);

        EypPdfİçerikBirleştir = new RelayCommand<object>(
            async parameter =>
            {
                string[] files = Scanner?.UnsupportedFiles?.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (files?.Length > 0)
                {
                    await files.SavePdfFilesAsync();
                }
            },
            parameter => true);

        EypPdfSeçiliDosyaSil = new RelayCommand<object>(parameter => Scanner?.UnsupportedFiles?.Remove(parameter as string), parameter => true);

        EypPdfDosyaEkle = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Pdf Dosyası (*.pdf)|*.pdf", Multiselect = true };
                if (openFileDialog.ShowDialog() == true)
                {
                    string[] files = openFileDialog.FileNames;
                    if (files.Length > 0)
                    {
                        foreach (string item in files)
                        {
                            Scanner?.UnsupportedFiles?.Add(item);
                        }
                    }
                }
            },
            parameter => true);

        int cycleindex = 0;
        CycleSelectedDocuments = new RelayCommand<object>(
            async parameter =>
            {
                ScannedImage scannedImage = Scanner.Resimler.Where(z => z.Seçili).ElementAtOrDefault(cycleindex);
                if (parameter is ListBox listBox && scannedImage is not null)
                {
                    listBox.ScrollIntoView(scannedImage);
                    scannedImage.Animate = true;
                    cycleindex++;
                    cycleindex %= Scanner?.Resimler?.Count(z => z.Seçili) ?? 0;
                    await Task.Delay(1000);
                    scannedImage.Animate = false;
                }
            },
            parameter => Scanner?.Resimler?.Count(z => z.Seçili) > 0);

        PdfWaterMark = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    int currentpage = pdfViewer.Sayfa;
                    string oldpdfpath = pdfViewer.PdfFilePath;
                    using (PdfDocument reader = PdfReader.Open(oldpdfpath))
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Alt)
                        {
                            for (int i = 0; i < reader.PageCount; i++)
                            {
                                using PdfDocument listDocument = reader.GenerateWatermarkedPdf(i, PdfWatermarkFontAngle, PdfWatermarkColor, PdfWatermarkFontSize, PdfWaterMarkText, PdfWatermarkFont);
                                listDocument.Save(oldpdfpath);
                            }
                        }
                        else
                        {
                            using PdfDocument document = reader.GenerateWatermarkedPdf(pdfViewer.Sayfa - 1, PdfWatermarkFontAngle, PdfWatermarkColor, PdfWatermarkFontSize, PdfWaterMarkText, PdfWatermarkFont);
                            document.Save(oldpdfpath);
                        }
                    }
                    await Task.Delay(1000);
                    pdfViewer.PdfFilePath = null;
                    pdfViewer.PdfFilePath = oldpdfpath;
                    pdfViewer.Sayfa = currentpage;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath) && !string.IsNullOrWhiteSpace(PdfWaterMarkText));

        MergeSelectedImagesToPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                List<ScannedImage> seçiliresimler = Scanner?.Resimler?.Where(z => z.Seçili).ToList();
                if (parameter is PdfViewer.PdfViewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                seçiliresimler.Any() &&
                MessageBox.Show($"{Translation.GetResStringValue("SAVESELECTED")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string pdfFilePath = pdfviewer.PdfFilePath;
                    string temporarypdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                    string[] processedfiles = Keyboard.Modifiers == ModifierKeys.Alt ? [pdfFilePath, temporarypdf] : [temporarypdf, pdfFilePath];
                    await Task.Run(
                        async () =>
                        {
                            using PdfDocument pdfDocument = await seçiliresimler.GeneratePdfAsync(Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, null, Settings.Default.ImgLoadResolution);
                            pdfDocument.Save(temporarypdf);
                            processedfiles.MergePdf().Save(pdfFilePath);
                        });
                    pdfviewer.Sayfa = 1;
                    NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                    if (Settings.Default.RemoveProcessedImage)
                    {
                        SeçiliListeTemizle.Execute(null);
                    }
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        PasteFileToPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    System.Windows.Forms.IDataObject clipboardData = Clipboard.GetDataObject();
                    if (clipboardData is null)
                    {
                        return;
                    }

                    string pdfFilePath = pdfviewer.PdfFilePath;
                    string temporaryPdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                    string[] processedFiles = Keyboard.Modifiers == ModifierKeys.Alt ? [pdfFilePath, temporaryPdf] : [temporaryPdf, pdfFilePath];
                    if (clipboardData.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] clipboardFiles = (string[])clipboardData.GetData(System.Windows.DataFormats.FileDrop);
                        List<string> clipboardPdfFiles = clipboardFiles.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
                        List<string> clipboardImageFiles = clipboardFiles.Where(z => imagefileextensions.Contains(Path.GetExtension(z).ToLower())).ToList();
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
                                        using (PdfDocument document = clipboardImageFiles.GeneratePdf(SelectedPaper))
                                        {
                                            document.Save(temporaryPdf);
                                        }

                                        processedFiles.MergePdf().Save(pdfFilePath);
                                    }
                                });

                            pdfviewer.Sayfa = 1;
                            NotifyPdfChange(pdfviewer, temporaryPdf, pdfFilePath);
                            clipboardImageFiles = null;
                            clipboardPdfFiles = null;
                        }
                    }

                    if (clipboardData.GetDataPresent(DataFormats.Bitmap))
                    {
                        using Bitmap bitmap = (Bitmap)clipboardData.GetData(DataFormats.Bitmap);
                        IntPtr gdibitmap = bitmap.GetHbitmap();
                        BitmapSource image = Imaging.CreateBitmapSourceFromHBitmap(gdibitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        _ = Helpers.DeleteObject(gdibitmap);
                        if (image != null)
                        {
                            BitmapFrame bitmapFrame = GenerateBitmapFrame(image);
                            await Task.Run(
                                () =>
                                {
                                    using (PdfDocument pdfDocument = bitmapFrame.GeneratePdf(null, Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution))
                                    {
                                        pdfDocument.Save(temporaryPdf);
                                    }

                                    processedFiles.MergePdf().Save(pdfFilePath);
                                });
                            pdfviewer.Sayfa = 1;
                            NotifyPdfChange(pdfviewer, temporaryPdf, pdfFilePath);
                            image = null;
                            processedFiles = null;
                            bitmapFrame = null;
                        }
                    }

                    clipboardData = null;
                    Clipboard.Clear();
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ReadPdfTag = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is string filepath && File.Exists(filepath))
                {
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
                    .Append($"{reader.FileSize / 1048576d:##.##}")
                    .AppendLine(" MB");
                    _ = MessageBox.Show(stringBuilder.ToString(), AppName);
                }
            },
            parameter => parameter is string filepath && File.Exists(filepath));

        AddAllFileToControlPanel = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                    {
                        PdfImportViewer.PdfViewer.PdfFilePath = pdfviewer.PdfFilePath;
                        SelectedTabIndex = 4;
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

                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(ms);
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    Scanner?.Resimler.Add(scannedImage);
                    ms = null;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        RotateSelectedPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string path = pdfviewer.PdfFilePath;
                    int currentpage = pdfviewer.Sayfa;
                    using PdfDocument inputDocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Import);
                    if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            SavePageRotated(path, inputDocument, -90);
                            pdfviewer.PdfFilePath = null;
                            pdfviewer.PdfFilePath = path;
                            return;
                        }

                        SavePageRotated(path, inputDocument, 90);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = path;
                        return;
                    }

                    SavePageRotated(path, inputDocument, Keyboard.Modifiers == ModifierKeys.Alt ? -90 : 90, pdfviewer.Sayfa - 1);
                    await Task.Delay(1000);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = path;
                    pdfviewer.Sayfa = currentpage;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ArrangePdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("REPLACEPAGE")} {SayfaBaşlangıç}-{SayfaBitiş}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    int start = SayfaBaşlangıç - 1;
                    int end = SayfaBitiş - 1;
                    await ArrangeFileAsync(pdfviewer.PdfFilePath, pdfviewer.PdfFilePath, start, end);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath) && SayfaBaşlangıç != SayfaBitiş);

        ReversePdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("SAVEPDF")} {Translation.GetResStringValue("REVERSE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    await ReverseFileAsync(pdfviewer.PdfFilePath, pdfviewer.PdfFilePath);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath) && pdfviewer.ToplamSayfa > 1);

        AddPdfAttachmentFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("ADDDOC")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    OpenFileDialog openFileDialog = new() { Filter = "Tüm Dosyalar (*.*)|*.*", Multiselect = true };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        string oldpdfpath = pdfviewer.PdfFilePath;
                        await AddAttachmentFileAsync(openFileDialog.FileNames, pdfviewer.PdfFilePath, pdfviewer.PdfFilePath);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = oldpdfpath;
                    }
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        LoadArchiveFile = new RelayCommand<object>(
            parameter =>
            {
                OpenFileDialog openFileDialog = new() { Filter = "Zip Dosyaları (*.zip)|*.zip", Multiselect = false };
                if (openFileDialog.ShowDialog() == true && parameter is ArchiveViewer archiveViewer)
                {
                    archiveViewer.ArchivePath = openFileDialog.FileName;
                }
            },
            parameter => true);

        ClosePdfFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer &&
                File.Exists(pdfviewer.PdfFilePath) &&
                MessageBox.Show($"{Translation.GetResStringValue("CLOSEFILE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.Source = null;
                    pdfviewer.Sayfa = 1;
                    pdfviewer.ToplamSayfa = 0;
                    SayfaBaşlangıç = 1;
                    SayfaBitiş = 1;
                    RefreshDocumentList = true;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

        ReverseData = new RelayCommand<object>(
            parameter =>
            {
                List<ScannedImage> scannedImages = Scanner.Resimler.Reverse().ToList();
                Scanner.Resimler = new ObservableCollection<ScannedImage>(scannedImages);
                Scanner.RefreshIndexNumbers(Scanner.Resimler);
            },
            parameter => Scanner?.Resimler?.Count > 1);

        FirstLastGroup = new RelayCommand<object>(
            parameter =>
            {
                List<ScannedImage> scannedImages = [.. Scanner.Resimler];
                Scanner.Resimler = new ObservableCollection<ScannedImage>(GroupByFirstLastList(scannedImages, GroupSplitCount));
                Scanner.RefreshIndexNumbers(Scanner.Resimler);
            },
            parameter => Scanner?.Resimler?.Count > 1);

        ShuffleData = new RelayCommand<object>(
            parameter =>
            {
                Random random = new();
                Scanner.Resimler = Shuffle(Scanner.Resimler, random);
                Scanner.RefreshIndexNumbers(Scanner.Resimler);
            },
            parameter => Scanner?.Resimler?.Count > 1);

        FirstLastSortSequenceData = new RelayCommand<object>(
            parameter =>
            {
                Scanner.Resimler = FirstLastSequence(Scanner.Resimler);
                Scanner.RefreshIndexNumbers(Scanner.Resimler);
            },
            parameter => Scanner?.Resimler?.Count > 2 && Scanner?.Resimler?.Count % 2 == 0);

        ReverseDataHorizontal = new RelayCommand<object>(
            parameter =>
            {
                int start = Scanner.Resimler.IndexOf(Scanner?.Resimler.FirstOrDefault(z => z.Seçili));
                int end = Scanner.Resimler.IndexOf(Scanner?.Resimler.LastOrDefault(z => z.Seçili));
                if (Scanner?.Resimler?.Count(z => z.Seçili) == end - start + 1)
                {
                    List<ScannedImage> scannedImages = [.. Scanner.Resimler];
                    scannedImages.Reverse(start, end - start + 1);
                    Scanner.Resimler = new ObservableCollection<ScannedImage>(scannedImages);
                    Scanner.RefreshIndexNumbers(Scanner.Resimler);
                }
            },
            parameter =>
            {
                List<ScannedImage> selected = Scanner?.Resimler?.Where(z => z.Seçili).ToList();
                int start = Scanner?.Resimler?.IndexOf(selected?.FirstOrDefault()) ?? 0;
                int end = Scanner?.Resimler?.IndexOf(selected?.LastOrDefault()) ?? 0;
                return Scanner?.Resimler?.Count(z => z.Seçili) > 1 && selected?.Count() == end - start + 1;
            });

        RemoveSelectedPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string path = pdfviewer.PdfFilePath;
                    int currentpage = pdfviewer.Sayfa;
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        if (MessageBox.Show($"{Translation.GetResStringValue("PAGENUMBER")} {SayfaBaşlangıç}-{SayfaBitiş} {Translation.GetResStringValue("DELETE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) ==
                        MessageBoxResult.Yes)
                        {
                            await RemovePdfPageAsync(path, SayfaBaşlangıç, SayfaBitiş);
                            pdfviewer.Sayfa = 1;
                            pdfviewer.PdfFilePath = null;
                            pdfviewer.PdfFilePath = path;
                            SayfaBaşlangıç = SayfaBitiş = 1;
                        }
                        return;
                    }

                    await RemovePdfPageAsync(path, currentpage, currentpage);
                    await Task.Delay(1000);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = path;
                    pdfviewer.Sayfa = currentpage;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath) && pdfviewer.ToplamSayfa > 1 && SayfaBaşlangıç <= SayfaBitiş && SayfaBitiş - SayfaBaşlangıç + 1 < pdfviewer.ToplamSayfa);

        ExtractPdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is string loadfilename && File.Exists(loadfilename))
                {
                    SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = $"{Path.GetFileNameWithoutExtension(loadfilename)} {Translation.GetResStringValue("PAGENUMBER")} {SayfaBaşlangıç}-{SayfaBitiş}.pdf" };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        string savefilename = saveFileDialog.FileName;
                        int start = SayfaBaşlangıç;
                        int end = SayfaBitiş;
                        await PdfPageRangeSaveFileAsync(loadfilename, savefilename, start, end);
                    }
                }
            },
            parameter => parameter is string loadfilename && File.Exists(loadfilename) && SayfaBaşlangıç <= SayfaBitiş);

        LoadPdfExtractFile = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    PdfPages = [];
                    for (int i = 1; i <= pdfViewer.ToplamSayfa; i++)
                    {
                        PdfPages.Add(new PdfData { PageNumber = i });
                    }
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        CopyPdfBitmapFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        Clipboard.SetImage(((BitmapSource)pdfViewer.Source).BitmapSourceToBitmap());
                        return;
                    }
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfViewer.PdfFilePath);
                    using MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfViewer.Sayfa, Settings.Default.ImgLoadResolution);
                    using Image image = Image.FromStream(ms);
                    Clipboard.SetImage(image);
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        ApplyPdfMedianFilter = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfViewer.PdfFilePath);
                    MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, PdfImportViewer.PdfViewer.Sayfa, Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(ms);
                    ms = null;
                    using PdfDocument document = bitmapFrame.MedianFilterBitmap(PdfMedianValue).GeneratePdf(null, Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, Settings.Default.ImgLoadResolution);
                    SaveFileDialog saveFileDialog = new() { Filter = "Pdf Dosyası(*.pdf)|*.pdf", FileName = $"{Translation.GetResStringValue("PAGENUMBER")} {pdfViewer.Sayfa}.pdf" };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        document.Save(saveFileDialog.FileName);
                        PdfMedianValue = 0;
                    }
                }
            },
            parameter => PdfMedianValue > 0 && parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        ExtractMultiplePdfFile = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer && pdfViewer.PdfFilePath is not null)
                {
                    string savefolder = ToolBox.CreateSaveFolder("SPLIT");
                    List<string> files = [];
                    List<PdfData> currentpages = PdfPages?.Where(currentpage => currentpage.Selected).ToList();
                    double pagecount = currentpages.Count;
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
            },
            parameter => PdfPages?.Any(z => z.Selected) == true);

        AddPageNumber = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer)
                {
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    int currentpage = pdfviewer.Sayfa;
                    using PdfDocument document = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify);
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        for (int i = 0; i < document.PageCount; i++)
                        {
                            PdfPage pageall = document.Pages[i];
                            using XGraphics gfxall = XGraphics.FromPdfPage(pageall, XGraphicsPdfPageOptions.Append);
                            gfxall.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfPageNumberAlignTextColor)), (i + 1).ToString(), PdfGeneration.GetPdfTextLayout(pageall)[0], PdfGeneration.GetPdfTextLayout(pageall)[1]);
                        }

                        document.Save(pdfviewer.PdfFilePath);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = oldpdfpath;
                        pdfviewer.Sayfa = 1;
                        return;
                    }

                    PdfPage page = document.Pages[pdfviewer.Sayfa - 1];
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    gfx.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfPageNumberAlignTextColor)), pdfviewer.Sayfa.ToString(), PdfGeneration.GetPdfTextLayout(page)[0], PdfGeneration.GetPdfTextLayout(page)[1]);
                    document.Save(pdfviewer.PdfFilePath);
                    await Task.Delay(1000);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                    pdfviewer.Sayfa = currentpage;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

        FlipPdfPage = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer)
                {
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    int currentpage = pdfviewer.Sayfa;
                    using PdfDocument document = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify);
                    PdfPage page = document.Pages[currentpage - 1];
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Replace);
                    XPoint center = new(page.Width / 2, page.Height / 2);
                    gfx.ScaleAtTransform(Keyboard.Modifiers == ModifierKeys.Alt ? 1 : -1, Keyboard.Modifiers == ModifierKeys.Alt ? -1 : 1, center);
                    BitmapImage bitmapImage = await PdfViewer.PdfViewer.ConvertToImgAsync(pdfviewer.PdfFilePath, currentpage);
                    XImage image = XImage.FromBitmapSource(bitmapImage);
                    gfx.DrawImage(image, 0, 0);
                    document.Save(pdfviewer.PdfFilePath);
                    image = null;
                    bitmapImage = null;
                    await Task.Delay(1000);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                    pdfviewer.Sayfa = currentpage;
                }
            },
            parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

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

        ResetPreviewSize = new RelayCommand<object>(parameter => Settings.Default.PreviewWidth = 155, parameter => true);

        ApplyCropCurrentImage = new RelayCommand<object>(
            parameter =>
            {
                BitmapFrame bitmapframe = BitmapFrame.Create(GenerateCroppedImage(SeçiliResim.Resim, Settings.Default.Top, Settings.Default.Left, Settings.Default.Bottom, Settings.Default.Right));
                bitmapframe.Freeze();
                SeçiliResim.Resim = bitmapframe;
            },
            parameter => SeçiliResim is not null && PageWidth == SeçiliResim.Resim.PixelWidth && PageHeight == SeçiliResim.Resim.PixelHeight && Settings.Default.Left != Settings.Default.Right && Settings.Default.Top != Settings.Default.Bottom);

        ApplyCropAllImages = new RelayCommand<object>(
            parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.Where(z => z.Seçili).ToList())
                {
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
                List<ScannedImage> distinct = Scanner?.Resimler?.Where(z => z.Seçili)?.Distinct(new ImageWidthHeightComparer()).ToList();
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

        DetectEmptyPages = new RelayCommand<object>(
            parameter => Parallel.ForEach(Scanner.Resimler, item => item.Seçili = item.Resim.Resize(0.1).BitmapSourceToBitmap().IsEmptyPage(Settings.Default.EmptyThreshold)),
            parameter => Scanner?.Resimler?.Any() == true);

        AddActiveVisibleContentImage = new RelayCommand<object>(
            parameter =>
            {
                ScrollViewer scrollviewer = ImgViewer?.FindVisualChildren<ScrollViewer>()?.First();
                if (scrollviewer != null)
                {
                    System.Windows.Controls.Image image = scrollviewer.Content as System.Windows.Controls.Image;
                    BitmapFrame bitmapFrame = BitmapFrame.Create(image?.ToRenderTargetBitmap(scrollviewer.ViewportWidth, scrollviewer.ViewportHeight));
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    Scanner?.Resimler.Insert(SeçiliResim.Index, scannedImage);
                }
            },
            parameter => SeçiliResim is not null);

        GridSplitterMouseDoubleClick =
       new RelayCommand<object>(
            parameter =>
            {
                TwainGuiControlLength = new GridLength(3, GridUnitType.Star);
                DocumentGridLength = new GridLength(5, GridUnitType.Star);
            },
            parameter => true);

        GridSplitterMouseRightButtonDown =
            new RelayCommand<object>(
            parameter =>
            {
                TwainGuiControlLength = new GridLength(1, GridUnitType.Star);
                DocumentGridLength = new GridLength(0, GridUnitType.Star);
            },
            parameter => true);

        PdfViewerFullScreen =
            new RelayCommand<object>(
            parameter =>
            {
                PdfImportViewerControl pdfImportViewerControl = new();
                pdfImportViewerControl.PdfViewer.PdfFilePath = parameter as string;
                pdfImportViewerControl.DataContext = this;
                maximizedWindow = new() { WindowState = WindowState.Maximized, ShowInTaskbar = true, Title = AppName, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                maximizedWindow.Closed += (s, e) =>
                                          {
                                              maximizedWindow = null;
                                              pdfImportViewerControl?.PdfViewer?.Dispose();
                                              pdfImportViewerControl.PdfViewer.PdfFilePath = null;
                                              RefreshDocumentList = true;
                                          };
                maximizedWindow.Content = pdfImportViewerControl;
                _ = maximizedWindow.ShowDialog();
            },
            parameter => !IsWindowOpen(maximizedWindow));

        ImageViewerFullScreen =
            new RelayCommand<object>(
            parameter =>
            {
                ImageViewer imageViewer = new() { PanoramaButtonVisibility = Visibility.Collapsed, PrintButtonVisibility = Visibility.Visible, ImageFilePath = parameter as string };
                maximizedWindow = new() { WindowState = WindowState.Maximized, ShowInTaskbar = true, Title = AppName, WindowStartupLocation = WindowStartupLocation.CenterOwner };
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

        XmlViewerFullScreen =
            new RelayCommand<object>(
            parameter =>
            {
                XmlViewerControl xmlViewerControl = new();
                XmlViewerControlModel.SetXmlContent(xmlViewerControl, parameter as string);
                maximizedWindow = new() { WindowState = WindowState.Maximized, ShowInTaskbar = true, Title = AppName, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                maximizedWindow.Closed += (s, e) =>
                                          {
                                              maximizedWindow = null;
                                              XmlViewerControlModel.SetXmlContent(xmlViewerControl, null);
                                          };
                maximizedWindow.Content = xmlViewerControl;
                _ = maximizedWindow.ShowDialog();
            },
            parameter => true);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static System.Windows.Input.Cursor DragCursor { get; set; }

    public RelayCommand<object> AddActiveVisibleContentImage { get; }

    public ICommand AddAllFileToControlPanel { get; }

    public ICommand AddFromClipBoard { get; }

    public ICommand AddPageNumber { get; }

    public ICommand AddPdfAttachmentFile { get; }

    public double AllImageRotationAngle
    {
        get => allImageRotationAngle;

        set
        {
            if (allImageRotationAngle != value)
            {
                allImageRotationAngle = value;
                OnPropertyChanged(nameof(AllImageRotationAngle));
            }
        }
    }

    public RelayCommand<object> ApplyCropAllImages { get; }

    public RelayCommand<object> ApplyCropCurrentImage { get; }

    public ICommand ApplyPdfMedianFilter { get; }

    public ICommand ArrangePdfFile { get; }

    public RelayCommand<object> AutoDeskewImage { get; }

    public byte[] CameraQRCodeData
    {
        get => cameraQRCodeData;

        set
        {
            if (cameraQRCodeData != value)
            {
                cameraQRCodeData = value;
                OnPropertyChanged(nameof(CameraQRCodeData));
            }
        }
    }

    public bool CanUndoImage
    {
        get => canUndoImage;

        set
        {
            if (canUndoImage != value)
            {
                canUndoImage = value;
                OnPropertyChanged(nameof(CanUndoImage));
            }
        }
    }

    public ICommand ClearPdfHistory { get; }

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

    public ICommand CopyPdfBitmapFile { get; }

    public CroppedBitmap CroppedOcrBitmap
    {
        get => croppedOcrBitmap;

        set
        {
            if (croppedOcrBitmap != value)
            {
                croppedOcrBitmap = value;
                OnPropertyChanged(nameof(CroppedOcrBitmap));
            }
        }
    }

    public ICommand CycleSelectedDocuments { get; }

    public byte[] DataBaseQrData
    {
        get => dataBaseQrData;

        set
        {
            if (dataBaseQrData != value)
            {
                dataBaseQrData = value;
                OnPropertyChanged(nameof(DataBaseQrData));
            }
        }
    }

    public ObservableCollection<OcrData> DataBaseTextData
    {
        get => dataBaseTextData;

        set
        {
            if (dataBaseTextData != value)
            {
                dataBaseTextData = value;
                OnPropertyChanged(nameof(DataBaseTextData));
            }
        }
    }

    public int DecodeHeight
    {
        get => decodeHeight;

        set
        {
            if (decodeHeight != value)
            {
                decodeHeight = value;
                OnPropertyChanged(nameof(DecodeHeight));
            }
        }
    }

    public RelayCommand<object> DetectEmptyPages { get; }

    public string DistinctImages
    {
        get => distinctImages;

        set
        {
            if (distinctImages != value)
            {
                distinctImages = value;
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
        get => documentPreviewIsExpanded;

        set
        {
            if (documentPreviewIsExpanded != value)
            {
                documentPreviewIsExpanded = value;
                OnPropertyChanged(nameof(DocumentPreviewIsExpanded));
            }
        }
    }

    public bool DragMoveStarted
    {
        get => dragMoveStarted;

        set
        {
            if (dragMoveStarted != value)
            {
                dragMoveStarted = value;
                OnPropertyChanged(nameof(DragMoveStarted));
            }
        }
    }

    public ICommand ExploreFile { get; }

    public ICommand ExtractMultiplePdfFile { get; }

    public RelayCommand<object> ExtractNugetPackage { get; }

    public ICommand ExtractPdfFile { get; }

    public ICommand EypPdfDosyaEkle { get; }

    public ICommand EypPdfİçerikBirleştir { get; }

    public ICommand EypPdfSeçiliDosyaSil { get; }

    public ICommand FastScanImage { get; }

    public RelayCommand<object> FirstLastGroup { get; }

    public RelayCommand<object> FirstLastSortSequenceData { get; }

    public RelayCommand<object> FlipPdfPage { get; }

    public RelayCommand<object> GridSplitterMouseDoubleClick { get; }

    public RelayCommand<object> GridSplitterMouseRightButtonDown { get; }

    public int GroupSplitCount
    {
        get => groupSplitCount;
        set
        {
            if (groupSplitCount != value)
            {
                groupSplitCount = value;
                OnPropertyChanged(nameof(GroupSplitCount));
            }
        }
    }

    public bool HelpIsOpened
    {
        get => helpIsOpened;
        set
        {
            if (helpIsOpened != value)
            {
                helpIsOpened = value;
                OnPropertyChanged(nameof(HelpIsOpened));
            }
        }
    }

    public bool IgnoreImageWidthHeight
    {
        get => ıgnoreImageWidthHeight;
        set
        {
            if (ıgnoreImageWidthHeight != value)
            {
                ıgnoreImageWidthHeight = value;
                OnPropertyChanged(nameof(IgnoreImageWidthHeight));
            }
        }
    }

    public RelayCommand<object> ImageViewerFullScreen { get; }

    public byte[] ImgData
    {
        get => ımgData;

        set
        {
            if (ımgData != value)
            {
                ımgData = value;
                OnPropertyChanged(nameof(ImgData));
            }
        }
    }

    public ICommand InsertClipBoardImage { get; }

    public ICommand InsertFileNamePlaceHolder { get; }

    public RelayCommand<object> InvertImage { get; }

    public RelayCommand<object> InvertSelectedImage { get; }

    public ICommand Kaydet { get; }

    public ICommand KayıtYoluBelirle { get; }

    public ICommand ListeTemizle { get; }

    public RelayCommand<object> LoadArchiveFile { get; }

    public ICommand LoadCroppedImage { get; }

    public ICommand LoadFileList { get; }

    public ICommand LoadImage { get; }

    public ICommand LoadPdfExtractFile { get; }

    public ICommand LoadSingleUdfFile { get; }

    public ICommand MergeSelectedImagesToPdfFile { get; }

    public RelayCommand<object> OpenHelpDialog { get; }

    public int PageHeight
    {
        get => pageHeight;
        set
        {
            if (pageHeight != value)
            {
                pageHeight = value;
                OnPropertyChanged(nameof(PageHeight));
            }
        }
    }

    public int PageWidth
    {
        get => pageWidth;
        set
        {
            if (pageWidth != value)
            {
                pageWidth = value;
                OnPropertyChanged(nameof(PageWidth));
            }
        }
    }

    public ObservableCollection<Paper> Papers
    {
        get => papers;

        set
        {
            if (papers != value)
            {
                papers = value;
                OnPropertyChanged(nameof(Papers));
            }
        }
    }

    public ICommand PasteFileToPdfFile { get; }

    public RelayCommand<object> PdfImportViewerTersiniİşaretle { get; }

    public RelayCommand<object> PdfImportViewerTümünüİşaretle { get; }

    public double PdfLoadProgressValue
    {
        get => pdfLoadProgressValue;

        set
        {
            if (pdfLoadProgressValue != value)
            {
                pdfLoadProgressValue = value;
                OnPropertyChanged(nameof(PdfLoadProgressValue));
            }
        }
    }

    public int PdfMedianValue
    {
        get => pdfMedianValue;

        set
        {
            if (pdfMedianValue != value)
            {
                pdfMedianValue = value;
                OnPropertyChanged(nameof(PdfMedianValue));
            }
        }
    }

    public ObservableCollection<PdfData> PdfPages
    {
        get => pdfPages;

        set
        {
            if (pdfPages != value)
            {
                pdfPages = value;
                OnPropertyChanged(nameof(PdfPages));
            }
        }
    }

    public int PdfSplitCount
    {
        get => pdfSplitCount;

        set
        {
            if (pdfSplitCount != value)
            {
                pdfSplitCount = value;
                OnPropertyChanged(nameof(PdfSplitCount));
            }
        }
    }

    public RelayCommand<object> PdfViewerFullScreen { get; }

    public ICommand PdfWaterMark { get; }

    public SolidColorBrush PdfWatermarkColor
    {
        get => pdfWatermarkColor;

        set
        {
            if (pdfWatermarkColor != value)
            {
                pdfWatermarkColor = value;
                OnPropertyChanged(nameof(PdfWatermarkColor));
            }
        }
    }

    public string PdfWatermarkFont
    {
        get => pdfWatermarkFont;

        set
        {
            if (pdfWatermarkFont != value)
            {
                pdfWatermarkFont = value;
                OnPropertyChanged(nameof(PdfWatermarkFont));
            }
        }
    }

    public double PdfWatermarkFontAngle
    {
        get => pdfWatermarkFontAngle;

        set
        {
            if (pdfWatermarkFontAngle != value)
            {
                pdfWatermarkFontAngle = value;
                OnPropertyChanged(nameof(PdfWatermarkFontAngle));
            }
        }
    }

    public double PdfWatermarkFontSize
    {
        get => pdfWatermarkFontSize;

        set
        {
            if (pdfWatermarkFontSize != value)
            {
                pdfWatermarkFontSize = value;
                OnPropertyChanged(nameof(PdfWatermarkFontSize));
            }
        }
    }

    public string PdfWaterMarkText
    {
        get => pdfWaterMarkText;

        set
        {
            if (pdfWaterMarkText != value)
            {
                pdfWaterMarkText = value;
                OnPropertyChanged(nameof(PdfWaterMarkText));
            }
        }
    }

    public RelayCommand<object> PrepareCropCurrentImage { get; }

    public ICommand ReadPdfTag { get; }

    public bool RefreshDocumentList
    {
        get => refreshDocumentList;
        set
        {
            if (refreshDocumentList != value)
            {
                refreshDocumentList = value;
                OnPropertyChanged(nameof(RefreshDocumentList));
            }
        }
    }

    public ICommand RemoveProfile { get; }

    public ICommand RemoveSelectedPage { get; }

    public ICommand ResetCrop { get; }

    public RelayCommand<object> ResetPreviewSize { get; }

    public ICommand ResimSil { get; }

    public ICommand ResimSilGeriAl { get; }

    public ICommand ReverseData { get; }

    public ICommand ReverseDataHorizontal { get; }

    public ICommand ReversePdfFile { get; }

    public ICommand RotateSelectedPage { get; }

    public ICommand SaveFileList { get; }

    public int SaveIndex
    {
        get => saveIndex;

        set
        {
            if (saveIndex != value)
            {
                saveIndex = value;
                OnPropertyChanged(nameof(SaveIndex));
            }
        }
    }

    public ICommand SaveProfile { get; }

    public int SayfaBaşlangıç
    {
        get => sayfaBaşlangıç;

        set
        {
            if (sayfaBaşlangıç != value)
            {
                sayfaBaşlangıç = value;
                OnPropertyChanged(nameof(SayfaBaşlangıç));
            }
        }
    }

    public int SayfaBitiş
    {
        get => sayfaBitiş;

        set
        {
            if (sayfaBitiş != value)
            {
                sayfaBitiş = value;
                OnPropertyChanged(nameof(SayfaBitiş));
            }
        }
    }

    public ICommand ScanImage { get; }

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

    public DoubleCollection ScanResolutionList { get; } = [72, 96, 120, 150, 200, 300, 450, 600, 1200, 2400, 4800];

    public ICommand SeçiliDirektPdfKaydet { get; }

    public ICommand SeçiliKaydet { get; }

    public ICommand SeçiliListeTemizle { get; }

    public ScannedImage SeçiliResim
    {
        get => seçiliResim;

        set
        {
            if (seçiliResim != value)
            {
                seçiliResim = value;
                OnPropertyChanged(nameof(SeçiliResim));
            }
        }
    }

    public int SeekIndex
    {
        get => seekIndex;

        set
        {
            if (seekIndex != value)
            {
                seekIndex = value;
                OnPropertyChanged(nameof(SeekIndex));
            }
        }
    }

    public Tuple<string, int, double, bool, double> SelectedCompressionProfile
    {
        get => selectedCompressionProfile;

        set
        {
            if (selectedCompressionProfile != value)
            {
                selectedCompressionProfile = value;
                OnPropertyChanged(nameof(SelectedCompressionProfile));
            }
        }
    }

    public PageFlip SelectedFlip
    {
        get => selectedFlip;
        set
        {
            if (selectedFlip != value)
            {
                selectedFlip = value;
                OnPropertyChanged(nameof(SelectedFlip));
            }
        }
    }

    public bool SelectedImageWidthHeightIsEqual
    {
        get => selectedImageWidthHeightIsEqual;
        set
        {
            if (selectedImageWidthHeightIsEqual != value)
            {
                selectedImageWidthHeightIsEqual = value;
                OnPropertyChanged(nameof(SelectedImageWidthHeightIsEqual));
            }
        }
    }

    public Orientation SelectedOrientation
    {
        get => selectedOrientation;

        set
        {
            if (selectedOrientation != value)
            {
                selectedOrientation = value;
                OnPropertyChanged(nameof(SelectedOrientation));
            }
        }
    }

    public Paper SelectedPaper
    {
        get => selectedPaper;

        set
        {
            if (selectedPaper != value)
            {
                selectedPaper = value;
                OnPropertyChanged(nameof(SelectedPaper));
            }
        }
    }

    public PageRotation SelectedRotation
    {
        get => selectedRotation;

        set
        {
            if (selectedRotation != value)
            {
                selectedRotation = value;
                OnPropertyChanged(nameof(SelectedRotation));
            }
        }
    }

    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set
        {
            if (selectedTabIndex != value)
            {
                selectedTabIndex = value;
                OnPropertyChanged(nameof(SelectedTabIndex));
            }
        }
    }

    public ICommand ShowDateFolderHelp { get; }

    public RelayCommand<object> ShuffleData { get; }

    public ICommand SplitPdf { get; }

    public ICommand Tersiniİşaretle { get; }

    public ICommand Tümünüİşaretle { get; }

    public ICommand TümünüİşaretleDikey { get; }

    public ICommand TümünüİşaretleYatay { get; }

    public ICommand TümününİşaretiniKaldır { get; }

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
        get => undoImage;

        set
        {
            if (undoImage != value)
            {
                undoImage = value;
                OnPropertyChanged(nameof(UndoImage));
            }
        }
    }

    public int? UndoImageIndex
    {
        get => undoImageIndex;

        set
        {
            if (undoImageIndex != value)
            {
                undoImageIndex = value;
                OnPropertyChanged(nameof(UndoImageIndex));
            }
        }
    }

    public FileVersionInfo Version => FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess()?.MainModule?.FileName);

    public ICommand WebAdreseGit { get; }

    public RelayCommand<object> XmlViewerFullScreen { get; }

    public static async Task ArrangeFileAsync(string loadfilename, string savefilename, int start, int end)
    {
        await Task.Run(
            () =>
            {
                using PdfDocument outputDocument = loadfilename.ArrangePdfPages(start, end);
                if (outputDocument != null)
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
            if (eypfilepath is not null && string.Equals(Path.GetExtension(eypfilepath), ".eyp", StringComparison.OrdinalIgnoreCase))
            {
                using ZipArchive archive = ZipFile.Open(eypfilepath, ZipArchiveMode.Read);
                if (archive != null)
                {
                    List<string> data = [];
                    ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "NihaiOzet.xml");
                    string source = $"{Path.GetTempPath()}{Guid.NewGuid()}.xml";
                    üstveri?.ExtractToFile(source, true);
                    XDocument xdoc = XDocument.Load(source);
                    if (xdoc != null)
                    {
                        foreach (string file in xdoc.Descendants().Select(z => Path.GetFileName((string)z.Attribute("URI"))).Where(z => !string.IsNullOrEmpty(z)))
                        {
                            ZipArchiveEntry zipArchiveEntry = archive.Entries.FirstOrDefault(entry => entry.Name == file);
                            if (zipArchiveEntry != null)
                            {
                                string destinationFileName =
                                    $"{Path.GetTempPath()}{Guid.NewGuid()}{Path.GetExtension(file.ToLower())}";
                                zipArchiveEntry.ExtractToFile(destinationFileName, true);
                                data.Add(destinationFileName);
                            }
                        }
                    }

                    return data;
                }
            }
        }
        catch (Exception)
        {
        }
        return null;
    }

    public static bool FileNameValid(string filename) => filename.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    public static BitmapFrame GenerateBitmapFrame(BitmapSource bitmapSource)
    {
        bitmapSource.Freeze();
        BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapSource);
        bitmapFrame.Freeze();
        return bitmapFrame;
    }

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
                _ = MessageBox.Show(ex.Message, AppName);
            }
        }
    }

    public static void NotifyPdfChange(PdfViewer.PdfViewer pdfviewer, string temporarypdf, string pdfFilePath)
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
        catch (Exception)
        {
        }
    }

    public static async Task RemovePdfPageAsync(string pdffilepath, int start, int end)
    {
        await Task.Run(
            () =>
            {
                PdfDocument inputDocument = PdfReader.Open(pdffilepath, PdfDocumentOpenMode.Import);
                if (inputDocument != null)
                {
                    for (int i = end; i >= start; i--)
                    {
                        inputDocument.Pages.RemoveAt(i - 1);
                    }
                    if (inputDocument.PageCount > 1)
                    {
                        inputDocument.Save(pdffilepath);
                    }
                }
            });
    }

    public static void SavePageRotated(string savepath, PdfDocument inputDocument, int angle)
    {
        foreach (PdfPage page in inputDocument.Pages)
        {
            page.Rotate += angle;
        }

        inputDocument.Save(savepath);
    }

    public static void SavePageRotated(string savepath, PdfDocument inputDocument, int angle, int pageindex)
    {
        inputDocument.Pages[pageindex].Rotate += angle;
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
                        switch (Path.GetExtension(filename.ToLower()))
                        {
                            case ".pdf":
                                await AddPdfFiles(filename);

                                break;

                            case ".eyp":
                                await AddEypFiles(filename);
                                break;

                            case ".jpg":
                            case ".jpeg":
                            case ".jfif":
                            case ".jfıf":
                            case ".jpe":
                            case ".png":
                            case ".gif":
                            case ".gıf":
                            case ".bmp":
                                await AddImageFiles(filename);
                                break;

                            case ".heic":
                                if (CheckWithCurrentOsVersion("10.0.17134"))
                                {
                                    await AddImageFiles(filename);
                                }

                                break;

                            case ".zip":
                                await Dispatcher.InvokeAsync(
                                    () =>
                                    {
                                        SelectedTabIndex = 3;
                                        ArchiveVwr.ArchivePath = filename;
                                    });
                                break;

                            case ".webp":
                                await AddWebpFiles(decodeheight, filename);
                                break;

                            case ".tıf" or ".tiff" or ".tıff" or ".tif":
                                await AddTiffFiles(filename);

                                break;

                            case ".xps":
                                await AddXpsFiles(filename);
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
        if (Scanner?.Tarayıcılar?.Count > 0)
        {
            string[] profiles = new string[6];
            string[] dpiValues = ["96", "200", "300"];
            string[] colorModes = ["COLOR", "BW"];
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
        if (sender is StackPanel stackpanel && e.Data.GetData(typeof(ScannedImage)) is ScannedImage droppedData && stackpanel.DataContext is ScannedImage target)
        {
            int removedIdx = Scanner.Resimler.IndexOf(droppedData);
            int targetIdx = Scanner.Resimler.IndexOf(target);

            if (removedIdx < targetIdx)
            {
                Scanner.Resimler.Insert(targetIdx + 1, droppedData);
                Scanner.Resimler.RemoveAt(removedIdx);
                return;
            }

            int remIdx = removedIdx + 1;
            if (Scanner.Resimler.Count + 1 > remIdx)
            {
                Scanner.Resimler.Insert(targetIdx, droppedData);
                Scanner.Resimler.RemoveAt(remIdx);
            }
        }
    }

    public async Task ListBoxDropFileAsync(DragEventArgs e)
    {
        if (fileloadtask?.IsCompleted == false)
        {
            _ = MessageBox.Show(Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault(), Translation.GetResStringValue("TRANSLATEPENDING"), AppName);
            return;
        }

        if (e.Data.GetData(typeof(Scanner)) is Scanner droppedData)
        {
            await Task.Run(() => AddFiles([droppedData.FileName], DecodeHeight));
            return;
        }

        if ((e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
        {
            await Task.Run(() => AddFiles(droppedfiles, DecodeHeight));
        }
    }

    public string LoadUdfFile(string filename)
    {
        ZipArchive archive = ZipFile.Open(filename, ZipArchiveMode.Read);
        ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "content.xml");
        string source = $"{Path.GetTempPath()}{Guid.NewGuid()}.xml";
        string xpssource = $"{Path.GetTempPath()}{Guid.NewGuid()}.xps";
        üstveri?.ExtractToFile(source, true);
        Template xmldata = DeSerialize<Template>(source);
        IDocumentPaginatorSource flowDocument = UdfParser.UdfParser.RenderDocument(xmldata);
        using (XpsDocument xpsDocument = new(xpssource, FileAccess.ReadWrite))
        {
            XpsDocumentWriter xw = XpsDocument.CreateXpsDocumentWriter(xpsDocument);
            xw.Write(flowDocument.DocumentPaginator);
        }

        return xpssource;
    }

    public void SplitPdfPageCount(string pdfpath, string savefolder, int pagecount)
    {
        using PdfDocument inputDocument = PdfReader.Open(pdfpath, PdfDocumentOpenMode.Import);
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
            _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Scanner.Resimler = null;
                twain = null;
                Scanner.CroppedImage = null;
                Scanner.CopyCroppedImage = null;
            }

            disposedValue = true;
        }
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static bool IsWindowOpen(Window window) => Application.Current?.Windows?.Cast<Window>().Any(x => x == window) == true;

    private async Task AddAttachmentFileAsync(string[] files, string loadfilename, string savefilename)
    {
        await Task.Run(
            () =>
            {
                using PdfDocument inputDocument = PdfReader.Open(loadfilename, PdfDocumentOpenMode.Modify);
                if (inputDocument != null)
                {
                    foreach (string item in files)
                    {
                        inputDocument.AddEmbeddedFile(Path.GetFileNameWithoutExtension(item), item);
                    }

                    inputDocument.Save(savefilename);
                }
            });
    }

    private async Task AddEypFiles(string filename)
    {
        List<string> files = EypFileExtract(filename);
        await Dispatcher.InvokeAsync(() => files.ForEach(z => Scanner?.UnsupportedFiles?.Add(z)));
        await AddFiles([.. files], DecodeHeight);
    }

    private async Task AddImageFiles(string filename)
    {
        BitmapImage main = await ImageViewer.LoadImageAsync(filename);
        BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(main.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(main);
        bitmapFrame.Freeze();
        ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
        await Dispatcher.InvokeAsync(() => Scanner?.Resimler.Add(img));
        main = null;
        bitmapFrame = null;
    }

    private async Task AddPdfFiles(string filename)
    {
        if (PdfViewer.PdfViewer.IsValidPdfFile(filename))
        {
            byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(filename);
            if (filedata != null)
            {
                double totalpagecount = await PdfViewer.PdfViewer.PdfPageCountAsync(filedata);
                MemoryStream ms;
                for (int i = 1; i <= totalpagecount; i++)
                {
                    ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(ms, Scanner.Deskew);
                    bitmapFrame.Freeze();
                    await Dispatcher.InvokeAsync(
                        () =>
                        {
                            Scanner?.Resimler.Add(new ScannedImage { Resim = bitmapFrame, FilePath = filename });
                            PdfLoadProgressValue = i / totalpagecount;
                        });
                    bitmapFrame = null;
                }

                _ = await Dispatcher.InvokeAsync(() => PdfLoadProgressValue = 0);
                filedata = null;
                ms = null;
            }
        }
    }

    private void AddPdfFilesToUnsupportedDocs(string[] droppedfiles)
    {
        foreach (string file in droppedfiles.Where(file => string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            Scanner?.UnsupportedFiles?.Add(file);
        }
    }

    private async Task AddTiffFiles(string filename)
    {
        TiffBitmapDecoder decoder = new(new Uri(filename), BitmapCreateOptions.None, BitmapCacheOption.None);
        int tiffpagecount = decoder.Frames.Count;
        decoder = null;
        for (int i = 0; i < tiffpagecount; i++)
        {
            BitmapFrame bitmapFrame = await Task.Run(
                () =>
                {
                    TiffBitmapDecoder decoder = new(new Uri(filename), BitmapCreateOptions.None, BitmapCacheOption.None);
                    BitmapImage image = decoder.Frames[i].ToTiffJpegByteArray(Format.TiffRenkli).ToBitmapImage();
                    image.Freeze();
                    BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(image.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(image);
                    bitmapFrame.Freeze();
                    decoder = null;
                    return bitmapFrame;
                });

            ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
            await Dispatcher.InvokeAsync(
                () =>
                {
                    Scanner?.Resimler.Add(img);
                    double progressvalue = (i + 1) / (double)tiffpagecount;
                    PdfLoadProgressValue = progressvalue == 1 ? 0 : progressvalue;
                });
        }
    }

    private async Task AddWebpFiles(int decodeheight, string filename)
    {
        BitmapImage main = (BitmapImage)filename.WebpDecode(true, decodeheight);
        BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(main.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(main);
        bitmapFrame.Freeze();
        ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
        await Dispatcher.InvokeAsync(() => Scanner?.Resimler.Add(img));
        main = null;
        bitmapFrame = null;
    }

    private async Task AddXpsFiles(string filename)
    {
        FixedDocumentSequence docSeq = null;
        await Dispatcher.InvokeAsync(
            () =>
            {
                using XpsDocument xpsDoc = new(filename, FileAccess.Read);
                docSeq = xpsDoc.GetFixedDocumentSequence();
            });
        int pagecount = docSeq.DocumentPaginator.PageCount;
        for (int i = 0; i < pagecount; i++)
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    using DocumentPage docPage = docSeq.DocumentPaginator.GetPage(i);
                    RenderTargetBitmap rtb = new((int)docPage.Size.Width, (int)docPage.Size.Height, 96, 96, PixelFormats.Default);
                    rtb.Render(docPage.Visual);
                    BitmapFrame bitmapframe = BitmapFrame.Create(rtb);
                    bitmapframe.Freeze();
                    ScannedImage img = new() { Resim = bitmapframe, FilePath = filename };
                    Scanner?.Resimler.Add(img);
                    double progressvalue = (i + 1) / (double)pagecount;
                    PdfLoadProgressValue = progressvalue == 1 ? 0 : progressvalue;
                    img = null;
                    bitmapframe = null;
                });
        }
        docSeq = null;
    }

    private void ButtonedTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Scanner.CaretPosition = (sender as ButtonedTextBox)?.CaretIndex ?? 0;

    private async void CameraUserControl_PropertyChangedAsync(object sender, PropertyChangedEventArgs e)
    {
        if (sender is CameraUserControl cameraUserControl)
        {
            if (e.PropertyName is "ResimData" && cameraUserControl.ResimData is not null)
            {
                MemoryStream ms = new(cameraUserControl.ResimData);
                BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(ms);
                bitmapFrame.Freeze();
                Scanner.Resimler.Add(new ScannedImage { Resim = bitmapFrame });
                ms = null;
            }

            if (e.PropertyName is "DetectQRCode")
            {
                if (cameraUserControl.DetectQRCode)
                {
                    CameraQrCodeTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
                    QrCode.QrCode qrcode = new();
                    CameraQrCodeTimer.Tick += (s, f2) =>
                                              {
                                                  CameraQRCodeData = cameraUserControl.CameraEncodeBitmapImage().ToArray();
                                                  Scanner.BarcodeContent = qrcode.GetImageBarcodeResult(CameraQRCodeData);
                                                  OnPropertyChanged(nameof(CameraQRCodeData));
                                              };
                    CameraQrCodeTimer?.Start();
                    return;
                }

                CameraQrCodeTimer?.Stop();
            }
        }
    }

    private bool CheckWithCurrentOsVersion(string version)
    {
        string osversion = $"{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}.{Environment.OSVersion.Version.Build}";
        Version current = new(osversion);
        Version compare = new(version);
        return current >= compare;
    }

    private BitmapFrame CreateBitmapFromClipBoard(System.Windows.Forms.IDataObject clipboardData)
    {
        using Bitmap bitmap = (Bitmap)clipboardData.GetData(DataFormats.Bitmap);
        IntPtr gdibitmap = bitmap.GetHbitmap();
        BitmapSource image = Imaging.CreateBitmapSourceFromHBitmap(gdibitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        _ = Helpers.DeleteObject(gdibitmap);
        return image != null ? GenerateBitmapFrame(image) : null;
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
                Scanner.PdfSaveProgressValue = i / (double)Scanner.Resimler.Count;
            }
        }

        if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
        {
            (await Scanner.Resimler.ToList().GeneratePdfAsync(Format.Tiff, SelectedPaper, Settings.Default.JpegQuality, PdfFileOcrData, (int)Settings.Default.Çözünürlük)).Save(Scanner.PdfFilePath);
        }

        if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
        {
            (await Scanner.Resimler.ToList().GeneratePdfAsync(Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, PdfFileOcrData, (int)Settings.Default.Çözünürlük)).Save(Scanner.PdfFilePath);
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
        twain.ScanningComplete -= FastScanComplete;
        Scanner.ArayüzEtkin = true;
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

    private void ImgViewer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Image img && img.Parent is ScrollViewer scrollviewer)
        {
            if (e.LeftButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.Control)
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
    }

    private async void ImgViewer_MouseMoveAsync(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Image img && img.Parent is ScrollViewer scrollviewer)
        {
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

                if (e.LeftButton == MouseButtonState.Released)
                {
                    cnv.Children.Remove(selectionbox);
                    width = Math.Abs(x2 - x1);
                    height = Math.Abs(y2 - y1);
                    double coordx = x1 + scrollviewer.HorizontalOffset;
                    double coordy = y1 + scrollviewer.VerticalOffset;
                    ImgData = BitmapMethods.CaptureScreen(coordx, coordy, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));

                    if (Keyboard.Modifiers == ModifierKeys.Shift && ImgData is not null)
                    {
                        MemoryStream ms = new(ImgData);
                        BitmapFrame bitmapframe = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(ms);
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
        }
    }

    private void ImgViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double change = e.Delta > 0 ? .05 : -.05;
            if (ImgViewer.Zoom + change <= 0.01)
            {
                ImgViewer.Zoom = 0.01;
            }
            else
            {
                ImgViewer.Zoom += change;
            }
        }
    }

    private bool IsBlackAndWhiteMode() => Settings.Default.BackMode == (int)ColourSetting.BlackAndWhite && Settings.Default.Mode == (int)ColourSetting.BlackAndWhite;

    private void Language_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        Scanner.UiLanguageChanged = false;
        Scanner.UiLanguageChanged = true;
        if (!Settings.Default.UseSelectedProfile)
        {
            Scanner.FileName = Translation.GetResStringValue("DEFAULTSCANNAME");
        }
    }

    private void LbEypContent_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Scanner)) is Scanner scanner && File.Exists(scanner.FileName))
        {
            AddPdfFilesToUnsupportedDocs([scanner.FileName]);
            return;
        }
        if ((e.Data.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
        {
            AddPdfFilesToUnsupportedDocs(droppedfiles);
        }
    }

    private async void ListBox_DropAsync(object sender, DragEventArgs e) => await ListBoxDropFileAsync(e);

    private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            Settings.Default.PreviewWidth += e.Delta > 0 ? 10 : -10;
            if (Settings.Default.PreviewWidth <= 85)
            {
                Settings.Default.PreviewWidth = 85;
            }

            if (Settings.Default.PreviewWidth >= 400)
            {
                Settings.Default.PreviewWidth = 400;
            }
        }
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

    private async Task PdfPageRangeSaveFileAsync(string loadfilename, string savefilename, int start, int end)
    {
        await Task.Run(
            () =>
            {
                using PdfDocument outputDocument = loadfilename.ExtractPdfPages(start, end);
                if (outputDocument != null)
                {
                    outputDocument.ApplyDefaultPdfCompression();
                    outputDocument.Save(savefilename);
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
                using PdfDocument inputDocument = PdfReader.Open(loadfilename, PdfDocumentOpenMode.Import);
                using PdfDocument outputdocument = new();
                if (outputdocument != null)
                {
                    for (int i = inputDocument.PageCount - 1; i >= 0; i--)
                    {
                        _ = outputdocument.AddPage(inputDocument.Pages[i]);
                    }

                    outputdocument.Save(savefilename);
                }
            });
    }

    private void Run_EypPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is Run run && e.LeftButton == MouseButtonState.Pressed)
        {
            _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
        }
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

    private async Task SaveJpgImageAsync(List<ScannedImage> images, string filename, int parallelDegree = 1)
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
                        }
                        if (Settings.Default.RemoveProcessedImage)
                        {
                            scannedimage.Resim = null;
                        }
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
                scanner.PdfSaveProgressValue = i / (double)images.Count;
            }

            scanner.PdfSaveProgressValue = 0;
        }

        scanner.SaveProgressBarForegroundBrush = defaultsaveprogressforegroundcolor;
        if (blackwhite)
        {
            (await images.GeneratePdfAsync(Format.Tiff, paper, Settings.Default.JpegQuality, scannedtext, dpi)).Save(filename);
            return;
        }

        (await images.GeneratePdfAsync(Format.Jpg, paper, Settings.Default.JpegQuality, scannedtext, dpi)).Save(filename);
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
        if (bitmapFrame is not null && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            await Dispatcher.Invoke(
                async () =>
                {
                    ObservableCollection<OcrData> ocrtext = await bitmapFrame.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage);
                    File.WriteAllText(fileName, string.Join(" ", ocrtext.Select(z => z.Text)));
                });
        }
    }

    private async Task SaveTxtFileAsync(List<ScannedImage> images, string fileName)
    {
        if (images is not null && !string.IsNullOrWhiteSpace(Scanner.SelectedTtsLanguage))
        {
            for (int i = 0; i < images.Count; i++)
            {
                await Dispatcher.Invoke(
                    async () =>
                    {
                        ObservableCollection<OcrData> ocrtext = await images[i].Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsync(Scanner.SelectedTtsLanguage);
                        File.WriteAllText(Path.Combine(Path.GetDirectoryName(fileName), $"{Path.GetFileNameWithoutExtension(fileName)}{i}.txt"), string.Join(" ", ocrtext.Select(z => z.Text)));
                    });
            }
        }
    }

    private void SaveWebpImage(BitmapFrame scannedImage, string filename) => Dispatcher.Invoke(() => File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg).WebpEncode(Settings.Default.WebpQuality)));

    private async Task SaveWebpImageAsync(List<ScannedImage> images, string filename, int parallelDegree = 1)
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
                        }
                        if (Settings.Default.RemoveProcessedImage)
                        {
                            scannedimage.Resim = null;
                        }
                    });
            });
    }

    private void SaveXpsImage(BitmapFrame scannedImage, string filename)
    {
        Dispatcher.Invoke(
            () =>
            {
                System.Windows.Controls.Image image = new();
                image.BeginInit();
                image.Source = scannedImage;
                image.EndInit();
                using XpsDocument xpsd = new(filename, FileAccess.Write);
                XpsDocumentWriter xw = XpsDocument.CreateXpsDocumentWriter(xpsd);
                xw.Write(image);
                image = null;
            });
    }

    private void SaveZipImage(List<ScannedImage> seçiliresimler, string fileName)
    {
        using ZipArchive archive = ZipFile.Open(fileName, ZipArchiveMode.Update);
        for (int i = 0; i < seçiliresimler.Count; i++)
        {
            string fPath = Path.Combine(Path.GetTempPath(), $"{seçiliresimler[i].Index}.jpg");
            File.WriteAllBytes(fPath, seçiliresimler[i].Resim.ToTiffJpegByteArray(Format.Jpg));
            _ = archive.CreateEntryFromFile(fPath, Path.GetFileName(fPath));
            File.Delete(fPath);
        }
    }

    private void ScanCommonSettings()
    {
        Scanner.ArayüzEtkin = false;
        _settings = DefaultScanSettings();
    }

    private void ScanComplete(object sender, ScanningCompleteEventArgs e)
    {
        if (Settings.Default.PlayNotificationAudio)
        {
            PlayNotificationSound(Settings.Default.AudioFilePath);
        }
        twain.ScanningComplete -= ScanComplete;
    }

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "CropLeft" or "CropTop" or "CropRight" or "CropBottom" && SeçiliResim != null)
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

    private ObservableCollection<T> Shuffle<T>(ObservableCollection<T> collection, Random random)
    {
        for (int i = collection.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            T temp = collection[i];
            collection[i] = collection[j];
            collection[j] = temp;
        }
        return collection;
    }

    private void StackPanel_Drop(object sender, DragEventArgs e) => DropFile(sender, e);

    private void StackPanel_EypDrop(object sender, DragEventArgs e)
    {
        if (sender is StackPanel stackpanel && e.Data.GetData(typeof(string)) is string droppedData && stackpanel.DataContext is string target)
        {
            int removedIdx = Scanner.UnsupportedFiles.IndexOf(droppedData);
            int targetIdx = Scanner.UnsupportedFiles.IndexOf(target);

            if (removedIdx < targetIdx)
            {
                Scanner.UnsupportedFiles.Insert(targetIdx + 1, droppedData);
                Scanner.UnsupportedFiles.RemoveAt(removedIdx);
                return;
            }

            int remIdx = removedIdx + 1;
            if (Scanner.UnsupportedFiles.Count + 1 > remIdx)
            {
                Scanner.UnsupportedFiles.Insert(targetIdx, droppedData);
                Scanner.UnsupportedFiles.RemoveAt(remIdx);
            }
        }
    }

    private void StackPanel_GiveFeedback(object sender, System.Windows.GiveFeedbackEventArgs e)
    {
        if (e.Effects == DragDropEffects.Move)
        {
            if (DragCursor != null)
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
        if (e.Image != null)
        {
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
                evrak = evrak.MedianFilterBitmap(Settings.Default.MedianValue);
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
                evrak = evrak.InvertBitmap();
            }

            evrak.Freeze();
            BitmapFrame bitmapFrame = BitmapFrame.Create(evrak);
            bitmapFrame.Freeze();
            evrak = null;
            Scanner?.Resimler?.Add(new ScannedImage { Resim = bitmapFrame, RotationAngle = (double)SelectedRotation, FlipAngle = (double)SelectedFlip });
        }
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
            DecodeHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.ImgLoadResolution);
            SetCropPageResolution();
        }

        if (e.PropertyName is "SeekIndex" && SeekIndex >= 0 && SeekIndex < Scanner.Resimler.Count)
        {
            Lb.SelectedIndex = SeekIndex;
            Lb?.ScrollIntoView(Lb.Items[SeekIndex]);
        }

        if (e.PropertyName is "AllImageRotationAngle" && AllImageRotationAngle != 0)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                foreach (ScannedImage image in Scanner.Resimler.Where(z => z.Seçili))
                {
                    image.Resim = await image.Resim.FlipImageAsync(AllImageRotationAngle);
                }

                AllImageRotationAngle = 0;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                foreach (ScannedImage image in Scanner.Resimler.Where(z => z.Seçili))
                {
                    image.Resim = await image.Resim.RotateImageAsync(AllImageRotationAngle);
                }

                AllImageRotationAngle = 0;
                return;
            }

            foreach (ScannedImage image in Scanner.Resimler)
            {
                image.Resim = await image.Resim.RotateImageAsync(AllImageRotationAngle);
            }

            AllImageRotationAngle = 0;
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            try
            {
                twain = new Twain(new WindowMessageHook(Window.GetWindow(Parent)));
                Scanner.Tarayıcılar = twain.SourceNames;
                twain.TransferImage += Twain_TransferImage;
                twain.ScanningComplete += Twain_ScanningComplete;
                switch (Scanner?.Tarayıcılar?.Count)
                {
                    case 0:
                        Settings.Default.SeçiliTarayıcı = string.Empty;
                        return;

                    case 1:
                        Settings.Default.SeçiliTarayıcı = Scanner.Tarayıcılar[0];
                        break;
                }
            }
            catch (Exception)
            {
                Scanner.ArayüzEtkin = false;
            }
        }
    }

    private void ZipExtractSingleFile(string zipfileName, string zipcontentfilename, string destinationfilename)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipfileName);
        archive.Entries?.FirstOrDefault(z => z.FullName == zipcontentfilename)?.ExtractToFile(destinationfilename, true);
    }
}