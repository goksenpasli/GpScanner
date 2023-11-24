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
    private readonly string AppName = Application.Current?.MainWindow?.Title;
    private List<TessFiles> checkedFiles;
    private bool ısFolderWritable;
    private string seçiliDil;
    private bool showHelpDesc;
    private string tessdatafolder;
    private ObservableCollection<TessFiles> tesseractFiles;

    public TesseractViewModel()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Tessdatafolder = $@"{Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName)}\tessdata";
        TesseractFiles = GetTesseractFiles(Tessdatafolder);
        IsFolderWritable = FolderWritable(Tessdatafolder);
        OcrDatas = TesseractDownloadData();
        ShowHelpDesc = TesseractFiles?.Count == 0;
        PropertyChanged += TesseractViewModel_PropertyChanged;
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
                        _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    if (File.Exists(filepath) && MessageBox.Show(Translation.GetResStringValue("DELETE"), AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(filepath);
                            TesseractFiles = GetTesseractFiles(Tessdatafolder);
                        }
                        catch (Exception ex)
                        {
                            _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
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
                            ocrData.ProgressValue = fileStream.Length / (double)response.Content.Headers.ContentLength * 100;
                        }

                        ocrData.IsEnabled = true;
                        TesseractFiles = GetTesseractFiles(Tessdatafolder);
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
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
                            _ = MessageBox.Show($"{Translation.GetResStringValue("FILE")} {Translation.GetResStringValue("EMPTY")}", AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                            File.Delete(file);
                            TesseractFiles = GetTesseractFiles(Tessdatafolder);
                        }
                    }
                }
            },
            parameter => true);

        ResetTesseractFilter = new RelayCommand<object>(parameter => TesseractView.cvs.View.Filter = null, parameter => TesseractView.cvs is not null);

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

    public RelayCommand<object> ResetTesseractFilter { get; }

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

    public bool ShowHelpDesc
    {
        get => showHelpDesc;
        set
        {
            if (showHelpDesc != value)
            {
                showHelpDesc = value;
                OnPropertyChanged(nameof(ShowHelpDesc));
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
                        string displayName = TesseractDownloadData()?.FirstOrDefault(z => z.OcrName == Path.GetFileName(filePath))?.OcrLangName;
                        TessFiles tessfiles = new() { DisplayName = displayName, Name = tessFileName, Checked = defaultTtsLang.Contains(tessFileName), FileSize = new FileInfo(filePath).Length / 1_048_576d };
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
        return
        [
            new() { OcrName = "afr.traineddata", OcrLangName = "Afrikaans" },
            new() { OcrName = "amh.traineddata", OcrLangName = "Amharic" },
            new() { OcrName = "ara.traineddata", OcrLangName = "Arabic" },
            new() { OcrName = "asm.traineddata", OcrLangName = "Assamese" },
            new() { OcrName = "aze.traineddata", OcrLangName = "Azerbaijani" },
            new() { OcrName = "aze_cyrl.traineddata", OcrLangName = "Azerbaijani (Cyrillic)" },
            new() { OcrName = "bel.traineddata", OcrLangName = "Belarusian" },
            new() { OcrName = "ben.traineddata", OcrLangName = "Bengali" },
            new() { OcrName = "bod.traineddata", OcrLangName = "Tibetan" },
            new() { OcrName = "bos.traineddata", OcrLangName = "Bosnian" },
            new() { OcrName = "bre.traineddata", OcrLangName = "Breton" },
            new() { OcrName = "bul.traineddata", OcrLangName = "Bulgarian" },
            new() { OcrName = "cat.traineddata", OcrLangName = "Catalan" },
            new() { OcrName = "ceb.traineddata", OcrLangName = "Cebuano" },
            new() { OcrName = "ces.traineddata", OcrLangName = "Czech" },
            new() { OcrName = "chi_sim.traineddata", OcrLangName = "Chinese (Simplified)" },
            new() { OcrName = "chi_sim_vert.traineddata", OcrLangName = "Chinese (Simplified, vertical)" },
            new() { OcrName = "chi_tra.traineddata", OcrLangName = "Chinese (Traditional)" },
            new() { OcrName = "chi_tra_vert.traineddata", OcrLangName = "Chinese (Traditional, vertical)" },
            new() { OcrName = "chr.traineddata", OcrLangName = "Cherokee" },
            new() { OcrName = "cos.traineddata", OcrLangName = "Corsican" },
            new() { OcrName = "cym.traineddata", OcrLangName = "Welsh" },
            new() { OcrName = "dan.traineddata", OcrLangName = "Danish" },
            new() { OcrName = "deu.traineddata", OcrLangName = "German" },
            new() { OcrName = "dzo.traineddata", OcrLangName = "Dzongkha" },
            new() { OcrName = "ell.traineddata", OcrLangName = "Greek" },
            new() { OcrName = "eng.traineddata", OcrLangName = "English" },
            new() { OcrName = "enm.traineddata", OcrLangName = "Middle English" },
            new() { OcrName = "epo.traineddata", OcrLangName = "Esperanto" },
            new() { OcrName = "equ.traineddata", OcrLangName = "Math / equation detection" },
            new() { OcrName = "est.traineddata", OcrLangName = "Estonian" },
            new() { OcrName = "eus.traineddata", OcrLangName = "Basque" },
            new() { OcrName = "fas.traineddata", OcrLangName = "Persian" },
            new() { OcrName = "fin.traineddata", OcrLangName = "Finnish" },
            new() { OcrName = "fra.traineddata", OcrLangName = "French" },
            new() { OcrName = "frk.traineddata", OcrLangName = "Frankish" },
            new() { OcrName = "frm.traineddata", OcrLangName = "Middle French" },
            new() { OcrName = "gle.traineddata", OcrLangName = "Irish" },
            new() { OcrName = "glg.traineddata", OcrLangName = "Galician" },
            new() { OcrName = "grc.traineddata", OcrLangName = "Ancient Greek" },
            new() { OcrName = "guj.traineddata", OcrLangName = "Gujarati" },
            new() { OcrName = "hat.traineddata", OcrLangName = "Haitian Creole" },
            new() { OcrName = "heb.traineddata", OcrLangName = "Hebrew" },
            new() { OcrName = "hin.traineddata", OcrLangName = "Hindi" },
            new() { OcrName = "hrv.traineddata", OcrLangName = "Croatian" },
            new() { OcrName = "hun.traineddata", OcrLangName = "Hungarian" },
            new() { OcrName = "hye.traineddata", OcrLangName = "Armenian" },
            new() { OcrName = "iku.traineddata", OcrLangName = "Inuktitut" },
            new() { OcrName = "ind.traineddata", OcrLangName = "Indonesian" },
            new() { OcrName = "isl.traineddata", OcrLangName = "Icelandic" },
            new() { OcrName = "ita.traineddata", OcrLangName = "Italian" },
            new() { OcrName = "ita_old.traineddata", OcrLangName = "Italian (Old)" },
            new() { OcrName = "jav.traineddata", OcrLangName = "Javanese" },
            new() { OcrName = "jpn.traineddata", OcrLangName = "Japanese" },
            new() { OcrName = "kan.traineddata", OcrLangName = "Kannada" },
            new() { OcrName = "kat.traineddata", OcrLangName = "Georgian" },
            new() { OcrName = "kat_old.traineddata", OcrLangName = "Georgian (Old)" },
            new() { OcrName = "kaz.traineddata", OcrLangName = "Kazakh" },
            new() { OcrName = "khm.traineddata", OcrLangName = "Khmer" },
            new() { OcrName = "kir.traineddata", OcrLangName = "Kirghiz" },
            new() { OcrName = "kmr.traineddata", OcrLangName = "Kurdish (Kurmanji)" },
            new() { OcrName = "kor.traineddata", OcrLangName = "Korean" },
            new() { OcrName = "kur.traineddata", OcrLangName = "Kurdish (Sorani)" },
            new() { OcrName = "lao.traineddata", OcrLangName = "Lao" },
            new() { OcrName = "lat.traineddata", OcrLangName = "Latin" },
            new() { OcrName = "lav.traineddata", OcrLangName = "Latvian" },
            new() { OcrName = "lit.traineddata", OcrLangName = "Lithuanian" },
            new() { OcrName = "ltz.traineddata", OcrLangName = "Luxembourgish" },
            new() { OcrName = "mal.traineddata", OcrLangName = "Malayalam" },
            new() { OcrName = "mar.traineddata", OcrLangName = "Marathi" },
            new() { OcrName = "mkd.traineddata", OcrLangName = "Macedonian" },
            new() { OcrName = "mlt.traineddata", OcrLangName = "Maltese" },
            new() { OcrName = "msa.traineddata", OcrLangName = "Malay" },
            new() { OcrName = "mya.traineddata", OcrLangName = "Burmese" },
            new() { OcrName = "nep.traineddata", OcrLangName = "Nepali" },
            new() { OcrName = "nld.traineddata", OcrLangName = "Dutch" },
            new() { OcrName = "nno.traineddata", OcrLangName = "Norwegian Nynorsk" },
            new() { OcrName = "nob.traineddata", OcrLangName = "Norwegian" },
            new() { OcrName = "oci.traineddata", OcrLangName = "Occitan" },
            new() { OcrName = "ori.traineddata", OcrLangName = "Oriya" },
            new() { OcrName = "osd.traineddata", OcrLangName = "Orientation and Script Detection" },
            new() { OcrName = "pan.traineddata", OcrLangName = "Punjabi" },
            new() { OcrName = "pol.traineddata", OcrLangName = "Polish" },
            new() { OcrName = "por.traineddata", OcrLangName = "Portuguese" },
            new() { OcrName = "pus.traineddata", OcrLangName = "Pashto" },
            new() { OcrName = "ron.traineddata", OcrLangName = "Romanian" },
            new() { OcrName = "rus.traineddata", OcrLangName = "Russian" },
            new() { OcrName = "san.traineddata", OcrLangName = "Sanskrit" },
            new() { OcrName = "sin.traineddata", OcrLangName = "Sinhala" },
            new() { OcrName = "slk.traineddata", OcrLangName = "Slovak" },
            new() { OcrName = "slv.traineddata", OcrLangName = "Slovenian" },
            new() { OcrName = "spa.traineddata", OcrLangName = "Spanish" },
            new() { OcrName = "spa_old.traineddata", OcrLangName = "Spanish (Old)" },
            new() { OcrName = "sqi.traineddata", OcrLangName = "Albanian" },
            new() { OcrName = "srp.traineddata", OcrLangName = "Serbian" },
            new() { OcrName = "srp_latn.traineddata", OcrLangName = "Serbian (Latin)" },
            new() { OcrName = "sun.traineddata", OcrLangName = "Sundanese" },
            new() { OcrName = "swa.traineddata", OcrLangName = "Swahili" },
            new() { OcrName = "swe.traineddata", OcrLangName = "Swedish" },
            new() { OcrName = "syr.traineddata", OcrLangName = "Syriac" },
            new() { OcrName = "tam.traineddata", OcrLangName = "Tamil" },
            new() { OcrName = "tat.traineddata", OcrLangName = "Tatar" },
            new() { OcrName = "tel.traineddata", OcrLangName = "Telugu" },
            new() { OcrName = "tgk.traineddata", OcrLangName = "Tajik" },
            new() { OcrName = "tgl.traineddata", OcrLangName = "Tagalog" },
            new() { OcrName = "tha.traineddata", OcrLangName = "Thai" },
            new() { OcrName = "tir.traineddata", OcrLangName = "Tigrinya" },
            new() { OcrName = "ton.traineddata", OcrLangName = "Tongan" },
            new() { OcrName = "tur.traineddata", OcrLangName = "Turkish" },
            new() { OcrName = "uig.traineddata", OcrLangName = "Uighur" },
            new() { OcrName = "ukr.traineddata", OcrLangName = "Ukrainian" },
            new() { OcrName = "urd.traineddata", OcrLangName = "Urdu" },
            new() { OcrName = "uzb.traineddata", OcrLangName = "Uzbek" },
            new() { OcrName = "uzb_cyrl.traineddata", OcrLangName = "Uzbek (Cyrillic)" },
            new() { OcrName = "vie.traineddata", OcrLangName = "Vietnamese" },
            new() { OcrName = "yor.traineddata", OcrLangName = "Yoruba" },
            new() { OcrName = "zho.traineddata", OcrLangName = "Chinese" },
            new() { OcrName = "zul.traineddata", OcrLangName = "Zulu" }];
    }

    private void TesseractViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "SeçiliDil" && !string.IsNullOrWhiteSpace(SeçiliDil) && TesseractView.cvs is not null)
        {
            TesseractView.cvs.Filter += (s, x) =>
                                        {
                                            TesseractOcrData tesseractOcrData = x.Item as TesseractOcrData;
                                            x.Accepted = tesseractOcrData.OcrLangName.Contains(SeçiliDil);
                                        };
        }
    }
}