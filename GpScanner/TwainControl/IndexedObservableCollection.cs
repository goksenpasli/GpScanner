using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using TwainControl.Properties;

namespace TwainControl;

public class IndexedObservableCollection<T> : ObservableCollection<T> where T : IIndexable
{
    private readonly Dictionary<string, SolidColorBrush> filePathColors = [];
    private readonly Random random = new();

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        for (int i = index; i < Count; i++)
        {
            this[i].Index = i + 1;
        }
        SetGroupFileIndicator();
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
        SetGroupFileIndicator();
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        for (int i = index; i < Count; i++)
        {
            this[i].Index = i + 1;
        }
        SetGroupFileIndicator();
    }

    protected override void SetItem(int index, T item)
    {
        base.SetItem(index, item);
        this[index].Index = index + 1;
        SetGroupFileIndicator();
    }

    private void SetGroupFileIndicator()
    {
        if (typeof(T) != typeof(ScannedImage))
        {
            return;
        }

        TwainCtrl.SaveRecoveryData(Items.Cast<ScannedImage>());

        if (!Settings.Default.ShowFileGroupIndicator)
        {
            return;
        }
        HashSet<string> usedFilePaths = [];

        foreach (ScannedImage image in Items.Cast<ScannedImage>())
        {
            if (image.FilePath is null)
            {
                continue;
            }

            _ = usedFilePaths.Add(image.FilePath);

            if (!filePathColors.TryGetValue(image.FilePath, out SolidColorBrush brush))
            {
                brush = new SolidColorBrush(Color.FromArgb(128, (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
                brush.Freeze();
                filePathColors[image.FilePath] = brush;
            }

            if (image.FileGroupColor != brush)
            {
                image.FileGroupColor = brush;
            }
        }

        foreach (string path in filePathColors.Keys.Except(usedFilePaths).ToList())
        {
            _ = filePathColors.Remove(path);
        }
    }
}