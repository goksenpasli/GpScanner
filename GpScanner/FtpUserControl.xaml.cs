using Extensions;
using GpScanner.Properties;
using GpScanner.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TwainControl;

namespace GpScanner;

/// <summary>
/// Interaction logic for FtpUserControl.xaml
/// </summary>
public partial class FtpUserControl : UserControl, INotifyPropertyChanged
{
    private double copyProgressValue;
    private DriveInfo selectedRemovableDrive;
    private readonly string AppName = Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Title;
    public FtpUserControl()
    {
        InitializeComponent();

        CopyToDrive = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Scanner scanner && File.Exists(scanner.FileName))
                {
                    string path = $"{SelectedRemovableDrive.RootDirectory.Name}{Path.GetFileName(scanner.FileName)}";
                    if (!File.Exists(path))
                    {
                        await CopyFileAsync(scanner.FileName, path, false, progress => CopyProgressValue = progress);
                        return;
                    }

                    if (MessageBox.Show($"{Translation.GetResStringValue("FILE")} {Translation.GetResStringValue("UPDATE")}",AppName, MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) ==
                    MessageBoxResult.Yes)
                    {
                        await CopyFileAsync(scanner.FileName, path, true, progress => CopyProgressValue = progress);
                    }
                }
            },
            parameter => SelectedRemovableDrive?.IsReady == true);

        UploadFtp = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Scanner scanner && File.Exists(scanner.FileName))
                {
                    string[] ftpdata = Settings.Default.SelectedFtp.Split('|');
                    await FtpUploadAsync(ftpdata[0], ftpdata[1], ftpdata[2], scanner.FileName, progress => scanner.FtpLoadProgressValue = progress);
                }
            },
            parameter => !string.IsNullOrWhiteSpace(Settings.Default.SelectedFtp));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public double CopyProgressValue
    {
        get => copyProgressValue;
        set
        {
            if (copyProgressValue != value)
            {
                copyProgressValue = value;
                OnPropertyChanged(nameof(CopyProgressValue));
            }
        }
    }

    public RelayCommand<object> CopyToDrive { get; }

    public IEnumerable<DriveInfo> RemovableDrives { get; } = DriveInfo.GetDrives()?.Where(z => z.DriveType == DriveType.Removable);

    public DriveInfo SelectedRemovableDrive
    {
        get => selectedRemovableDrive;
        set
        {
            if (selectedRemovableDrive != value)
            {
                selectedRemovableDrive = value;
                OnPropertyChanged(nameof(SelectedRemovableDrive));
            }
        }
    }

    public RelayCommand<object> UploadFtp { get; }

    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async Task CopyFileAsync(string sourceFilePath, string destinationFilePath, bool overwrite, Action<double> progressCallback)
    {
        try
        {
            FileMode mode = overwrite ? FileMode.Create : FileMode.CreateNew;

            using FileStream sourceStream = new(sourceFilePath, FileMode.Open, FileAccess.Read);
            using FileStream destinationStream = new(destinationFilePath, mode, FileAccess.Write);
            byte[] buffer = new byte[4096];
            int bytesRead;
            long totalBytesRead = 0;
            long fileSize = sourceStream.Length;

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destinationStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                progressCallback(totalBytesRead / (double)fileSize);
            }
            buffer = null;
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task FtpUploadAsync(string uri, string userName, string password, string filename, Action<int> ftpProgressCallback)
    {
        try
        {
            using WebClient webClient = new();
            webClient.Credentials = new NetworkCredential(userName, password.Decrypt());
            webClient.UploadProgressChanged += (sender, args) => ftpProgressCallback(args.ProgressPercentage);
            string address = $"{uri}/{Directory.GetParent(filename).Name}{Path.GetFileName(filename)}";
            _ = await webClient.UploadFileTaskAsync(address, WebRequestMethods.Ftp.UploadFile, filename);
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show(ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}