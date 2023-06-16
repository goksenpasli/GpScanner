using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Media;
using System.Windows.Shell;
using Extensions;
using PdfSharp.Drawing;
using TwainControl.Properties;

namespace TwainControl;

public class Scanner : InpcBase, IDataErrorInfo
{
    public Scanner()
    {
        PropertyChanged += Scanner_PropertyChanged;
        Resimler.CollectionChanged -= Resimler_CollectionChanged;
        Resimler.CollectionChanged += Resimler_CollectionChanged;
    }

    public bool AllowCopy {
        get => allowCopy;

        set {
            if (allowCopy != value)
            {
                allowCopy = value;
                OnPropertyChanged(nameof(AllowCopy));
            }
        }
    }

    public bool AllowEdit {
        get => allowEdit;

        set {
            if (allowEdit != value)
            {
                allowEdit = value;
                OnPropertyChanged(nameof(AllowEdit));
            }
        }
    }

    public bool AllowPrint {
        get => allowPrint;

        set {
            if (allowPrint != value)
            {
                allowPrint = value;
                OnPropertyChanged(nameof(AllowPrint));
            }
        }
    }

    public bool ApplyDataBaseOcr {
        get => applyDataBaseOcr;

        set {
            if (applyDataBaseOcr != value)
            {
                applyDataBaseOcr = value;
                OnPropertyChanged(nameof(ApplyDataBaseOcr));
            }
        }
    }

    public bool ApplyMedian {
        get => applyMedian;

        set {
            if (applyMedian != value)
            {
                applyMedian = value;
                OnPropertyChanged(nameof(ApplyMedian));
            }
        }
    }

    public bool ApplyPdfSaveOcr {
        get => applyPdfSaveOcr;

        set {
            if (applyPdfSaveOcr != value)
            {
                applyPdfSaveOcr = value;
                OnPropertyChanged(nameof(ApplyPdfSaveOcr));
            }
        }
    }

    public bool ArayüzEtkin {
        get => arayüzetkin;

        set {
            if (arayüzetkin != value)
            {
                arayüzetkin = value;
                OnPropertyChanged(nameof(ArayüzEtkin));
            }
        }
    }

    public bool AutoSave {
        get => autoSave;

        set {
            if (autoSave != value)
            {
                autoSave = value;
                OnPropertyChanged(nameof(AutoSave));
            }
        }
    }

    public string BarcodeContent {
        get => barcodeContent;

        set {
            if (barcodeContent != value)
            {
                barcodeContent = value;
                OnPropertyChanged(nameof(BarcodeContent));
            }
        }
    }

    public bool BorderAnimation {
        get => borderAnimation;

        set {
            if (borderAnimation != value)
            {
                borderAnimation = value;
                OnPropertyChanged(nameof(BorderAnimation));
            }
        }
    }

    public int BoyAdet {
        get => boyAdet;

        set {
            if (boyAdet != value)
            {
                boyAdet = value;
                OnPropertyChanged(nameof(BoyAdet));
            }
        }
    }

    public double Brightness {
        get => brightness;

        set {
            if (brightness != value)
            {
                brightness = value;
                OnPropertyChanged(nameof(Brightness));
            }
        }
    }

    public int CaretPosition {
        get => caretPosition;

        set {
            if (caretPosition != value)
            {
                caretPosition = value;
                OnPropertyChanged(nameof(CaretPosition));
            }
        }
    }

    public ObservableCollection<Chart> Chart {
        get => chart;

        set {
            if (chart != value)
            {
                chart = value;
                OnPropertyChanged(nameof(Chart));
            }
        }
    }

    public ImageSource CopyCroppedImage {
        get => copyCroppedImage;

        set {
            if (copyCroppedImage != value)
            {
                copyCroppedImage = value;
                OnPropertyChanged(nameof(CopyCroppedImage));
            }
        }
    }

    public string CreatorAppName {
        get => creatorAppName;

        set {
            if (creatorAppName != value)
            {
                creatorAppName = value;
                OnPropertyChanged(nameof(CreatorAppName));
            }
        }
    }

    public double CropBottom {
        get => cropBottom;

        set {
            if (cropBottom != value)
            {
                cropBottom = value;
                OnPropertyChanged(nameof(CropBottom));
            }
        }
    }

    public bool CropDialogExpanded {
        get => cropDialogExpanded;

        set {
            if (cropDialogExpanded != value)
            {
                cropDialogExpanded = value;
                OnPropertyChanged(nameof(CropDialogExpanded));
            }
        }
    }

    public double CropLeft {
        get => cropLeft;

        set {
            if (cropLeft != value)
            {
                cropLeft = value;
                OnPropertyChanged(nameof(CropLeft));
            }
        }
    }

    public ImageSource CroppedImage {
        get => croppedImage;

        set {
            if (croppedImage != value)
            {
                croppedImage = value;
                OnPropertyChanged(nameof(CroppedImage));
            }
        }
    }

    public double CroppedImageAngle {
        get => croppedImageAngle;

        set {
            if (croppedImageAngle != value)
            {
                croppedImageAngle = value;
                OnPropertyChanged(nameof(CroppedImageAngle));
            }
        }
    }

    public double CropRight {
        get => cropRight;

        set {
            if (cropRight != value)
            {
                cropRight = value;
                OnPropertyChanged(nameof(CropRight));
            }
        }
    }

    public double CropTop {
        get => cropTop;

        set {
            if (cropTop != value)
            {
                cropTop = value;
                OnPropertyChanged(nameof(CropTop));
            }
        }
    }

    public bool Deskew {
        get => deskew;

        set {
            if (deskew != value)
            {
                deskew = value;
                OnPropertyChanged(nameof(Deskew));
            }
        }
    }

    public bool DetectEmptyPage {
        get => detectEmptyPage;

        set {
            if (detectEmptyPage != value)
            {
                detectEmptyPage = value;
                OnPropertyChanged(nameof(DetectEmptyPage));
            }
        }
    }

    public bool DetectPageSeperator {
        get => detectPageSeperator;

        set {
            if (detectPageSeperator != value)
            {
                detectPageSeperator = value;
                OnPropertyChanged(nameof(DetectPageSeperator));
            }
        }
    }

    public bool Duplex {
        get => duplex;

        set {
            if (duplex != value)
            {
                duplex = value;
                OnPropertyChanged(nameof(Duplex));
            }
        }
    }

    public int EnAdet {
        get => enAdet;

        set {
            if (enAdet != value)
            {
                enAdet = value;
                OnPropertyChanged(nameof(EnAdet));
            }
        }
    }

    public string Error => string.Empty;

    public bool FileIsPdfFile {
        get => string.Equals(Path.GetExtension(FileName), ".pdf", StringComparison.OrdinalIgnoreCase);

        set {
            if (fileisPdfFile != value)
            {
                fileisPdfFile = value;
                OnPropertyChanged(nameof(FileIsPdfFile));
            }
        }
    }

    public string FileName {
        get => fileName;

        set {
            if (fileName != value)
            {
                fileName = value;
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(SaveFileName));
            }
        }
    }

    public string FileOcrContent {
        get => fileOcrContent;

        set {
            if (fileOcrContent != value)
            {
                fileOcrContent = value;
                OnPropertyChanged(nameof(FileOcrContent));
            }
        }
    }

    public string[] FolderDateFormats { get; set; } = { "d.MM.yyyy", "dd.MM.yyyy", "d-MM-yyyy", "dd-MM-yyyy", "dddd", "MMMM", "yyyy" };

    public int FtpLoadProgressValue {
        get => ftpLoadProgressValue;

        set {
            if (ftpLoadProgressValue != value)
            {
                ftpLoadProgressValue = value;
                OnPropertyChanged(nameof(FtpLoadProgressValue));
            }
        }
    }

    public bool InvertImage {
        get => ınvertImage;

        set {
            if (ınvertImage != value)
            {
                ınvertImage = value;
                OnPropertyChanged(nameof(InvertImage));
            }
        }
    }

    public PdfPageLayout Layout {
        get => layout;

        set {
            if (layout != value)
            {
                layout = value;
                OnPropertyChanged(nameof(Layout));
            }
        }
    }

    public string LocalizedPath {
        get => ExtensionMethods.GetDisplayName(Settings.Default.AutoFolder);

        set {
            if (localizedPath != value)
            {
                localizedPath = value;
                OnPropertyChanged(nameof(LocalizedPath));
            }
        }
    }

    public int MedianValue {
        get => medianValue;

        set {
            if (medianValue != value)
            {
                medianValue = value;
                OnPropertyChanged(nameof(MedianValue));
            }
        }
    }

    public bool PaperBackScan {
        get => paperBackScan;

        set {
            if (paperBackScan != value)
            {
                paperBackScan = value;
                OnPropertyChanged(nameof(PaperBackScan));
            }
        }
    }

    public bool PasswordProtect {
        get => passwordProtect;

        set {
            if (passwordProtect != value)
            {
                passwordProtect = value;
                OnPropertyChanged(nameof(PasswordProtect));
            }
        }
    }

    public XKnownColor PdfAlignTextColor {
        get => pdfAlignTextColor;

        set {
            if (pdfAlignTextColor != value)
            {
                pdfAlignTextColor = value;
                OnPropertyChanged(nameof(PdfAlignTextColor));
            }
        }
    }

    public string PdfFilePath {
        get => pdfFilePath;

        set {
            if (pdfFilePath != value)
            {
                pdfFilePath = value;
                OnPropertyChanged(nameof(PdfFilePath));
            }
        }
    }

    public bool PdfPageNumberDraw {
        get => pdfPageNumberDraw;

        set {
            if (pdfPageNumberDraw != value)
            {
                pdfPageNumberDraw = value;
                OnPropertyChanged(nameof(PdfPageNumberDraw));
            }
        }
    }

    public SecureString PdfPassword {
        get => pdfPassword;

        set {
            if (pdfPassword != value)
            {
                pdfPassword = value;
                OnPropertyChanged(nameof(PdfPassword));
            }
        }
    }

    public double PdfSaveProgressValue {
        get => pdfSaveProgressValue;

        set {
            if (pdfSaveProgressValue != value)
            {
                pdfSaveProgressValue = value;
                OnPropertyChanged(nameof(PdfSaveProgressValue));
            }
        }
    }

    public string ProfileName {
        get => profileName;

        set {
            if (profileName != value)
            {
                profileName = value;
                OnPropertyChanged(nameof(ProfileName));
            }
        }
    }

    public TaskbarItemProgressState ProgressState {
        get => progressState;

        set {
            if (progressState != value)
            {
                progressState = value;
                OnPropertyChanged(nameof(ProgressState));
            }
        }
    }

    public IEnumerable<string> QrData {
        get => qrData;

        set {
            if (qrData != value)
            {
                qrData = value;
                OnPropertyChanged(nameof(QrData));
            }
        }
    }

    public ObservableCollection<ScannedImage> Resimler {
        get => resimler;

        set {
            if (resimler != value)
            {
                resimler = value;
                OnPropertyChanged(nameof(Resimler));
            }
        }
    }

    public double RotateAngle {
        get => rotateAngle;

        set {
            if (rotateAngle != value)
            {
                rotateAngle = value;
                OnPropertyChanged(nameof(RotateAngle));
            }
        }
    }

    public string SaveFileName {
        get {
            saveFileName = new[] { "[", "]" }.Any(FileName.Contains)
                ? FileName.Replace("[DATE]", DateTime.Now.Day.ToString())
                    .Replace("[MONTH]", DateTime.Now.Month.ToString())
                    .Replace("[YEAR]", DateTime.Now.Year.ToString())
                    .Replace("[HOUR]", DateTime.Now.Hour.ToString())
                    .Replace("[MINUTE]", DateTime.Now.Minute.ToString())
                    .Replace("[SECOND]", DateTime.Now.Second.ToString())
                    .Replace("[GUID]", Guid.NewGuid().ToString())
                    .Replace("[USERNAME]", Environment.UserName)
                : FileName;
            return saveFileName;
        }

        set {
            if (saveFileName != value)
            {
                saveFileName = value;
                OnPropertyChanged(nameof(SaveFileName));
            }
        }
    }

    public Brush SaveProgressBarForegroundBrush {
        get => saveProgressBarForegroundBrush;

        set {
            if (saveProgressBarForegroundBrush != value)
            {
                saveProgressBarForegroundBrush = value;
                OnPropertyChanged(nameof(SaveProgressBarForegroundBrush));
            }
        }
    }

    public bool SaveProgressIndeterminate {
        get => saveProgressIndeterminate;

        set {
            if (saveProgressIndeterminate != value)
            {
                saveProgressIndeterminate = value;
                OnPropertyChanged(nameof(SaveProgressIndeterminate));
            }
        }
    }

    public bool Seçili {
        get => seçili;

        set {
            if (seçili != value)
            {
                seçili = value;
                OnPropertyChanged(nameof(Seçili));
            }
        }
    }

    public int SeçiliResimSayısı {
        get => seçiliResimSayısı;

        set {
            if (seçiliResimSayısı != value)
            {
                seçiliResimSayısı = value;
                OnPropertyChanged(nameof(SeçiliResimSayısı));
            }
        }
    }

    public string SeçiliTarayıcı {
        get => seçiliTarayıcı;

        set {
            if (seçiliTarayıcı != value)
            {
                seçiliTarayıcı = value;
                OnPropertyChanged(nameof(SeçiliTarayıcı));
            }
        }
    }

    public string SelectedProfile {
        get => selectedProfile;

        set {
            if (selectedProfile != value)
            {
                selectedProfile = value;
                OnPropertyChanged(nameof(SelectedProfile));
            }
        }
    }

    public string SelectedTtsLanguage {
        get => selectedTtsLanguage;

        set {
            if (selectedTtsLanguage != value)
            {
                selectedTtsLanguage = value;
                OnPropertyChanged(nameof(SelectedTtsLanguage));
            }
        }
    }

    public bool ShowProgress {
        get => showProgress;

        set {
            if (showProgress != value)
            {
                showProgress = value;
                OnPropertyChanged(nameof(ShowProgress));
            }
        }
    }

    public bool ShowUi {
        get => showUi;

        set {
            if (showUi != value)
            {
                showUi = value;
                OnPropertyChanged(nameof(ShowUi));
            }
        }
    }

    public double SliceCountHeight {
        get => sliceCountHeight;

        set {
            if (sliceCountHeight != value)
            {
                sliceCountHeight = value;
                OnPropertyChanged(nameof(SliceCountHeight));
            }
        }
    }

    public double SliceCountWidth {
        get => sliceCountWidth;

        set {
            if (sliceCountWidth != value)
            {
                sliceCountWidth = value;
                OnPropertyChanged(nameof(SliceCountWidth));
            }
        }
    }

    public string SourceColor {
        get => sourceColor;

        set {
            if (sourceColor != value)
            {
                sourceColor = value;
                OnPropertyChanged(nameof(SourceColor));
            }
        }
    }

    public IList<string> Tarayıcılar {
        get => tarayıcılar;

        set {
            if (tarayıcılar != value)
            {
                tarayıcılar = value;
                OnPropertyChanged(nameof(Tarayıcılar));
            }
        }
    }

    public string TargetColor {
        get => targetColor;

        set {
            if (targetColor != value)
            {
                targetColor = value;
                OnPropertyChanged(nameof(TargetColor));
            }
        }
    }

    public double Threshold {
        get => threshold;

        set {
            if (threshold != value)
            {
                threshold = value;
                OnPropertyChanged(nameof(Threshold));
            }
        }
    }

    public int ToolBarBwThreshold {
        get => toolBarBwThreshold;

        set {
            if (toolBarBwThreshold != value)
            {
                toolBarBwThreshold = value;
                OnPropertyChanged(nameof(ToolBarBwThreshold));
            }
        }
    }

    public ObservableCollection<string> UnsupportedFiles {
        get => unsupportedFiles;

        set {
            if (unsupportedFiles != value)
            {
                unsupportedFiles = value;
                OnPropertyChanged(nameof(UnsupportedFiles));
            }
        }
    }

    public bool UseMozJpegEncoding {
        get => useMozJpegEncoding;

        set {
            if (useMozJpegEncoding != value)
            {
                useMozJpegEncoding = value;
                OnPropertyChanged(nameof(UseMozJpegEncoding));
            }
        }
    }

    public bool UsePageSeperator {
        get => usePageSeperator;

        set {
            if (usePageSeperator != value)
            {
                usePageSeperator = value;
                OnPropertyChanged(nameof(UsePageSeperator));
            }
        }
    }

    public string UserName {
        get => userName;

        set {
            if (userName != value)
            {
                userName = value;
                OnPropertyChanged(nameof(UserName));
            }
        }
    }

    public string Watermark {
        get => watermark;

        set {
            if (watermark != value)
            {
                watermark = value;
                OnPropertyChanged(nameof(Watermark));
            }
        }
    }

    public double WatermarkAngle {
        get => watermarkAngle;

        set {
            if (watermarkAngle != value)
            {
                watermarkAngle = value;
                OnPropertyChanged(nameof(WatermarkAngle));
            }
        }
    }

    public SolidColorBrush WatermarkColor {
        get => watermarkColor;

        set {
            if (watermarkColor != value)
            {
                watermarkColor = value;
                OnPropertyChanged(nameof(WatermarkColor));
            }
        }
    }

    public string WatermarkFont {
        get => watermarkFont;

        set {
            if (watermarkFont != value)
            {
                watermarkFont = value;
                OnPropertyChanged(nameof(WatermarkFont));
            }
        }
    }

    public double WatermarkTextSize {
        get => watermarkTextSize;

        set {
            if (watermarkTextSize != value)
            {
                watermarkTextSize = value;
                OnPropertyChanged(nameof(WatermarkTextSize));
            }
        }
    }

    public string this[string columnName] => columnName switch
    {
        "FileName" when string.IsNullOrWhiteSpace(FileName) => "Dosya Adını Boş Geçmeyin.",
        "FileName" when !TwainCtrl.FileNameValid(FileName) => "Dosya Adında Hatalı Karakter Var Düzeltin.",
        "ProfileName" when string.IsNullOrWhiteSpace(ProfileName) => "Profil Adını Boş Geçmeyin.",
        _ => null
    };

    public void Resimler_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            for (int i = e.NewStartingIndex; i < Resimler.Count; i++)
            {
                Resimler[i].Index = i + 1;
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            for (int i = e.OldStartingIndex; i < Resimler.Count; i++)
            {
                Resimler[i].Index = i + 1;
            }
        }
    }

    public static readonly Brush DefaultSaveProgressforegroundbrush =
                                                                                                                                                                                                                                                                                                                                    (Brush)new BrushConverter().ConvertFromString("#FF06B025");

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "PdfSaveProgressValue" && PdfSaveProgressValue == 1)
        {
            ProgressState = TaskbarItemProgressState.None;
        }
    }

    private bool allowCopy = true;

    private bool allowEdit = true;

    private bool allowPrint = true;

    private bool applyDataBaseOcr = Ocr.Ocr.TesseractDataExists;

    private bool applyMedian;

    private bool applyPdfSaveOcr;

    private bool arayüzetkin = true;

    private bool autoSave = Directory.Exists(Settings.Default.AutoFolder);

    private string barcodeContent;

    private bool borderAnimation;

    private int boyAdet = 1;

    private double brightness;

    private int caretPosition;

    private ObservableCollection<Chart> chart;

    private ImageSource copyCroppedImage;

    private string creatorAppName = "GPSCANNER";

    private double cropBottom;

    private bool cropDialogExpanded;

    private double cropLeft;

    private ImageSource croppedImage;

    private double croppedImageAngle;

    private double cropRight;

    private double cropTop;

    private bool deskew;

    private bool detectEmptyPage;

    private bool detectPageSeperator;

    private bool duplex;

    private int enAdet = 1;

    private bool fileisPdfFile;

    private string fileName = "Tarama";

    private string fileOcrContent;

    private int ftpLoadProgressValue;

    private bool ınvertImage;

    private PdfPageLayout layout = PdfPageLayout.Middle;

    private string localizedPath;

    private int medianValue;

    private bool paperBackScan;

    private bool passwordProtect;

    private XKnownColor pdfAlignTextColor = XKnownColor.Black;

    private string pdfFilePath;

    private bool pdfPageNumberDraw;

    private SecureString pdfPassword;

    private double pdfSaveProgressValue;

    private string profileName;

    private TaskbarItemProgressState progressState = TaskbarItemProgressState.None;

    private IEnumerable<string> qrData;

    private ObservableCollection<ScannedImage> resimler = new();

    private double rotateAngle;

    private string saveFileName;

    private Brush saveProgressBarForegroundBrush = DefaultSaveProgressforegroundbrush;

    private bool saveProgressIndeterminate;

    private bool seçili;

    private int seçiliResimSayısı;

    private string seçiliTarayıcı;

    private string selectedProfile;

    private string selectedTtsLanguage;

    private bool showProgress;

    private bool showUi;

    private double sliceCountHeight = 1;

    private double sliceCountWidth = 2;

    private string sourceColor = "Transparent";

    private IList<string> tarayıcılar;

    private string targetColor = "Transparent";

    private double threshold = 1;

    private int toolBarBwThreshold = 160;

    private ObservableCollection<string> unsupportedFiles = new();

    private bool useMozJpegEncoding;

    private bool usePageSeperator;

    private string userName = Environment.UserName;

    private string watermark;

    private double watermarkAngle = 315;

    private SolidColorBrush watermarkColor = Brushes.Red;

    private string watermarkFont = "Arial";

    private double watermarkTextSize = 64;
}