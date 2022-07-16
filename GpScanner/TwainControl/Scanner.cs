using Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl
{
    public class ScannedImage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public BitmapFrame Resim
        {
            get => resim;

            set
            {
                if (resim != value)
                {
                    resim = value;
                    OnPropertyChanged(nameof(Resim));
                }
            }
        }

        public bool Seçili
        {
            get => seçili;

            set
            {
                if (seçili != value)
                {
                    seçili = value;
                    OnPropertyChanged(nameof(Seçili));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private BitmapFrame resim;

        private bool seçili;
    }

    public class Scanner : INotifyPropertyChanged, IDataErrorInfo
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public bool AllowCopy
        {
            get => allowCopy; set

            {
                if (allowCopy != value)
                {
                    allowCopy = value;
                    OnPropertyChanged(nameof(AllowCopy));
                }
            }
        }

        public bool AllowEdit
        {
            get => allowEdit; set

            {
                if (allowEdit != value)
                {
                    allowEdit = value;
                    OnPropertyChanged(nameof(AllowEdit));
                }
            }
        }

        public bool AllowPrint
        {
            get => allowPrint;

            set
            {
                if (allowPrint != value)
                {
                    allowPrint = value;
                    OnPropertyChanged(nameof(AllowPrint));
                }
            }
        }

        public bool ArayüzEtkin
        {
            get => arayüzetkin;

            set
            {
                if (arayüzetkin != value)
                {
                    arayüzetkin = value;
                    OnPropertyChanged(nameof(ArayüzEtkin));
                }
            }
        }

        public bool AutoRotate
        {
            get => autoRotate;

            set
            {
                if (autoRotate != value)
                {
                    autoRotate = value;
                    OnPropertyChanged(nameof(AutoRotate));
                }
            }
        }

        public bool AutoSave
        {
            get => autoSave;

            set
            {
                if (autoSave != value)
                {
                    autoSave = value;
                    OnPropertyChanged(nameof(AutoSave));
                }
            }
        }

        public bool BorderDetect
        {
            get => borderDetect;

            set
            {
                if (borderDetect != value)
                {
                    borderDetect = value;
                    OnPropertyChanged(nameof(BorderDetect));
                }
            }
        }

        public int BoyAdet
        {
            get => boyAdet;

            set

            {
                if (boyAdet != value)
                {
                    boyAdet = value;
                    OnPropertyChanged(nameof(BoyAdet));
                }
            }
        }

        public int CaretPosition
        {
            get => caretPosition;

            set
            {
                if (caretPosition != value)
                {
                    caretPosition = value;
                    OnPropertyChanged(nameof(CaretPosition));
                }
            }
        }

        public double CropBottom
        {
            get => cropBottom; set

            {
                if (cropBottom != value)
                {
                    cropBottom = value;
                    OnPropertyChanged(nameof(CropBottom));
                }
            }
        }

        public bool CropDialogExpanded
        {
            get => cropDialogExpanded;

            set
            {
                if (cropDialogExpanded != value)
                {
                    cropDialogExpanded = value;
                    OnPropertyChanged(nameof(CropDialogExpanded));
                }
            }
        }

        public double CropLeft
        {
            get => cropLeft; set

            {
                if (cropLeft != value)
                {
                    cropLeft = value;
                    OnPropertyChanged(nameof(CropLeft));
                }
            }
        }

        public ImageSource CroppedImage
        {
            get => croppedImage;

            set
            {
                if (croppedImage != value)
                {
                    croppedImage = value;
                    OnPropertyChanged(nameof(CroppedImage));
                }
            }
        }

        public double CropRight
        {
            get => cropRight; set

            {
                if (cropRight != value)
                {
                    cropRight = value;
                    OnPropertyChanged(nameof(CropRight));
                }
            }
        }

        public double CropTop
        {
            get => cropTop;

            set
            {
                if (cropTop != value)
                {
                    cropTop = value;
                    OnPropertyChanged(nameof(CropTop));
                }
            }
        }

        public bool Deskew
        {
            get => deskew;

            set
            {
                if (deskew != value)
                {
                    deskew = value;
                    OnPropertyChanged(nameof(Deskew));
                }
            }
        }

        public bool Duplex
        {
            get => duplex;

            set
            {
                if (duplex != value)
                {
                    duplex = value;
                    OnPropertyChanged(nameof(Duplex));
                }
            }
        }

        public int EnAdet
        {
            get => enAdet;

            set
            {
                if (enAdet != value)
                {
                    enAdet = value;
                    OnPropertyChanged(nameof(EnAdet));
                }
            }
        }

        public string Error => string.Empty;

        public int Eşik
        {
            get => eşik;

            set
            {
                if (eşik != value)
                {
                    eşik = value;
                    OnPropertyChanged(nameof(Eşik));
                }
            }
        }

        public string FileName
        {
            get => fileName;

            set
            {
                if (fileName != value)
                {
                    fileName = value;
                    OnPropertyChanged(nameof(FileName));
                    OnPropertyChanged(nameof(SaveFileName));
                }
            }
        }

        public string LocalizedPath
        {
            get => ExtensionMethods.GetDisplayName(Settings.Default.AutoFolder);

            set
            {
                if (localizedPath != value)
                {
                    localizedPath = value;
                    OnPropertyChanged(nameof(LocalizedPath));
                }
            }
        }

        public bool PasswordProtect
        {
            get => passwordProtect;

            set
            {
                if (passwordProtect != value)
                {
                    passwordProtect = value;
                    OnPropertyChanged(nameof(PasswordProtect));
                }
            }
        }

        public SecureString PdfPassword
        {
            get => pdfPassword;

            set
            {
                if (pdfPassword != value)
                {
                    pdfPassword = value;
                    OnPropertyChanged(nameof(PdfPassword));
                }
            }
        }

        public string ProfileName
        {
            get => profileName;

            set
            {
                if (profileName != value)
                {
                    profileName = value;
                    OnPropertyChanged(nameof(ProfileName));
                }
            }
        }

        public ObservableCollection<ScannedImage> Resimler
        {
            get => resimler;

            set
            {
                if (resimler != value)
                {
                    resimler = value;
                    OnPropertyChanged(nameof(Resimler));
                }
            }
        }

        public double RotateAngle
        {
            get => rotateAngle;

            set
            {
                if (rotateAngle != value)
                {
                    rotateAngle = value;
                    OnPropertyChanged(nameof(RotateAngle));
                }
            }
        }

        public string SaveFileName
        {
            get
            {
                saveFileName = new string[] { "[", "]" }.Any(FileName.Contains)
                    ? FileName.
                       Replace("[GÜN]", DateTime.Now.Day.ToString()).
                       Replace("[AY]", DateTime.Now.Month.ToString()).
                       Replace("[YIL]", DateTime.Now.Year.ToString()).
                       Replace("[SAAT]", DateTime.Now.Hour.ToString()).
                       Replace("[DAKİKA]", DateTime.Now.Minute.ToString()).
                       Replace("[SANİYE]", DateTime.Now.Second.ToString()).
                       Replace("[GUID]", Guid.NewGuid().ToString()).
                       Replace("[USERNAME]", Environment.UserName)
                    : FileName;
                return saveFileName;
            }

            set
            {
                if (saveFileName != value)
                {
                    saveFileName = value;
                    OnPropertyChanged(nameof(SaveFileName));
                }
            }
        }

        public bool Seçili
        {
            get => seçili;

            set
            {
                if (seçili != value)
                {
                    seçili = value;
                    OnPropertyChanged(nameof(Seçili));
                }
            }
        }

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

        public int SeçiliResimSayısı
        {
            get => seçiliResimSayısı;

            set
            {
                if (seçiliResimSayısı != value)
                {
                    seçiliResimSayısı = value;
                    OnPropertyChanged(nameof(SeçiliResimSayısı));
                }
            }
        }

        public string SeçiliTarayıcı
        {
            get => seçiliTarayıcı;

            set
            {
                if (seçiliTarayıcı != value)
                {
                    seçiliTarayıcı = value;
                    OnPropertyChanged(nameof(SeçiliTarayıcı));
                }
            }
        }

        public string SelectedProfile
        {
            get => selectedProfile;

            set
            {
                if (selectedProfile != value)
                {
                    selectedProfile = value;
                    OnPropertyChanged(nameof(SelectedProfile));
                }
            }
        }

        public bool SeperateSave
        {
            get => seperateSave;

            set
            {
                if (seperateSave != value)
                {
                    seperateSave = value;
                    OnPropertyChanged(nameof(SeperateSave));
                }
            }
        }

        public bool ShowProgress
        {
            get => showProgress;

            set
            {
                if (showProgress != value)
                {
                    showProgress = value;
                    OnPropertyChanged(nameof(ShowProgress));
                }
            }
        }

        public bool ShowUi
        {
            get => showUi;

            set
            {
                if (showUi != value)
                {
                    showUi = value;
                    OnPropertyChanged(nameof(ShowUi));
                }
            }
        }

        public int ShutDownMode
        {
            get => shutDownMode;

            set
            {
                if (shutDownMode != value)
                {
                    shutDownMode = value;
                    OnPropertyChanged(nameof(ShutDownMode));
                }
            }
        }

        public bool Tarandı
        {
            get => tarandı;

            set
            {
                if (tarandı != value)
                {
                    tarandı = value;
                    OnPropertyChanged(nameof(Tarandı));
                }
            }
        }

        public IList<string> Tarayıcılar
        {
            get => tarayıcılar;

            set
            {
                if (tarayıcılar != value)
                {
                    tarayıcılar = value;
                    OnPropertyChanged(nameof(Tarayıcılar));
                }
            }
        }

        public string Watermark
        {
            get => watermark;

            set
            {
                if (watermark != value)
                {
                    watermark = value;
                    OnPropertyChanged(nameof(Watermark));
                }
            }
        }

        public double WatermarkAngle
        {
            get => watermarkAngle;

            set
            {
                if (watermarkAngle != value)
                {
                    watermarkAngle = value;
                    OnPropertyChanged(nameof(WatermarkAngle));
                }
            }
        }

        public string WatermarkFont
        {
            get => watermarkFont;

            set
            {
                if (watermarkFont != value)
                {
                    watermarkFont = value;
                    OnPropertyChanged(nameof(WatermarkFont));
                }
            }
        }

        public double WatermarkTextSize
        {
            get => watermarkTextSize;

            set
            {
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
            "FileName" when FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 => "Dosya Adında Hatalı Karakter Var Düzeltin.",
            "ProfileName" when string.IsNullOrWhiteSpace(ProfileName) => "Profil Adını Boş Geçmeyin.",
            _ => null
        };

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool allowCopy = true;

        private bool allowEdit = true;

        private bool allowPrint = true;

        private bool arayüzetkin = true;

        private bool autoRotate;

        private bool autoSave = Directory.Exists(Settings.Default.AutoFolder);

        private bool borderDetect;

        private int boyAdet = 1;

        private int caretPosition;

        private double cropBottom;

        private bool cropDialogExpanded;

        private double cropLeft;

        private ImageSource croppedImage;

        private double cropRight;

        private double cropTop;

        private bool deskew;

        private bool duplex;

        private int enAdet = 1;

        private int eşik = 160;

        private string fileName = "Tarama";

        private string localizedPath;

        private bool passwordProtect;

        private SecureString pdfPassword;

        private string profileName;

        private ObservableCollection<ScannedImage> resimler = new();

        private double rotateAngle;

        private string saveFileName;

        private bool seçili;

        private ScannedImage seçiliResim;

        private int seçiliResimSayısı;

        private string seçiliTarayıcı;

        private string selectedProfile;

        private bool seperateSave;

        private bool showProgress;

        private bool showUi;

        private int shutDownMode;

        private bool tarandı;

        private IList<string> tarayıcılar;

        private string watermark;

        private double watermarkAngle = 315;

        private string watermarkFont = "Arial";

        private double watermarkTextSize = 64;
    }
}