﻿using System.Collections;
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
    public class Scanner : INotifyPropertyChanged
    {
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

        private ObservableCollection<BitmapFrame> resimler = new();

        private ImageSource seçiliResim;

        private IList seçiliresimler = new ObservableCollection<BitmapFrame>();

        private string seçiliTarayıcı;

        private bool seperateSave;

        private bool showProgress;

        private bool showUi;

        private bool tarandı;

        private IList<string> tarayıcılar;

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

        public ICommand Kaydet { get; }

        public ICommand KayıtYoluBelirle { get; }

        public ICommand ListeTemizle { get; }

        public ICommand ResetCroppedImage { get; }

        public ObservableCollection<BitmapFrame> Resimler
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

        public ICommand Seçilikaydet { get; }

        public ImageSource SeçiliResim
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

        public IList SeçiliResimler
        {
            get => seçiliresimler;

            set
            {
                if (seçiliresimler != value)
                {
                    seçiliresimler = value;
                    OnPropertyChanged(nameof(SeçiliResimler));
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

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}