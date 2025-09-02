using System;
using System.ComponentModel;
using System.IO;
using System.IO.Packaging;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Xps.Packaging;
using System.Windows.Xps.Serialization;

namespace Extensions.Controls;

public partial class XpsViewer : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty ControlsVisibilityProperty = DependencyProperty.Register("ControlsVisibility", typeof(Visibility), typeof(XpsViewer), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty FitToHeightProperty = DependencyProperty.Register("FitToHeight", typeof(bool), typeof(XpsViewer), new PropertyMetadata(false, Changed));
    public static readonly DependencyPropertyKey PageNumberProperty = DependencyProperty.RegisterReadOnly("PageNumber", typeof(int), typeof(XpsViewer), new PropertyMetadata(0));
    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = DependencyProperty.Register("VerticalScrollBarVisibility", typeof(ScrollBarVisibility), typeof(XpsViewer), new PropertyMetadata(ScrollBarVisibility.Visible));
    public static readonly DependencyProperty XpsDataFilePathProperty = DependencyProperty.Register("XpsDataFilePath", typeof(string), typeof(XpsViewer), new PropertyMetadata(null, XpsDataFilePathChanged));

    public XpsViewer()
    {
        InitializeComponent();
        DataContext = this;
        Viewer?.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ScrollChangedEvent));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public Visibility ControlsVisibility { get => (Visibility)GetValue(ControlsVisibilityProperty); set => SetValue(ControlsVisibilityProperty, value); }

    public IDocumentPaginatorSource Document
    {
        get;

        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(Document));
            }
        }
    }

    public bool FitToHeight { get => (bool)GetValue(FitToHeightProperty); set => SetValue(FitToHeightProperty, value); }

    public int PageNumber => (int)GetValue(PageNumberProperty.DependencyProperty);

    public ScrollBarVisibility VerticalScrollBarVisibility { get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); set => SetValue(VerticalScrollBarVisibilityProperty, value); }

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

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpsViewer xpsViewer)
        {
            if ((bool)e.NewValue)
            {
                xpsViewer.Viewer.FitToHeight();
            }
            else
            {
                xpsViewer.Viewer.Zoom = 100;
            }
        }
    }

    private static void XpsDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpsViewer xpsViewer && e.NewValue is string filepath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filepath))
                {
                    xpsViewer.Document = null;
                    return;
                }
                if (File.Exists(filepath))
                {
                    XpsDocument doc = new(filepath, FileAccess.Read);
                    xpsViewer.Document = doc.GetFixedDocumentSequence();
                }
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

    private void ScrollChangedEvent(object sender, ScrollChangedEventArgs e) => SetValue(PageNumberProperty, Viewer?.MasterPageNumber);
}