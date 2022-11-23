using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace GpScanner.ViewModel
{
    public static class StillImageHelper
    {
        public const string MSG_KILL_PIPE_SERVER = "KILL_PIPE_SERVER";

        public static string DEVICE_PREFIX = "/StiDevice:";

        public static void ActivateProcess(Process process)
        {
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                _ = SetForegroundWindow(process.MainWindowHandle);
            }
        }

        public static IEnumerable<Process> GetAllGPScannerProcess()
        {
            Process currentProcess = Process.GetCurrentProcess();
            return Process.GetProcessesByName(currentProcess.ProcessName)
                .Where(x => x.Id != currentProcess.Id)
                .OrderByDescending(x => x.StartTime);
        }

        public static void KillServer()
        {
            if (_serverRunning)
            {
                _ = SendMessage(Process.GetCurrentProcess(), MSG_KILL_PIPE_SERVER);
            }
        }

        public static void Register()
        {
            try
            {
                string exe = Assembly.GetEntryAssembly().Location;

                using RegistryKey key1 = Registry.LocalMachine.CreateSubKey(REGKEY_AUTOPLAY_HANDLER_GPSCANNER);
                key1.SetValue("Action", "Scan with GpScanner");
                key1.SetValue("CLSID", "{A55803CC-4D53-404c-8557-FD63DBA95D24}");
                key1.SetValue("DefaultIcon", "sti.dll,0");
                key1.SetValue("InitCmdLine", $"/WiaCmd;{exe} /StiDevice:%1 /StiEvent:%2;");
                key1.SetValue("Provider", "GpScanner");

                using RegistryKey key2 = Registry.LocalMachine.CreateSubKey(REGKEY_STI_APP);
                key2.SetValue("GpScanner", $"{exe} /StiDevice:%1 /StiEvent:%2");

                using RegistryKey key3 = Registry.LocalMachine.CreateSubKey(REGKEY_STI_EVENT_GPSCANNER);
                key3.SetValue("Cmdline", $"{exe} /StiDevice:%1 /StiEvent:%2");
                key3.SetValue("Desc", "Scan with GpScanner");
                key3.SetValue("Icon", "sti.dll,0");
                key3.SetValue("Name", "GpScanner");

                using RegistryKey key4 = Registry.LocalMachine.CreateSubKey(REGKEY_STI_EVENT_SCANBUTTON);
                key4.SetValue("Cmdline", $"{exe} /StiDevice:%1 /StiEvent:%2");
                key4.SetValue("Desc", "Scan with GpScanner");
                key4.SetValue("Icon", "sti.dll,0");
                key4.SetValue("Name", "GpScanner");
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
        }

        public static bool SendMessage(Process recipient, string msg)
        {
            try
            {
                using NamedPipeClientStream pipeClient = new(".", GetPipeName(recipient), PipeDirection.Out);
                pipeClient.Connect(TIMEOUT);
                StreamString streamString = new(pipeClient);
                _ = streamString.WriteString(msg);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void StartServer(Action<string> msgCallback)
        {
            if (_serverRunning)
            {
                return;
            }
            Thread thread = new(() =>
            {
                try
                {
                    using NamedPipeServerStream pipeServer = new(GetPipeName(Process.GetCurrentProcess()), PipeDirection.In);
                    while (true)
                    {
                        pipeServer.WaitForConnection();
                        StreamString streamString = new(pipeServer);
                        string msg = streamString.ReadString();
                        if (msg == MSG_KILL_PIPE_SERVER)
                        {
                            break;
                        }
                        msgCallback(msg);
                        pipeServer.Disconnect();
                    }
                }
                catch (Exception)
                {
                }
                _serverRunning = false;
            });
            _serverRunning = true;
            thread.Start();
        }

        public static void Unregister()
        {
            try
            {
                Registry.LocalMachine.DeleteSubKey(REGKEY_AUTOPLAY_HANDLER_GPSCANNER, false);
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(REGKEY_STI_APP, true);
                key?.DeleteValue("GpScanner", false);

                Registry.LocalMachine.DeleteSubKey(REGKEY_STI_EVENT_GPSCANNER, false);
                Registry.LocalMachine.DeleteSubKey(REGKEY_STI_EVENT_SCANBUTTON, false);

                RegistryKey events = Registry.LocalMachine.OpenSubKey(REGKEY_IMAGE_EVENTS, true);
                if (events != null)
                {
                    foreach (string eventType in events.GetSubKeyNames())
                    {
                        events.DeleteSubKey(eventType + @"\{143762b8-772a-47af-bae6-08e0a1d0ca89}", false);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
        }

        private const string PIPE_NAME_FORMAT = "GPSCANNER_PIPE_143762b8-772a-47af-bae6-08e0a1d0ca89_{0}";

        private const string REGKEY_AUTOPLAY_HANDLER_GPSCANNER = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{143762b8-772a-47af-bae6-08e0a1d0ca89}";

        private const string REGKEY_IMAGE_EVENTS = @"SYSTEM\CurrentControlSet\Control\Class\{6bdd1fc6-810f-11d0-bec7-08002be2092f}\0000\Events";

        private const string REGKEY_STI_APP = @"SOFTWARE\Microsoft\Windows\CurrentVersion\StillImage\Registered Applications";

        private const string REGKEY_STI_EVENT_GPSCANNER = @"SYSTEM\CurrentControlSet\Control\StillImage\Events\STIProxyEvent\{143762b8-772a-47af-bae6-08e0a1d0ca89}";

        private const string REGKEY_STI_EVENT_SCANBUTTON = @"SYSTEM\CurrentControlSet\Control\StillImage\Events\ScanButton\{143762b8-772a-47af-bae6-08e0a1d0ca89}";

        private const int TIMEOUT = 1000;

        private static bool _serverRunning;

        public static bool FirstLanuchScan { get;  set; }

        private static string GetPipeName(Process process)
        {
            return string.Format(PIPE_NAME_FORMAT, process.Id);
        }

        private class StreamString
        {
            public StreamString(Stream ioStream)
            {
                this.ioStream = ioStream;
                streamEncoding = new UnicodeEncoding();
            }

            public string ReadString()
            {
                int len = ioStream.ReadByte() * 256;
                len += ioStream.ReadByte();
                byte[] inBuffer = new byte[len];
                _ = ioStream.Read(inBuffer, 0, len);

                return streamEncoding.GetString(inBuffer);
            }

            public int WriteString(string outString)
            {
                byte[] outBuffer = streamEncoding.GetBytes(outString);
                int len = outBuffer.Length;
                if (len > ushort.MaxValue)
                {
                    len = ushort.MaxValue;
                }
                ioStream.WriteByte((byte)(len / 256));
                ioStream.WriteByte((byte)(len & 255));
                ioStream.Write(outBuffer, 0, len);
                ioStream.Flush();

                return outBuffer.Length + 2;
            }

            private readonly Stream ioStream;

            private readonly UnicodeEncoding streamEncoding;
        }
    }
}