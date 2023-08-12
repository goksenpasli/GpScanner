using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Extensions.Controls;

/// <summary>
/// Interaction logic for XpsViewer.xaml
/// </summary>
public class PageRangeDocumentPaginator : DocumentPaginator
{
    private readonly int _endIndex;
    private readonly DocumentPaginator _paginator;
    private readonly int _startIndex;

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
}
