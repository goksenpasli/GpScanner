using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Extensions;
using IMAPI2;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using MessageBox = System.Windows.MessageBox;

namespace DvdBurner
{
    public class Burner : Control, INotifyPropertyChanged
    {
        public static readonly DependencyProperty BurnDirectoryProperty = DependencyProperty.Register("BurnDirectory", typeof(string), typeof(Burner), new PropertyMetadata(string.Empty));

        static Burner()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Burner), new FrameworkPropertyMetadata(typeof(Burner)));
        }

        public Burner()
        {
            BurnDvd = new RelayCommand<object>(parameter =>
                {
                    if (Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                    {
                        _ = MessageBox.Show(WarnText);
                        return;
                    }

                    dynamic Index; // Index to recording drive.
                    dynamic recorder = null; // Recorder object
                    dynamic FolderPath; // Directory of files to burn
                    dynamic Stream; // Data stream for burning device
                    Index = 0; // First drive on the system
                    FolderPath = BurnDirectory; // Files to transfer to disc
                    Burntask = Task.Run(() =>
                    {
                        // Create a DiscMaster2 object to connect to optical drives.
                        try
                        {
                            dynamic g_DiscMaster = new MsftDiscMaster2();
                            if (g_DiscMaster.Count > 0)
                            {
                                dynamic uniqueId;
                                recorder = new MsftDiscRecorder2();
                                uniqueId = g_DiscMaster.Item(Index);
                                recorder.InitializeDiscRecorder(uniqueId);

                                // Create an image stream for a specified directory.
                                dynamic FSI; // Disc file system
                                dynamic Dir; // Root directory of the disc file system
                                dynamic dataWriter;

                                // Create a new file system image and retrieve root directory
                                FSI = new IMAPI2FS.MsftFileSystemImage();
                                Dir = FSI.Root;

                                //Create the new disc format and set the recorder
                                dataWriter = new MsftDiscFormat2Data();
                                dataWriter.Recorder = recorder;
                                dataWriter.ClientName = AppName;
                                FSI.VolumeName = CdLabel;
                                FSI.ChooseImageDefaults(recorder);
                                dataWriter.Update += new DDiscFormat2DataEvents_UpdateEventHandler(DataWriter_Update);
                                Dir.AddTree(FolderPath, false);
                                dynamic result = FSI.CreateResultImage();
                                Stream = result.ImageStream;
                                dataWriter.ForceOverwrite = true;
                                dataWriter.Write(Stream);
                            }
                        }
                        catch (Exception ex)
                        {
                            ActionText = ex.Message;
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
                parameter => Directory.Exists(BurnDirectory) && !string.IsNullOrWhiteSpace(CdLabel) &&
                             Directory.EnumerateFiles(BurnDirectory)?.Any() == true);

            SelectBurnDir = new RelayCommand<object>(parameter =>
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog
                {
                    Description = "Yazılacak Klasörü Seçin."
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    BurnDirectory = dialog.SelectedPath;
                }
            }, parameter => true);

            EraseDvd = new RelayCommand<object>(parameter =>
            {
                if (Burntask?.IsCompleted == false || Erasetask?.IsCompleted == false)
                {
                    _ = MessageBox.Show(WarnText);
                    return;
                }

                Erasetask = Task.Run(() =>
                {
                    MsftDiscRecorder2 recorder = null;
                    try
                    {
                        dynamic g_DiscMaster = new MsftDiscMaster2();
                        dynamic uniqueId;
                        dynamic Index = 0;
                        MsftDiscFormat2Erase discFormatErase = null;
                        if (g_DiscMaster.Count > 0)
                        {
                            recorder = new MsftDiscRecorder2();
                            uniqueId = g_DiscMaster.Item(Index);
                            recorder.InitializeDiscRecorder(uniqueId);
                            discFormatErase = new MsftDiscFormat2Erase
                            {
                                Recorder = recorder,
                                ClientName = AppName,
                                FullErase = false
                            };
                            discFormatErase.EraseMedia();
                        }
                    }
                    catch (Exception ex)
                    {
                        ActionText = ex.Message;
                    }
                    finally
                    {
                        if (Eject)
                        {
                            recorder?.EjectMedia();
                        }
                    }
                });
            }, parameter => true);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ActionText {
            get => actionText;

            set {
                if (actionText != value)
                {
                    actionText = value;
                    OnPropertyChanged(nameof(ActionText));
                }
            }
        }

        public string BurnDirectory {
            get => (string)GetValue(BurnDirectoryProperty);
            set => SetValue(BurnDirectoryProperty, value);
        }

        public RelayCommand<object> BurnDvd { get; }

        public string CdLabel {
            get => cdLabel;

            set {
                if (cdLabel != value)
                {
                    cdLabel = value;
                    OnPropertyChanged(nameof(CdLabel));
                }
            }
        }

        public bool Eject {
            get => eject;

            set {
                if (eject != value)
                {
                    eject = value;
                    OnPropertyChanged(nameof(Eject));
                }
            }
        }

        public RelayCommand<object> EraseDvd { get; }

        public bool ProgressIndeterminate {
            get => progressIndeterminate;

            set {
                if (progressIndeterminate != value)
                {
                    progressIndeterminate = value;
                    OnPropertyChanged(nameof(ProgressIndeterminate));
                }
            }
        }

        public double ProgressValue {
            get => progressValue;

            set {
                if (progressValue != value)
                {
                    progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }

        public RelayCommand<object> SelectBurnDir { get; }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private const string WarnText = "İşlem Sürüyor. Bitmesini Bekleyin.";

        private static Task Burntask;

        private static Task Erasetask;

        private readonly string AppName = Application.Current?.MainWindow?.Title;

        private string actionText;

        private string cdLabel = DateTime.Now.ToString();

        private bool eject = true;

        private bool progressIndeterminate;

        private double progressValue;

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
                        ActionText = "Bilinmeyen İşlem." + progress.CurrentAction.ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                ActionText = "Hata" + ex.Message;
            }
        }

        private dynamic FormatPercent(dynamic d)
        {
            ProgressValue = (double)d;
            return d.ToString("0%");
        }
    }
}