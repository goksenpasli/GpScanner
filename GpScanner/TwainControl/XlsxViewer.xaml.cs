using ExcelDataReader;
using Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        public XlsxViewer()
        {
            InitializeComponent();
            DataContext = this;
            PropertyChanged += XlsxViewer_PropertyChanged;
            CopyRows = new RelayCommand<object>(
                parameter =>
                {
                    IList selectedItems = parameter as IList;
                    StringBuilder stringBuilder = new();
                    foreach (DataRowView item in selectedItems)
                    {
                        _ = stringBuilder.AppendLine(string.Join("\t", item.Row.ItemArray));
                    }
                    try
                    {
                        Clipboard.SetText(stringBuilder.ToString());
                    }
                    catch (COMException ex)
                    {
                        const uint CLIPBRD_E_CANT_OPEN = 0x800401D0;
                        if ((uint)ex.ErrorCode != CLIPBRD_E_CANT_OPEN)
                        {
                            throw;
                        }
                    }
                },
                parameter => parameter is IList selecteditems && selecteditems?.Count > 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RelayCommand<object> CopyRows { get; }

        public double Progress
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public string Search
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Search));
                }
            }
        }

        public DataTable SelectedTable
        {
            get;

            set
            {
                if (field != value)
                {
                    field = value;

                    OnPropertyChanged(nameof(SelectedTable));
                }
            }
        }

        public DataTableCollection Tablolar
        {
            get;

            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(Tablolar));
                }
            }
        }

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
                        using FileStream fs = File.Open(uriString, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        viewer.Tablolar = Path.GetExtension(uriString) switch
                        {
                            ".csv" => (await viewer.StreamToDtAsync(fs, true)).Tables,
                            _ => (await viewer.StreamToDtAsync(fs)).Tables,
                        };
                        viewer.SelectedTable = viewer.Tablolar[0];
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
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

        private void XlsxViewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Search" && SelectedTable is not null)
            {
                DataTable defaulttable = Tablolar[SelectedTable.TableName];
                string tablename = SelectedTable.TableName;
                if (!string.IsNullOrWhiteSpace(Search))
                {
                    IEnumerable<DataRow> filteredrows = defaulttable.Rows.OfType<DataRow>().Where(z => z.ItemArray.Any(rowitem => rowitem is not null && rowitem is string content && content.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0));
                    if (filteredrows.Any())
                    {
                        SelectedTable = filteredrows.CopyToDataTable();
                        SelectedTable.TableName = tablename;
                    }
                    return;
                }
                SelectedTable = defaulttable;
            }
        }
    }
}
