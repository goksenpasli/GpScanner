using System.ComponentModel;
using TwainWpf.TwainNative;

namespace TwainWpf
{
    /// <summary>
    /// Settings for hardware image rotation.  Includes hardware deskewing detection
    /// </summary>
    public class RotationSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets or sets a value indicating whether [automatic border detection].
        /// </summary>
        /// <value>
        /// <c>true</c> if [automatic border detection]; otherwise, <c>false</c>.
        /// </value>
        public bool AutomaticBorderDetection
        {
            get => _automaticBorderDetection;

            set
            {
                if(value != _automaticBorderDetection)
                {
                    _automaticBorderDetection = value;
                    OnPropertyChanged(nameof(AutomaticBorderDetection));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [automatic deskew].
        /// </summary>
        /// <value><c>true</c> if [automatic deskew]; otherwise, <c>false</c>.</value>
        public bool AutomaticDeskew
        {
            get => _automaticDeskew;

            set
            {
                if(value != _automaticDeskew)
                {
                    _automaticDeskew = value;
                    OnPropertyChanged(nameof(AutomaticDeskew));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [automatic rotate].
        /// </summary>
        /// <value><c>true</c> if [automatic rotate]; otherwise, <c>false</c>.</value>
        public bool AutomaticRotate
        {
            get => _automaticRotate;

            set
            {
                if(value != _automaticRotate)
                {
                    _automaticRotate = value;
                    OnPropertyChanged(nameof(AutomaticRotate));
                }
            }
        }

        /// <summary>
        /// Gets or sets the flip side rotation.
        /// </summary>
        /// <value>The flip side rotation.</value>
        public FlipRotation FlipSideRotation
        {
            get => _flipSideRotation;

            set
            {
                if(value != _flipSideRotation)
                {
                    _flipSideRotation = value;
                    OnPropertyChanged(nameof(FlipSideRotation));
                }
            }
        }

        private bool _automaticBorderDetection;

        private bool _automaticDeskew;

        private bool _automaticRotate;

        private FlipRotation _flipSideRotation;

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged = delegate
        {
        };

        protected void OnPropertyChanged(string propertyName)
        { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
        #endregion INotifyPropertyChanged Members
    }
}