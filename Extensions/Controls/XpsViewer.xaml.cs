using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Xps.Packaging;

namespace Extensions.Controls
{
    /// <summary>
    /// Interaction logic for XpsViewer.xaml
    /// </summary>
    public class PageRangeDocumentPaginator : DocumentPaginator
    {
        public PageRangeDocumentPaginator(DocumentPaginator paginator, PageRange pageRange)
        {
            _startIndex = pageRange.PageFrom - 1;
            _endIndex = pageRange.PageTo - 1;
            _paginator = paginator;
            _endIndex = Math.Min(_endIndex, _paginator.PageCount - 1);
        }

        public override bool IsPageCountValid => true;

        public override int PageCount => _startIndex > _paginator.PageCount - 1 || _startIndex > _endIndex ? 0 : _endIndex - _startIndex + 1;

        public override Size PageSize { get => _paginator.PageSize; set => _paginator.PageSize = value; }

        public override IDocumentPaginatorSource Source => _paginator.Source;

        public override DocumentPage GetPage(int pageNumber)
        {
            DocumentPage page = _paginator.GetPage(pageNumber + _startIndex);
            ContainerVisual cv = new();
            if (page.Visual is FixedPage page1)
            {
                foreach (object child in page1.Children)
                {
                    UIElement childClone = (UIElement)child.GetType().GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(child, null);
                    FieldInfo parentField = childClone.GetType().GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (parentField != null)
                    {
                        parentField.SetValue(childClone, null);
                        _ = cv.Children.Add(childClone);
                    }
                }

                return new DocumentPage(cv, page.Size, page.BleedBox, page.ContentBox);
            }

            return page;
        }

        private readonly int _endIndex;

        private readonly DocumentPaginator _paginator;

        private readonly int _startIndex;
    }

    public partial class XpsViewer : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty XpsDataFilePathProperty = DependencyProperty.Register("XpsDataFilePath", typeof(string), typeof(XpsViewer), new PropertyMetadata(null, XpsDataFilePathChanged));

        public XpsViewer()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IDocumentPaginatorSource Document
        {
            get => document;

            set
            {
                if (document != value)
                {
                    document = value;
                    OnPropertyChanged(nameof(Document));
                }
            }
        }

        public string XpsDataFilePath { get => (string)GetValue(XpsDataFilePathProperty); set => SetValue(XpsDataFilePathProperty, value); }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private IDocumentPaginatorSource document;

        private static void XpsDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is XpsViewer xpsViewer && e.NewValue != null)
            {
                try
                {
                    XpsDocument doc = new(e.NewValue as string, FileAccess.Read);
                    xpsViewer.Document = doc.GetFixedDocumentSequence();
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(ex.Message, Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            XpsViewer xpsViewer = (sender as DocumentViewer)?.DataContext as XpsViewer;
            e.CanExecute = xpsViewer.Document is not null;
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PrintDialog dlg = new() { UserPageRangeEnabled = true };
            if (dlg.ShowDialog() == true)
            {
                XpsViewer xpsViewer = (sender as DocumentViewer)?.DataContext as XpsViewer;
                DocumentPaginator paginator = xpsViewer.Document.DocumentPaginator;
                if (dlg.PageRangeSelection == PageRangeSelection.UserPages)
                {
                    paginator = new PageRangeDocumentPaginator(xpsViewer.Document.DocumentPaginator, dlg.PageRange);
                }

                dlg.PrintDocument(paginator, Application.Current?.MainWindow?.Title);
            }
        }
    }
}