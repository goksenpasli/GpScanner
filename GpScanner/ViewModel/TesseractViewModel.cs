using Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Input;

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
        }

        public List<OcrData> OcrDatas { get; set; }

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

        private ObservableCollection<string> tesseractFiles;

        private ObservableCollection<string> GetTesseractFiles(string tesseractfolder)
        {
            return Directory.Exists(tesseractfolder) ? new ObservableCollection<string>(Directory.EnumerateFiles(tesseractfolder, "*.traineddata").Select(Path.GetFileNameWithoutExtension)) : null;
        }

        private List<OcrData> TesseractDownloadData()
        {
            return new()
            {
                new OcrData(){OcrName = "tur.traineddata", ProgressValue = 0, DisplayName = "TÜRKÇE TESSERACT"},
                new OcrData(){OcrName = "eng.traineddata", ProgressValue = 0, DisplayName = "ENGLISH TESSERACT"},
                new OcrData(){OcrName = "ara.traineddata", ProgressValue = 0, DisplayName = "عربي TESSERACT"},
                new OcrData(){OcrName = "deu.traineddata", ProgressValue = 0, DisplayName = "DEUTSCH TESSERACT"},
                new OcrData(){OcrName = "ita.traineddata", ProgressValue = 0, DisplayName = "ITALIANO TESSERACT"},
                new OcrData(){OcrName = "fra.traineddata", ProgressValue = 0, DisplayName = "FRANÇAIS TESSERACT"}
            };
        }
    }
}