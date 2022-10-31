﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using Ocr;
using PdfSharp.Pdf;
using TwainControl;
using ZXing;
using static Extensions.ExtensionMethods;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class GpScannerViewModel : InpcBase
    {
        public GpScannerViewModel()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.DatabaseFile))
            {
                XmlDataPath = Settings.Default.DatabaseFile = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath) + @"\Data.xml";
                Settings.Default.Save();
            }
            PdfGeneration.Scanner.SelectedTtsLanguage = Settings.Default.DefaultTtsLang;

            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += GpScannerViewModel_PropertyChanged;

            GenerateFoldTimer();
            Dosyalar = GetScannerFileData();
            ChartData = GetChartsData();
            SeçiliDil = Settings.Default.DefaultLang;
            SeçiliGün = DateTime.Today;
            ScannerData = new ScannerData() { Data = DataYükle() };
            TesseractViewModel = new TesseractViewModel();
            TranslateViewModel = new TranslateViewModel();

            RegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Register(), parameter => true);

            UnRegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Unregister(), parameter => true);

            PdfBirleştir = new RelayCommand<object>(async parameter =>
            {
                SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                    FileName = Translation.GetResStringValue("MERGE")
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        await Task.Run(() => PdfGeneration.MergePdf(Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName).ToArray()).Save(saveFileDialog.FileName));
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }, parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase));
                return CheckedPdfCount > 1;
            });

            OcrPage = new RelayCommand<object>(parameter =>
            {
                if (parameter is TwainCtrl twainCtrl)
                {
                    byte[] imgdata = twainCtrl.SeçiliResim.Resim.ToTiffJpegByteArray(Format.Jpg);
                    _ = GetScannedTextAsync(imgdata);
                    imgdata = null;
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is TwainCtrl twainCtrl && twainCtrl.SeçiliResim is not null);

            OpenOriginalFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is string filepath)
                {
                    DocumentViewerWindow documentViewerWindow = new();
                    documentViewerWindow.pdfviewer.PdfFilePath = filepath;
                    documentViewerWindow.Show();
                    documentViewerWindow.Unloaded += (s, e) =>
                    {
                        documentViewerWindow.pdfviewer = null;
                        GC.Collect();
                    };
                }
            }, parameter => parameter is string filepath && File.Exists(filepath));

            ChangeDataFolder = new RelayCommand<object>(parameter =>
            {
                OpenFileDialog openFileDialog = new()
                {
                    Filter = "Xml Dosyası(*.xml)|*.xml",
                    FileName = "Data.xml"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    XmlDataPath = Settings.Default.DatabaseFile = openFileDialog.FileName;
                    Settings.Default.Save();
                }
            }, parameter => true);

            Tümünüİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (Scanner item in MainWindow.cvs.View.OfType<Scanner>().Where(z => Path.GetExtension(z.FileName.ToLower()) == ".pdf"))
                {
                    item.Seçili = true;
                }
            }, parameter => Dosyalar?.Count > 0);

            TümününİşaretiniKaldır = new RelayCommand<object>(parameter =>
            {
                foreach (Scanner item in MainWindow.cvs.View)
                {
                    item.Seçili = false;
                }
            }, parameter => Dosyalar?.Count > 0);

            Tersiniİşaretle = new RelayCommand<object>(parameter =>
            {
                foreach (Scanner item in MainWindow.cvs.View.OfType<Scanner>().Where(z => Path.GetExtension(z.FileName.ToLower()) == ".pdf"))
                {
                    item.Seçili = !item.Seçili;
                }
            }, parameter => Dosyalar?.Count > 0);

            TransferImage = new RelayCommand<object>(parameter =>
            {
                if (parameter is object[] data && data[0] is PdfViewer.PdfViewer pdfviewer && data[1] is TwainCtrl twainCtrl)
                {
                    BitmapFrame bitmapFrame = twainCtrl.GenerateBitmapFrame((BitmapSource)pdfviewer.Source);
                    bitmapFrame.Freeze();
                    ScannedImage scannedImage = new() { Seçili = false, Resim = bitmapFrame };
                    twainCtrl.Scanner?.Resimler.Add(scannedImage);
                }
            }, parameter => true);

            ExtractPdfFile = new RelayCommand<object>(async parameter =>
            {
                if (parameter is string filename)
                {
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                        FileName = $"{Path.GetFileNameWithoutExtension(filename)} {SayfaBaşlangıç}-{SayfaBitiş}.pdf"
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        await Task.Run(() =>
                        {
                            using PdfDocument outputDocument = PdfGeneration.ExtractPdfPages(filename, SayfaBaşlangıç, SayfaBitiş);
                            PdfGeneration.DefaultPdfCompression(outputDocument);
                            outputDocument.Save(saveFileDialog.FileName);
                        });
                    }
                }
            }, parameter => SayfaBaşlangıç <= SayfaBitiş);

            SavePatchProfile = new RelayCommand<object>(parameter =>
            {
                StringBuilder sb = new();
                string profile = sb
                    .Append(PatchFileName)
                    .Append("|")
                    .Append(PatchTag)
                    .ToString();
                _ = Settings.Default.PatchCodes.Add(profile);
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => !string.IsNullOrWhiteSpace(PatchFileName) && !string.IsNullOrWhiteSpace(PatchTag) && !Settings.Default.PatchCodes.Cast<string>().Select(z => z.Split('|')[1]).Contains(PatchTag) && PatchFileName?.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);

            RemovePatchProfile = new RelayCommand<object>(parameter =>
            {
                Settings.Default.PatchCodes.Remove(parameter as string);
                PatchProfileName = null;
                Settings.Default.Save();
                Settings.Default.Reload();
            }, parameter => true);

            BelgeİleBirleştir = new RelayCommand<object>(async parameter =>
            {
                if (parameter is TwainCtrl twainCtrl)
                {
                    try
                    {
                        string file = SelectedDocument.FileName;
                        string temporarypdf = Path.GetTempPath() + Guid.NewGuid() + ".pdf";
                        ObservableCollection<ScannedImage> scannedimages = twainCtrl.Scanner.Resimler;
                        await Task.Run(() =>
                        {
                            PdfGeneration.GeneratePdf(scannedimages.Where(z => z.Seçili), Format.Jpg, twainCtrl.SelectedPaper).Save(temporarypdf);
                            PdfGeneration.MergePdf(new string[] { temporarypdf, file }).Save(file);
                            File.Delete(temporarypdf);
                        });
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }, parameter =>
            {
                CheckedPdfCount = Dosyalar?.Count(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase));
                return CheckedPdfCount == 1 && parameter is TwainCtrl twainCtrl && twainCtrl.Scanner.Resimler.Count(z => z.Seçili) > 0;
            });

            ModifyGridWidth = new RelayCommand<object>(parameter =>
            {
                switch (parameter)
                {
                    case "0":
                        MainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
                        MainWindowGuiControlLength = new(2, GridUnitType.Star);
                        return;

                    case "1":
                        MainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);
                        MainWindowGuiControlLength = new(0, GridUnitType.Star);
                        return;
                }
            }, parameter => true);

            DatabaseSave = new RelayCommand<object>(parameter => ScannerData.Serialize());

            DateBack = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.Value.AddDays(-1), parameter => SeçiliGün > DateTime.MinValue);

            DateForward = new RelayCommand<object>(parameter => SeçiliGün = SeçiliGün.Value.AddDays(1), parameter => SeçiliGün < DateTime.Today);
        }

        public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;

        public static string XmlDataPath
        {
            get => xmlDataPath;

            set

            {
                if (xmlDataPath != value)
                {
                    xmlDataPath = value;
                    StaticPropertyChanged?.Invoke(null, new(nameof(XmlDataPath)));
                }
            }
        }

        public string AramaMetni
        {
            get => aramaMetni;

            set
            {
                if (aramaMetni != value)
                {
                    aramaMetni = value;
                    OnPropertyChanged(nameof(AramaMetni));
                }
            }
        }

        public string BarcodeContent
        {
            get => barcodeContent;

            set
            {
                if (barcodeContent != value)
                {
                    barcodeContent = value;
                    OnPropertyChanged(nameof(BarcodeContent));
                }
            }
        }

        public ObservableCollection<string> BarcodeList
        {
            get => barcodeList;

            set
            {
                if (barcodeList != value)
                {
                    barcodeList = value;
                    OnPropertyChanged(nameof(BarcodeList));
                }
            }
        }

        public ResultPoint[] BarcodePosition
        {
            get => barcodePosition; set

            {
                if (barcodePosition != value)
                {
                    barcodePosition = value;
                    OnPropertyChanged(nameof(BarcodePosition));
                }
            }
        }

        public ICommand BelgeİleBirleştir { get; }

        public XmlLanguage CalendarLang
        {
            get => calendarLang;

            set
            {
                if (calendarLang != value)
                {
                    calendarLang = value;
                    OnPropertyChanged(nameof(CalendarLang));
                }
            }
        }

        public ICommand ChangeDataFolder { get; }

        public ObservableCollection<Chart> ChartData
        {
            get => chartData;

            set
            {
                if (chartData != value)
                {
                    chartData = value;
                    OnPropertyChanged(nameof(ChartData));
                }
            }
        }

        public int? CheckedPdfCount
        {
            get => checkedPdfCount;

            set
            {
                if (checkedPdfCount != value)
                {
                    checkedPdfCount = value;
                    OnPropertyChanged(nameof(CheckedPdfCount));
                }
            }
        }

        public ICommand DatabaseSave { get; }

        public ICommand DateBack { get; }

        public ICommand DateForward { get; }

        public bool DetectBarCode
        {
            get => detectBarCode;

            set
            {
                if (detectBarCode != value)
                {
                    detectBarCode = value;
                    OnPropertyChanged(nameof(DetectBarCode));
                }
            }
        }

        public bool DetectPageSeperator
        {
            get => detectPageSeperator;

            set
            {
                if (detectPageSeperator != value)
                {
                    detectPageSeperator = value;
                    OnPropertyChanged(nameof(DetectPageSeperator));
                }
            }
        }

        public ObservableCollection<Scanner> Dosyalar
        {
            get => dosyalar;

            set
            {
                if (dosyalar != value)
                {
                    dosyalar = value;
                    OnPropertyChanged(nameof(Dosyalar));
                }
            }
        }

        public ICommand ExtractPdfFile { get; }

        public double Fold
        {
            get => fold;

            set
            {
                if (fold != value)
                {
                    fold = value;
                    OnPropertyChanged(nameof(Fold));
                }
            }
        }

        public ObservableCollection<Size> GetPreviewSize
        {
            get => new ObservableCollection<Size>
                {
                    new Size(240,315),
                    new Size(320,420),
                    new Size(426,560),
                };

            set => getPreviewSize = value;
        }

        public GridLength MainWindowDocumentGuiControlLength
        {
            get => mainWindowDocumentGuiControlLength; set

            {
                if (mainWindowDocumentGuiControlLength != value)
                {
                    mainWindowDocumentGuiControlLength = value;
                    OnPropertyChanged(nameof(MainWindowDocumentGuiControlLength));
                }
            }
        }

        public GridLength MainWindowGuiControlLength
        {
            get => mainWindowGuiControlLength; set

            {
                if (mainWindowGuiControlLength != value)
                {
                    mainWindowGuiControlLength = value;
                    OnPropertyChanged(nameof(MainWindowGuiControlLength));
                }
            }
        }

        public ICommand ModifyGridWidth { get; }

        public bool OcrIsBusy
        {
            get => ocrısBusy;

            set
            {
                if (ocrısBusy != value)
                {
                    ocrısBusy = value;
                    OnPropertyChanged(nameof(OcrIsBusy));
                }
            }
        }

        public ICommand OcrPage { get; }

        public ICommand OpenOriginalFile { get; }

        public string PatchFileName
        {
            get => patchFileName; set

            {
                if (patchFileName != value)
                {
                    patchFileName = value;
                    OnPropertyChanged(nameof(PatchFileName));
                }
            }
        }

        public string PatchProfileName
        {
            get => patchProfileName;

            set
            {
                if (patchProfileName != value)
                {
                    patchProfileName = value;
                    OnPropertyChanged(nameof(PatchProfileName));
                }
            }
        }

        public string PatchTag
        {
            get => patchTag;

            set
            {
                if (patchTag != value)
                {
                    patchTag = value;
                    OnPropertyChanged(nameof(PatchTag));
                }
            }
        }

        public ICommand PdfBirleştir { get; }

        public bool PdfOnlyText
        {
            get => pdfOnlyText;

            set
            {
                if (pdfOnlyText != value)
                {
                    pdfOnlyText = value;
                    OnPropertyChanged(nameof(PdfOnlyText));
                }
            }
        }

        public ICommand RegisterSti { get; }

        public ICommand RemovePatchProfile { get; }

        public ICommand SavePatchProfile { get; }

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
            get => sayfaBitiş; set

            {
                if (sayfaBitiş != value)
                {
                    sayfaBitiş = value;
                    OnPropertyChanged(nameof(SayfaBitiş));
                }
            }
        }

        public ObservableCollection<OcrData> ScannedText
        {
            get => scannedText;

            set
            {
                if (scannedText != value)
                {
                    scannedText = value;
                    OnPropertyChanged(nameof(ScannedText));
                }
            }
        }

        public bool ScannedTextWindowOpen
        {
            get => scannedTextWindowOpen;

            set
            {
                if (scannedTextWindowOpen != value)
                {
                    scannedTextWindowOpen = value;
                    OnPropertyChanged(nameof(ScannedTextWindowOpen));
                }
            }
        }

        public ScannerData ScannerData { get; set; }

        public string SeçiliDil
        {
            get => seçiliDil;

            set
            {
                if (seçiliDil != value)
                {
                    seçiliDil = value;
                    OnPropertyChanged(nameof(SeçiliDil));
                }
            }
        }

        public DateTime? SeçiliGün
        {
            get => seçiliGün; set

            {
                if (seçiliGün != value)
                {
                    seçiliGün = value;
                    OnPropertyChanged(nameof(SeçiliGün));
                }
            }
        }

        public Scanner SelectedDocument
        {
            get => selectedDocument;

            set
            {
                if (selectedDocument != value)
                {
                    selectedDocument = value;
                    OnPropertyChanged(nameof(SelectedDocument));
                }
            }
        }

        public Size SelectedSize
        {
            get => selectedSize;

            set
            {
                if (selectedSize != value)
                {
                    selectedSize = value;
                    OnPropertyChanged(nameof(SelectedSize));
                }
            }
        }

        public ICommand Tersiniİşaretle { get; }

        public TesseractViewModel TesseractViewModel
        {
            get => tesseractViewModel;

            set
            {
                if (tesseractViewModel != value)
                {
                    tesseractViewModel = value;
                    OnPropertyChanged(nameof(TesseractViewModel));
                }
            }
        }

        public ICommand TransferImage { get; }

        public TranslateViewModel TranslateViewModel
        {
            get => translateViewModel;

            set
            {
                if (translateViewModel != value)
                {
                    translateViewModel = value;
                    OnPropertyChanged(nameof(TranslateViewModel));
                }
            }
        }

        public ICommand Tümünüİşaretle { get; }

        public ICommand TümününİşaretiniKaldır { get; }

        public ICommand UnRegisterSti { get; }

        public static ObservableCollection<Data> DataYükle()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return null;
            }
            if (File.Exists(XmlDataPath))
            {
                return XmlDataPath.DeSerialize<ScannerData>().Data;
            }
            _ = Directory.CreateDirectory(Path.GetDirectoryName(XmlDataPath));
            return new ObservableCollection<Data>();
        }

        public ObservableCollection<Chart> GetChartsData()
        {
            ObservableCollection<Chart> list = new();
            try
            {
                foreach (IGrouping<int, Scanner> chart in Dosyalar.Where(z => DateTime.TryParse(Directory.GetParent(z.FileName).Name, out DateTime _)).GroupBy(z => DateTime.Parse(Directory.GetParent(z.FileName).Name).Day).OrderBy(z => z.Key))
                {
                    list.Add(new Chart() { Description = chart.Key.ToString(), ChartBrush = RandomColor(), ChartValue = chart.Count() });
                }
                return list;
            }
            catch (Exception)
            {
                return list;
            }
        }

        public Result GetImageBarcodeResult(byte[] imgbyte)
        {
            using MemoryStream ms = new(imgbyte);
            using System.Drawing.Bitmap bmp = new(ms);
            IBarcodeReader reader = new BarcodeReader();
            Result result = reader.Decode(bmp);
            imgbyte = null;
            return result;
        }

        public Result GetImageBarcodeResult(System.Drawing.Bitmap bitmap)
        {
            IBarcodeReader reader = new BarcodeReader();
            return reader.Decode(bitmap);
        }

        public string GetPatchCodeResult(string barcode)
        {
            IEnumerable<string> patchcodes = Settings.Default.PatchCodes.Cast<string>();
            return patchcodes.Any(z => z.Split('|')[1] == barcode)
                ? (patchcodes?.FirstOrDefault(z => z.Split('|')[1] == barcode)?.Split('|')[0])
                : "Tarama";
        }

        public async Task<ObservableCollection<OcrData>> GetScannedTextAsync(byte[] imgdata, bool opentextwindow = true)
        {
            if (imgdata is not null)
            {
                OcrIsBusy = true;
                ScannedTextWindowOpen = false;
                ScannedText = await imgdata.OcrAsyc(Settings.Default.DefaultTtsLang);
                if (ScannedText != null)
                {
                    TranslateViewModel.Metin = string.Join(" ", ScannedText.Select(z => z.Text));
                    OcrIsBusy = false;
                    ScannedTextWindowOpen = opentextwindow;
                }
                imgdata = null;
                return ScannedText;
            }
            return null;
        }

        public ObservableCollection<Scanner> GetScannerFileData()
        {
            if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
            {
                ObservableCollection<Scanner> list = new();
                try
                {
                    foreach (string dosya in Directory.EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*", SearchOption.AllDirectories).Where(s => (new string[] { ".pdf", ".tiff", ".tif", ".jpg", ".png", ".bmp", ".zip" }).Any(ext => ext == Path.GetExtension(s).ToLower())))
                    {
                        list.Add(new Scanner() { FileName = dosya, Seçili = false });
                    }
                    return list;
                }
                catch (UnauthorizedAccessException)
                {
                    return list;
                }
            }
            return null;
        }

        private static DispatcherTimer timer;

        private static string xmlDataPath = Settings.Default.DatabaseFile;

        private string aramaMetni;

        private string barcodeContent;

        private ObservableCollection<string> barcodeList = new();

        private ResultPoint[] barcodePosition;

        private XmlLanguage calendarLang;

        private ObservableCollection<Chart> chartData;

        private int? checkedPdfCount = 0;

        private bool detectBarCode;

        private bool detectPageSeperator;

        private ObservableCollection<Scanner> dosyalar;

        private double fold = 0.3;

        private ObservableCollection<Size> getPreviewSize;

        private GridLength mainWindowDocumentGuiControlLength = new(1, GridUnitType.Star);

        private GridLength mainWindowGuiControlLength = new(2, GridUnitType.Star);

        private bool ocrısBusy;

        private string patchFileName;

        private string patchProfileName = string.Empty;

        private string patchTag;

        private bool pdfOnlyText;

        private int sayfaBaşlangıç = 1;

        private int sayfaBitiş = 1;

        private ObservableCollection<OcrData> scannedText = new();

        private bool scannedTextWindowOpen;

        private string seçiliDil;

        private DateTime? seçiliGün;

        private Scanner selectedDocument;

        private Size selectedSize = new Size(240, 315);

        private TesseractViewModel tesseractViewModel;

        private TranslateViewModel translateViewModel;

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "DefaultTtsLang")
            {
                PdfGeneration.Scanner.SelectedTtsLanguage = Settings.Default.DefaultTtsLang;
            }
            Settings.Default.Save();
        }

        private void GenerateFoldTimer()
        {
            timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += OnTick;
            timer.Start();
        }

        private void GpScannerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SeçiliGün")
            {
                MainWindow.cvs.Filter += (s, x) => x.Accepted = Directory.GetParent((x.Item as Scanner)?.FileName).Name.StartsWith(SeçiliGün.Value.ToShortDateString());
            }
            if (e.PropertyName is "AramaMetni")
            {
                if (string.IsNullOrEmpty(AramaMetni))
                {
                    OnPropertyChanged(nameof(SeçiliGün));
                    return;
                }
                MainWindow.cvs.Filter += (s, x) => x.Accepted = ScannerData.Data.Any(z => z.FileName == (x.Item as Scanner)?.FileName && z.FileContent?.Contains(AramaMetni, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (e.PropertyName is "SeçiliDil")
            {
                switch (SeçiliDil)
                {
                    case "TÜRKÇE":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                        CalendarLang = XmlLanguage.GetLanguage("tr-TR");
                        break;

                    case "ENGLISH":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("en-EN");
                        CalendarLang = XmlLanguage.GetLanguage("en-EN");
                        break;

                    case "FRANÇAIS":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                        CalendarLang = XmlLanguage.GetLanguage("fr-FR");
                        break;

                    case "ITALIANO":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
                        CalendarLang = XmlLanguage.GetLanguage("it-IT");
                        break;

                    case "عربي":
                        TranslationSource.Instance.CurrentCulture = CultureInfo.GetCultureInfo("ar-AR");
                        CalendarLang = XmlLanguage.GetLanguage("ar-AR");
                        break;
                }
                Settings.Default.DefaultLang = SeçiliDil;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            Fold -= 0.01;
            if (Fold <= 0)
            {
                Fold = 0;
                timer.Stop();
                timer.Tick -= OnTick;
            }
        }
    }
}