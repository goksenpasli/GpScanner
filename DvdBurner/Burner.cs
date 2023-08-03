using Extensions;
using IMAPI2;
using IMAPI2FS;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using MessageBox = System.Windows.MessageBox;

namespace DvdBurner
{
    public enum DiscSizes
    {
        CD = 700,
        MINIDVD = (int)(1.3 * 1024),
        DVD5 = (int)(4.37 * 1024),
        DVD9 = (int)(7.91 * 1024),
    }

    public class Burner : Control, INotifyPropertyChanged
    {
        private const string WarnText = "İşlem Sürüyor. Bitmesini Bekleyin.";
        private static Task Burntask;
        private static Task Erasetask;
        private readonly string AppName = Application.Current?.MainWindow?.Title;
        private string actionText;
        private string cdLabel = DateTime.Now.ToString();
        private long discMaxSize = (int)DiscSizes.CD;
        private bool eject = true;
        private ObservableCollection<string> files = new ObservableCollection<string>();
        private Brush progressForegroundBrush;
        private bool progressIndeterminate;
        private double progressValue;
        private DiscSizes selectedDiscSize = DiscSizes.CD;
        private long totalFileSize;

        static Burner() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Burner), new FrameworkPropertyMetadata(typeof(Burner))); }

        public Burner()
        {
            PropertyChanged += Burner_PropertyChanged;
            BurnDvd = new RelayCommand<object>(
                parameter =>
                {
                    if(Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText);
                        return;
                    }

                    dynamic Index;
                    dynamic recorder = null;
                    dynamic Stream;
                    Index = 0;
                    Burntask = Task.Run(
                        () =>
                        {
                            try
                            {
                                dynamic g_DiscMaster = new MsftDiscMaster2();
                                if(g_DiscMaster.Count > 0)
                                {
                                    dynamic uniqueId;
                                    recorder = new MsftDiscRecorder2();
                                    uniqueId = g_DiscMaster.Item(Index);
                                    recorder.InitializeDiscRecorder(uniqueId);

                                    dynamic FSI;
                                    dynamic dataWriter;

                                    FSI = new MsftFileSystemImage();
                                    _ = FSI.Root;

                                    dataWriter = new MsftDiscFormat2Data();
                                    dataWriter.Recorder = recorder;
                                    dataWriter.ClientName = AppName;
                                    FSI.VolumeName = CdLabel;
                                    FSI.ChooseImageDefaults(recorder);
                                    dataWriter.Update += new DDiscFormat2DataEvents_UpdateEventHandler(DataWriter_Update);
                                    IFsiDirectoryItem rootDirectory = FSI.Root;
                                    foreach(string file in Files)
                                    {
                                        string fileName = Path.GetFileName(file);
                                        rootDirectory.AddFile(fileName, ManagedIStream.Create(new FileStream(file, FileMode.Open, FileAccess.Read)));
                                    }
                                    dynamic result = FSI.CreateResultImage();
                                    Stream = result?.ImageStream;
                                    dataWriter.ForceOverwrite = true;
                                    dataWriter.Write(Stream);
                                }
                            } catch(Exception ex)
                            {
                                ActionText = ex.Message;
                            } finally
                            {
                                if(Eject)
                                {
                                    recorder?.EjectMedia();
                                }
                            }
                        });
                },
                parameter => !string.IsNullOrWhiteSpace(CdLabel) && Files?.Any() == true);

            SelectBurnDir = new RelayCommand<object>(
                parameter =>
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog { Multiselect = true, Filter = "Tüm Dosyalar (*.*)|*.*", };
                    if(openFileDialog.ShowDialog() == true)
                    {
                        foreach(string item in openFileDialog.FileNames)
                        {
                            Files.Add(item);
                        }
                        TotalFileSize = GetTotalFileSize(Files.ToArray());
                        ProgressForegroundBrush = TotalFileSize > (int)SelectedDiscSize ? Brushes.Red : Brushes.Green;
                    }
                },
                parameter => true);

            RemoveFile = new RelayCommand<object>(
                parameter =>
                {
                    if(Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText);
                        return;
                    }
                    if(parameter is string file && Files.Remove(file))
                    {
                        TotalFileSize = GetTotalFileSize(Files.ToArray());
                        ProgressForegroundBrush = TotalFileSize > (int)SelectedDiscSize ? Brushes.Red : Brushes.Green;
                    }
                },
                parameter => true);

            EraseDvd = new RelayCommand<object>(
                parameter =>
                {
                    if(Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText);
                        return;
                    }

                    Erasetask = Task.Run(
                        () =>
                        {
                            MsftDiscRecorder2 recorder = null;
                            try
                            {
                                dynamic g_DiscMaster = new MsftDiscMaster2();
                                dynamic uniqueId;
                                dynamic Index = 0;
                                MsftDiscFormat2Erase discFormatErase = null;
                                if(g_DiscMaster.Count > 0)
                                {
                                    recorder = new MsftDiscRecorder2();
                                    uniqueId = g_DiscMaster.Item(Index);
                                    recorder.InitializeDiscRecorder(uniqueId);
                                    discFormatErase = new MsftDiscFormat2Erase { Recorder = recorder, ClientName = AppName, FullErase = false };
                                    discFormatErase.EraseMedia();
                                }
                            } catch(Exception ex)
                            {
                                ActionText = ex.Message;
                            } finally
                            {
                                if(Eject)
                                {
                                    recorder?.EjectMedia();
                                }
                            }
                        });
                },
                parameter => true);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ActionText
        {
            get => actionText;

            set
            {
                if(actionText != value)
                {
                    actionText = value;
                    OnPropertyChanged(nameof(ActionText));
                }
            }
        }

        public RelayCommand<object> BurnDvd { get; }

        public string CdLabel
        {
            get => cdLabel;

            set
            {
                if(cdLabel != value)
                {
                    cdLabel = value;
                    OnPropertyChanged(nameof(CdLabel));
                }
            }
        }

        public long DiscMaxSize
        {
            get => discMaxSize;
            set
            {
                if(discMaxSize != value)
                {
                    discMaxSize = value;
                    OnPropertyChanged(nameof(DiscMaxSize));
                }
            }
        }

        public bool Eject
        {
            get => eject;

            set
            {
                if(eject != value)
                {
                    eject = value;
                    OnPropertyChanged(nameof(Eject));
                }
            }
        }

        public RelayCommand<object> EraseDvd { get; }

        public ObservableCollection<string> Files
        {
            get => files;
            set
            {
                if(files != value)
                {
                    files = value;
                    OnPropertyChanged(nameof(Files));
                }
            }
        }

        public Brush ProgressForegroundBrush
        {
            get => progressForegroundBrush;
            set
            {
                if(progressForegroundBrush != value)
                {
                    progressForegroundBrush = value;
                    OnPropertyChanged(nameof(ProgressForegroundBrush));
                }
            }
        }

        public bool ProgressIndeterminate
        {
            get => progressIndeterminate;

            set
            {
                if(progressIndeterminate != value)
                {
                    progressIndeterminate = value;
                    OnPropertyChanged(nameof(ProgressIndeterminate));
                }
            }
        }

        public double ProgressValue
        {
            get => progressValue;

            set
            {
                if(progressValue != value)
                {
                    progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        public RelayCommand<object> RemoveFile { get; }

        public RelayCommand<object> SelectBurnDir { get; }

        public DiscSizes SelectedDiscSize
        {
            get => selectedDiscSize;
            set
            {
                if(selectedDiscSize != value)
                {
                    selectedDiscSize = value;
                    OnPropertyChanged(nameof(SelectedDiscSize));
                }
            }
        }

        public long TotalFileSize
        {
            get => totalFileSize;
            set
            {
                if(totalFileSize != value)
                {
                    totalFileSize = value;
                    OnPropertyChanged(nameof(TotalFileSize));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        private void Burner_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName is "SelectedDiscSize")
            {
                DiscMaxSize = (int)SelectedDiscSize;
                ProgressForegroundBrush = TotalFileSize > (int)SelectedDiscSize ? Brushes.Red : Brushes.Green;
            }
        }

        private void DataWriter_Update(dynamic @object, dynamic progress)
        {
            try
            {
                switch((int)progress.CurrentAction)
                {
                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_CALIBRATING_POWER:
                        ActionText = "Kalibrasyon Gücü (OPC).";
                        break;

                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_COMPLETED:
                        ActionText = "Bitti.";
                        ProgressIndeterminate = false;
                        break;

                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_FINALIZATION:
                        ProgressIndeterminate = true;
                        ActionText = "Sonlandırılıyor.";
                        break;

                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_FORMATTING_MEDIA:
                        ActionText = "Medya Biçimlendiriliyor.";
                        break;

                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_INITIALIZING_HARDWARE:
                        ActionText = "Başlatılıyor.";
                        break;

                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_VALIDATING_MEDIA:
                        ActionText = "Medya Doğrulanıyor.";
                        break;

                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_VERIFYING:
                        ActionText = "Veri Doğrulanıyor.";
                        break;

                    case (int)IMAPI_FORMAT2_DATA_WRITE_ACTION.IMAPI_FORMAT2_DATA_WRITE_ACTION_WRITING_DATA:
                        dynamic totalSectors;
                        dynamic writtenSectors;
                        dynamic startLba;
                        dynamic lastWrittenLba;
                        dynamic percentDone;
                        totalSectors = progress.SectorCount;
                        startLba = progress.StartLba;
                        lastWrittenLba = progress.LastWrittenLba;
                        writtenSectors = lastWrittenLba - startLba;
                        percentDone =
                            FormatPercent(Convert.ToDecimal(writtenSectors) / Convert.ToDecimal(totalSectors));
                        ActionText = percentDone;
                        break;

                    default:
                        ActionText = "Bilinmeyen İşlem." + progress?.CurrentAction.ToString();
                        break;
                }
            } catch(Exception ex)
            {
                ActionText = $"Hata{ex.Message}";
            }
        }

        private dynamic FormatPercent(dynamic d)
        {
            ProgressValue = (double)d;
            return d.ToString("0%");
        }

        private long GetTotalFileSize(string[] files)
        {
            long totalLength = 0;
            foreach(string item in files)
            {
                FileInfo fileInfo = new FileInfo(item);
                totalLength += fileInfo.Length;
            }
            return totalLength / 1024 / 1024;
        }
    }
}