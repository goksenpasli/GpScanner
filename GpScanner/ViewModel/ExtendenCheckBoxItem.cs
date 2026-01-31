using Extensions;

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
}