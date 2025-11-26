using System;
using System.Collections.ObjectModel;

namespace TwainControl;

public class IndexedObservableCollection<T> : ObservableCollection<T> where T : IIndexable
{
    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        for (int i = index; i < Count; i++)
        {
            this[i].Index = i + 1;
        }
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        base.MoveItem(oldIndex, newIndex);

        int start = Math.Min(oldIndex, newIndex);
        int end = Math.Max(oldIndex, newIndex);
        for (int i = start; i <= end; i++)
        {
            this[i].Index = i + 1;
        }
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        for (int i = index; i < Count; i++)
        {
            this[i].Index = i + 1;
        }
    }

    protected override void SetItem(int index, T item)
    {
        base.SetItem(index, item);
        this[index].Index = index + 1;
    }
}