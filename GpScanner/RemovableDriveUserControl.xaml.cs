using Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TwainControl;

namespace GpScanner;

/// <summary>
/// Interaction logic for RemovableDriveUserControl.xaml
/// </summary>
public partial class RemovableDriveUserControl : UserControl, INotifyPropertyChanged
{
    private readonly string AppName = Application.Current?.Windows?.Cast<Window>()?.FirstOrDefault()?.Title;

    public RemovableDriveUserControl()
    {
        InitializeComponent();

        CopyToDrive = new RelayCommand<object>(
            async parameter =>
            {
                if (parameter is Scanner scanner && File.Exists(scanner.FileName))
                {
                    string path = $"{SelectedRemovableDrive?.RootDirectory.Name}{Path.GetFileName(scanner.FileName)}";
                    if (!File.Exists(path))
                    {
                        await CopyFileAsync(scanner.FileName, path, false, progress => CopyProgressValue = progress);
                        return;
                    }

                    if (MessageBox.Show($"{Translation.GetResStringValue("FILE")} {Translation.GetResStringValue("UPDATE")}", AppName, MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
                    {
                        await CopyFileAsync(scanner.FileName, path, true, progress => CopyProgressValue = progress);
                    }
                }
            },
            parameter => SelectedRemovableDrive?.IsReady == true);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public double CopyProgressValue
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CopyProgressValue));
            }
        }
    }

    public RelayCommand<object> CopyToDrive { get; }

    public IEnumerable<DriveInfo> RemovableDrives { get; } = DriveInfo.GetDrives()?.Where(z => z.DriveType == DriveType.Removable);

    public DriveInfo SelectedRemovableDrive
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(SelectedRemovableDrive));
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher;
        if (dispatcher?.CheckAccess() == true)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            _ = dispatcher?.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    private async Task CopyFileAsync(string sourceFilePath, string destinationFilePath, bool overwrite, Action<double> progressCallback = null)
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

            while ((bytesRead = await sourceStream?.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destinationStream?.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                progressCallback?.Invoke(totalBytesRead / (double)fileSize);
            }
            buffer = null;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(ex?.Message);
        }
    }
}