using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public static class ListBoxHelper
    {
        public static readonly DependencyProperty SelectedItemsMaxCountProperty = DependencyProperty.RegisterAttached("SelectedItemsMaxCount", typeof(int), typeof(ListBoxHelper), new PropertyMetadata(int.MaxValue, OnSelectedItemsChanged));

        public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.RegisterAttached("SelectedItems", typeof(IList), typeof(ListBoxHelper), new FrameworkPropertyMetadata(default(IList), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

        public static IList GetSelectedItems(DependencyObject d)
        {
            return (IList)d.GetValue(SelectedItemsProperty);
        }

        public static int GetSelectedItemsMaxCount(DependencyObject obj)
        {
            return (int)obj.GetValue(SelectedItemsMaxCountProperty);
        }

        public static void SetSelectedItems(DependencyObject d, IList value)
        {
            d.SetValue(SelectedItemsProperty, value);
        }

        public static void SetSelectedItemsMaxCount(DependencyObject obj, int value)
        {
            obj.SetValue(SelectedItemsMaxCountProperty, value);
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                IList selectedItems = GetSelectedItems(listBox);
                int maxitem = GetSelectedItemsMaxCount(listBox);
                listBox.SelectionChanged += delegate
                {
                    if (listBox.SelectedItems.Count > maxitem)
                    {
                        listBox.SelectedItems.Clear();
                        MessageBox.Show($"En Fazla {maxitem} Adet Seçim Yapabilirsiniz.", Application.Current?.MainWindow?.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }

                    selectedItems?.Clear();
                    foreach (object item in listBox.SelectedItems)
                    {
                        _ = selectedItems?.Add(item);
                    }
                };
            }
        }
    }
}