using System.ComponentModel;
using TwainWpf.TwainNative;

namespace TwainWpf
{
    /// <summary>
    /// Page settings used for automatic document feeders scanning.
    /// </summary>
    public class PageSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// Default Page setup - A4 Letter and Portrait orientation
        /// </summary>
        public static readonly PageSettings Default = new PageSettings() { Size = PageType.UsLetter, Orientation = Orientation.Default };

        public PageSettings()
        {
            Size = PageType.UsLetter;
            Orientation = Orientation.Default;
        }

        /// <summary>
        /// Gets or sets the page orientation.
        /// </summary>
        /// <value>The orientation.</value>
        public Orientation Orientation
        {
            get => _orientation;

            set
            {
                if(value != _orientation)
                {
                    _orientation = value;
                    OnPropertyChanged(nameof(Orientation));
                }
            }
        }

        /// <summary>
        /// Gets or sets the Page Size.
        /// </summary>
        /// <value>The size.</value>
        public PageType Size
        {
            get => _size;

            set
            {
                if(value != _size)
                {
                    _size = value;
                    OnPropertyChanged("PaperSize");
                }
            }
        }

        private Orientation _orientation;

        private PageType _size;

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged = delegate
        {
        };

        protected void OnPropertyChanged(string propertyName) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
        #endregion INotifyPropertyChanged Members
    }
}