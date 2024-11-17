namespace Extensions
{
    public class CheckBoxItem : InpcBase
    {
        public object Content
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Content));
                }
            }
        }

        public bool IsChecked
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public bool IsEnabled
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        } = true;

        public string Name
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }
    }
}
