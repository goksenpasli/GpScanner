using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace GpScanner.ViewModel
{
    public class IdleTimeIndexer
    {
        private readonly GpScannerViewModel _gpScannerViewModel;
        private readonly DispatcherTimer _idleTimer;
        private readonly int _minute;
        private bool _isIdle;

        public IdleTimeIndexer(GpScannerViewModel gpScannerViewModel, int minute)
        {
            _minute = minute;
            _gpScannerViewModel = gpScannerViewModel ?? throw new ArgumentNullException(nameof(gpScannerViewModel));
            _idleTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(5) };
        }

        public void StartIdleOcrTimer()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }
            _idleTimer.Tick += CheckIdleState;
            _idleTimer.Start();
        }

        public void StopIdleOcrTimer()
        {
            _idleTimer.Tick -= CheckIdleState;
            _idleTimer.Stop();
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private void CheckIdleState(object sender, EventArgs e)
        {
            if (_gpScannerViewModel.UnIndexedFiles?.Count == 0)
            {
                return;
            }
            int idlewaittime = _minute * 60 * 1000;
            uint idleTime = GetIdleTime();
            if (idleTime >= idlewaittime && !_isIdle)
            {
                _isIdle = true;
                OnIdleDetected();
            }
            else if (idleTime < idlewaittime && _isIdle)
            {
                _isIdle = false;
                OnUserActivityDetected();
            }
        }

        private uint GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            return GetLastInputInfo(ref lastInputInfo) ? (uint)Environment.TickCount - lastInputInfo.dwTime : 0;
        }

        private void OnIdleDetected()
        {
            _gpScannerViewModel?.LoadUnindexedFiles.Execute(null);

            if (_gpScannerViewModel?.UnindexedAllFilesOcr?.CanExecute(null) == true)
            {
                _gpScannerViewModel.UnindexedAllFilesOcr.Execute(null);
            }
        }

        private void OnUserActivityDetected() => _gpScannerViewModel?.unindexedfileocrcancellationToken?.Cancel();

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
    }
}
