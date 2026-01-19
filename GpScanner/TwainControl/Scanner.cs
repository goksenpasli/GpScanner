using Extensions;
using PdfSharp.Drawing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using TwainControl.Properties;

namespace TwainControl;

public class Scanner : InpcBase, IDataErrorInfo
{
    private bool fileisPdfFile;
    private string localizedPath;

    public Scanner() { PropertyChanged += Scanner_PropertyChanged; }

    public bool AllowCopy
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AllowCopy));
            }
        }
    } = true;

    public bool AllowEdit
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AllowEdit));
            }
        }
    } = true;

    public bool AllowPrint
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AllowPrint));
            }
        }
    } = true;

    public bool ApplyDataBaseOcr
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ApplyDataBaseOcr));
            }
        }
    } = Ocr.Ocr.TesseractDataExists;

    public bool ApplyMedian
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ApplyMedian));
            }
        }
    }

    public bool ApplyPdfSaveOcr
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ApplyPdfSaveOcr));
            }
        }
    }

    public bool ArayüzEtkin
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ArayüzEtkin));
            }
        }
    } = true;

    public string AutoCropColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AutoCropColor));
            }
        }
    } = "Black";

    public bool AutoSave
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(AutoSave));
            }
        }
    } = Directory.Exists(Settings.Default.AutoFolder);

    public string BarcodeContent
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BarcodeContent));
            }
        }
    }

    public bool BorderAnimation
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BorderAnimation));
            }
        }
    }

    public int BoyAdet
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(BoyAdet));
            }
        }
    } = 1;

    public double Brightness
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Brightness));
            }
        }
    }

    public int CaretPosition
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CaretPosition));
            }
        }
    }

    public ObservableCollection<Chart> Chart
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Chart));
            }
        }
    }

    public ImageSource CopyCroppedImage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CopyCroppedImage));
            }
        }
    }

    public string CreatorAppName
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CreatorAppName));
            }
        }
    } = $"{TwainCtrl.AppName} {FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).FileVersion}";

    public double CropBottom
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropBottom));
            }
        }
    }

    public bool CropDialogExpanded
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropDialogExpanded));
            }
        }
    }

    public double CropLeft
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropLeft));
            }
        }
    }

    public ImageSource CroppedImage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CroppedImage));
            }
        }
    }

    public double CroppedImageAngle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CroppedImageAngle));
            }
        }
    }

    public int CroppedImageIndex
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CroppedImageIndex));
            }
        }
    }

    public BitmapSource CroppedImageThumb
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CroppedImageThumb));
            }
        }
    }

    public double CropRight
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropRight));
            }
        }
    }

    public double CropTop
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CropTop));
            }
        }
    }

    public bool DetectEmptyPage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DetectEmptyPage));
            }
        }
    }

    public bool DetectPageSeperator
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(DetectPageSeperator));
            }
        }
    }

    public bool Duplex
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Duplex));
            }
        }
    }

    public int EnAdet
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(EnAdet));
            }
        }
    } = 1;

    public string Error => string.Empty;

    public bool FileIsPdfFile
    {
        get => string.Equals(Path.GetExtension(FileName), ".pdf", StringComparison.OrdinalIgnoreCase);

        set
        {
            if (fileisPdfFile != value)
            {
                fileisPdfFile = value;
                OnPropertyChanged(nameof(FileIsPdfFile));
            }
        }
    }

    public string FileName
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(SaveFileName));
            }
        }
    } = "Tarama";

    public string FileOcrContent
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileOcrContent));
            }
        }
    }

    public float FileSize
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FileSize));
            }
        }
    }

    public Dictionary<string, int> FolderDateFormats
    {
        get;
    } = new Dictionary<string, int>
    {
        { "d.MM.yyyy", 1 },
        { "dd.MM.yyyy", 1 },
        { "d-MM-yyyy", 1 },
        { "dd-MM-yyyy", 1 },
        { "yyyy.MM.dd", 2 },
        { "yyyy-MM-dd", 2 },
        { "yyyy-MM-d", 2 },
        { "yyyy.MM.d", 2 },
        { "MM-dd-yyyy", 3 },
        { "MM-d-yyyy", 3 },
        { "MM.dd.yyyy", 3 },
        { "MM.d.yyyy", 3 },
        { "dddd", 4 },
        { "MMMM", 4 },
        { "yyyy", 4 }
    };

    public string FolderName
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(FolderName));
            }
        }
    }

    public double Hue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Hue));
            }
        }
    }

    public bool InvertImage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(InvertImage));
            }
        }
    }

    public PdfPageLayout Layout
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Layout));
            }
        }
    } = PdfPageLayout.Middle;

    public double Lightness
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Lightness));
            }
        }
    } = 1;

    public string LocalizedPath
    {
        get => ShellIcon.GetDisplayName(Settings.Default.AutoFolder);

        set
        {
            if (localizedPath != value)
            {
                localizedPath = value;
                OnPropertyChanged(nameof(LocalizedPath));
            }
        }
    }

    public int MedianValue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MedianValue));
            }
        }
    }

    public PdfCollection MergePdfFiles
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(MergePdfFiles));
            }
        }
    } = [];

    public bool PaperBackScan
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PaperBackScan));
            }
        }
    }

    public bool PasswordProtect
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PasswordProtect));
            }
        }
    }

    public bool PdfBatchNumberIsFirst
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfBatchNumberIsFirst));
            }
        }
    }

    public string PdfBatchNumberText
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfBatchNumberText));
            }
        }
    } = string.Empty;

    public string PdfFilePath
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfFilePath));
            }
        }
    }

    public XKnownColor PdfPageNumberAlignTextColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageNumberAlignTextColor));
            }
        }
    } = XKnownColor.Black;

    public bool PdfPageNumberDraw
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageNumberDraw));
            }
        }
    }

    public double PdfPageNumberSize
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageNumberSize));
            }
        }
    } = 12;

    public string PdfPageText
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageText));
            }
        }
    }

    public double PdfPageTextAngle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageTextAngle));
            }
        }
    } = 315d;

    public string PdfPageTextColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageTextColor));
            }
        }
    } = "Black";

    public bool PdfPageTextDraw
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageTextDraw));
            }
        }
    }

    public double PdfPageTextSize
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPageTextSize));
            }
        }
    } = 32d;

    public string PdfPassword
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfPassword));
            }
        }
    }

    public double PdfSaveProgressValue
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(PdfSaveProgressValue));
            }
        }
    }

    public string ProfileName
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ProfileName));
            }
        }
    }

    public TaskbarItemProgressState ProgressState
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ProgressState));
            }
        }
    } = TaskbarItemProgressState.None;

    public IEnumerable<string> QrData
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(QrData));
            }
        }
    }

    public IndexedObservableCollection<ScannedImage> Resimler
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Resimler));
            }
        }
    } = [];

    public double RotateAngle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(RotateAngle));
            }
        }
    }

    public double Saturation
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Saturation));
            }
        }
    } = 1;

    public string SaveFileFullPath
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SaveFileFullPath));
            }
        }
    }

    public string SaveFileName
    {
        get
        {
            if (new[] { "[", "]" }.Any(FileName.Contains))
            {
                string tempfilename = FileName;
                foreach (KeyValuePair<string, string> entry in FileContextMenuDictionary())
                {
                    tempfilename = tempfilename.Replace(entry.Key, entry.Value);
                    field = tempfilename;
                }
            }
            else
            {
                field = FileName;
            }

            return field;
        }

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SaveFileName));
            }
        }
    }

    public Brush SaveProgressBarForegroundBrush
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SaveProgressBarForegroundBrush));
            }
        }
    } = (Brush)new BrushConverter().ConvertFromString("#FF06B025");

    public bool SaveProgressIndeterminate
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SaveProgressIndeterminate));
            }
        }
    }

    public bool ScanSeperate
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ScanSeperate));
            }
        }
    }

    public bool Seçili
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Seçili));
            }
        }
    }

    public int SeçiliResimSayısı
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SeçiliResimSayısı));
            }
        }
    }

    public string SelectedProfile
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedProfile));
            }
        }
    }

    public string SelectedTtsLanguage
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedTtsLanguage));
            }
        }
    }

    public bool ShowProgress
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ShowProgress));
            }
        }
    }

    public bool ShowUi
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ShowUi));
            }
        }
    }

    public double SliceCountHeight
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SliceCountHeight));
            }
        }
    } = 1;

    public double SliceCountWidth
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SliceCountWidth));
            }
        }
    } = 2;

    public string SourceColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SourceColor));
            }
        }
    } = "Transparent";

    public IList<string> Tarayıcılar
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Tarayıcılar));
            }
        }
    }

    public string TargetColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(TargetColor));
            }
        }
    } = "Transparent";

    public double Threshold
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Threshold));
            }
        }
    }

    public int ToolBarBwThreshold
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(ToolBarBwThreshold));
            }
        }
    } = 160;

    public CultureInfo UiLanguage
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UiLanguage));
            }
        }
    }

    public bool UseFilmScanner
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UseFilmScanner));
            }
        }
    }

    public bool UseMozJpegEncoding
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UseMozJpegEncoding));
            }
        }
    }

    public bool UsePageSeperator
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UsePageSeperator));
            }
        }
    }

    public string UserName
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(UserName));
            }
        }
    } = Environment.UserName;

    public int VerticalLineThreshold
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(VerticalLineThreshold));
            }
        }
    } = 6;

    public string Watermark
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Watermark));
            }
        }
    }

    public double WatermarkAngle
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(WatermarkAngle));
            }
        }
    } = 315;

    public SolidColorBrush WatermarkColor
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(WatermarkColor));
            }
        }
    } = Brushes.Red;

    public string WatermarkFont
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(WatermarkFont));
            }
        }
    } = "Arial";

    public double WatermarkTextSize
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(WatermarkTextSize));
            }
        }
    } = 64;

    public string this[string columnName] => columnName switch
    {
        "FileName" when string.IsNullOrWhiteSpace(FileName) => "EMPTY",
        "FileName" when !TwainCtrl.FileNameValid(FileName) => "INVALIDFILENAME",
        "ProfileName" when string.IsNullOrWhiteSpace(ProfileName) => "EMPTY",
        "AutoSave" when !AutoSave => "AUTOFOLDER",
        "SelectedTtsLanguage" when string.IsNullOrWhiteSpace(SelectedTtsLanguage) => "TESSLANGSELECT",
        "PasswordProtect" when PasswordProtect => "ENCRYPT",
        _ => null
    };

    public static Dictionary<string, string> FileContextMenuDictionary()
    {
        return new Dictionary<string, string>
        {
            { "[DATE]", DateTime.Now.Day.ToString() },
            { "[MONTH]", DateTime.Now.Month.ToString() },
            { "[YEAR]", DateTime.Now.Year.ToString() },
            { "[HOUR]", DateTime.Now.Hour.ToString() },
            { "[MINUTE]", DateTime.Now.Minute.ToString() },
            { "[SECOND]", DateTime.Now.Second.ToString() },
            { "[GUID]", Guid.NewGuid().ToString() },
            { "[COMPUTER]", Environment.MachineName },
            { "[USERNAME]", Environment.UserName },
            { "[RESOLUTION]", Settings.Default.Çözünürlük.ToString() }
        };
    }

    private void Scanner_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "PdfSaveProgressValue" && PdfSaveProgressValue == 1)
        {
            ProgressState = TaskbarItemProgressState.None;
        }
        if (e.PropertyName is "CroppedImage" && CroppedImage is not null)
        {
            CroppedImage.Freeze();
            BitmapSource bitmapSource = (BitmapSource)CroppedImage;
            if (Settings.Default.DefaultThumbPictureAutoResize)
            {
                double resizeratio = Math.Min(Settings.Default.AutomaticThumbSize / (double)bitmapSource.PixelWidth, Settings.Default.AutomaticThumbSize / (double)bitmapSource.PixelHeight);
                CroppedImageThumb = bitmapSource.Resize(resizeratio);
                return;
            }
            CroppedImageThumb = bitmapSource.Resize(Settings.Default.DefaultThumbPictureResizeRatio / 100d);
        }
    }
}