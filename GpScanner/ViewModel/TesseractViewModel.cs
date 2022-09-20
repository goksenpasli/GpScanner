using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Input;
using Extensions;

namespace GpScanner.ViewModel
{
    public class TesseractViewModel : InpcBase
    {
        public TesseractViewModel()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string tessdatafolder = Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName) + @"\tessdata";
            TesseractFiles = GetTesseractFiles(tessdatafolder);

            OcrDatas = TesseractDownloadData();

            TesseractDataFilesDownloadLink = new RelayCommand<object>(parameter => _ = Process.Start(parameter as string), parameter => true);

            TesseractDownload = new RelayCommand<object>(async parameter =>
            {
                try
                {
                    TesseractOcrData ocrData = parameter as TesseractOcrData;
                    using WebClient client = new();
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        ocrData.ProgressValue = e.ProgressPercentage;
                        ocrData.IsEnabled = ocrData.ProgressValue == 100;
                    };
                    client.DownloadFileCompleted += (s, e) => TesseractFiles = GetTesseractFiles(tessdatafolder);
                    await client.DownloadFileTaskAsync(new Uri($"https://github.com/tesseract-ocr/tessdata/raw/main/{ocrData.OcrName}"), $@"{tessdatafolder}\{ocrData.OcrName}");
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, parameter => true);

            PropertyChanged += TesseractViewModel_PropertyChanged;
        }

        public ObservableCollection<TesseractOcrData> OcrDatas { get; set; }

        public bool ShowAllLanguages
        {
            get => showAllLanguages;

            set
            {
                if (showAllLanguages != value)
                {
                    showAllLanguages = value;
                    OnPropertyChanged(nameof(ShowAllLanguages));
                }
            }
        }

        public ICommand TesseractDataFilesDownloadLink { get; }

        public ICommand TesseractDownload { get; }

        public ObservableCollection<string> TesseractFiles
        {
            get => tesseractFiles;

            set
            {
                if (tesseractFiles != value)
                {
                    tesseractFiles = value;
                    OnPropertyChanged(nameof(TesseractFiles));
                }
            }
        }

        private bool showAllLanguages;

        private ObservableCollection<string> tesseractFiles;

        private ObservableCollection<string> GetTesseractFiles(string tesseractfolder)
        {
            return Directory.Exists(tesseractfolder) ? new ObservableCollection<string>(Directory.EnumerateFiles(tesseractfolder, "*.traineddata").Select(Path.GetFileNameWithoutExtension)) : null;
        }

        private ObservableCollection<TesseractOcrData> TesseractDownloadData()
        {
            return new()
            {
                new TesseractOcrData(){OcrName = "afr.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "amh.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ara.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "asm.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "aze.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "aze_cyrl.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "bel.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ben.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "bod.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "bos.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "bre.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "bul.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "cat.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ceb.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ces.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "chi_sim.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "chi_sim_vert.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "chi_tra.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "chi_tra_vert.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "chr.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "cos.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "cym.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "dan.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "dan_frak.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "deu.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "deu_frak.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "div.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "dzo.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ell.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "eng.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "enm.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "epo.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "equ.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "est.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "eus.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "fao.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "fas.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "fil.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "fin.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "fra.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "frk.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "frm.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "fry.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "gla.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "gle.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "glg.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "grc.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "guj.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "hat.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "heb.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "hin.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "hrv.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "hun.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "hye.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "iku.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ind.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "isl.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ita.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "jav.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "jpn.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "jpn_vert.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "kan.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "kat.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "kat_old.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "kaz.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "khm.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "kir.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "kmr.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "kor.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "kor_vert.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "lao.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "lat.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "lav.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "lit.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ltz.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "mal.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "mar.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "mkd.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "mlt.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "mon.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "mri.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "msa.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "mya.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "nep.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "nld.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "nor.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "oci.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ori.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "osd.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "pan.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "pol.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "por.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "pus.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "que.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ron.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "rus.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "san.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "sin.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "slk.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "slk_frak.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "slv.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "snd.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "spa.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "sqi.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "srp.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "srp_latn.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "sun.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "swa.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "swe.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "syr.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tam.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tat.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tel.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tgk.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tgl.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tha.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tir.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ton.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "tur.traineddata", ProgressValue = 0, IsVisible=Visibility.Visible},
                new TesseractOcrData(){OcrName = "uig.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "ukr.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "urd.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "uzb.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "uzb_cyrl.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "vie.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "yid.traineddata", ProgressValue = 0},
                new TesseractOcrData(){OcrName = "yor.traineddata", ProgressValue = 0},
            };
        }

        private void TesseractViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "ShowAllLanguages" && ShowAllLanguages)
            {
                foreach (TesseractOcrData item in OcrDatas.ToList())
                {
                    item.IsVisible = Visibility.Visible;
                }
            }
        }
    }
}