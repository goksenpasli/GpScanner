using Extensions;
using GpScanner.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Input;
using TwainControl;

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
                    OcrData ocrData = parameter as OcrData;
                    using WebClient client = new();
                    client.DownloadProgressChanged += (s, e) => ocrData.ProgressValue = e.ProgressPercentage;
                    client.DownloadFileCompleted += (s, e) => TesseractFiles = GetTesseractFiles(tessdatafolder);
                    await client.DownloadFileTaskAsync(new Uri($"https://github.com/tesseract-ocr/tessdata/raw/main/{ocrData.OcrName}"), $@"{tessdatafolder}\{ocrData.OcrName}");
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, parameter => true);

            OcrPage = new RelayCommand<object>(parameter =>
            {
                if (parameter is Scanner scanner)
                {
                    ScannedText = scanner.SeçiliResim.Resim.ToTiffJpegByteArray(ExtensionMethods.Format.Png).OcrYap(Settings.Default.DefaultTtsLang);
                }
            }, parameter => !string.IsNullOrWhiteSpace(Settings.Default.DefaultTtsLang) && parameter is Scanner scanner && scanner.SeçiliResim is not null);
        }

        public List<OcrData> OcrDatas { get; set; }

        public ICommand OcrPage { get; }

        public string ScannedText
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

        private string scannedText;

        private ObservableCollection<string> tesseractFiles;

        private ObservableCollection<string> GetTesseractFiles(string tesseractfolder)
        {
            return Directory.Exists(tesseractfolder) ? new ObservableCollection<string>(Directory.EnumerateFiles(tesseractfolder, "*.traineddata").Select(Path.GetFileNameWithoutExtension)) : null;
        }

        private List<OcrData> TesseractDownloadData()
        {
            return new()
            {
                new OcrData(){OcrName = "tur.traineddata", ProgressValue = 0, DisplayName = "TÜRKÇE TESSERACT İNDİR"},
                new OcrData(){OcrName = "eng.traineddata", ProgressValue = 0, DisplayName = "İNGİLİZCE TESSERACT İNDİR"},
                new OcrData(){OcrName = "ara.traineddata", ProgressValue = 0, DisplayName = "ARAPÇA TESSERACT İNDİR"},
                new OcrData(){OcrName = "deu.traineddata", ProgressValue = 0, DisplayName = "ALMANCA TESSERACT İNDİR"},
                new OcrData(){OcrName = "ita.traineddata", ProgressValue = 0, DisplayName = "İTALYANCA TESSERACT İNDİR"},
                new OcrData(){OcrName = "fra.traineddata", ProgressValue = 0, DisplayName = "FRANSIZCA TESSERACT İNDİR"}
            };
        }
    }
}