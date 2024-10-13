using ExcelDataReader;
using System;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for XlsxViewer.xaml
    /// </summary>
    public partial class XlsxViewer : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty XlsxDataFilePathProperty = DependencyProperty.Register("XlsxDataFilePath", typeof(string), typeof(XlsxViewer), new PropertyMetadata(null, XlsxDataFilePathChanged));
        private double progress;
        private DataTableCollection tablolar;

        public XlsxViewer()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double Progress
        {
            get => progress;
            set
            {
                if (progress != value)
                {
                    progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public DataTableCollection Tablolar
        {
            get => tablolar;

            set
            {
                if (tablolar != value)
                {
                    tablolar = value;
                    OnPropertyChanged(nameof(Tablolar));
                }
            }
        }

        public DataView XlsDataVieW { get; set; }

        public string XlsxDataFilePath { get => (string)GetValue(XlsxDataFilePathProperty); set => SetValue(XlsxDataFilePathProperty, value); }

        protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static async void XlsxDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is XlsxViewer viewer && e.NewValue is string uriString)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(uriString))
                    {
                        viewer.Tablolar = null;
                        return;
                    }

                    if (File.Exists(uriString))
                    {
                        using FileStream fs = File.Open(uriString, FileMode.Open, FileAccess.Read);
                        viewer.Tablolar = Path.GetExtension(uriString) switch
                        {
                            ".csv" => (await viewer.StreamToDtAsync(fs, true)).Tables,
                            _ => (await viewer.StreamToDtAsync(fs)).Tables,
                        };
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private async Task<DataSet> StreamToDtAsync(FileStream stream, bool isCsv = false)
        {
            return await Task.Run(
                () =>
                {
                    IExcelDataReader reader = isCsv ? ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.GetEncoding(1254) }) : ExcelReaderFactory.CreateReader(stream);
                    using (reader)
                    {
                        return reader.AsDataSet(
                            new ExcelDataSetConfiguration
                            {
                                UseColumnDataType = true,
                                ConfigureDataTable =
                                _ => new ExcelDataTableConfiguration
                                {
                                    FilterRow =
                                    (rowReader) =>
                                    {
                                        Progress = Math.Ceiling(rowReader.Depth / (double)rowReader.RowCount * 100);
                                        return true;
                                    },
                                    EmptyColumnNamePrefix = "Kolon"
                                }
                            });
                    }
                });
        }
    }
}
