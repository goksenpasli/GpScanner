using Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using WebPWrapper;

namespace GpScanner.ViewModel
{
    public class BatchSupportedExtensions : InpcBase, IDataErrorInfo
    {
        public BatchSupportedExtensions()
        {
            LoadFromSettings();
            AttachAutoSave();
            LoadFileFiltersCheckboxes();
        }

        public ObservableCollection<TessFiles> BatchImageFileExtensions
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(BatchImageFileExtensions));
                }
            }
        } = [ new TessFiles() { Name = ".tiff", Checked = true }, new TessFiles() { Name = ".tif", Checked = true }, new TessFiles() { Name = ".jpg", Checked = true }, new TessFiles() { Name = ".jpe", Checked = true }, new TessFiles()
        {
            Name = ".gif",
            Checked = true
        }, new TessFiles() { Name = ".jpeg", Checked = true }, new TessFiles() { Name = ".jfif", Checked = true }, new TessFiles() { Name = ".png", Checked = true }, new TessFiles() { Name = ".bmp", Checked = true }, new TessFiles()
        {
            Name = ".jb2",
            Checked = true
        }, new TessFiles() { Name = ".webp", Checked = false, Enabled = WebP.WebpDllExists }, ];

        public string Error => string.Empty;

        public bool NoneItemChecked
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(NoneItemChecked));
                }
            }
        }

        public string this[string columnName] => columnName switch
        {
            "NoneItemChecked" when NoneItemChecked => "WARNEXT",
            _ => null
        };

        public void SaveToSettings()
        {
            Properties.Settings.Default.BatchImageExtensions = string.Join("|", BatchImageFileExtensions.Where(x => x.Checked).Select(x => x.Name));
            Properties.Settings.Default.Save();
        }

        private void AttachAutoSave()
        {
            foreach (TessFiles tessfile in BatchImageFileExtensions)
            {

                tessfile.PropertyChanged += (_, e) =>
                                            {
                                                if (e.PropertyName == nameof(tessfile.Checked))
                                                {
                                                    SaveToSettings();
                                                    LoadFileFiltersCheckboxes();
                                                }
                                            };

            }
        }

        private void LoadFileFiltersCheckboxes() => NoneItemChecked = BatchImageFileExtensions.All(item => !item.Checked);

        private void LoadFromSettings()
        {
            string saved = Properties.Settings.Default.BatchImageExtensions;
            if (!string.IsNullOrWhiteSpace(saved))
            {
                HashSet<string> enabledExts = [ .. saved.Split([ '|' ], StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToLowerInvariant()) ];

                foreach (TessFiles item in BatchImageFileExtensions)
                {
                    item.Checked = enabledExts.Contains(item.Name.ToLowerInvariant());
                }
            }
            else
            {
                foreach (TessFiles item in BatchImageFileExtensions)
                {
                    item.Checked = false;
                }
            }

        }
    }
}
