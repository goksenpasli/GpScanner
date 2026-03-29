using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwainControl;

namespace GpScanner.ViewModel
{
    public static class MultiFolderWatcher
    {
        public static void Watch(GpScannerViewModel gpScannerViewModel, IEnumerable<string> foldersToWatch)
        {
            foreach (string folder in foldersToWatch)
            {
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    FileSystemWatcher watcher = new(folder) { NotifyFilter = NotifyFilters.FileName, Filter = "*.pdf", IncludeSubdirectories = true, EnableRaisingEvents = true };

                    watcher.Renamed += async (s, e) => await HandleFileRenamed(gpScannerViewModel, e.OldFullPath, e.FullPath);
                }
            }
        }

        private static async Task HandleFileRenamed(GpScannerViewModel gpScannerViewModel, string oldFullPath, string newFullPath)
        {
            try
            {
                using AppDbContext context = new();
                foreach (Data item in context?.Data?.Where(z => z.FileName == oldFullPath))
                {
                    item.FileName = newFullPath;
                }
                gpScannerViewModel.RefreshItems(gpScannerViewModel.Dosyalar, item => item.FileName == oldFullPath, item => item.FileName = newFullPath);
                _ = context.SaveChanges();
            }
            catch (Exception ex)
            {
                await GpScannerViewModel.WriteToLogFile($@"{GpScannerViewModel.ProfileFolder}\{GpScannerViewModel.ErrorFile}", ex?.Message);
            }
        }
    }
}
