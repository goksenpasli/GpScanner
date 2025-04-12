using Extensions;
using System.Collections.ObjectModel;
using System.Linq;

namespace GpScanner.ViewModel
{
    public class ExtendenCheckBoxItem : CheckBoxItem
    {
        public bool SearchInArchive
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SearchInArchive));
                }
            }
        }

        public bool SearchInArchiveSupported
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(SearchInArchiveSupported));
                }
            }
        }
    }

    public class FileCategory
    {
        public FileCategory()
        {
            SelectAllCommand = new RelayCommand<object>(parameter => Extensions.ToList().ForEach(i => i.IsChecked = true), parameter => Extensions?.Any() == true);
            SelectInverseCommand = new RelayCommand<object>(parameter => Extensions.ToList().ForEach(i => i.IsChecked = !i.IsChecked), parameter => Extensions?.Any() == true);
            ClearAllCommand = new RelayCommand<object>(parameter => Extensions.ToList().ForEach(i => i.IsChecked = false), parameter => Extensions?.Any() == true);
        }

        public string Category { get; internal set; }

        public RelayCommand<object> ClearAllCommand { get; }

        public ObservableCollection<ExtendenCheckBoxItem> Extensions { get; internal set; }

        public RelayCommand<object> SelectAllCommand { get; }

        public RelayCommand<object> SelectInverseCommand { get; }
    }
}