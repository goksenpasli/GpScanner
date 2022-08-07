using System;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace GpScanner.ViewModel
{
    public static class StillImageHelper
    {
        public static string DEVICE_PREFIX = "/StiDevice:";

        public static bool ShouldScan { get; set; }

        public static void Register()
        {
            try
            {
                string exe = Assembly.GetEntryAssembly().Location;

                using RegistryKey key1 = Registry.LocalMachine.CreateSubKey(REGKEY_AUTOPLAY_HANDLER_GPSCANNER);
                key1.SetValue("Action", "Scan with GpScanner");
                key1.SetValue("CLSID", "WIACLSID");
                key1.SetValue("DefaultIcon", "sti.dll,0");
                key1.SetValue("InitCmdLine", $"/WiaCmd;{exe} /StiDevice:%1 /StiEvent:%2;");
                key1.SetValue("Provider", "GpScanner");

                using RegistryKey key2 = Registry.LocalMachine.CreateSubKey(REGKEY_STI_APP);
                key2.SetValue("GpScanner", exe);

                using RegistryKey key3 = Registry.LocalMachine.CreateSubKey(REGKEY_STI_EVENT_GPSCANNER);
                key3.SetValue("Cmdline", $"{exe} /StiDevice:%1 /StiEvent:%2");
                key3.SetValue("Desc", "Scan with GpScanner");
                key3.SetValue("Icon", $"{exe},0");
                key3.SetValue("Name", "GpScanner");
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
        }

        public static void Unregister()
        {
            try
            {
                Registry.LocalMachine.DeleteSubKey(REGKEY_AUTOPLAY_HANDLER_GPSCANNER, false);
                using RegistryKey key2 = Registry.LocalMachine.OpenSubKey(REGKEY_STI_APP, true);
                key2?.DeleteValue("GpScanner", false);

                Registry.LocalMachine.DeleteSubKey(REGKEY_STI_EVENT_GPSCANNER, false);

                RegistryKey events = Registry.LocalMachine.OpenSubKey(REGKEY_IMAGE_EVENTS, true);
                if (events != null)
                {
                    foreach (string eventType in events.GetSubKeyNames())
                    {
                        events.DeleteSubKey(eventType + @"\{1C3A7177-F3A7-439E-BE47-E304A185F932}", false);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
        }

        private const string REGKEY_AUTOPLAY_HANDLER_GPSCANNER = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\Handlers\WIA_{1c3a7177-f3a7-439e-be47-e304a185f932}";

        private const string REGKEY_IMAGE_EVENTS = @"SYSTEM\CurrentControlSet\Control\Class\{6bdd1fc6-810f-11d0-bec7-08002be2092f}\0000\Events";

        private const string REGKEY_STI_APP = @"SOFTWARE\Microsoft\Windows\CurrentVersion\StillImage\Registered Applications";

        private const string REGKEY_STI_EVENT_GPSCANNER = @"SYSTEM\CurrentControlSet\Control\StillImage\Events\STIProxyEvent\{1c3a7177-f3a7-439e-be47-e304a185f932}";
    }
}