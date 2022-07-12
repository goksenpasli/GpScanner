using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
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

        public string SelectedProfile { get => selectedProfile;
            set
            {
                if (selectedProfile != value)
                {
                    selectedProfile = value;
                    OnPropertyChanged(nameof(SelectedProfile));
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

        public ICommand ExploreFile { get; }

        public ICommand FastScanImage { get; }

        public string FileName
        {
            get => fileName;

            set
            {
                if (fileName != value)
                {
                    fileName = value;
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public string LocalizedPath
        {
            get => TwainCtrl.GetDisplayName(Settings.Default.AutoFolder);

            set
            {
                if (localizedPath != value)
                {
                    localizedPath = value;
                    OnPropertyChanged(nameof(LocalizedPath));
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

        public ICommand ResetCroppedImage { get; }

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

        public ICommand ResimSil { get; }

        public ICommand SaveCroppedImage { get; }

        public ICommand ScanImage { get; }

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

        public ICommand Seçilikaydet { get; }

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

        public ICommand SplitImage { get; }

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

        public string this[string columnName] => columnName switch
        {
            "FileName" when string.IsNullOrWhiteSpace(FileName) => "Dosya Adını Boş Geçmeyin.",
            _ => null
        };

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool arayüzetkin = true;

        private bool autoRotate;

        private bool autoSave = Directory.Exists(Settings.Default.AutoFolder);

        private bool borderDetect;

        private int boyAdet = 1;

        private double cropBottom;

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

        private string profileName = "Profil";

        private ObservableCollection<ScannedImage> resimler = new();

        private bool seçili;

        private ScannedImage seçiliResim;

        private int seçiliResimSayısı;

        private string seçiliTarayıcı;

        private bool seperateSave;

        private bool showProgress;

        private bool showUi;

        private bool tarandı;

        private IList<string> tarayıcılar;
        private string selectedProfile;
    }
}