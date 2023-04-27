using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.Xml.Linq;
using System.Xml.Serialization;
using Extensions;
using Extensions.Controls;
using Microsoft.Win32;
using Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TwainControl.Properties;
using TwainWpf;
using TwainWpf.TwainNative;
using TwainWpf.Wpf;
using UdfParser;
using static Extensions.ExtensionMethods;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TwainControl
{
    public partial class TwainCtrl : UserControl, INotifyPropertyChanged, IDisposable
    {
        public static DispatcherTimer CameraQrCodeTimer;

        public static Task Filesavetask;

        public TwainCtrl()
        {
            InitializeComponent();
            DataContext = this;
            Scanner = new Scanner();
            PdfGeneration.Scanner = Scanner;

            Scanner.PropertyChanged += Scanner_PropertyChanged;
            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += TwainCtrl_PropertyChanged;

            SelectedTab = TbCtrl?.Items[0] as TabItem;
            Papers = BitmapMethods.GetPapers();
            ToolBox.Paper = SelectedPaper = Papers.FirstOrDefault(z => z.PaperType == "A4");

            if (Settings.Default.UseSelectedProfile)
            {
                Scanner.SelectedProfile = Settings.Default.DefaultProfile;
            }

            ScanImage = new RelayCommand<object>(parameter =>
            {
                GC.Collect();
                ScanCommonSettings();
                twain.SelectSource(Scanner.SeçiliTarayıcı);
                twain.StartScanning(_settings);
            }, parameter => !Environment.Is64BitProcess && Scanner?.Tarayıcılar?.Count > 0);

            FastScanImage = new RelayCommand<object>(parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                    return;
                }
                GC.Collect();
                ScanCommonSettings();
                Scanner.Resimler = new ObservableCollection<ScannedImage>();
                twain.SelectSource(Scanner.SeçiliTarayıcı);
                twain.StartScanning(_settings);
                twain.ScanningComplete += Fastscan;
            }, parameter => !Environment.Is64BitProcess && Scanner?.AutoSave == true && !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Scanner?.Tarayıcılar?.Count > 0);

            ResimSil = new RelayCommand<object>(parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
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
                if (MessageBox.Show(Translation.GetResStringValue("REMOVESELECTED"), Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    RemoveSelectedImage(item);
                }
                void RemoveSelectedImage(ScannedImage item)
                {
                    _ = Scanner.Resimler?.Remove(item);
                    ToolBox.ResetCropMargin();
                    GC.Collect();
                }
            }, parameter => Scanner.ArayüzEtkin);

            ResimSilGeriAl = new RelayCommand<object>(parameter =>
            {
                Scanner.Resimler?.Insert((int)UndoImageIndex, UndoImage);
                CanUndoImage = false;
                UndoImage = null;
                UndoImageIndex = null;
                GC.Collect();
            }, parameter => CanUndoImage && UndoImage is not null);

            ExploreFile = new RelayCommand<object>(parameter => OpenFolderAndSelectItem(Path.GetDirectoryName(parameter as string), Path.GetFileName(parameter as string)), parameter => true);

            Kaydet = new RelayCommand<object>(parameter =>
            {
                if (parameter is BitmapFrame bitmapFrame)
                {
                    if (Filesavetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                        return;
                    }
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Tif Resmi (*.tif)|*.tif|Jpg Resmi (*.jpg)|*.jpg|Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps|Txt Dosyası (*.txt)|*.txt",
                        FileName = Scanner.SaveFileName,
                        FilterIndex = SaveIndex + 1,
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        Filesavetask = Task.Run(async () =>
                        {
                            switch (saveFileDialog.FilterIndex)
                            {
                                case 1:
                                    SaveTifImage(bitmapFrame, saveFileDialog.FileName);
                                    return;

                                case 2:
                                    SaveJpgImage(bitmapFrame, saveFileDialog.FileName);
                                    return;

                                case 3:
                                    await SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, SelectedPaper);
                                    return;

                                case 4:
                                    await SavePdfImage(bitmapFrame, saveFileDialog.FileName, Scanner, SelectedPaper, true);
                                    return;

                                case 5:
                                    SaveXpsImage(bitmapFrame, saveFileDialog.FileName);
                                    return;

                                case 6:
                                    await SaveTxtFile(bitmapFrame, saveFileDialog.FileName, Scanner);
                                    break;
                            }
                        });
                    }
                }
            }, parameter => !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            Tümünüİşaretle = new RelayCommand<object>(parameter =>
            {
                List<ScannedImage> resimler = Scanner.Resimler.ToList();
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    for (int i = 0; i < resimler.Count; i++)
                    {
                        if (i % 2 == 1)
                        {
                            ScannedImage item = resimler[i];
                            item.Seçili = true;
                        }
                    }
                    return;
                }
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    for (int i = 0; i < resimler.Count; i++)
                    {
                        if (i % 2 == 0)
                        {
                            ScannedImage item = resimler[i];
                            item.Seçili = true;
                        }
                    }
                    return;
                }
                foreach (ScannedImage item in resimler)
                {
                    item.Seçili = true;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            TümünüİşaretleDikey = new RelayCommand<object>(parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in Scanner.Resimler.ToList().Where(item => item.Resim.PixelWidth <= item.Resim.PixelHeight))
                {
                    item.Seçili = true;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            TümünüİşaretleYatay = new RelayCommand<object>(parameter =>
            {
                TümününİşaretiniKaldır.Execute(null);
                foreach (ScannedImage item in Scanner.Resimler.ToList().Where(item => item.Resim.PixelHeight < item.Resim.PixelWidth))
                {
                    item.Seçili = true;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            TümününİşaretiniKaldır = new RelayCommand<object>(parameter =>
            {
                SeçiliResim = null;
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = false;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            Tersiniİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.ToList())
                {
                    item.Seçili = !item.Seçili;
                }
            }, parameter => Scanner?.Resimler?.Count > 0);

            KayıtYoluBelirle = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new()
                {
                    Description = Translation.GetResStringValue("AUTOFOLDER"),
                    SelectedPath = Settings.Default.AutoFolder
                };
                string oldpath = Settings.Default.AutoFolder;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Settings.Default.AutoFolder = dialog.SelectedPath;
                    Scanner.LocalizedPath = GetDisplayName(dialog.SelectedPath);
                }
                if (!string.IsNullOrWhiteSpace(oldpath) && oldpath != Settings.Default.AutoFolder)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("AUTOFOLDERCHANGE"), Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }, parameter => true);

            Seçilikaydet = new RelayCommand<object>(parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                    return;
                }
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası (*.pdf)|*.pdf|Siyah Beyaz Pdf Dosyası (*.pdf)|*.pdf|Jpg Resmi (*.jpg)|*.jpg|Tif Resmi (*.tif)|*.tif|Txt Dosyası (*.txt)|*.txt",
                    FileName = Scanner.SaveFileName
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    Filesavetask = Task.Run(async () =>
                    {
                        List<ScannedImage> seçiliresimler = Scanner?.Resimler?.Where(z => z.Seçili).ToList();
                        switch (saveFileDialog.FilterIndex)
                        {
                            case 1:
                                await SavePdfImage(seçiliresimler, saveFileDialog.FileName, Scanner, SelectedPaper, false, (int)Settings.Default.ImgLoadResolution);
                                return;

                            case 2:
                                await SavePdfImage(seçiliresimler, saveFileDialog.FileName, Scanner, SelectedPaper, true, (int)Settings.Default.ImgLoadResolution);
                                return;

                            case 3:
                                await SaveJpgImage(seçiliresimler, saveFileDialog.FileName, Scanner);
                                return;

                            case 4:
                                await SaveTifImage(seçiliresimler, saveFileDialog.FileName, Scanner);
                                return;

                            case 5:
                                await SaveTxtFile(seçiliresimler, saveFileDialog.FileName, Scanner);
                                return;
                        }
                    }).ContinueWith((_) => Dispatcher.Invoke(() =>
                    {
                        if (Settings.Default.RemoveProcessedImage)
                        {
                            SeçiliListeTemizle.Execute(null);
                        }
                    }));
                }
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner?.Resimler.Count(z => z.Seçili) ?? 0;
                return !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.SeçiliResimSayısı > 0 && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            });

            SeçiliDirektPdfKaydet = new RelayCommand<object>(parameter =>
            {
                Filesavetask = Task.Run(async () =>
                {
                    List<ScannedImage> seçiliresimler = Scanner.Resimler.Where(z => z.Seçili).ToList();
                    if (Scanner.ApplyDataBaseOcr)
                    {
                        Scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
                        Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
                        for (int i = 0; i < seçiliresimler.Count; i++)
                        {
                            ScannedImage scannedimage = seçiliresimler[i];
                            byte[] imgdata = scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg);
                            ObservableCollection<OcrData> ocrdata = await imgdata.OcrAsyc(Scanner.SelectedTtsLanguage);
                            Dispatcher.Invoke(() =>
                            {
                                DataBaseQrData = imgdata;
                                DataBaseTextData = ocrdata;
                            });
                            ocrdata = null;
                            imgdata = null;
                            Scanner.PdfSaveProgressValue = i / (double)seçiliresimler.Count;
                        }
                    }
                    if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
                    {
                        await SavePdfImage(seçiliresimler, PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, true, (int)Settings.Default.ImgLoadResolution);
                    }
                    if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
                    {
                        await SavePdfImage(seçiliresimler, PdfGeneration.GetPdfScanPath(), Scanner, SelectedPaper, false, (int)Settings.Default.ImgLoadResolution);
                    }
                }).ContinueWith((_) => Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(Scanner.Resimler));
                    if (Settings.Default.RemoveProcessedImage)
                    {
                        SeçiliListeTemizle.Execute(null);
                    }
                }));
            }, parameter =>
            {
                Scanner.SeçiliResimSayısı = Scanner?.Resimler.Count(z => z.Seçili) ?? 0;
                return !string.IsNullOrWhiteSpace(Scanner?.FileName) && Scanner?.AutoSave == true && Scanner?.SeçiliResimSayısı > 0 && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            });

            ListeTemizle = new RelayCommand<object>(parameter =>
            {
                if (Filesavetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TASKSRUNNING"));
                    return;
                }
                if ((Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) && SeçiliListeTemizle.CanExecute(null))
                {
                    SeçiliListeTemizle.Execute(null);
                    return;
                }

                if (MessageBox.Show(Translation.GetResStringValue("LISTREMOVEWARN"), Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    Scanner.Resimler?.Clear();
                    UndoImage = null;
                    ToolBox.ResetCropMargin();
                    GC.Collect();
                }
            }, parameter => Scanner?.Resimler?.Count > 0 && Scanner.ArayüzEtkin);

            SeçiliListeTemizle = new RelayCommand<object>(parameter =>
            {
                foreach (ScannedImage item in Scanner.Resimler.Where(z => z.Seçili).ToList())
                {
                    _ = Scanner.Resimler?.Remove(item);
                }
                UndoImage = null;
                ToolBox.ResetCropMargin();
                GC.Collect();
            }, parameter => Scanner?.Resimler?.Any(z => z.Seçili) == true);

            ShowDateFolderHelp = new RelayCommand<object>(parameter =>
            {
                StringBuilder sb = new();
                foreach (string item in Scanner.FolderDateFormats)
                {
                    _ = sb.Append(item).Append(' ').AppendLine(DateTime.Today.ToString(item, TranslationSource.Instance.CurrentCulture));
                }
                _ = MessageBox.Show(sb.ToString(), Application.Current?.MainWindow?.Title);
            }, parameter => true);

            SaveProfile = new RelayCommand<object>(parameter =>
            {
                StringBuilder sb = new();
                string profile = sb
                    .Append(Scanner.ProfileName)
                    .Append("|")
                    .Append(Settings.Default.Çözünürlük)
                    .Append("|")
                    .Append(Settings.Default.Adf)
                    .Append("|")
                    .Append(Settings.Default.Mode)
                    .Append("|")
                    .Append(Scanner.Duplex)
                    .Append("|")
                    .Append(Scanner.ShowUi)
                    .Append("|")
                    .Append(false)//Scanner.SeperateSave
                    .Append("|")
                    .Append(Settings.Default.ShowFile)
                    .Append("|")
                    .Append(Scanner.DetectEmptyPage)
                    .Append("|")
                    .Append(Scanner.FileName)
                    .ToString();
                _ = Settings.Default.Profile.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
                Scanner.ProfileName = "";
            }, parameter => !string.IsNullOrWhiteSpace(Scanner?.ProfileName) && !Settings.Default.Profile.Cast<string>().Select(z => z.Split('|')[0]).Contains(Scanner?.ProfileName) && Scanner?.FileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Scanner?.ProfileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            RemoveProfile = new RelayCommand<object>(parameter =>
            {
                Settings.Default.Profile.Remove(parameter as string);
                Settings.Default.DefaultProfile = null;
                Settings.Default.UseSelectedProfile = false;
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => true);

            LoadCroppedImage = new RelayCommand<object>(parameter =>
            {
                Scanner.CroppedImage = SeçiliResim.Resim;
                Scanner.CroppedImage.Freeze();
                Scanner.CopyCroppedImage = Scanner.CroppedImage;
                Scanner.CroppedImage.Freeze();
            }, parameter => SeçiliResim is not null);

            InsertFileNamePlaceHolder = new RelayCommand<object>(parameter =>
            {
                string placeholder = parameter as string;
                Scanner.FileName = $"{Scanner.FileName.Substring(0, Scanner.CaretPosition)}{placeholder}{Scanner.FileName.Substring(Scanner.CaretPosition, Scanner.FileName.Length - Scanner.CaretPosition)}";
            }, parameter => true);

            WebAdreseGit = new RelayCommand<object>(parameter => GotoPage(parameter as string), parameter => true);

            OcrPage = new RelayCommand<object>(parameter => ImgData = Scanner.CroppedImage.ToTiffJpegByteArray(Format.Jpg), parameter => Scanner?.CroppedImage is not null);

            LoadImage = new RelayCommand<object>(parameter =>
            {
                if (fileloadtask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(Translation.GetResStringValue("TRANSLATEPENDING"));
                    return;
                }
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Tüm Dosyalar (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle;*.pdf;*.xps;*.eyp|Resim Dosyası (*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle)|*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.gif;*.tif;*.tiff;*.bmp;*.dib;*.rle|Pdf Dosyası (*.pdf)|*.pdf|Xps Dosyası (*.xps)|*.xps|Eyp Dosyası (*.eyp)|*.eyp",
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    GC.Collect();
                    AddFiles(openFileDialog.FileNames, DecodeHeight);
                    GC.Collect();
                }
            }, parameter => true);

            MaximizePdfControl = new RelayCommand<object>(parameter =>
            {
                if (SelectedTab?.Content is PdfImportViewerControl pdfImportViewerControl)
                {
                    Window maximizePdfWindow = new()
                    {
                        Owner = Application.Current.MainWindow,
                        Content = pdfImportViewerControl,
                        WindowState = WindowState.Maximized,
                        ShowInTaskbar = false,
                        Title = "GPSCANNER",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        DataContext = this
                    };
                    SelectedTab = TbCtrl?.Items[0] as TabItem;
                    _ = maximizePdfWindow.ShowDialog();

                    maximizePdfWindow.Closed += (s, e) =>
                    {
                        SelectedTab = TbCtrl?.Items[1] as TabItem;
                        SelectedTab.Content = pdfImportViewerControl;
                        maximizePdfWindow = null;
                    };
                }
            }, parameter => SelectedTab?.Content is PdfImportViewerControl);

            LoadSingleUdfFile = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Uyap Dokuman Formatı (*.udf)|*.udf",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true && parameter is XpsViewer xpsViewer)
                {
                    xpsViewer.XpsDataFilePath = LoadUdfFile(openFileDialog.FileName);
                }
            }, parameter => true);

            AddSinglePdfPage = new RelayCommand<object>(parameter =>
            {
                if (parameter is BitmapSource imageSource)
                {
                    BitmapSource bitmapSource = imageSource.Width < imageSource.Height ? imageSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) :
                    imageSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                    BitmapFrame bitmapFrame = BitmapFrame.Create(imageSource, bitmapSource.BitmapSourceToBitmap().ToBitmapImage(ImageFormat.Jpeg, Settings.Default.PreviewWidth));
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    Scanner?.Resimler.Add(scannedImage);
                    bitmapFrame = null;
                    GC.Collect();
                }
            }, parameter => parameter is BitmapSource);

            SendMail = new RelayCommand<object>(parameter =>
            {
                try
                {
                    Mail.Mail.SendMail(MailData);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(nameof(MailData), ex);
                }
            }, parameter => !string.IsNullOrWhiteSpace(MailData));

            SplitPdf = new RelayCommand<object>(parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string savefolder = ToolBox.CreateSaveFolder("SPLIT");
                    SplitPdfPageCount(pdfviewer.PdfFilePath, savefolder, PdfSplitCount);
                    WebAdreseGit.Execute(savefolder);
                    GC.Collect();
                }
            }, parameter => PdfSplitCount > 0);

            AddFromClipBoard = new RelayCommand<object>(parameter =>
            {
                System.Windows.Forms.IDataObject clipboardData = System.Windows.Forms.Clipboard.GetDataObject();
                if (clipboardData?.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap) == true)
                {
                    using Bitmap bitmap = (Bitmap)clipboardData.GetData(System.Windows.Forms.DataFormats.Bitmap);
                    BitmapSource image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (image != null)
                    {
                        BitmapFrame bitmapFrame = GenerateBitmapFrame(image, SelectedPaper);
                        bitmapFrame.Freeze();
                        ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                        Scanner?.Resimler.Add(scannedImage);
                        scannedImage = null;
                    }
                }
            }, parameter => true);

            SaveFileList = new RelayCommand<object>(parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Txt Dosyası (*.txt)|*.txt",
                    FileName = "Filedata.txt"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    using StreamWriter file = new(saveFileDialog.FileName);
                    foreach (ScannedImage image in Scanner?.Resimler?.GroupBy(z => z.FilePath).Select(z => z.FirstOrDefault()))
                    {
                        file.WriteLine(image.FilePath);
                    }
                }
            }, parameter => Scanner?.Resimler?.Count(z => !string.IsNullOrWhiteSpace(z.FilePath)) > 0);

            LoadFileList = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Txt Dosyası (*.txt)|*.txt",
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    GC.Collect();
                    AddFiles(File.ReadAllLines(openFileDialog.FileName), DecodeHeight);
                    GC.Collect();
                }
            }, parameter => true);

            EypPdfİçerikBirleştir = new RelayCommand<object>(async parameter =>
            {
                string[] files = Scanner.UnsupportedFiles.Where(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (files.Length > 0)
                {
                    await files.SavePdfFiles();
                }
            }, parameter => Scanner?.UnsupportedFiles?.Count(z => string.Equals(Path.GetExtension(z), ".pdf", StringComparison.OrdinalIgnoreCase)) > 1);

            EypPdfSeçiliDosyaSil = new RelayCommand<object>(parameter => Scanner?.UnsupportedFiles?.Remove(parameter as string), parameter => true);

            EypPdfDosyaEkle = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Pdf Dosyası (*.pdf)|*.pdf",
                    Multiselect = true
                };
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
            }, parameter => true);

            int cycleindex = 0;
            CycleSelectedDocuments = new RelayCommand<object>(async parameter =>
            {
                if (parameter is ListBox listBox)
                {
                    ScannedImage scannedImage = Scanner?.Resimler?.Where(z => z.Seçili).ElementAtOrDefault(cycleindex);
                    if (scannedImage is not null)
                    {
                        listBox.ScrollIntoView(scannedImage);
                        scannedImage.Animate = true;
                        cycleindex++;
                        if (cycleindex >= Scanner?.Resimler?.Count(z => z.Seçili))
                        {
                            cycleindex = 0;
                        }
                        await Task.Delay(900);
                        scannedImage.Animate = false;
                    }
                }
            }, parameter => Scanner?.Resimler?.Count(z => z.Seçili) > 0);

            PdfWaterMark = new RelayCommand<object>(parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    string oldpdfpath = pdfViewer.PdfFilePath;
                    using PdfDocument reader = PdfReader.Open(pdfViewer.PdfFilePath);
                    if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                    {
                        for (int i = 0; i < reader.PageCount; i++)
                        {
                            using PdfDocument listdocument = GenerateWatermarkedPdf(reader, i, PdfWatermarkFontAngle);
                            listdocument.Save(pdfViewer.PdfFilePath);
                        }
                        pdfViewer.PdfFilePath = null;
                        pdfViewer.PdfFilePath = oldpdfpath;
                        return;
                    }
                    using PdfDocument document = GenerateWatermarkedPdf(reader, pdfViewer.Sayfa - 1, PdfWatermarkFontAngle);
                    document.Save(pdfViewer.PdfFilePath);
                    pdfViewer.PdfFilePath = null;
                    pdfViewer.PdfFilePath = oldpdfpath;
                }
            }, parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath) && !string.IsNullOrWhiteSpace(PdfWaterMarkText));

            MergeSelectedImagesToPdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is object[] data && data[0] is TwainCtrl twainCtrl && data[1] is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    IEnumerable<ScannedImage> seçiliresimler = twainCtrl.Scanner.Resimler.Where(z => z.Seçili);
                    if (seçiliresimler.Any())
                    {
                        PdfDocument pdfDocument = null;
                        string temporarypdf = null;
                        string pdfFilePath = pdfviewer.PdfFilePath;
                        await Task.Run(async () =>
                        {
                            pdfDocument = await seçiliresimler.ToList().GeneratePdf(Format.Jpg, twainCtrl.SelectedPaper, Settings.Default.JpegQuality, null, (int)Settings.Default.Çözünürlük);
                            temporarypdf = $"{Path.GetTempPath()}{Guid.NewGuid()}.pdf";
                            pdfDocument.Save(temporarypdf);
                            string[] processedfiles = new string[] { temporarypdf, pdfFilePath };
                            processedfiles.MergePdf().Save(pdfFilePath);
                        });

                        NotifyPdfChange(pdfviewer, temporarypdf, pdfFilePath);
                        if (Settings.Default.RemoveProcessedImage)
                        {
                            twainCtrl.SeçiliListeTemizle.Execute(null);
                        }
                        GC.Collect();
                    }
                }
            }, parameter => true);

            ReadPdfTag = new RelayCommand<object>(parameter =>
            {
                if (parameter is string filepath && File.Exists(filepath))
                {
                    if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                    {
                        ExploreFile.Execute(filepath);
                        return;
                    }
                    using PdfDocument reader = PdfReader.Open(filepath, PdfDocumentOpenMode.ReadOnly);
                    StringBuilder stringBuilder = new();
                    _ = stringBuilder.AppendLine(filepath).
                    AppendFormat("PDF {0:#.#}", reader.Version / 10d).AppendLine().
                    AppendLine(reader.Info.Title).
                    Append(reader.PageCount).AppendLine().
                    AppendLine(reader.Info.Producer).
                    AppendLine(reader.Info.Keywords).
                    AppendLine(reader.Info.Creator).
                    AppendLine(reader.Info.Author).
                    Append(reader.Info.CreationDate).AppendLine().
                    Append(reader.Info.ModificationDate).AppendLine().
                    AppendFormat("{0:##.##} MB", reader.FileSize / 1048576d).AppendLine();
                    _ = MessageBox.Show(stringBuilder.ToString(), Application.Current?.MainWindow?.Title);
                }
            }, parameter => parameter is string filepath && File.Exists(filepath));

            AddAllFileToControlPanel = new RelayCommand<object>(async parameter =>
            {
                if (parameter is object[] data && data[0] is TwainCtrl twainCtrl && data[1] is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.LeftCtrl) && TbCtrl?.Items[1] is TabItem selectedtab)
                    {
                        ((PdfImportViewerControl)selectedtab.Content).PdfViewer.PdfFilePath = pdfviewer.PdfFilePath;
                        SelectedTab = selectedtab;
                        return;
                    }
                    if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                    {
                        twainCtrl.AddFiles(new string[] { pdfviewer.PdfFilePath }, twainCtrl.DecodeHeight);
                        GC.Collect();
                        return;
                    }
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    {
                        string savefilename = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                        await SaveFile(pdfviewer.PdfFilePath, savefilename, pdfviewer.Sayfa, pdfviewer.ToplamSayfa);
                        twainCtrl.AddFiles(new string[] { savefilename }, twainCtrl.DecodeHeight);
                        GC.Collect();
                        return;
                    }
                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(pdfviewer.PdfFilePath);
                    MemoryStream ms = await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, pdfviewer.Sayfa, (int)Settings.Default.ImgLoadResolution);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, twainCtrl.SelectedPaper, false);
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    twainCtrl.Scanner?.Resimler.Add(scannedImage);
                    filedata = null;
                    bitmapFrame = null;
                    scannedImage = null;
                    ms = null;
                    GC.Collect();
                }
            }, parameter => true);

            RotateSelectedPage = new RelayCommand<object>(parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string path = pdfviewer.PdfFilePath;
                    int currentpage = pdfviewer.Sayfa;
                    using PdfDocument inputDocument = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Import);
                    if ((Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftAlt)) || (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.RightAlt)))
                    {
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
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
                    SavePageRotated(path, inputDocument, (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) ? -90 : 90, pdfviewer.Sayfa - 1);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.Sayfa = currentpage;
                    pdfviewer.PdfFilePath = path;
                }
            }, parameter => parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath));

            ArrangePdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath) && MessageBox.Show($"{Translation.GetResStringValue("REPLACEPAGE")} {SayfaBaşlangıç}-{SayfaBitiş}", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    int start = SayfaBaşlangıç - 1;
                    int end = SayfaBitiş - 1;
                    await ArrangeFile(pdfviewer.PdfFilePath, pdfviewer.PdfFilePath, start, end);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                }
            }, parameter => SayfaBaşlangıç != SayfaBitiş);

            ClosePdfFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.Source = null;
                    SayfaBaşlangıç = 1;
                    SayfaBitiş = 1;
                }
            }, parameter => parameter is PdfViewer.PdfViewer pdfviewer && pdfviewer.PdfFilePath is not null);

            ReverseData = new RelayCommand<object>(parameter => Scanner.Resimler = new ObservableCollection<ScannedImage>(Scanner.Resimler.Reverse()), parameter => Scanner?.Resimler?.Count > 1);

            ReverseDataHorizontal = new RelayCommand<object>(parameter =>
            {
                int start = Scanner.Resimler.IndexOf(Scanner?.Resimler.FirstOrDefault(z => z.Seçili));
                int end = Scanner.Resimler.IndexOf(Scanner?.Resimler.LastOrDefault(z => z.Seçili));
                if (Scanner?.Resimler?.Count(z => z.Seçili) == end - start + 1)
                {
                    List<ScannedImage> scannedImages = Scanner.Resimler.ToList();
                    scannedImages.Reverse(start, end - start + 1);
                    Scanner.Resimler = new ObservableCollection<ScannedImage>(scannedImages);
                    scannedImages = null;
                }
            }, parameter =>
            {
                int start = Scanner?.Resimler?.IndexOf(Scanner?.Resimler?.Where(z => z.Seçili)?.FirstOrDefault()) ?? 0;
                int end = Scanner?.Resimler?.IndexOf(Scanner?.Resimler?.Where(z => z.Seçili)?.LastOrDefault()) ?? 0;
                return Scanner?.Resimler?.Count(z => z.Seçili) > 1 && Scanner?.Resimler?.Count(z => z.Seçili) == end - start + 1;
            });

            RemoveSelectedPage = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer && File.Exists(pdfviewer.PdfFilePath))
                {
                    string path = pdfviewer.PdfFilePath;
                    if (MessageBox.Show($"{Translation.GetResStringValue("PAGENUMBER")} {SayfaBaşlangıç}-{SayfaBitiş} {Translation.GetResStringValue("DELETE")}", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        await RemovePdfPage(path, SayfaBaşlangıç, SayfaBitiş);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = path;
                        pdfviewer.Sayfa = 1;
                        SayfaBaşlangıç = SayfaBitiş = 1;
                    }
                }
            }, parameter => parameter is PdfViewer.PdfViewer pdfviewer && pdfviewer.ToplamSayfa > 1 && SayfaBaşlangıç <= SayfaBitiş && (SayfaBitiş - SayfaBaşlangıç + 1) < pdfviewer.ToplamSayfa);

            ExtractPdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is string loadfilename && File.Exists(loadfilename))
                {
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                        FileName = $"{Path.GetFileNameWithoutExtension(loadfilename)} {Translation.GetResStringValue("PAGENUMBER")} {SayfaBaşlangıç}-{SayfaBitiş}.pdf"
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        string savefilename = saveFileDialog.FileName;
                        int start = SayfaBaşlangıç;
                        int end = SayfaBitiş;
                        await SaveFile(loadfilename, savefilename, start, end);
                    }
                }
            }, parameter => parameter is string loadfilename && File.Exists(loadfilename) && SayfaBaşlangıç <= SayfaBitiş);

            LoadPdfExtractFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath))
                {
                    PdfPages = new();
                    for (int i = 1; i <= pdfViewer.ToplamSayfa; i++)
                    {
                        PdfPages.Add(new PdfData() { PageNumber = i });
                    }
                }
            }, parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));

            ExtractMultiplePdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfViewer)
                {
                    string savefolder = ToolBox.CreateSaveFolder("SPLIT");
                    List<string> files = new();
                    foreach (PdfData currentpage in PdfPages.Where(currentpage => currentpage.Selected))
                    {
                        string savefilename = $"{savefolder}\\{Path.GetFileNameWithoutExtension(pdfViewer.PdfFilePath)} {currentpage.PageNumber}.pdf";
                        await SaveFile(pdfViewer.PdfFilePath, savefilename, currentpage.PageNumber, currentpage.PageNumber);
                        files.Add(savefilename);
                    }
                    if (MessageBox.Show($"{Translation.GetResStringValue("MERGEPDF")}", Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        using PdfDocument mergedPdf = files.ToArray().MergePdf();
                        mergedPdf.Save($"{savefolder}\\{Path.GetFileNameWithoutExtension(pdfViewer.PdfFilePath)} {Translation.GetResStringValue("MERGE")}.pdf");
                    }
                    WebAdreseGit.Execute(savefolder);
                    files = null;
                }
            }, parameter => PdfPages?.Any(z => z.Selected) == true);

            AddPageNumber = new RelayCommand<object>(parameter =>
            {
                if (parameter is PdfViewer.PdfViewer pdfviewer)
                {
                    string oldpdfpath = pdfviewer.PdfFilePath;
                    int currentpage = pdfviewer.Sayfa;
                    using PdfDocument document = PdfReader.Open(pdfviewer.PdfFilePath, PdfDocumentOpenMode.Modify);
                    if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                    {
                        for (int i = 0; i < document.PageCount; i++)
                        {
                            PdfPage pageall = document.Pages[i];
                            using XGraphics gfxall = XGraphics.FromPdfPage(pageall, XGraphicsPdfPageOptions.Append);
                            gfxall.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfAlignTextColor)), (i + 1).ToString(), PdfGeneration.GetPdfTextLayout(pageall)[0], PdfGeneration.GetPdfTextLayout(pageall)[1]);
                        }
                        document.Save(pdfviewer.PdfFilePath);
                        pdfviewer.PdfFilePath = null;
                        pdfviewer.PdfFilePath = oldpdfpath;
                        pdfviewer.Sayfa = 1;
                        return;
                    }
                    PdfPage page = document.Pages[pdfviewer.Sayfa - 1];
                    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    gfx.DrawText(new XSolidBrush(XColor.FromKnownColor(Scanner.PdfAlignTextColor)), pdfviewer.Sayfa.ToString(), PdfGeneration.GetPdfTextLayout(page)[0], PdfGeneration.GetPdfTextLayout(page)[1]);
                    document.Save(pdfviewer.PdfFilePath);
                    pdfviewer.PdfFilePath = null;
                    pdfviewer.PdfFilePath = oldpdfpath;
                    pdfviewer.Sayfa = currentpage;
                }
            }, parameter => parameter is PdfViewer.PdfViewer pdfViewer && File.Exists(pdfViewer.PdfFilePath));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand AddAllFileToControlPanel { get; }

        public ICommand AddFromClipBoard { get; }

        public ICommand AddPageNumber { get; }

        public ICommand AddSinglePdfPage { get; }

        public double AllImageRotationAngle {
            get => allImageRotationAngle;

            set {
                if (allImageRotationAngle != value)
                {
                    allImageRotationAngle = value;
                    OnPropertyChanged(nameof(AllImageRotationAngle));
                }
            }
        }

        public ICommand ArrangePdfFile { get; }

        public byte[] CameraQRCodeData {
            get => cameraQRCodeData;

            set {
                if (cameraQRCodeData != value)
                {
                    cameraQRCodeData = value;
                    OnPropertyChanged(nameof(CameraQRCodeData));
                }
            }
        }

        public bool CanUndoImage {
            get => canUndoImage; set {

                if (canUndoImage != value)
                {
                    canUndoImage = value;
                    OnPropertyChanged(nameof(CanUndoImage));
                }
            }
        }

        public ICommand ClosePdfFile { get; }

        public List<Tuple<string, int, double, bool, double>> CompressionProfiles => new()
            {
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("BW"), 0, (double) Resolution.Low, false, (double) Quality.Low),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Low, true, (double) Quality.Low),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("BW"), 0, (double) Resolution.Medium, false, (double) Quality.Medium),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Medium, true, (double) Quality.Medium),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("BW"), 0, (double) Resolution.Standard, false, (double) Quality.Standard),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Standard, true, (double) Quality.Standard),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("BW"), 0, (double) Resolution.High, false, (double) Quality.High),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.High, true, (double) Quality.High),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("BW"), 0, (double) Resolution.Ultra, false, (double) Quality.Ultra),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Ultra, true, (double) Quality.Ultra),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Low, false, (double) Quality.Low),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Medium, false, (double) Quality.Medium),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Standard, false, (double) Quality.Standard),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.High, false, (double) Quality.High),
                  new Tuple < string, int, double, bool, double > (Translation.GetResStringValue("COLOR"), 2, (double) Resolution.Ultra, false, (double) Quality.Ultra),
            };

        public CroppedBitmap CroppedOcrBitmap {
            get => croppedOcrBitmap;

            set {
                if (croppedOcrBitmap != value)
                {
                    croppedOcrBitmap = value;
                    OnPropertyChanged(nameof(CroppedOcrBitmap));
                }
            }
        }

        public ICommand CycleSelectedDocuments { get; }

        public byte[] DataBaseQrData {
            get => dataBaseQrData;

            set {
                if (dataBaseQrData != value)
                {
                    dataBaseQrData = value;
                    OnPropertyChanged(nameof(DataBaseQrData));
                }
            }
        }

        public ObservableCollection<OcrData> DataBaseTextData {
            get => dataBaseTextData; set {

                if (dataBaseTextData != value)
                {
                    dataBaseTextData = value;
                    OnPropertyChanged(nameof(DataBaseTextData));
                }
            }
        }

        public int DecodeHeight {
            get => decodeHeight;

            set {
                if (decodeHeight != value)
                {
                    decodeHeight = value;
                    OnPropertyChanged(nameof(DecodeHeight));
                }
            }
        }

        public GridLength DocumentGridLength {
            get => documentGridLength;

            set {
                if (documentGridLength != value)
                {
                    documentGridLength = value;
                    OnPropertyChanged(nameof(DocumentGridLength));
                }
            }
        }

        public bool DocumentPreviewIsExpanded {
            get => documentPreviewIsExpanded;

            set {
                if (documentPreviewIsExpanded != value)
                {
                    documentPreviewIsExpanded = value;
                    OnPropertyChanged(nameof(DocumentPreviewIsExpanded));
                }
            }
        }

        public bool DragMoveStarted {
            get => dragMoveStarted; set {

                if (dragMoveStarted != value)
                {
                    dragMoveStarted = value;
                    OnPropertyChanged(nameof(DragMoveStarted));
                }
            }
        }

        public ICommand ExploreFile { get; }

        public ICommand ExtractMultiplePdfFile { get; }

        public ICommand ExtractPdfFile { get; }

        public ICommand EypPdfDosyaEkle { get; }

        public ICommand EypPdfİçerikBirleştir { get; }

        public ICommand EypPdfSeçiliDosyaSil { get; }

        public ICommand FastScanImage { get; }

        public byte[] ImgData {
            get => ımgData;

            set {
                if (ımgData != value)
                {
                    ımgData = value;
                    OnPropertyChanged(nameof(ImgData));
                }
            }
        }

        public ICommand InsertFileNamePlaceHolder { get; }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand LoadCroppedImage { get; }

        public ICommand LoadFileList { get; }

        public ICommand LoadImage { get; }

        public ICommand LoadPdfExtractFile { get; }

        public ICommand LoadSingleUdfFile { get; }

        public string MailData {
            get => mailData;

            set {
                if (mailData != value)
                {
                    mailData = value;
                    OnPropertyChanged(nameof(MailData));
                }
            }
        }

        public ICommand MaximizePdfControl { get; }

        public ICommand MergeSelectedImagesToPdfFile { get; }

        public ICommand OcrPage { get; }

        public ObservableCollection<Paper> Papers {
            get => papers;

            set {
                if (papers != value)
                {
                    papers = value;
                    OnPropertyChanged(nameof(Papers));
                }
            }
        }

        public double PdfLoadProgressValue {
            get => pdfLoadProgressValue;

            set {
                if (pdfLoadProgressValue != value)
                {
                    pdfLoadProgressValue = value;
                    OnPropertyChanged(nameof(PdfLoadProgressValue));
                }
            }
        }

        public ObservableCollection<PdfData> PdfPages {
            get => pdfPages; set {

                if (pdfPages != value)
                {
                    pdfPages = value;
                    OnPropertyChanged(nameof(PdfPages));
                }
            }
        }

        public int PdfSplitCount {
            get => pdfSplitCount;

            set {
                if (pdfSplitCount != value)
                {
                    pdfSplitCount = value;
                    OnPropertyChanged(nameof(PdfSplitCount));
                }
            }
        }

        public ICommand PdfWaterMark { get; }

        public SolidColorBrush PdfWatermarkColor {
            get => pdfWatermarkColor; set {

                if (pdfWatermarkColor != value)
                {
                    pdfWatermarkColor = value;
                    OnPropertyChanged(nameof(PdfWatermarkColor));
                }
            }
        }

        public string PdfWatermarkFont {
            get => pdfWatermarkFont; set {

                if (pdfWatermarkFont != value)
                {
                    pdfWatermarkFont = value;
                    OnPropertyChanged(nameof(PdfWatermarkFont));
                }
            }
        }

        public double PdfWatermarkFontAngle {
            get => pdfWatermarkFontAngle; set {

                if (pdfWatermarkFontAngle != value)
                {
                    pdfWatermarkFontAngle = value;
                    OnPropertyChanged(nameof(PdfWatermarkFontAngle));
                }
            }
        }

        public double PdfWatermarkFontSize {
            get => pdfWatermarkFontSize;

            set {
                if (pdfWatermarkFontSize != value)
                {
                    pdfWatermarkFontSize = value;
                    OnPropertyChanged(nameof(PdfWatermarkFontSize));
                }
            }
        }

        public string PdfWaterMarkText {
            get => pdfWaterMarkText;

            set {
                if (pdfWaterMarkText != value)
                {
                    pdfWaterMarkText = value;
                    OnPropertyChanged(nameof(PdfWaterMarkText));
                }
            }
        }

        public ICommand ReadPdfTag { get; }

        public ICommand RemoveProfile { get; }

        public ICommand RemoveSelectedPage { get; }

        public ICommand ResimSil { get; }

        public ICommand ResimSilGeriAl { get; }

        public ICommand ReverseData { get; }

        public ICommand ReverseDataHorizontal { get; }

        public ICommand RotateSelectedPage { get; }

        public ICommand SaveFileList { get; }

        public int SaveIndex {
            get => saveIndex; set {

                if (saveIndex != value)
                {
                    saveIndex = value;
                    OnPropertyChanged(nameof(SaveIndex));
                }
            }
        }

        public ICommand SaveProfile { get; }

        public int SayfaBaşlangıç {
            get => sayfaBaşlangıç;

            set {
                if (sayfaBaşlangıç != value)
                {
                    sayfaBaşlangıç = value;
                    OnPropertyChanged(nameof(SayfaBaşlangıç));
                }
            }
        }

        public int SayfaBitiş {
            get => sayfaBitiş; set {

                if (sayfaBitiş != value)
                {
                    sayfaBitiş = value;
                    OnPropertyChanged(nameof(SayfaBitiş));
                }
            }
        }

        public ICommand ScanImage { get; }

        public Scanner Scanner {
            get => scanner;

            set {
                if (scanner != value)
                {
                    scanner = value;
                    OnPropertyChanged(nameof(Scanner));
                }
            }
        }

        public ICommand SeçiliDirektPdfKaydet { get; }

        public ICommand Seçilikaydet { get; }

        public ICommand SeçiliListeTemizle { get; }

        public ScannedImage SeçiliResim {
            get => seçiliResim;

            set {
                if (seçiliResim != value)
                {
                    seçiliResim = value;
                    OnPropertyChanged(nameof(SeçiliResim));
                }
            }
        }

        public Tuple<string, int, double, bool, double> SelectedCompressionProfile {
            get => selectedCompressionProfile;

            set {
                if (selectedCompressionProfile != value)
                {
                    selectedCompressionProfile = value;
                    OnPropertyChanged(nameof(SelectedCompressionProfile));
                }
            }
        }

        public TwainWpf.TwainNative.Orientation SelectedOrientation {
            get => selectedOrientation; set {

                if (selectedOrientation != value)
                {
                    selectedOrientation = value;
                    OnPropertyChanged(nameof(SelectedOrientation));
                }
            }
        }

        public Paper SelectedPaper {
            get => selectedPaper;

            set {
                if (selectedPaper != value)
                {
                    selectedPaper = value;
                    OnPropertyChanged(nameof(SelectedPaper));
                }
            }
        }

        public PageRotation SelectedRotation {
            get => selectedRotation; set {

                if (selectedRotation != value)
                {
                    selectedRotation = value;
                    OnPropertyChanged(nameof(SelectedRotation));
                }
            }
        }

        public TabItem SelectedTab {
            get => selectedTab; set {

                if (selectedTab != value)
                {
                    selectedTab = value;
                    OnPropertyChanged(nameof(SelectedTab));
                }
            }
        }

        public ICommand SendMail { get; }

        public ICommand ShowDateFolderHelp { get; }

        public ICommand SplitPdf { get; }

        public ICommand Tersiniİşaretle { get; }

        public ICommand Tümünüİşaretle { get; }

        public ICommand TümünüİşaretleDikey { get; }

        public ICommand TümünüİşaretleYatay { get; }

        public ICommand TümününİşaretiniKaldır { get; }

        public GridLength TwainGuiControlLength {
            get => twainGuiControlLength;

            set {
                if (twainGuiControlLength != value)
                {
                    twainGuiControlLength = value;
                    OnPropertyChanged(nameof(TwainGuiControlLength));
                }
            }
        }

        public ScannedImage UndoImage {
            get => undoImage; set {

                if (undoImage != value)
                {
                    undoImage = value;
                    OnPropertyChanged(nameof(UndoImage));
                }
            }
        }

        public int? UndoImageIndex {
            get => undoImageIndex;

            set {
                if (undoImageIndex != value)
                {
                    undoImageIndex = value;
                    OnPropertyChanged(nameof(UndoImageIndex));
                }
            }
        }

        public ICommand WebAdreseGit { get; }

        public static async Task ArrangeFile(string loadfilename, string savefilename, int start, int end)
        {
            await Task.Run(() =>
            {
                using PdfDocument outputDocument = loadfilename.ArrangePdfPages(start, end);
                outputDocument.DefaultPdfCompression();
                outputDocument.Save(savefilename);
            });
        }

        public static List<List<T>> ChunkBy<T>(List<T> source, int chunkSize)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        public static List<string> EypFileExtract(string eypfilepath)
        {
            using ZipArchive archive = ZipFile.Open(eypfilepath, ZipArchiveMode.Read);
            if (archive != null)
            {
                List<string> data = new();
                ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "NihaiOzet.xml");
                string source = Path.GetTempPath() + Guid.NewGuid() + ".xml";
                üstveri?.ExtractToFile(source, true);
                XDocument xdoc = XDocument.Load(source);
                if (xdoc != null)
                {
                    foreach (string file in xdoc.Descendants().Select(z => Path.GetFileName((string)z.Attribute("URI"))).Where(z => !string.IsNullOrEmpty(z)))
                    {
                        ZipArchiveEntry zipArchiveEntry = archive.Entries.FirstOrDefault(entry => entry.Name == file);
                        if (zipArchiveEntry != null)
                        {
                            string destinationFileName = Path.GetTempPath() + Guid.NewGuid() + Path.GetExtension(file.ToLower());
                            zipArchiveEntry.ExtractToFile(destinationFileName, true);
                            data.Add(destinationFileName);
                        }
                    }
                }
                return data;
            }
            return null;
        }

        public static BitmapFrame GenerateBitmapFrame(BitmapSource bitmapSource, Paper thumbnailpaper)
        {
            bitmapSource.Freeze();
            BitmapSource thumbnail = bitmapSource.PixelWidth < bitmapSource.PixelHeight ? bitmapSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / thumbnailpaper.Width * thumbnailpaper.Height) : bitmapSource.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / thumbnailpaper.Height * thumbnailpaper.Width);
            thumbnail.Freeze();
            BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapSource, thumbnail);
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
                    throw new ArgumentException(nameof(path), ex);
                }
            }
        }

        public static void NotifyPdfChange(PdfViewer.PdfViewer pdfviewer, string temporarypdf, string pdfFilePath)
        {
            File.Delete(temporarypdf);
            pdfviewer.PdfFilePath = null;
            pdfviewer.PdfFilePath = pdfFilePath;
        }

        public static async Task RemovePdfPage(string pdffilepath, int start, int end)
        {
            await Task.Run(() =>
            {
                PdfDocument inputDocument = PdfReader.Open(pdffilepath, PdfDocumentOpenMode.Import);
                for (int i = end; i >= start; i--)
                {
                    inputDocument.Pages.RemoveAt(i - 1);
                }
                inputDocument.Save(pdffilepath);
            });
        }

        public static void SaveJpgImage(BitmapFrame scannedImage, string filename)
        {
            File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Jpg, Settings.Default.JpegQuality));
        }

        public static async Task SaveJpgImage(List<ScannedImage> images, string filename, Scanner scanner)
        {
            await Task.Run(async () =>
            {
                string directory = Path.GetDirectoryName(filename);
                Uri uri = new("pack://application:,,,/TwainControl;component/Icons/okay.png", UriKind.Absolute);
                for (int i = 0; i < images.Count; i++)
                {
                    ScannedImage scannedimage = images[i];
                    File.WriteAllBytes(directory.SetUniqueFile(Path.GetFileNameWithoutExtension(filename), "jpg"), scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg, Settings.Default.JpegQuality));
                    scanner.PdfSaveProgressValue = i / (double)images.Count;
                    if (uri != null && Settings.Default.RemoveProcessedImage)
                    {
                        scannedimage.Resim = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(uri, 0);
                    }
                }
                scanner.PdfSaveProgressValue = 0;
                GC.Collect();
            });
        }

        public static async Task SavePdfImage(BitmapFrame scannedImage, string filename, Scanner scanner, Paper paper, bool blackwhite = false)
        {
            ObservableCollection<OcrData> ocrtext = null;
            if (scanner?.ApplyPdfSaveOcr == true && !string.IsNullOrEmpty(scanner?.SelectedTtsLanguage))
            {
                scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
                ocrtext = await scannedImage.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage);
            }
            scanner.SaveProgressBarForegroundBrush = Scanner.DefaultSaveProgressforegroundbrush;
            if (blackwhite)
            {
                scannedImage.GeneratePdf(ocrtext, Format.Tiff, paper, Settings.Default.JpegQuality, (int)Settings.Default.ImgLoadResolution).Save(filename);
                return;
            }
            scannedImage.GeneratePdf(ocrtext, Format.Jpg, paper, Settings.Default.JpegQuality, (int)Settings.Default.ImgLoadResolution).Save(filename);
        }

        public static async Task SavePdfImage(List<ScannedImage> images, string filename, Scanner scanner, Paper paper, bool blackwhite = false, int dpi = 120)
        {
            List<ObservableCollection<OcrData>> scannedtext = null;
            if (scanner?.ApplyPdfSaveOcr == true && !string.IsNullOrEmpty(scanner?.SelectedTtsLanguage))
            {
                scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
                scannedtext = new List<ObservableCollection<OcrData>>();
                scanner.ProgressState = TaskbarItemProgressState.Normal;
                for (int i = 0; i < images.Count; i++)
                {
                    ScannedImage image = images[i];
                    scannedtext.Add(await image.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage));
                    scanner.PdfSaveProgressValue = i / (double)images.Count;
                }
                scanner.PdfSaveProgressValue = 0;
            }
            scanner.SaveProgressBarForegroundBrush = Scanner.DefaultSaveProgressforegroundbrush;
            if (blackwhite)
            {
                (await images.GeneratePdf(Format.Tiff, paper, Settings.Default.JpegQuality, scannedtext, dpi)).Save(filename);
                return;
            }
          (await images.GeneratePdf(Format.Jpg, paper, Settings.Default.JpegQuality, scannedtext, dpi)).Save(filename);
        }

        public static async Task SaveTifImage(List<ScannedImage> images, string filename, Scanner scanner)
        {
            await Task.Run(() =>
            {
                TiffBitmapEncoder tifccittencoder = new() { Compression = TiffCompressOption.Ccitt4 };
                for (int i = 0; i < images.Count; i++)
                {
                    ScannedImage scannedimage = images[i];
                    tifccittencoder.Frames.Add(scannedimage.Resim);
                    scanner.PdfSaveProgressValue = i / (double)images.Count;
                }
                scanner.PdfSaveProgressValue = 0;
                GC.Collect();
                scanner.SaveProgressIndeterminate = true;
                using FileStream stream = new(filename, FileMode.Create);
                tifccittencoder.Save(stream);
                scanner.SaveProgressIndeterminate = false;
            });
        }

        public static void SaveTifImage(BitmapFrame scannedImage, string filename)
        {
            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.Tiff));
                return;
            }
            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
            {
                File.WriteAllBytes(filename, scannedImage.ToTiffJpegByteArray(Format.TiffRenkli));
            }
        }

        public static async Task SaveTxtFile(BitmapFrame bitmapFrame, string fileName, Scanner scanner)
        {
            if (bitmapFrame is not null && !string.IsNullOrEmpty(scanner.SelectedTtsLanguage))
            {
                ObservableCollection<OcrData> ocrtext = await bitmapFrame.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage);
                File.WriteAllText(fileName, string.Join(" ", ocrtext.Select(z => z.Text)));
            }
        }

        public static async Task SaveTxtFile(List<ScannedImage> images, string fileName, Scanner scanner)
        {
            if (images is not null && !string.IsNullOrEmpty(scanner.SelectedTtsLanguage))
            {
                for (int i = 0; i < images.Count; i++)
                {
                    ObservableCollection<OcrData> ocrtext = await images[i].Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(scanner.SelectedTtsLanguage);
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName) + i + ".txt"), string.Join(" ", ocrtext.Select(z => z.Text)));
                }
            }
        }

        public static void SaveXpsImage(BitmapFrame scannedImage, string filename)
        {
            Application.Current.Dispatcher.Invoke(() =>
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

        public void AddFiles(string[] filenames, int decodeheight)
        {
            fileloadtask = Task.Run(async () =>
            {
                try
                {
                    foreach (string filename in filenames)
                    {
                        switch (Path.GetExtension(filename.ToLower()))
                        {
                            case ".pdf":
                                {
                                    byte[] filedata = await PdfViewer.PdfViewer.ReadAllFileAsync(filename);
                                    if (filedata.IsValidPdfFile())
                                    {
                                        await AddPdfFile(filedata, filename);
                                    }
                                    filedata = null;
                                    break;
                                }
                            case ".eyp":
                                {
                                    List<string> files = EypFileExtract(filename);
                                    await Dispatcher.InvokeAsync(() => files.ForEach(z => Scanner?.UnsupportedFiles?.Add(z)));
                                    AddFiles(files.ToArray(), DecodeHeight);
                                    break;
                                }

                            case ".jpg":
                            case ".jpeg":
                            case ".jfif":
                            case ".jfıf":
                            case ".jpe":
                            case ".png":
                            case ".gif":
                            case ".gıf":
                            case ".bmp":
                                {
                                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrameAsync(new Uri(filename), decodeheight, Settings.Default.DefaultPictureResizeRatio);
                                    bitmapFrame.Freeze();
                                    ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
                                    await Dispatcher.InvokeAsync(() => Scanner?.Resimler.Add(img));
                                    img = null;
                                    bitmapFrame = null;
                                    break;
                                }

                            case ".tıf" or ".tiff" or ".tıff" or ".tif":
                                {
                                    TiffBitmapDecoder decoder = new(new Uri(filename), BitmapCreateOptions.None, BitmapCacheOption.None);
                                    for (int i = 0; i < decoder.Frames.Count; i++)
                                    {
                                        byte[] data = decoder.Frames[i].ToTiffJpegByteArray(Format.Jpg);
                                        MemoryStream ms = new(data);
                                        BitmapFrame image = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, SelectedPaper);
                                        image.Freeze();
                                        BitmapSource thumbimage = image.PixelWidth < image.PixelHeight ? image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) : image.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                                        thumbimage.Freeze();
                                        BitmapFrame bitmapFrame = BitmapFrame.Create(image, thumbimage);
                                        bitmapFrame.Freeze();
                                        ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
                                        Dispatcher.Invoke(() => Scanner?.Resimler.Add(img));
                                        img = null;
                                        bitmapFrame = null;
                                        image = null;
                                        data = null;
                                        ms = null;
                                    }
                                    break;
                                }
                            case ".xps":
                                {
                                    FixedDocumentSequence docSeq = null;
                                    DocumentPage docPage = null;
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        using XpsDocument xpsDoc = new(filename, FileAccess.Read);
                                        docSeq = xpsDoc.GetFixedDocumentSequence();
                                    });
                                    BitmapFrame bitmapframe = null;
                                    for (int i = 0; i < docSeq.DocumentPaginator.PageCount; i++)
                                    {
                                        await Dispatcher.InvokeAsync(() =>
                                        {
                                            docPage = docSeq.DocumentPaginator.GetPage(i);
                                            RenderTargetBitmap rtb = new((int)docPage.Size.Width, (int)docPage.Size.Height, 96, 96, PixelFormats.Default);
                                            rtb.Render(docPage.Visual);
                                            bitmapframe = BitmapFrame.Create(rtb);
                                            bitmapframe.Freeze();
                                        });
                                        BitmapSource thumbimage = bitmapframe.PixelWidth < bitmapframe.PixelHeight ? bitmapframe.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height) : bitmapframe.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Height * SelectedPaper.Width);
                                        thumbimage.Freeze();
                                        BitmapFrame bitmapFrame = BitmapFrame.Create(bitmapframe, thumbimage);
                                        bitmapFrame.Freeze();
                                        ScannedImage img = new() { Resim = bitmapFrame, FilePath = filename };
                                        await Dispatcher.InvokeAsync(() => Scanner?.Resimler.Add(img));
                                        img = null;
                                        bitmapFrame = null;
                                        thumbimage = null;
                                    }
                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    filenames = null;
                    throw new ArgumentException(nameof(filenames), ex);
                }
            });
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void DropFile(object sender, DragEventArgs e)
        {
            if (sender is Run run && e.Data.GetData(typeof(ScannedImage)) is ScannedImage droppedData && run.DataContext is ScannedImage target)
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

        public void DropPreviewFile(object sender, MouseEventArgs e)
        {
            if (sender is Run run && e.LeftButton == MouseButtonState.Pressed)
            {
                DragMoveStarted = true;
                _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
                DragMoveStarted = false;
            }
        }

        public async Task ListBoxDropFile(DragEventArgs e)
        {
            if (fileloadtask?.IsCompleted == false)
            {
                _ = MessageBox.Show(Application.Current.MainWindow, Translation.GetResStringValue("TRANSLATEPENDING"));
                return;
            }
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                await Task.Run(() => AddFiles(droppedfiles, DecodeHeight));
            }
        }

        public string LoadUdfFile(string filename)
        {
            ZipArchive archive = ZipFile.Open(filename, ZipArchiveMode.Read);
            ZipArchiveEntry üstveri = archive.Entries.FirstOrDefault(entry => entry.Name == "content.xml");
            string source = Path.GetTempPath() + Guid.NewGuid() + ".xml";
            string xpssource = Path.GetTempPath() + Guid.NewGuid() + ".xps";
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
                throw new ArgumentException(nameof(xmldatapath), ex);
            }
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

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private const double Inch = 2.54d;

        private static readonly SolidColorBrush bluesaveprogresscolor = System.Windows.Media.Brushes.DeepSkyBlue;

        private static readonly Rectangle selectionbox = new()
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 0, 0)),
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 0, 255, 0)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
        };

        private ScanSettings _settings;

        private double allImageRotationAngle;

        private byte[] cameraQRCodeData;

        private bool canUndoImage;

        private CroppedBitmap croppedOcrBitmap;

        private byte[] dataBaseQrData;

        private ObservableCollection<OcrData> dataBaseTextData;

        private int decodeHeight;

        private bool disposedValue;

        private GridLength documentGridLength = new(5, GridUnitType.Star);

        private bool documentPreviewIsExpanded = true;

        private bool dragMoveStarted;

        private Task fileloadtask;

        private double height;

        private byte[] ımgData;

        private bool isMouseDown;

        private bool isRightMouseDown;

        private string mailData;

        private System.Windows.Point mousedowncoord;

        private ObservableCollection<Paper> papers;

        private double pdfLoadProgressValue;

        private ObservableCollection<PdfData> pdfPages;

        private int pdfSplitCount = 0;

        private SolidColorBrush pdfWatermarkColor = System.Windows.Media.Brushes.Red;

        private string pdfWatermarkFont = "Arial";

        private double pdfWatermarkFontAngle = 315d;

        private double pdfWatermarkFontSize = 72d;

        private string pdfWaterMarkText;

        private int saveIndex = 2;

        private int sayfaBaşlangıç = 1;

        private int sayfaBitiş = 1;

        private Scanner scanner;

        private ScannedImage seçiliResim;

        private Tuple<string, int, double, bool, double> selectedCompressionProfile;

        private TwainWpf.TwainNative.Orientation selectedOrientation = TwainWpf.TwainNative.Orientation.Default;

        private Paper selectedPaper = new();

        private PageRotation selectedRotation = PageRotation.NONE;

        private TabItem selectedTab;

        private Twain twain;

        private GridLength twainGuiControlLength = new(3, GridUnitType.Star);

        private ScannedImage undoImage;

        private int? undoImageIndex;

        private double width;

        private static async Task SaveFile(string loadfilename, string savefilename, int start, int end)
        {
            await Task.Run(() =>
            {
                using PdfDocument outputDocument = loadfilename.ExtractPdfPages(start, end);
                outputDocument.DefaultPdfCompression();
                outputDocument.Save(savefilename);
            });
        }

        private async Task AddPdfFile(byte[] filedata, string filepath = null)
        {
            double totalpagecount = await PdfViewer.PdfViewer.PdfPageCountAsync(filedata);
            for (int i = 1; i <= totalpagecount; i++)
            {
                BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrame(await PdfViewer.PdfViewer.ConvertToImgStreamAsync(filedata, i, (int)Settings.Default.ImgLoadResolution), SelectedPaper, Scanner.Deskew);
                bitmapFrame.Freeze();
                await Dispatcher.InvokeAsync(() =>
                {
                    ScannedImage item = new() { Resim = bitmapFrame, FilePath = filepath };
                    Scanner?.Resimler.Add(item);
                    item = null;
                    PdfLoadProgressValue = i / totalpagecount;
                });
                bitmapFrame = null;
            }
            _ = await Dispatcher.InvokeAsync(() => PdfLoadProgressValue = 0);
            filedata = null;
            GC.Collect();
        }

        private void ButtonedTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Scanner.CaretPosition = (sender as ButtonedTextBox)?.CaretIndex ?? 0;
        }

        private async void CameraUserControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is CameraUserControl cameraUserControl)
            {
                if (e.PropertyName is "ResimData" && cameraUserControl.ResimData is not null)
                {
                    MemoryStream ms = new(cameraUserControl.ResimData);
                    BitmapFrame bitmapFrame = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, SelectedPaper);
                    bitmapFrame.Freeze();
                    Scanner.Resimler.Add(new ScannedImage() { Resim = bitmapFrame });
                    ms = null;
                }
                if (e.PropertyName is "DetectQRCode")
                {
                    if (cameraUserControl.DetectQRCode)
                    {
                        CameraQrCodeTimer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
                        CameraQrCodeTimer.Tick += (s, f2) =>
                        {
                            using MemoryStream ms = new();
                            cameraUserControl.EncodeBitmapImage(ms);
                            CameraQRCodeData = ms.ToArray();
                            OnPropertyChanged(nameof(CameraQRCodeData));
                        };
                        CameraQrCodeTimer?.Start();
                        return;
                    }
                    CameraQrCodeTimer?.Stop();
                }
            }
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
                Resolution = new ResolutionSettings() { Dpi = (int)Settings.Default.Çözünürlük, ColourSetting = ColourSetting.Colour },
                Page = new PageSettings() { Orientation = SelectedOrientation }
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
                ColourSetting.BlackAndWhite => bitmap.ConvertBlackAndWhite(Settings.Default.BwThreshold, false).ToBitmapImage(ImageFormat.Tiff, decodepixelheight),
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

        private async void Fastscan(object sender, ScanningCompleteEventArgs e)
        {
            Scanner.ArayüzEtkin = false;
            OnPropertyChanged(nameof(Scanner.DetectPageSeperator));
            Scanner.PdfFilePath = PdfGeneration.GetPdfScanPath();
            if (Scanner.ApplyDataBaseOcr)
            {
                Scanner.SaveProgressBarForegroundBrush = bluesaveprogresscolor;
                for (int i = 0; i < Scanner.Resimler.Count; i++)
                {
                    ScannedImage scannedimage = Scanner.Resimler[i];
                    DataBaseTextData = await scannedimage.Resim.ToTiffJpegByteArray(Format.Jpg).OcrAsyc(Scanner.SelectedTtsLanguage);
                    Scanner.PdfSaveProgressValue = i / (double)Scanner.Resimler.Count;
                }
            }
            if ((ColourSetting)Settings.Default.Mode == ColourSetting.BlackAndWhite)
            {
                (await Scanner.Resimler.ToList().GeneratePdf(Format.Tiff, SelectedPaper, Settings.Default.JpegQuality, null, (int)Settings.Default.Çözünürlük)).Save(Scanner.PdfFilePath);
            }
            if ((ColourSetting)Settings.Default.Mode is ColourSetting.Colour or ColourSetting.GreyScale)
            {
                (await Scanner.Resimler.ToList().GeneratePdf(Format.Jpg, SelectedPaper, Settings.Default.JpegQuality, null, (int)Settings.Default.Çözünürlük)).Save(Scanner.PdfFilePath);
            }
            if (Settings.Default.ShowFile)
            {
                ExploreFile.Execute(Scanner.PdfFilePath);
            }

            OnPropertyChanged(nameof(Scanner.Resimler));
            Scanner.Resimler.Clear();
            twain.ScanningComplete -= Fastscan;
            Scanner.ArayüzEtkin = true;
        }

        private PdfDocument GenerateWatermarkedPdf(PdfDocument pdfdocument, int sayfa, double rotation)
        {
            PdfPage page = pdfdocument.Pages[sayfa];
            XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            gfx.TranslateTransform(page.Width / 2, page.Height / 2);
            gfx.RotateTransform(rotation);
            gfx.TranslateTransform(-page.Width / 2, -page.Height / 2);
            XStringFormat format = new() { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Near };
            XBrush brush = new XSolidBrush(XColor.FromArgb(PdfWatermarkColor.Color.A, PdfWatermarkColor.Color.R, PdfWatermarkColor.Color.G, PdfWatermarkColor.Color.B));
            XFont font = new(PdfWatermarkFont, PdfWatermarkFontSize);
            XSize size = gfx.MeasureString(PdfWaterMarkText, font);
            gfx.DrawString(PdfWaterMarkText, font, brush, new XPoint((page.Width - size.Width) / 2, (page.Height - size.Height) / 2), format);
            return pdfdocument;
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TwainGuiControlLength = new(3, GridUnitType.Star);
            DocumentGridLength = new(5, GridUnitType.Star);
        }

        private void GridSplitter_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TwainGuiControlLength = new(1, GridUnitType.Star);
            DocumentGridLength = new(0, GridUnitType.Star);
        }

        private void ImgViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Image img && img.Parent is ScrollViewer scrollviewer)
            {
                if (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.LeftCtrl))
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

        private async void ImgViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Image img && img.Parent is ScrollViewer scrollviewer)
            {
                if (isRightMouseDown)
                {
                    System.Windows.Point mousemovecoord = (img.DesiredSize.Width < img.ActualWidth) ? e.GetPosition(img) : e.GetPosition(scrollviewer);
                    mousemovecoord.X += scrollviewer.HorizontalOffset;
                    mousemovecoord.Y += scrollviewer.VerticalOffset;
                    double widthmultiply = SeçiliResim.Resim.PixelWidth / (double)((img.DesiredSize.Width < img.ActualWidth) ? img.ActualWidth : img.DesiredSize.Width);
                    double heightmultiply = SeçiliResim.Resim.PixelHeight / (double)((img.DesiredSize.Height < img.ActualHeight) ? img.ActualHeight : img.DesiredSize.Height);

                    Int32Rect sourceRect = new((int)(mousemovecoord.X * widthmultiply), (int)(mousemovecoord.Y * heightmultiply), 1, 1);
                    if (sourceRect.X < SeçiliResim.Resim.PixelWidth && sourceRect.Y < SeçiliResim.Resim.PixelHeight)
                    {
                        CroppedBitmap croppedbitmap = new(SeçiliResim.Resim, sourceRect);
                        if (croppedbitmap != null)
                        {
                            byte[] pixels = new byte[4];
                            croppedbitmap.CopyPixels(pixels, 4, 0);
                            croppedbitmap.Freeze();
                            Scanner.SourceColor = System.Windows.Media.Color.FromRgb(pixels[2], pixels[1], pixels[0]).ToString();
                        }
                    }

                    if (e.RightButton == MouseButtonState.Released)
                    {
                        isRightMouseDown = false;
                        Cursor = Cursors.Arrow;
                    }
                }

                if (isMouseDown)
                {
                    System.Windows.Point mousemovecoord = e.GetPosition(scrollviewer);
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
                        width = Math.Abs(mousemovecoord.X - mousedowncoord.X);
                        height = Math.Abs(mousemovecoord.Y - mousedowncoord.Y);
                        double captureX, captureY;
                        captureX = mousedowncoord.X < mousemovecoord.X ? mousedowncoord.X : mousemovecoord.X;
                        captureY = mousedowncoord.Y < mousemovecoord.Y ? mousedowncoord.Y : mousemovecoord.Y;
                        ImgData = BitmapMethods.CaptureScreen(captureX, captureY, width, height, scrollviewer, BitmapFrame.Create((BitmapSource)img.Source));

                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            if (ImgData is not null)
                            {
                                MemoryStream ms = new(ImgData);
                                BitmapFrame bitmapframe = await BitmapMethods.GenerateImageDocumentBitmapFrame(ms, SelectedPaper);
                                bitmapframe.Freeze();
                                ScannedImage item = new() { Resim = bitmapframe };
                                Scanner.Resimler.Add(item);
                            }
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
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                double change = (double)(e.Delta > 0 ? .05 : -.05);
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

        private void LbEypContent_Drop(object sender, DragEventArgs e)
        {
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                foreach (string file in droppedfiles.Where(file => string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase)))
                {
                    Scanner?.UnsupportedFiles?.Add(file);
                }
            }
        }

        private async void ListBox_Drop(object sender, DragEventArgs e)
        {
            await ListBoxDropFile(e);
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                Settings.Default.PreviewWidth += e.Delta > 0 ? 10 : -10;
                if (Settings.Default.PreviewWidth <= 85)
                {
                    Settings.Default.PreviewWidth = 85;
                }
                if (Settings.Default.PreviewWidth >= 300)
                {
                    Settings.Default.PreviewWidth = 300;
                }
            }
        }

        private void Run_Drop(object sender, DragEventArgs e)
        {
            DropFile(sender, e);
        }

        private void Run_EypDrop(object sender, DragEventArgs e)
        {
            if (sender is Run run && e.Data.GetData(typeof(string)) is string droppedData && run.DataContext is string target)
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

        private void Run_EypPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Run run && e.LeftButton == MouseButtonState.Pressed)
            {
                _ = DragDrop.DoDragDrop(run, run.DataContext, DragDropEffects.Move);
            }
        }

        private void Run_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            DropPreviewFile(sender, e);
        }

        private void SavePageRotated(string savepath, PdfDocument inputDocument, int angle)
        {
            foreach (PdfPage page in inputDocument.Pages)
            {
                page.Rotate += angle;
            }
            inputDocument.Save(savepath);
        }

        private void SavePageRotated(string savepath, PdfDocument inputDocument, int angle, int pageindex)
        {
            inputDocument.Pages[pageindex].Rotate += angle;
            inputDocument.Save(savepath);
        }

        private void ScanCommonSettings()
        {
            Scanner.ArayüzEtkin = false;
            _settings = DefaultScanSettings();
            _settings.Rotation = new RotationSettings { AutomaticBorderDetection = true, AutomaticRotate = true, AutomaticDeskew = true };
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

        private void Twain_ScanningComplete(object sender, ScanningCompleteEventArgs e)
        {
            Scanner.ArayüzEtkin = true;
        }

        private void Twain_TransferImage(object sender, TransferImageEventArgs e)
        {
            if (e.Image != null)
            {
                using Bitmap bitmap = e.Image;
                if (Scanner.DetectEmptyPage && bitmap.IsEmptyPage(Settings.Default.EmptyThreshold))
                {
                    return;
                }
                int decodepixelheight = (int)(SelectedPaper.Height / Inch * Settings.Default.Çözünürlük);
                BitmapSource evrak = Scanner?.Resimler.Count % 2 == 0 ? EvrakOluştur(bitmap, (ColourSetting)Settings.Default.Mode, decodepixelheight) : Scanner?.PaperBackScan == true ? EvrakOluştur(bitmap, (ColourSetting)Settings.Default.BackMode, decodepixelheight) : EvrakOluştur(bitmap, (ColourSetting)Settings.Default.Mode, decodepixelheight);
                evrak.Freeze();
                BitmapSource önizleme = evrak.Resize(Settings.Default.PreviewWidth, Settings.Default.PreviewWidth / SelectedPaper.Width * SelectedPaper.Height);
                önizleme.Freeze();
                BitmapFrame bitmapFrame = BitmapFrame.Create(evrak, önizleme);
                bitmapFrame.Freeze();
                Scanner?.Resimler?.Add(new ScannedImage() { Resim = bitmapFrame, RotationAngle = (double)SelectedRotation });
                evrak = null;
                önizleme = null;
                bitmapFrame = null;
            }
        }

        private async void TwainCtrl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SelectedCompressionProfile" && SelectedCompressionProfile is not null)
            {
                Settings.Default.Mode = SelectedCompressionProfile.Item2;
                Settings.Default.Çözünürlük = SelectedCompressionProfile.Item3;
                Settings.Default.ImgLoadResolution = SelectedCompressionProfile.Item3;
                Settings.Default.JpegQuality = (int)SelectedCompressionProfile.Item5;
                Scanner.UseMozJpegEncoding = SelectedCompressionProfile.Item4 && MozJpeg.MozJpeg.MozJpegDllExists;
            }
            if (e.PropertyName is "SelectedPaper" && SelectedPaper is not null)
            {
                DecodeHeight = (int)(SelectedPaper.Height / Inch * Settings.Default.ImgLoadResolution);
                ToolBox.Paper = SelectedPaper;
            }
            if (e.PropertyName is "AllImageRotationAngle" && AllImageRotationAngle != 0)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    foreach (ScannedImage image in Scanner.Resimler.Where(z => z.Seçili).ToList())
                    {
                        image.Resim = await image.Resim.FlipImageAsync(AllImageRotationAngle);
                    }
                    AllImageRotationAngle = 0;
                    return;
                }
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    foreach (ScannedImage image in Scanner.Resimler.Where(z => z.Seçili).ToList())
                    {
                        image.Resim = await image.Resim.RotateImageAsync(AllImageRotationAngle);
                    }
                    AllImageRotationAngle = 0;
                    return;
                }
                foreach (ScannedImage image in Scanner.Resimler.ToList())
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
                    if (Scanner.Tarayıcılar?.Count > 0)
                    {
                        Scanner.SeçiliTarayıcı = Scanner.Tarayıcılar[0];
                    }
                    twain.TransferImage += Twain_TransferImage;
                    twain.ScanningComplete += Twain_ScanningComplete;
                }
                catch (Exception)
                {
                    Scanner.ArayüzEtkin = false;
                }
            }
        }
    }
}