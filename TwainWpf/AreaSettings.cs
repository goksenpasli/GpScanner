using System.ComponentModel;
using TwainWpf.TwainNative;

namespace TwainWpf
{
    public class AreaSettings : INotifyPropertyChanged
    {
        public AreaSettings(Units units, float top, float left, float bottom, float right)
        {
            _units = units;
            _top = top;
            _left = left;
            _bottom = bottom;
            _right = right;
        }

        public float Bottom {
            get { return _bottom; }

            private set {
                _bottom = value;
                OnPropertyChanged(nameof(Bottom));
            }
        }

        public float Left {
            get { return _left; }

            private set {
                _left = value;
                OnPropertyChanged(nameof(Left));
            }
        }

        public float Right {
            get { return _right; }

            private set {
                _right = value;
                OnPropertyChanged(nameof(Right));
            }
        }

        public float Top {
            get { return _top; }

            private set {
                _top = value;
                OnPropertyChanged(nameof(Top));
            }
        }

        public Units Units {
            get { return _units; }

            set {
                _units = value;
                OnPropertyChanged(nameof(Units));
            }
        }

        private float _bottom;

        private float _left;

        private float _right;

        private float _top;

        private Units _units;

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged = delegate
        {
        };

        protected void OnPropertyChanged(string propertyName) => PropertyChanged(this, new PropertyChangedEventArgs(propertyName));

        #endregion INotifyPropertyChanged Members
    }
}