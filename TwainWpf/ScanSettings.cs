using System.ComponentModel;

namespace TwainWpf
{
    public class ScanSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// The value to set to scan all available pages.
        /// </summary>
        public const short TransferAllPages = -1;

        public ScanSettings() { ShouldTransferAllPages = true; }

        /// <summary>
        /// Indicates if the transfer should not start when no paper was detected (e.g. by the ADF).
        /// </summary>
        public bool AbortWhenNoPaperDetectable
        {
            get => _abortWhenNoPaperDetectable;

            set
            {
                if(value != _abortWhenNoPaperDetectable)
                {
                    _abortWhenNoPaperDetectable = value;
                    OnPropertyChanged(nameof(AbortWhenNoPaperDetectable));
                }
            }
        }

        public AreaSettings Area
        {
            get => _area;

            set
            {
                if(value != _area)
                {
                    _area = value;
                    OnPropertyChanged(nameof(Area));
                }
            }
        }

        /// <summary>
        /// The page / paper settings. Set null to use current defaults.
        /// </summary>
        /// <value>The page.</value>
        public PageSettings Page
        {
            get => _page;

            set
            {
                if(value != _page)
                {
                    _page = value;
                    OnPropertyChanged(nameof(Page));
                }
            }
        }

        /// <summary>
        /// The resolution settings. Set null to use current defaults.
        /// </summary>
        public ResolutionSettings Resolution
        {
            get => _resolution;

            set
            {
                if(value != _resolution)
                {
                    _resolution = value;
                    OnPropertyChanged(nameof(Resolution));
                }
            }
        }

        /// <summary>
        /// Gets or sets the rotation.
        /// </summary>
        /// <value>The rotation.</value>
        public RotationSettings Rotation
        {
            get => _rotation;

            set
            {
                if(value != _rotation)
                {
                    _rotation = value;
                    OnPropertyChanged(nameof(Rotation));
                }
            }
        }

        /// <summary>
        /// Indicates if all pages should be transferred.
        /// </summary>
        public bool ShouldTransferAllPages { get => _transferCount == TransferAllPages; set => TransferCount = value ? TransferAllPages : (short)1; }

        /// <summary>
        /// Gets or sets a value indicating whether [show progress indicator ui]. If TRUE, the Source will display a
        /// progress indicator during acquisition and transfer, regardless of whether the Source's user interface is
        /// active. If FALSE, the progress indicator will be suppressed if the Source's user interface is inactive. The
        /// Source will continue to display device-specific instructions and error messages even with the Source user
        /// interface and progress indicators turned off.
        /// </summary>
        /// <value><c>true</c> if [show progress indicator ui]; otherwise, <c>false</c>.</value>
        public bool? ShowProgressIndicatorUi
        {
            get => _showProgressIndicatorUi;

            set
            {
                if(value != _showProgressIndicatorUi)
                {
                    _showProgressIndicatorUi = value;
                    OnPropertyChanged("ShowProgressIndicatorUI");
                }
            }
        }

        /// <summary>
        /// Indicates if the TWAIN/driver user interface should be used to pick the scan settings.
        /// </summary>
        public bool ShowTwainUi
        {
            get => _showTwainUi;

            set
            {
                if(value != _showTwainUi)
                {
                    _showTwainUi = value;
                    OnPropertyChanged("ShowTwainUI");
                }
            }
        }

        /// <summary>
        /// The number of pages to transfer.
        /// </summary>
        public short TransferCount
        {
            get => _transferCount;

            set
            {
                if(value != _transferCount)
                {
                    _transferCount = value;
                    OnPropertyChanged(nameof(TransferCount));
                    OnPropertyChanged(nameof(ShouldTransferAllPages));
                }
            }
        }

        /// <summary>
        /// Indicates if the automatic document feeder (ADF) should continue feeding document(s) to scan after the
        /// negotiated number of pages are acquired. UseDocumentFeeder must be true
        /// </summary>
        public bool? UseAutoFeeder
        {
            get => _useAutoFeeder;

            set
            {
                if(value != _useAutoFeeder)
                {
                    _useAutoFeeder = value;
                    OnPropertyChanged(nameof(UseAutoFeeder));
                }
            }
        }

        /// <summary>
        /// Indicates if the source should continue scanning without waiting for the application to request the image
        /// transfers.
        /// </summary>
        public bool? UseAutoScanCache
        {
            get => _useAutoScanCache;

            set
            {
                if(value != _useAutoScanCache)
                {
                    _useAutoScanCache = value;
                    OnPropertyChanged(nameof(UseAutoScanCache));
                }
            }
        }

        /// <summary>
        /// Indicates if the automatic document feeder (ADF) should be the source of the document(s) to scan.
        /// </summary>
        public bool? UseDocumentFeeder
        {
            get => _useDocumentFeeder;

            set
            {
                if(value != _useDocumentFeeder)
                {
                    _useDocumentFeeder = value;
                    OnPropertyChanged(nameof(UseDocumentFeeder));
                }
            }
        }

        /// <summary>
        /// Whether to use duplexing, if supported.
        /// </summary>
        public bool? UseDuplex
        {
            get => _useDuplex;

            set
            {
                if(value != _useDuplex)
                {
                    _useDuplex = value;
                    OnPropertyChanged(nameof(UseDuplex));
                }
            }
        }

        /// <summary>
        /// Indicates if the transmitted light film Scanner should be used as the light source.
        /// </summary>
        public bool? UseFilmScanner
        {
            get => _useFilmScanner;

            set
            {
                if(value != _useFilmScanner)
                {
                    _useFilmScanner = value;
                    OnPropertyChanged(nameof(UseFilmScanner));
                }
            }
        }

        /// <summary>
        /// Default scan settings.
        /// </summary>
        public static readonly ScanSettings Default = new ScanSettings()
        {
            Resolution = ResolutionSettings.ColourPhotocopier,
            Page = PageSettings.Default,
            Rotation = new RotationSettings()
        };

        private bool _abortWhenNoPaperDetectable;

        private AreaSettings _area;

        private PageSettings _page;

        private ResolutionSettings _resolution;

        private RotationSettings _rotation;

        private bool? _showProgressIndicatorUi;

        private bool _showTwainUi;

        private short _transferCount;

        private bool? _useAutoFeeder;

        private bool? _useAutoScanCache;

        private bool? _useDocumentFeeder;

        private bool? _useDuplex;

        private bool? _useFilmScanner;

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