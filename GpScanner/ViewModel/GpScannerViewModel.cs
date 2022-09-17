using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Extensions;
using GpScanner.Properties;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using TwainControl;
using static Extensions.ExtensionMethods;
using Twainsettings = TwainControl.Properties;

namespace GpScanner.ViewModel
{
    public class GpScannerViewModel : InpcBase
    {
        public static readonly string xmldatapath = Path.GetDirectoryName(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath) + @"\Data.xml";

        public GpScannerViewModel()
        {
            Dosyalar = GetScannerFileData();
            ChartData = GetChartsData();
            ScannerData = new ScannerData() { Data = DataYükle() };
            SeçiliGün = DateTime.Today;
            SeçiliDil = Settings.Default.DefaultLang;
            GenerateFoldTimer();

            TesseractViewModel = new TesseractViewModel();
            TranslateViewModel = new TranslateViewModel();

            ResetFilter = new RelayCommand<object>(parameter => MainWindow.cvs.View.Filter = null, parameter => MainWindow.cvs.View is not null);

            RegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Register(), parameter => true);

            UnRegisterSti = new RelayCommand<object>(parameter => StillImageHelper.Unregister(), parameter => true);

            PdfBirleştir = new RelayCommand<object>(parameter =>
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
                        TwainCtrl.MergePdf(Dosyalar.Where(z => z.Seçili && string.Equals(Path.GetExtension(z.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)).Select(z => z.FileName).ToArray()).Save(saveFileDialog.FileName);
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
                    byte[] imgdata = twainCtrl.SeçiliResim.Resim.ToTiffJpegByteArray(Format.Png);
                    _ = Ocr(imgdata);
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is TwainCtrl twainCtrl && twainCtrl.SeçiliResim is not null);

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
                if (parameter is object[] data && data[0] is not null)
                {
                    BitmapSource thumbnail = ((BitmapSource)data[0]).Resize(84, 117);
                    ScannedImage scannedImage = new() { Seçili = true, Resim = BitmapFrame.Create((BitmapSource)data[0], thumbnail) };
                    (data[1] as TwainCtrl)?.Scanner?.Resimler.Add(scannedImage);
                }
            }, parameter => true);

            ExtractPdfFile = new RelayCommand<object>(parameter =>
            {
                if (parameter is string filename)
                {
                    using PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Import);
                    using PdfDocument outputDocument = new();
                    for (int i = SayfaBaşlangıç - 1; i <= SayfaBitiş - 1; i++)
                    {
                        _ = outputDocument.AddPage(inputDocument.Pages[i]);
                    }
                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        TwainCtrl.DefaultPdfCompression(outputDocument);
                        outputDocument.Save(saveFileDialog.FileName);
                    }
                }
            }, parameter => SayfaBaşlangıç <= SayfaBitiş);

            SaveOcrPdf = new RelayCommand<object>(parameter =>
            {
                if (parameter is TwainCtrl twainCtrl)
                {
                    double dpiX = PresentationSource.FromVisual(twainCtrl).CompositionTarget.TransformToDevice.M11;
                    double dpiY = PresentationSource.FromVisual(twainCtrl).CompositionTarget.TransformToDevice.M22;
                    double ratio = SystemParameters.FullPrimaryScreenWidth / SystemParameters.FullPrimaryScreenHeight;

                    using PdfDocument document = new();
                    PdfPage page = document.AddPage();
                    using XGraphics gfx = XGraphics.FromPdfPage(page);
                    using MemoryStream ms = new(twainCtrl.SeçiliResim.Resim.ToTiffJpegByteArray(Format.Png));
                    using XImage xImage = XImage.FromStream(ms);
                    XSize size = PageSizeConverter.ToSize(PageSize.A4);
                    gfx.DrawImage(xImage, 0, 0, size.Width, size.Height);
                    TwainCtrl.DefaultPdfCompression(document);

                    XTextFormatter textformatter = new(gfx);
                    foreach (OcrData item in ScannedText)
                    {
                        XRect adjustedBounds = AdjustBounds(item.Rect, page.Width / twainCtrl.SeçiliResim.Resim.Width / ratio * dpiX, page.Height / twainCtrl.SeçiliResim.Resim.Height / ratio * dpiY);
                        int adjustedFontSize = CalculateFontSize(item.DisplayName, adjustedBounds, gfx);
                        XFont font = new("Segoe UI", adjustedFontSize, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
                        textformatter.DrawString(item.DisplayName, font, XBrushes.Transparent, adjustedBounds);
                    }

                    SaveFileDialog saveFileDialog = new()
                    {
                        Filter = "Pdf Dosyası(*.pdf)|*.pdf",
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        document.Save(saveFileDialog.FileName);
                    }
                }
            }, parameter => parameter is TwainCtrl twainCtrl && twainCtrl.SeçiliResim?.Resim is not null && ScannedText is not null);

            DatabaseSave = new RelayCommand<object>(parameter => ScannerData.Serialize());

            Settings.Default.PropertyChanged += Default_PropertyChanged;
            PropertyChanged += GpScannerViewModel_PropertyChanged;
            TranslateViewModel.PropertyChanged += TranslateViewModel_PropertyChanged;
            OnPropertyChanged(nameof(SeçiliDil));
        }

        public bool AddOcrToDataBase
        {
            get => addOcrToDataBase;

            set
            {
                if (addOcrToDataBase != value)
                {
                    addOcrToDataBase = value;
                    OnPropertyChanged(nameof(AddOcrToDataBase));
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

        public RelayCommand<object> DatabaseSave { get; }

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

        public RelayCommand<object> ExtractPdfFile { get; }

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

        public bool IsBusy
        {
            get => ısBusy;

            set
            {
                if (ısBusy != value)
                {
                    ısBusy = value;
                    OnPropertyChanged(nameof(IsBusy));
                }
            }
        }

        public ICommand OcrPage { get; }

        public ICommand PdfBirleştir { get; }

        public ICommand RegisterSti { get; }

        public ICommand ResetFilter { get; }

        public RelayCommand<object> SaveOcrPdf { get; }

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

        public bool ShowPdfPreview
        {
            get => showPdfPreview;

            set
            {
                if (showPdfPreview != value)
                {
                    showPdfPreview = value;
                    OnPropertyChanged(nameof(ShowPdfPreview));
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

        public RelayCommand<object> TransferImage { get; }

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
            if (File.Exists(xmldatapath))
            {
                return xmldatapath.DeSerialize<ScannerData>().Data;
            }
            _ = Directory.CreateDirectory(Path.GetDirectoryName(xmldatapath));
            return new ObservableCollection<Data>();
        }

        public ObservableCollection<Chart> GetChartsData()
        {
            try
            {
                ObservableCollection<Chart> list = new();
                foreach (IGrouping<int, Scanner> chart in Dosyalar.GroupBy(z => DateTime.Parse(Directory.GetParent(z.FileName).Name).Day).OrderBy(z => z.Key))
                {
                    list.Add(new Chart() { Description = chart.Key.ToString(), ChartBrush = RandomColor(), ChartValue = chart.Count() });
                }
                return list;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public ObservableCollection<Scanner> GetScannerFileData()
        {
            if (Directory.Exists(Twainsettings.Settings.Default.AutoFolder))
            {
                ObservableCollection<Scanner> list = new();
                foreach (string dosya in Directory.EnumerateFiles(Twainsettings.Settings.Default.AutoFolder, "*.*", SearchOption.AllDirectories).Where(s => (new string[] { ".pdf", ".tif", ".jpg", ".png", ".bmp", ".zip" }).Any(ext => ext == Path.GetExtension(s).ToLower())))
                {
                    list.Add(new Scanner() { FileName = dosya, Seçili = false });
                }
                return list;
            }
            return null;
        }

        public async Task<ObservableCollection<OcrData>> Ocr(byte[] imgdata)
        {
            if (imgdata is not null)
            {
                _ = await Task.Run(() =>
                {
                    ScannedText = null;
                    IsBusy = true;
                    ScannedText = imgdata.OcrYap(Settings.Default.DefaultTtsLang);
                    if (ScannedText != null)
                    {
                        IsBusy = false;
                        TranslateViewModel.Metin = string.Join(" ", ScannedText.Select(z => z.DisplayName));
                        if (!string.IsNullOrWhiteSpace(TranslateViewModel.Metin))
                        {
                            ScannedTextWindowOpen = true;
                        }
                    }
                    imgdata = null;
                    return ScannedText;
                }).ConfigureAwait(false);
            }
            return null;
        }

        private static DispatcherTimer timer;

        private bool addOcrToDataBase = true;

        private string aramaMetni;

        private XmlLanguage calendarLang;

        private ObservableCollection<Chart> chartData;

        private int? checkedPdfCount = 0;

        private ObservableCollection<Scanner> dosyalar;

        private double fold = 0.3;

        private bool ısBusy;

        private int sayfaBaşlangıç = 1;

        private int sayfaBitiş = 1;

        private ObservableCollection<OcrData> scannedText = new();

        private bool scannedTextWindowOpen;

        private string seçiliDil;

        private DateTime? seçiliGün;

        private Scanner selectedDocument;

        private bool showPdfPreview;

        private TesseractViewModel tesseractViewModel;

        private TranslateViewModel translateViewModel;

        private static XRect AdjustBounds(Tesseract.Rect b, double hAdjust, double vAdjust)
        {
            return new(b.X1 * hAdjust, b.Y1 * vAdjust, b.Width * hAdjust, b.Height * vAdjust);
        }

        private static int CalculateFontSize(string text, XRect adjustedBounds, XGraphics gfx)
        {
            int fontSizeGuess = Math.Max(1, (int)adjustedBounds.Height);
            XSize measuredBoundsForGuess = gfx.MeasureString(text, new XFont("Times New Roman", fontSizeGuess, XFontStyle.Regular));
            double adjustmentFactor = adjustedBounds.Width / measuredBoundsForGuess.Width;
            return Math.Max(1, (int)Math.Floor(fontSizeGuess * adjustmentFactor));
        }

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
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
                MainWindow.cvs.Filter += (s, x) =>
                {
                    Scanner scanner = x.Item as Scanner;
                    x.Accepted = Path.GetFileNameWithoutExtension(scanner?.FileName).Contains(AramaMetni, StringComparison.OrdinalIgnoreCase) ||
                    ScannerData.Data.Any(z => z.FileName == scanner.FileName && z.FileContent?.Contains(AramaMetni, StringComparison.OrdinalIgnoreCase) == true);
                };
            }

            if (e.PropertyName is "AddOcrToDataBase" && SelectedDocument?.Seçili == true)
            {
                ScannerData.Data.Add(new Data() { Id = DataSerialize.RandomNumber(), FileName = SelectedDocument?.FileName, FileContent = TranslateViewModel?.Metin });
                DatabaseSave.Execute(null);
                SelectedDocument.Seçili = false;
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

        private Brush RandomColor()
        {
            Random rand = new(Guid.NewGuid().GetHashCode());
            return new SolidColorBrush(Color.FromRgb((byte)rand.Next(0, 256), (byte)rand.Next(0, 256), (byte)rand.Next(0, 256)));
        }

        private void TranslateViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Metin")
            {
                OnPropertyChanged(nameof(AddOcrToDataBase));
            }
        }
    }
}