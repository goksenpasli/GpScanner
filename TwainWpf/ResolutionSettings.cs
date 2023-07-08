using System.ComponentModel;

namespace TwainWpf
{
    public enum ColourSetting
    {
        BlackAndWhite,

        GreyScale,

        Colour
    }

    public class ResolutionSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// Colour photocopier quality resolution.
        /// </summary>
        public static readonly ResolutionSettings ColourPhotocopier = new ResolutionSettings() { Dpi = 300, ColourSetting = ColourSetting.Colour };
        /// <summary>
        /// Fax quality resolution.
        /// </summary>
        public static readonly ResolutionSettings Fax = new ResolutionSettings() { Dpi = 200, ColourSetting = ColourSetting.BlackAndWhite };
        /// <summary>
        /// Photocopier quality resolution.
        /// </summary>
        public static readonly ResolutionSettings Photocopier = new ResolutionSettings() { Dpi = 300, ColourSetting = ColourSetting.GreyScale };
        private ColourSetting _colourSettings;
        private int? _dpi;

        /// <summary>
        /// The colour settings to use.
        /// </summary>
        public ColourSetting ColourSetting
        {
            get => _colourSettings;

            set
            {
                if(value != _colourSettings)
                {
                    _colourSettings = value;
                    OnPropertyChanged(nameof(ColourSetting));
                }
            }
        }

        /// <summary>
        /// The DPI to scan at. Set to null to use the current default setting.
        /// </summary>
        public int? Dpi
        {
            get => _dpi;

            set
            {
                if(value != _dpi)
                {
                    _dpi = value;
                    OnPropertyChanged(nameof(Dpi));
                }
            }
        }

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged = delegate
        {
        };

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion INotifyPropertyChanged Members
    }
}