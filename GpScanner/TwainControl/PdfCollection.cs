using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace TwainControl;

public class PdfCollection : ObservableCollection<ExtendedPdfData>
{
    protected override void InsertItem(int index, ExtendedPdfData item)
    {
        base.InsertItem(index, item);
        UpdatePageNumbers();
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        base.MoveItem(oldIndex, newIndex);
        UpdatePageNumbers();
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        UpdatePageNumbers();
    }

    protected override void SetItem(int index, ExtendedPdfData item)
    {
        base.SetItem(index, item);
        UpdatePageNumbers();
    }

    private void Page_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "PageNumber")
        {
            foreach (ExtendedPdfData page in this)
            {
                page.BorderBrush = null;
            }
            foreach (ExtendedPdfData item in this?.GroupBy(x => x.PageNumber).Where(g => g.Count() > 1).SelectMany(g => g))
            {
                item.BorderBrush = Brushes.Red;
            }
        }
    }

    private void UpdatePageNumbers()
    {
        for (int i = 0; i < Count; i++)
        {
            this[i].PageNumber = i + 1;
        }

        foreach (ExtendedPdfData page in this)
        {
            page.PropertyChanged -= Page_PropertyChanged;
            page.PropertyChanged += Page_PropertyChanged;
        }
    }
}
