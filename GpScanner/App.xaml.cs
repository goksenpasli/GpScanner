using GpScanner.Properties;
using GpScanner.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Threading;

namespace GpScanner;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private MainWindow mainwindow;
    private Thread splashThread = null;
    private SplashWindow splashWindow;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
#if !DEBUG
        Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
#endif
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(Run), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        if (Settings.Default.ShowSplash)
        {
            splashThread = new Thread(
                () =>
                {
                    splashWindow = new SplashWindow() { Topmost = true };
                    splashWindow.MouseLeftButtonDown += (s, e) => splashWindow.DragMove();
                    splashWindow.Show();
                    Dispatcher.Run();
                })
            {
                IsBackground = true
            };
            splashThread.SetApartmentState(ApartmentState.STA);
            splashThread.Start();
        }

        mainwindow = new MainWindow();
        mainwindow.Loaded += Window_Loaded;

        if (e.Args.Contains("/silent") && Settings.Default.StartWithWindows)
        {
            Settings.Default.MinimizeTray = true;
            Settings.Default.ShowTrayIcon = true;
            mainwindow.WindowState = WindowState.Minimized;
            mainwindow.ShowInTaskbar = false;
        }
        mainwindow.Show();

        foreach (string arg in e.Args)
        {
            if (!arg.StartsWith(StillImageHelper.DEVICE_PREFIX, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }
            List<Process> processes = [.. StillImageHelper.GetAllGPScannerProcess()];
            StillImageHelper.FirstLanuchScan = !processes.Any();
            foreach (Process process in processes)
            {
                StillImageHelper.ActivateProcess(process);
                if (StillImageHelper.SendMessage(process, StillImageHelper.DEVICE_PREFIX, MainWindow?.Title))
                {
                    Environment.Exit(0);
                }
            }
        }
    }

    private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            () =>
            {
                string message = $"[{DateTime.Now:G}] {e.Exception.Message}";
                string stackTrace = e.Exception.StackTrace;
                _ = MessageBox.Show(message, MainWindow?.Title ?? "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                _ = GpScannerViewModel.WriteToLogFile($@"{GpScannerViewModel.ProfileFolder}\{GpScannerViewModel.ErrorFile}", $"{message}\n{stackTrace}").ConfigureAwait(false);
            });
        e.Handled = true;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        splashWindow?.Dispatcher.Invoke(splashWindow.Close);
        mainwindow.Topmost = true;
        mainwindow.Topmost = false;
        _ = mainwindow.Activate();
    }
}