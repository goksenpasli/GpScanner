using Extensions;
using GpScanner.Properties;
using Ocr;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using TwainControl;
using InpcBase = Extensions.InpcBase;

namespace GpScanner.ViewModel;

public class TesseractViewModel : InpcBase, IDataErrorInfo

{
    private List<TessFiles> checkedFiles;
    private bool ısFolderWritable;
    private bool showAllLanguages;
    private string tessdatafolder;
    private ObservableCollection<TessFiles> tesseractFiles;

    public TesseractViewModel()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Tessdatafolder = $@"{Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName)}\tessdata";
        TesseractFiles = GetTesseractFiles(Tessdatafolder);
        IsFolderWritable = FolderWritable(Tessdatafolder);
        OcrDatas = TesseractDownloadData();

        TesseractDataFilesDownloadLink = new RelayCommand<object>(
            parameter =>
            {
                string path = parameter as string;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    try
                    {
                        _ = Process.Start(path);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            },
            parameter => true);

        TesseractRemove = new RelayCommand<object>(
            parameter =>
            {
                if (parameter is TessFiles tessFile)
                {
                    string filepath = $"{Tessdatafolder}\\{tessFile.Name}.traineddata";
                    if (File.Exists(filepath) && MessageBox.Show(Translation.GetResStringValue("DELETE"), Application.Current?.MainWindow?.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(filepath);
                            TesseractFiles = GetTesseractFiles(Tessdatafolder);
                        }
                        catch (Exception ex)
                        {
                            _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            },
            parameter => parameter is TessFiles tessFile && !tessFile.Checked && TesseractFiles?.Count > 1);

        TesseractDownload = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is TesseractOcrData ocrData)
                {
                    string datafile = Path.Combine(tessdatafolder, ocrData.OcrName);

                    try
                    {
                        using HttpClient client = new();
                        _ = client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537");

                        HttpResponseMessage response = await client.GetAsync($"https://github.com/tesseract-ocr/tessdata_best/raw/main/{ocrData.OcrName}", HttpCompletionOption.ResponseHeadersRead);
                        _ = response.EnsureSuccessStatusCode();

                        using Stream contentStream = await response.Content.ReadAsStreamAsync();
                        using FileStream fileStream = new(datafile, FileMode.Create, FileAccess.Write, FileShare.None);

                        const int bufferSize = 8192;
                        byte[] buffer = new byte[bufferSize];
                        int bytesRead;
                        ocrData.IsEnabled = false;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, bufferSize)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            ocrData.ProgressValue =
                                fileStream.Length / (double)response.Content.Headers.ContentLength * 100;
                        }

                        ocrData.IsEnabled = true;
                        TesseractFiles = GetTesseractFiles(Tessdatafolder);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                        if (File.Exists(datafile))
                        {
                            File.Delete(datafile);
                        }
                    }
                    finally
                    {
                        ocrData.IsEnabled = true;
                        string file = Path.Combine(tessdatafolder, ocrData.OcrName);
                        if (File.Exists(file) && new FileInfo(file).Length == 0)
                        {
                            _ = MessageBox.Show($"{Translation.GetResStringValue("FILE")} {Translation.GetResStringValue("EMPTY")}", Application.Current?.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                            File.Delete(file);
                            TesseractFiles = GetTesseractFiles(Tessdatafolder);
                        }
                    }
                }
            },
            parameter => true);
        PropertyChanged += TesseractViewModel_PropertyChanged;

        if (PdfGeneration.Scanner is not null)
        {
            PdfGeneration.Scanner.SelectedTtsLanguage = Settings.Default.DefaultTtsLang;
        }
    }

    public List<TessFiles> CheckedFiles
    {
        get => checkedFiles;
        set
        {
            if (checkedFiles != value)
            {
                checkedFiles = value;
                OnPropertyChanged(nameof(CheckedFiles));
            }
        }
    }

    public string Error => string.Empty;

    public bool IsFolderWritable
    {
        get => ısFolderWritable;
        set
        {
            if (ısFolderWritable != value)
            {
                ısFolderWritable = value;
                OnPropertyChanged(nameof(IsFolderWritable));
            }
        }
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

    public string Tessdatafolder
    {
        get => tessdatafolder;

        set
        {
            if (tessdatafolder != value)
            {
                tessdatafolder = value;
                OnPropertyChanged(nameof(Tessdatafolder));
            }
        }
    }

    public ICommand TesseractDataFilesDownloadLink { get; }

    public ICommand TesseractDownload { get; }

    public ObservableCollection<TessFiles> TesseractFiles
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

    public RelayCommand<object> TesseractRemove { get; }

    public string this[string columnName] => columnName switch
    {
        "TesseractFiles" when TesseractFiles?.Count(z => z.Checked) == 0 || string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) => $"{Translation.GetResStringValue("TESSLANGSELECT")}",
        "IsFolderWritable" when !IsFolderWritable => $"{Translation.GetResStringValue("NO ACTION")}",
        _ => null
    };

    public ObservableCollection<TessFiles> GetTesseractFiles(string tesseractfolder)
    {
        if (Directory.Exists(tesseractfolder))
        {
            string[] defaultTtsLang = Settings.Default.DefaultTtsLang.Split('+');
            ObservableCollection<TessFiles> tesseractfiles = new(
                Directory.EnumerateFiles(tesseractfolder, "*.traineddata")
                    .Select(
                        filePath =>
                        {
                            string tessFileName = Path.GetFileNameWithoutExtension(filePath);
                            TessFiles tessfiles = new() { Name = tessFileName, Checked = defaultTtsLang.Contains(tessFileName), FileSize = new FileInfo(filePath).Length / 1_048_576d };
                            tessfiles.PropertyChanged += Tess_PropertyChanged;
                            return tessfiles;
                        }));
            CheckedFiles = tesseractfiles?.Where(item => item.Checked).ToList();
            return tesseractfiles;
        }

        return null;
    }

    private bool FolderWritable(string folderPath)
    {
        string tempFilePath = Path.Combine(folderPath, Path.GetRandomFileName());
        try
        {
            FileStream fs = File.Create(tempFilePath);
            fs.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private void Tess_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Checked")
        {
            CheckedFiles = TesseractFiles?.Where(item => item.Checked).ToList();
            if (!CheckedFiles.Any())
            {
                Settings.Default.BatchFolder = string.Empty;
                PdfGeneration.Scanner.ApplyPdfSaveOcr = false;
                PdfGeneration.Scanner.ApplyDataBaseOcr = false;
            }

            PdfGeneration.Scanner.SelectedTtsLanguage = Settings.Default.DefaultTtsLang = string.Join("+", CheckedFiles.Select(item => item.Name));
            OnPropertyChanged(nameof(TesseractFiles));
        }
    }

    private ObservableCollection<TesseractOcrData> TesseractDownloadData()
    {
        return new ObservableCollection<TesseractOcrData>
        {
            new() { OcrName = "afr.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "amh.traineddata" },
            new() { OcrName = "ara.traineddata" },
            new() { OcrName = "asm.traineddata" },
            new() { OcrName = "aze.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "aze_cyrl.traineddata" },
            new() { OcrName = "bel.traineddata" },
            new() { OcrName = "ben.traineddata" },
            new() { OcrName = "bod.traineddata" },
            new() { OcrName = "bos.traineddata" },
            new() { OcrName = "bre.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "bul.traineddata" },
            new() { OcrName = "cat.traineddata" },
            new() { OcrName = "ceb.traineddata" },
            new() { OcrName = "ces.traineddata" },
            new() { OcrName = "chi_sim.traineddata" },
            new() { OcrName = "chi_sim_vert.traineddata" },
            new() { OcrName = "chi_tra.traineddata" },
            new() { OcrName = "chi_tra_vert.traineddata" },
            new() { OcrName = "chr.traineddata" },
            new() { OcrName = "cos.traineddata" },
            new() { OcrName = "cym.traineddata" },
            new() { OcrName = "dan.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "dan_frak.traineddata" },
            new() { OcrName = "deu.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "deu_frak.traineddata" },
            new() { OcrName = "div.traineddata" },
            new() { OcrName = "dzo.traineddata" },
            new() { OcrName = "ell.traineddata" },
            new() { OcrName = "eng.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "enm.traineddata" },
            new() { OcrName = "epo.traineddata" },
            new() { OcrName = "equ.traineddata" },
            new() { OcrName = "est.traineddata" },
            new() { OcrName = "eus.traineddata" },
            new() { OcrName = "fao.traineddata" },
            new() { OcrName = "fas.traineddata" },
            new() { OcrName = "fil.traineddata" },
            new() { OcrName = "fin.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "fra.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "frk.traineddata" },
            new() { OcrName = "frm.traineddata" },
            new() { OcrName = "fry.traineddata" },
            new() { OcrName = "gla.traineddata" },
            new() { OcrName = "gle.traineddata" },
            new() { OcrName = "glg.traineddata" },
            new() { OcrName = "grc.traineddata" },
            new() { OcrName = "guj.traineddata" },
            new() { OcrName = "hat.traineddata" },
            new() { OcrName = "heb.traineddata" },
            new() { OcrName = "hin.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "hrv.traineddata" },
            new() { OcrName = "hun.traineddata" },
            new() { OcrName = "hye.traineddata" },
            new() { OcrName = "iku.traineddata" },
            new() { OcrName = "ind.traineddata" },
            new() { OcrName = "isl.traineddata" },
            new() { OcrName = "ita.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "jav.traineddata" },
            new() { OcrName = "jpn.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "jpn_vert.traineddata" },
            new() { OcrName = "kan.traineddata" },
            new() { OcrName = "kat.traineddata" },
            new() { OcrName = "kat_old.traineddata" },
            new() { OcrName = "kaz.traineddata" },
            new() { OcrName = "khm.traineddata" },
            new() { OcrName = "kir.traineddata" },
            new() { OcrName = "kmr.traineddata" },
            new() { OcrName = "kor.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "kor_vert.traineddata" },
            new() { OcrName = "lao.traineddata" },
            new() { OcrName = "lat.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "lav.traineddata" },
            new() { OcrName = "lit.traineddata" },
            new() { OcrName = "ltz.traineddata" },
            new() { OcrName = "mal.traineddata" },
            new() { OcrName = "mar.traineddata" },
            new() { OcrName = "mkd.traineddata" },
            new() { OcrName = "mlt.traineddata" },
            new() { OcrName = "mon.traineddata" },
            new() { OcrName = "mri.traineddata" },
            new() { OcrName = "msa.traineddata" },
            new() { OcrName = "mya.traineddata" },
            new() { OcrName = "nep.traineddata" },
            new() { OcrName = "nld.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "nor.traineddata" },
            new() { OcrName = "oci.traineddata" },
            new() { OcrName = "ori.traineddata" },
            new() { OcrName = "osd.traineddata" },
            new() { OcrName = "pan.traineddata" },
            new() { OcrName = "pol.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "por.traineddata" },
            new() { OcrName = "pus.traineddata" },
            new() { OcrName = "que.traineddata" },
            new() { OcrName = "ron.traineddata" },
            new() { OcrName = "rus.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "san.traineddata" },
            new() { OcrName = "sin.traineddata" },
            new() { OcrName = "slk.traineddata" },
            new() { OcrName = "slk_frak.traineddata" },
            new() { OcrName = "slv.traineddata" },
            new() { OcrName = "snd.traineddata" },
            new() { OcrName = "spa.traineddata" },
            new() { OcrName = "sqi.traineddata" },
            new() { OcrName = "srp.traineddata" },
            new() { OcrName = "srp_latn.traineddata" },
            new() { OcrName = "sun.traineddata" },
            new() { OcrName = "swa.traineddata" },
            new() { OcrName = "swe.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "syr.traineddata" },
            new() { OcrName = "tam.traineddata" },
            new() { OcrName = "tat.traineddata" },
            new() { OcrName = "tel.traineddata" },
            new() { OcrName = "tgk.traineddata" },
            new() { OcrName = "tgl.traineddata" },
            new() { OcrName = "tha.traineddata" },
            new() { OcrName = "tir.traineddata" },
            new() { OcrName = "ton.traineddata" },
            new() { OcrName = "tur.traineddata", IsVisible = Visibility.Visible },
            new() { OcrName = "uig.traineddata" },
            new() { OcrName = "ukr.traineddata" },
            new() { OcrName = "urd.traineddata" },
            new() { OcrName = "uzb.traineddata" },
            new() { OcrName = "uzb_cyrl.traineddata" },
            new() { OcrName = "vie.traineddata" },
            new() { OcrName = "yid.traineddata" },
            new() { OcrName = "yor.traineddata" }
        };
    }

    private void TesseractViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "ShowAllLanguages" && ShowAllLanguages)
        {
            foreach (TesseractOcrData item in OcrDatas)
            {
                item.IsVisible = Visibility.Visible;
            }
        }
    }
}