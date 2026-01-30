using Extensions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using TwainControl;

namespace GpScanner.ViewModel
{
    public class SupportedExtensions : InpcBase
    {
        public SupportedExtensions()
        {
            LoadFromSettings();
            AttachAutoSave();
        }

        public ObservableCollection<FileCategory> FileCategories
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(FileCategories));
                }
            }
        } = [ new FileCategory
        {
            Category = Translation.GetResStringValue("IMG"),
            Extensions =
            [ new() { Name = ".webp", IsChecked = true }, new() { Name = ".tiff", IsChecked = true }, new() { Name = ".tif", IsChecked = true }, new() { Name = ".jpg", IsChecked = true }, new() { Name = ".jpeg", IsChecked = true }, new()
            {
                Name = ".jpe",
                IsChecked = true
            }, new() { Name = ".png", IsChecked = true }, new() { Name = ".bmp", IsChecked = true }, new() { Name = ".jb2", IsChecked = true }, new() { Name = ".jb2zip", IsChecked = true }, new() { Name = ".cbz", IsChecked = true }, new()
            {
                Name = ".cbr",
                IsChecked = true
            }, new() { Name = ".j2k", IsChecked = true } ]
        }, new FileCategory
        {
            Category = Translation.GetResStringValue("DOCUMENT"),
            Extensions =
            [ new() { Name = ".pdf", IsChecked = true }, new() { Name = ".xps", IsChecked = true }, new() { Name = ".xml", IsChecked = true }, new() { Name = ".xsl", IsChecked = true }, new() { Name = ".xslt", IsChecked = true }, new()
            {
                Name = ".xaml",
                IsChecked = true
            }, new() { Name = ".xls", IsChecked = false }, new() { Name = ".xlsx", IsChecked = false }, new() { Name = ".xlsb", IsChecked = false }, new() { Name = ".csv", IsChecked = false }, new() { Name = ".ods", IsChecked = false }, new()
            {
                Name = ".odt",
                IsChecked = false
            }, new() { Name = ".docx", IsChecked = false }, new() { Name = ".eyp", IsChecked = true } ]
        }, new FileCategory
        {
            Category = Translation.GetResStringValue("ARCHIVE"),
            Extensions =
            [ new() { Name = ".zip", IsChecked = true, SearchInArchiveSupported = true }, new() { Name = ".rar", IsChecked = true, SearchInArchiveSupported = true }, new() { Name = ".7z", IsChecked = true, SearchInArchiveSupported = true }, new()
            {
                Name = ".tar",
                IsChecked = true,
                SearchInArchiveSupported = true
            }, new() { Name = ".arj", IsChecked = true, SearchInArchiveSupported = true }, new() { Name = ".gzip", IsChecked = true, SearchInArchiveSupported = true } ]
        }, new FileCategory
        {
            Category = Translation.GetResStringValue("VID"),
            Extensions =
            [ new() { Name = ".mp4", IsChecked = true }, new() { Name = ".wmv", IsChecked = true }, new() { Name = ".mpg", IsChecked = true }, new() { Name = ".3gp", IsChecked = true }, new() { Name = ".mov", IsChecked = true }, new()
            {
                Name = ".avi",
                IsChecked = true
            }, new() { Name = ".mpeg", IsChecked = true } ]
        }, ];

        public void LoadFromSettings()
        {
            string data = Properties.Settings.Default.SupportedExtensions;

            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            foreach (string cat in data.Split([ '|' ], StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = cat.Split(':');
                if (parts.Length != 2)
                {
                    continue;
                }

                string categoryName = parts[0];
                FileCategory category = FileCategories.FirstOrDefault(c => c.Category == categoryName);

                if (category == null)
                {
                    continue;
                }

                foreach (string extPair in parts[1].Split([ ';' ], StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = extPair.Split('=');
                    if (kv.Length != 2)
                    {
                        continue;
                    }

                    ExtendenCheckBoxItem ext = category.Extensions.FirstOrDefault(e => e.Name == kv[0]);

                    _ = ext?.IsChecked = kv[1] == "1";
                }
            }
        }

        public void SaveToSettings()
        {
            StringBuilder sb = new();

            foreach (FileCategory category in FileCategories)
            {
                _ = sb.Append(category.Category);
                _ = sb.Append(':');

                foreach (ExtendenCheckBoxItem ext in category.Extensions)
                {
                    _ = sb.Append(ext.Name);
                    _ = sb.Append('=');
                    _ = sb.Append(ext.IsChecked ? '1' : '0');
                    _ = sb.Append(';');
                }

                _ = sb.Append('|');
            }

            Properties.Settings.Default.SupportedExtensions = sb.ToString();
            Properties.Settings.Default.Save();
        }

        private void AttachAutoSave()
        {
            foreach (FileCategory category in FileCategories)
            {
                foreach (ExtendenCheckBoxItem ext in category.Extensions)
                {
                    ext.PropertyChanged += (_, e) =>
                                           {
                                               if (e.PropertyName == nameof(ext.IsChecked))
                                               {
                                                   SaveToSettings();
                                               }
                                           };
                }
            }
        }
    }
}
