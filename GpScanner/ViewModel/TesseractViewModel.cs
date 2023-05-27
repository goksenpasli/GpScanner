using Extensions;
using Ocr;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Input;
using InpcBase = Extensions.InpcBase;

namespace GpScanner.ViewModel;

public class TesseractViewModel : InpcBase
{
    public TesseractViewModel()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Tessdatafolder = $@"{Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName)}\tessdata";
        TesseractFiles = GetTesseractFiles(Tessdatafolder);

        OcrDatas = TesseractDownloadData();

        TesseractDataFilesDownloadLink = new RelayCommand<object>(
            parameter =>
            {
                string path = parameter as string;
                if(!string.IsNullOrWhiteSpace(path))
                {
                    try
                    {
                        _ = Process.Start(path);
                    } catch(Exception ex)
                    {
                        _ = MessageBox.Show(
                            ex.Message,
                            Application.Current?.MainWindow?.Title,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            },
            parameter => true);

        TesseractDownload = new RelayCommand<object>(
            async parameter =>
            {
                if(parameter is TesseractOcrData ocrData)
                {
                    string datafile = null;
                    try
                    {
                        datafile = $@"{tessdatafolder}\{ocrData.OcrName}";
                        using WebClient client = new();
                        client.DownloadProgressChanged += (s, e) =>
                        {
                            ocrData.ProgressValue = e.ProgressPercentage;
                            ocrData.IsEnabled = ocrData.ProgressValue == 100;
                        };
                        client.DownloadFileCompleted += (s, e) =>
                        {
                            if(e.Error is null)
                            {
                                TesseractFiles = GetTesseractFiles(Tessdatafolder);
                            }
                        };
                        Uri address = new($"https://github.com/tesseract-ocr/tessdata_best/raw/main/{ocrData.OcrName}");
                        await client.DownloadFileTaskAsync(address, datafile);
                    } catch(Exception ex)
                    {
                        _ = MessageBox.Show(
                            ex.Message,
                            Application.Current?.MainWindow?.Title,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        if(new FileInfo(datafile)?.Length == 0)
                        {
                            File.Delete(datafile);
                        }
                    } finally
                    {
                        ocrData.IsEnabled = true;
                    }
                }
            },
            parameter => true);

        PropertyChanged += TesseractViewModel_PropertyChanged;
    }

    public ObservableCollection<TesseractOcrData> OcrDatas { get; set; }

    public bool ShowAllLanguages
    {
        get => showAllLanguages;

        set
        {
            if(showAllLanguages != value)
            {
                showAllLanguages = value;
                OnPropertyChanged(nameof(ShowAllLanguages));
            }
        }
    }

    public string Tessdatafolder
    {
        get => tessdatafolder;

        set
        {
            if(tessdatafolder != value)
            {
                tessdatafolder = value;
                OnPropertyChanged(nameof(Tessdatafolder));
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
            if(tesseractFiles != value)
            {
                tesseractFiles = value;
                OnPropertyChanged(nameof(TesseractFiles));
            }
        }
    }

    private bool showAllLanguages;

    private string tessdatafolder;

    private ObservableCollection<string> tesseractFiles;

    private ObservableCollection<string> GetTesseractFiles(string tesseractfolder)
    {
        return Directory.Exists(tesseractfolder)
            ? new ObservableCollection<string>(
                Directory.EnumerateFiles(tesseractfolder, "*.traineddata").Select(Path.GetFileNameWithoutExtension))
            : null;
    }

    private ObservableCollection<TesseractOcrData> TesseractDownloadData()
    {
        return new ObservableCollection<TesseractOcrData>
        {
            new TesseractOcrData { OcrName = "afr.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "amh.traineddata" },
            new TesseractOcrData { OcrName = "ara.traineddata" },
            new TesseractOcrData { OcrName = "asm.traineddata" },
            new TesseractOcrData { OcrName = "aze.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "aze_cyrl.traineddata" },
            new TesseractOcrData { OcrName = "bel.traineddata" },
            new TesseractOcrData { OcrName = "ben.traineddata" },
            new TesseractOcrData { OcrName = "bod.traineddata" },
            new TesseractOcrData { OcrName = "bos.traineddata" },
            new TesseractOcrData { OcrName = "bre.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "bul.traineddata" },
            new TesseractOcrData { OcrName = "cat.traineddata" },
            new TesseractOcrData { OcrName = "ceb.traineddata" },
            new TesseractOcrData { OcrName = "ces.traineddata" },
            new TesseractOcrData { OcrName = "chi_sim.traineddata" },
            new TesseractOcrData { OcrName = "chi_sim_vert.traineddata" },
            new TesseractOcrData { OcrName = "chi_tra.traineddata" },
            new TesseractOcrData { OcrName = "chi_tra_vert.traineddata" },
            new TesseractOcrData { OcrName = "chr.traineddata" },
            new TesseractOcrData { OcrName = "cos.traineddata" },
            new TesseractOcrData { OcrName = "cym.traineddata" },
            new TesseractOcrData { OcrName = "dan.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "dan_frak.traineddata" },
            new TesseractOcrData { OcrName = "deu.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "deu_frak.traineddata" },
            new TesseractOcrData { OcrName = "div.traineddata" },
            new TesseractOcrData { OcrName = "dzo.traineddata" },
            new TesseractOcrData { OcrName = "ell.traineddata" },
            new TesseractOcrData { OcrName = "eng.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "enm.traineddata" },
            new TesseractOcrData { OcrName = "epo.traineddata" },
            new TesseractOcrData { OcrName = "equ.traineddata" },
            new TesseractOcrData { OcrName = "est.traineddata" },
            new TesseractOcrData { OcrName = "eus.traineddata" },
            new TesseractOcrData { OcrName = "fao.traineddata" },
            new TesseractOcrData { OcrName = "fas.traineddata" },
            new TesseractOcrData { OcrName = "fil.traineddata" },
            new TesseractOcrData { OcrName = "fin.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "fra.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "frk.traineddata" },
            new TesseractOcrData { OcrName = "frm.traineddata" },
            new TesseractOcrData { OcrName = "fry.traineddata" },
            new TesseractOcrData { OcrName = "gla.traineddata" },
            new TesseractOcrData { OcrName = "gle.traineddata" },
            new TesseractOcrData { OcrName = "glg.traineddata" },
            new TesseractOcrData { OcrName = "grc.traineddata" },
            new TesseractOcrData { OcrName = "guj.traineddata" },
            new TesseractOcrData { OcrName = "hat.traineddata" },
            new TesseractOcrData { OcrName = "heb.traineddata" },
            new TesseractOcrData { OcrName = "hin.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "hrv.traineddata" },
            new TesseractOcrData { OcrName = "hun.traineddata" },
            new TesseractOcrData { OcrName = "hye.traineddata" },
            new TesseractOcrData { OcrName = "iku.traineddata" },
            new TesseractOcrData { OcrName = "ind.traineddata" },
            new TesseractOcrData { OcrName = "isl.traineddata" },
            new TesseractOcrData { OcrName = "ita.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "jav.traineddata" },
            new TesseractOcrData { OcrName = "jpn.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "jpn_vert.traineddata" },
            new TesseractOcrData { OcrName = "kan.traineddata" },
            new TesseractOcrData { OcrName = "kat.traineddata" },
            new TesseractOcrData { OcrName = "kat_old.traineddata" },
            new TesseractOcrData { OcrName = "kaz.traineddata" },
            new TesseractOcrData { OcrName = "khm.traineddata" },
            new TesseractOcrData { OcrName = "kir.traineddata" },
            new TesseractOcrData { OcrName = "kmr.traineddata" },
            new TesseractOcrData { OcrName = "kor.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "kor_vert.traineddata" },
            new TesseractOcrData { OcrName = "lao.traineddata" },
            new TesseractOcrData { OcrName = "lat.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "lav.traineddata" },
            new TesseractOcrData { OcrName = "lit.traineddata" },
            new TesseractOcrData { OcrName = "ltz.traineddata" },
            new TesseractOcrData { OcrName = "mal.traineddata" },
            new TesseractOcrData { OcrName = "mar.traineddata" },
            new TesseractOcrData { OcrName = "mkd.traineddata" },
            new TesseractOcrData { OcrName = "mlt.traineddata" },
            new TesseractOcrData { OcrName = "mon.traineddata" },
            new TesseractOcrData { OcrName = "mri.traineddata" },
            new TesseractOcrData { OcrName = "msa.traineddata" },
            new TesseractOcrData { OcrName = "mya.traineddata" },
            new TesseractOcrData { OcrName = "nep.traineddata" },
            new TesseractOcrData { OcrName = "nld.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "nor.traineddata" },
            new TesseractOcrData { OcrName = "oci.traineddata" },
            new TesseractOcrData { OcrName = "ori.traineddata" },
            new TesseractOcrData { OcrName = "osd.traineddata" },
            new TesseractOcrData { OcrName = "pan.traineddata" },
            new TesseractOcrData { OcrName = "pol.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "por.traineddata" },
            new TesseractOcrData { OcrName = "pus.traineddata" },
            new TesseractOcrData { OcrName = "que.traineddata" },
            new TesseractOcrData { OcrName = "ron.traineddata" },
            new TesseractOcrData { OcrName = "rus.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "san.traineddata" },
            new TesseractOcrData { OcrName = "sin.traineddata" },
            new TesseractOcrData { OcrName = "slk.traineddata" },
            new TesseractOcrData { OcrName = "slk_frak.traineddata" },
            new TesseractOcrData { OcrName = "slv.traineddata" },
            new TesseractOcrData { OcrName = "snd.traineddata" },
            new TesseractOcrData { OcrName = "spa.traineddata" },
            new TesseractOcrData { OcrName = "sqi.traineddata" },
            new TesseractOcrData { OcrName = "srp.traineddata" },
            new TesseractOcrData { OcrName = "srp_latn.traineddata" },
            new TesseractOcrData { OcrName = "sun.traineddata" },
            new TesseractOcrData { OcrName = "swa.traineddata" },
            new TesseractOcrData { OcrName = "swe.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "syr.traineddata" },
            new TesseractOcrData { OcrName = "tam.traineddata" },
            new TesseractOcrData { OcrName = "tat.traineddata" },
            new TesseractOcrData { OcrName = "tel.traineddata" },
            new TesseractOcrData { OcrName = "tgk.traineddata" },
            new TesseractOcrData { OcrName = "tgl.traineddata" },
            new TesseractOcrData { OcrName = "tha.traineddata" },
            new TesseractOcrData { OcrName = "tir.traineddata" },
            new TesseractOcrData { OcrName = "ton.traineddata" },
            new TesseractOcrData { OcrName = "tur.traineddata", IsVisible = Visibility.Visible },
            new TesseractOcrData { OcrName = "uig.traineddata" },
            new TesseractOcrData { OcrName = "ukr.traineddata" },
            new TesseractOcrData { OcrName = "urd.traineddata" },
            new TesseractOcrData { OcrName = "uzb.traineddata" },
            new TesseractOcrData { OcrName = "uzb_cyrl.traineddata" },
            new TesseractOcrData { OcrName = "vie.traineddata" },
            new TesseractOcrData { OcrName = "yid.traineddata" },
            new TesseractOcrData { OcrName = "yor.traineddata" }
        };
    }

    private void TesseractViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName is "ShowAllLanguages" && ShowAllLanguages)
        {
            foreach(TesseractOcrData item in OcrDatas.ToList())
            {
                item.IsVisible = Visibility.Visible;
            }
        }
    }
}