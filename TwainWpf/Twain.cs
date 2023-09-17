using System;
using System.Collections.Generic;

namespace TwainWpf
{
    public class Twain
    {
        private readonly DataSourceManager _dataSourceManager;

        public Twain(IWindowsMessageHook messageHook)
        {
            ScanningComplete += delegate
            {
            };
            TransferImage += delegate
            {
            };

            _dataSourceManager = new DataSourceManager(DataSourceManager.DefaultApplicationId, messageHook);
            _dataSourceManager.ScanningComplete += (object sender, ScanningCompleteEventArgs args) => ScanningComplete(this, args);
            _dataSourceManager.TransferImage += (object sender, TransferImageEventArgs args) => TransferImage(this, args);
        }

        /// <summary>
        /// Notification that the scanning has completed.
        /// </summary>
        public event EventHandler<ScanningCompleteEventArgs> ScanningComplete;

        public event EventHandler<TransferImageEventArgs> TransferImage;

        /// <summary>
        /// Gets the product name for the default source.
        /// </summary>
        public string DefaultSourceName
        {
            get
            {
                using (DataSource source = DataSource.GetDefault(_dataSourceManager.ApplicationId, _dataSourceManager.MessageHook))
                {
                    return source.SourceId.ProductName;
                }
            }
        }

        /// <summary>
        /// Gets a list of source product names.
        /// </summary>
        public IList<string> SourceNames
        {
            get
            {
                List<string> result = new List<string>();
                List<DataSource> sources = DataSource.GetAllSources(_dataSourceManager.ApplicationId, _dataSourceManager.MessageHook);

                foreach (DataSource source in sources)
                {
                    result.Add(source.SourceId.ProductName);
                    source.Dispose();
                }

                return result;
            }
        }

        /// <summary>
        /// Shows a dialog prompting the use to select the source to scan from.
        /// </summary>
        public void SelectSource() => _dataSourceManager.SelectSource();

        /// <summary>
        /// Selects a source based on the product name string.
        /// </summary>
        /// <param name="sourceName">The source product name.</param>
        public void SelectSource(string sourceName)
        {
            DataSource source = DataSource.GetSource(sourceName, _dataSourceManager.ApplicationId, _dataSourceManager.MessageHook);

            _dataSourceManager.SelectSource(source);
        }

        /// <summary>
        /// Starts scanning.
        /// </summary>
        public void StartScanning(ScanSettings settings) => _dataSourceManager.StartScan(settings);
    }
}