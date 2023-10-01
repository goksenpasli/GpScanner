using Extensions;
using IMAPI2;
using IMAPI2FS;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using MessageBox = System.Windows.MessageBox;

namespace DvdBurner
{
    [TemplatePart(Name = "Lb", Type = typeof(ListBox))]
    public class Burner : Control, INotifyPropertyChanged
    {
        private const string WarnText = "İşlem Sürüyor. Bitmesini Bekleyin.";
        private static Task Burntask;
        private static Task Erasetask;
        private readonly string AppName = Application.Current?.MainWindow?.Title;
        private string actionText;
        private SolidColorBrush actionTextForeground = Brushes.Black;
        private string cdLabel = DateTime.Now.ToString();
        private long discMaxSize = (int)DiscSizes.CD;
        private Dictionary<string, string> drives;
        private bool eject = true;
        private ObservableCollection<string> files = new();
        private bool ısCdWriterAvailable = true;
        private ListBox lb;
        private Brush progressForegroundBrush;
        private bool progressIndeterminate;
        private double progressValue;
        private DiscSizes selectedDiscSize = DiscSizes.CD;
        private dynamic selectedDrive;
        private long totalFileSize;

        static Burner() { DefaultStyleKeyProperty.OverrideMetadata(typeof(Burner), new FrameworkPropertyMetadata(typeof(Burner))); }

        public Burner()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                Files = ["File", "File", "File"];
            }
            PropertyChanged += Burner_PropertyChanged;

            MsftDiscMaster2 g_DiscMaster = new();
            if (!g_DiscMaster.IsSupportedEnvironment)
            {
                IsCdWriterAvailable = false;
                return;
            }

            Drives = GetCdWriters(g_DiscMaster);

            BurnDvd = new RelayCommand<object>(
                parameter =>
                {
                    if (Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText, AppName);
                        return;
                    }

                    dynamic recorder = null;
                    dynamic Stream;
                    Burntask = Task.Run(
                        () =>
                        {
                            try
                            {
                                recorder = new MsftDiscRecorder2();
                                recorder.InitializeDiscRecorder(SelectedDrive);

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
                                foreach (string file in Files.Where(file => File.Exists(file)))
                                {
                                    string fileName = Path.GetFileName(file);
                                    rootDirectory?.AddFile(fileName, ManagedIStream.Create(new FileStream(file, FileMode.Open, FileAccess.Read)));
                                }

                                dynamic result = FSI.CreateResultImage();
                                Stream = result?.ImageStream;
                                dataWriter.ForceOverwrite = true;
                                dataWriter.Write(Stream);
                            }
                            catch (Exception ex)
                            {
                                ActionText = ex.Message.Trim();
                                ActionTextForeground = Brushes.Red;
                            }
                            finally
                            {
                                if (Eject)
                                {
                                    recorder?.EjectMedia();
                                }
                            }
                        });
                },
                parameter => !string.IsNullOrWhiteSpace(CdLabel) && Files?.Any() == true && SelectedDrive != null);

            SelectBurnDir = new RelayCommand<object>(
                parameter =>
                {
                    if (Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText, AppName);
                        return;
                    }

                    OpenFileDialog openFileDialog = new() { Multiselect = true, Filter = "Tüm Dosyalar (*.*)|*.*", };
                    if (openFileDialog.ShowDialog() == true)
                    {
                        ActionTextForeground = Brushes.Black;
                        ActionText = string.Empty;
                        AddFiles(openFileDialog.FileNames);
                        UpdateProgressFileSize();
                    }
                },
                parameter => true);

            RemoveFile = new RelayCommand<object>(
                parameter =>
                {
                    if (Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText, AppName);
                        return;
                    }
                    if (parameter is string file && Files.Remove(file))
                    {
                        UpdateProgressFileSize();
                    }
                },
                parameter => true);

            EraseDvd = new RelayCommand<object>(
                parameter =>
                {
                    if (Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText, AppName);
                        return;
                    }

                    Erasetask = Task.Run(
                        () =>
                        {
                            MsftDiscRecorder2 recorder = null;
                            try
                            {
                                MsftDiscFormat2Erase discFormatErase = null;
                                if (g_DiscMaster.Count > 0)
                                {
                                    ActionText = "Medya Siliniyor.";
                                    recorder = new MsftDiscRecorder2();
                                    recorder.InitializeDiscRecorder(SelectedDrive);
                                    discFormatErase = new MsftDiscFormat2Erase { Recorder = recorder, ClientName = AppName, FullErase = false };
                                    discFormatErase.EraseMedia();
                                }
                            }
                            catch (Exception ex)
                            {
                                ActionText = ex.Message.Trim();
                                ActionTextForeground = Brushes.Red;
                            }
                            finally
                            {
                                if (Eject)
                                {
                                    recorder?.EjectMedia();
                                }
                            }
                        });
                },
                parameter => SelectedDrive != null);

            GetSupportedDiscFormats = new RelayCommand<object>(
                parameter =>
                {
                    dynamic recorder = new MsftDiscRecorder2();
                    recorder.InitializeDiscRecorder(SelectedDrive);
                    IEnumerable<int> values = Enum.GetValues(typeof(IMAPI_PROFILE_TYPE)).OfType<IMAPI_PROFILE_TYPE>().Select(z => (int)z);
                    List<string> supportedformats =
                    [
                        .. from object supportedMediaType in (object[])recorder.SupportedProfiles
                           where values.Contains((int)supportedMediaType)
                           select Enum.GetName(typeof(IMAPI_PROFILE_TYPE), supportedMediaType),
                    ];
                    _ = MessageBox.Show(string.Join("\n", supportedformats), AppName);
                    recorder = null;
                },
                parameter => SelectedDrive != null);

            RemoveAllFile = new RelayCommand<object>(
                parameter =>
                {
                    Files.Clear();
                    UpdateProgressFileSize();
                },
                parameter => Files?.Any() == true);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ActionText
        {
            get => actionText;

            set
            {
                if (actionText != value)
                {
                    actionText = value;
                    OnPropertyChanged(nameof(ActionText));
                }
            }
        }

        public SolidColorBrush ActionTextForeground
        {
            get => actionTextForeground;

            set
            {
                if (actionTextForeground != value)
                {
                    actionTextForeground = value;
                    OnPropertyChanged(nameof(ActionTextForeground));
                }
            }
        }

        public RelayCommand<object> BurnDvd { get; }

        public string CdLabel
        {
            get => cdLabel;

            set
            {
                if (cdLabel != value)
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
                if (discMaxSize != value)
                {
                    discMaxSize = value;
                    OnPropertyChanged(nameof(DiscMaxSize));
                }
            }
        }

        public Dictionary<string, string> Drives
        {
            get => drives;
            set
            {
                if (drives != value)
                {
                    drives = value;
                    OnPropertyChanged(nameof(Drives));
                }
            }
        }

        public bool Eject
        {
            get => eject;

            set
            {
                if (eject != value)
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
                if (files != value)
                {
                    files = value;
                    OnPropertyChanged(nameof(Files));
                }
            }
        }

        public RelayCommand<object> GetSupportedDiscFormats { get; }

        public bool IsCdWriterAvailable
        {
            get => ısCdWriterAvailable;
            set
            {
                if (ısCdWriterAvailable != value)
                {
                    ısCdWriterAvailable = value;
                    OnPropertyChanged(nameof(IsCdWriterAvailable));
                }
            }
        }

        public Brush ProgressForegroundBrush
        {
            get => progressForegroundBrush;
            set
            {
                if (progressForegroundBrush != value)
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
                if (progressIndeterminate != value)
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
                if (progressValue != value)
                {
                    progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        public RelayCommand<object> RemoveAllFile { get; }

        public RelayCommand<object> RemoveFile { get; }

        public RelayCommand<object> SelectBurnDir { get; }

        public DiscSizes SelectedDiscSize
        {
            get => selectedDiscSize;
            set
            {
                if (selectedDiscSize != value)
                {
                    selectedDiscSize = value;
                    OnPropertyChanged(nameof(SelectedDiscSize));
                }
            }
        }

        public dynamic SelectedDrive
        {
            get => selectedDrive;
            set
            {
                if (selectedDrive != value)
                {
                    selectedDrive = value;
                    OnPropertyChanged(nameof(SelectedDrive));
                }
            }
        }

        public long TotalFileSize
        {
            get => totalFileSize;
            set
            {
                if (totalFileSize != value)
                {
                    totalFileSize = value;
                    OnPropertyChanged(nameof(TotalFileSize));
                }
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            lb = GetTemplateChild("Lb") as ListBox;
            if (lb != null)
            {
                lb.Drop -= Listbox_Drop;
                lb.Drop += Listbox_Drop;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void AddFiles(string[] files)
        {
            foreach (string item in files)
            {
                if (!Files.Select(Path.GetFileName).Contains(Path.GetFileName(item)))
                {
                    Files.Add(item);
                }
                else
                {
                    ActionTextForeground = Brushes.Red;
                    ActionText = "Aynı İsimde Dosya Var.";
                }
            }
        }

        private void Burner_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "SelectedDiscSize")
            {
                DiscMaxSize = (int)SelectedDiscSize;
                ProgressForegroundBrush = TotalFileSize > (int)SelectedDiscSize ? Brushes.Red : Brushes.Green;
            }
        }

        private void DataWriter_Update(dynamic @object, dynamic progress)
        {
            try
            {
                switch ((int)progress.CurrentAction)
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
                        totalSectors = progress.SectorCount;
                        startLba = progress.StartLba;
                        lastWrittenLba = progress.LastWrittenLba;
                        writtenSectors = lastWrittenLba - startLba;
                        ActionText = FormatPercent(Convert.ToDecimal(writtenSectors) / Convert.ToDecimal(totalSectors));
                        break;

                    default:
                        ActionText = "Bilinmeyen İşlem." + progress?.CurrentAction.ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                ActionText = $"Hata{ex.Message}";
            }
        }

        private dynamic FormatPercent(dynamic d)
        {
            ProgressValue = (double)d;
            return d.ToString("0%");
        }

        private Dictionary<string, string> GetCdWriters(dynamic discMaster)
        {
            Dictionary<string, string> listdrives = [];
            dynamic discRecorder;
            for (int i = 0; i < discMaster.Count; i++)
            {
                discRecorder = new MsftDiscRecorder2();
                dynamic uniqueId = discMaster?.Item[i];
                discRecorder.InitializeDiscRecorder(uniqueId);
                string volumePathName = discRecorder?.VolumePathNames[0];
                string productId = discRecorder.ProductId;
                listdrives.Add($"{volumePathName} {productId}", uniqueId);
            }
            return listdrives;
        }

        private long GetTotalFileSizeMB(string[] files) => files.Aggregate(0L, (accumulator, item) => accumulator += new FileInfo(item).Length) / 1024 / 1024;

        private void Listbox_Drop(object sender, DragEventArgs e)
        {
            string[] droppedfiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedfiles?.Length > 0)
            {
                AddFiles(droppedfiles);
                UpdateProgressFileSize();
            }
        }

        private void UpdateProgressFileSize()
        {
            TotalFileSize = GetTotalFileSizeMB([.. Files]);
            ProgressForegroundBrush = TotalFileSize > (int)SelectedDiscSize ? Brushes.Red : Brushes.Green;
        }
    }
}