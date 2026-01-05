using ExcelDataReader;
using Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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

        public async Task<DataTableCollection> GetDataTableCollection(FileStream fs, string uriString)
        {
            Progress<double> progress = new(v => Progress = v);
            return Path.GetExtension(uriString).ToLowerInvariant() switch
            {
                ".csv" => (await StreamToDtAsync(fs, progress, true)).Tables,
                ".xls" or ".xlsx" or ".xlsb" => (await StreamToDtAsync(fs, progress)).Tables,
                ".ods" => (await OdsReader.ReadOdsFile(fs, uriString)).Tables,
                _ => null,
            };
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if ((e?.Data?.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
            {
                if (Path.GetExtension(droppedfiles[0]).ToLowerInvariant() is ".csv" or ".xls" or ".xlsx" or ".xlsb" or ".ods")
                {
                    XlsxDataFilePath = droppedfiles[0];
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher?.CheckAccess() == true)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _ = dispatcher?.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }

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
                        viewer.Tablolar = await viewer.GetDataTableCollection(fs, uriString);
                        if (viewer.Tablolar is not null)
                        {
                            viewer.SelectedTable = viewer.Tablolar[0];
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex?.Message);
                }
            }
        }

        private async Task<DataSet> StreamToDtAsync(FileStream stream, IProgress<double> progress, bool isCsv = false)
        {
            return await Task.Run(
                () =>
                {
                    using IExcelDataReader reader = isCsv ? ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { FallbackEncoding = Encoding.GetEncoding(1254) }) : ExcelReaderFactory.CreateReader(stream);

                    int lastReported = -1;

                    return reader.AsDataSet(
                        new ExcelDataSetConfiguration
                        {
                            UseColumnDataType = true,
                            ConfigureDataTable =
                                _ => new ExcelDataTableConfiguration
                                {
                                    FilterRow =
                                    r =>
                                    {
                                        int depth = r.Depth + 1;

                                        if (depth == 1)
                                        {
                                            lastReported = 1;
                                            progress.Report(0);
                                            return true;
                                        }

                                        if (r.RowCount > 0)
                                        {
                                            int step = Math.Max(1, r.RowCount / 100);

                                            if (depth - lastReported >= step || depth == r.RowCount)
                                            {
                                                lastReported = depth;
                                                double value = depth * 100.0 / r.RowCount;
                                                progress.Report(value);
                                            }
                                        }
                                        return true;
                                    }
                                }
                        });
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
                    if (filteredrows?.Any() == true)
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
