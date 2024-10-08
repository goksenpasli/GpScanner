using System;
using System.ComponentModel;
using System.IO;
using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Xps.Packaging;
using System.Windows.Xps.Serialization;

namespace Extensions.Controls;

public partial class XpsViewer : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyPropertyKey PageNumberProperty = DependencyProperty.RegisterReadOnly("PageNumber", typeof(int), typeof(XpsViewer), new PropertyMetadata(0));
    public static readonly DependencyProperty XpsDataFilePathProperty = DependencyProperty.Register("XpsDataFilePath", typeof(string), typeof(XpsViewer), new PropertyMetadata(null, XpsDataFilePathChanged));
    private IDocumentPaginatorSource document;
    public int PageNumber => (int)GetValue(PageNumberProperty.DependencyProperty);

    public XpsViewer()
    {
        InitializeComponent();
        DataContext = this;
        Viewer?.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ScrollChangedEvent));
    }

    private void ScrollChangedEvent(object sender, ScrollChangedEventArgs e) => SetValue(PageNumberProperty, Viewer?.MasterPageNumber);

    public event PropertyChangedEventHandler PropertyChanged;

    public IDocumentPaginatorSource Document {
        get => document;

        set {
            if (document != value)
            {
                document = value;
                OnPropertyChanged(nameof(Document));
            }
        }
    }

    public string XpsDataFilePath { get => (string)GetValue(XpsDataFilePathProperty); set => SetValue(XpsDataFilePathProperty, value); }

    public FixedDocumentSequence WriteXPS(FlowDocument flowDocument)
    {
        MemoryStream ms = new();
        Package package = Package.Open(ms, FileMode.Create, FileAccess.ReadWrite);
        Uri packUri = new("pack://temp.xps");
        PackageStore.RemovePackage(packUri);
        PackageStore.AddPackage(packUri, package);
        using XpsDocument xpsDocument = new(package, CompressionOption.SuperFast, packUri.ToString());
        DocumentPaginator paginator = ((IDocumentPaginatorSource)flowDocument).DocumentPaginator;
        using (XpsSerializationManager xpsSerializationManager = new(new XpsPackagingPolicy(xpsDocument), false))
        {
            xpsSerializationManager.SaveAsXaml(paginator);
        }
        ms = null;
        return xpsDocument.GetFixedDocumentSequence();
    }

    protected virtual void OnPropertyChanged(string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static void XpsDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpsViewer xpsViewer && e.NewValue is not null)
        {
            try
            {
                XpsDocument doc = new(e.NewValue as string, FileAccess.Read);
                xpsViewer.Document = doc.GetFixedDocumentSequence();
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex?.Message);
            }
        }
    }

    private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        XpsViewer xpsViewer = (sender as DocumentViewer)?.DataContext as XpsViewer;
        e.CanExecute = xpsViewer?.Document is not null;
    }

    private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        PrintDialog dlg = new() { UserPageRangeEnabled = true };
        if (dlg?.ShowDialog() == true)
        {
            XpsViewer xpsViewer = (sender as DocumentViewer)?.DataContext as XpsViewer;
            DocumentPaginator paginator = xpsViewer?.Document?.DocumentPaginator;
            if (dlg.PageRangeSelection == PageRangeSelection.UserPages)
            {
                paginator = new PageRangeDocumentPaginator(xpsViewer?.Document?.DocumentPaginator, dlg.PageRange);
            }

            dlg.PrintDocument(paginator, string.Empty);
        }
    }
}