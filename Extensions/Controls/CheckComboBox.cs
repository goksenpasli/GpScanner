using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class CheckComboBox : ComboBox
    {
        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register("Items", typeof(ObservableCollection<CheckBoxItem>), typeof(CheckComboBox), new PropertyMetadata(new ObservableCollection<CheckBoxItem>()));
        public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register("SelectedItems", typeof(string), typeof(CheckComboBox), new PropertyMetadata(string.Empty, OnSelectedItemsChanged));
        public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register("Watermark", typeof(string), typeof(CheckComboBox), new PropertyMetadata(string.Empty));

        static CheckComboBox() { DefaultStyleKeyProperty.OverrideMetadata(typeof(CheckComboBox), new FrameworkPropertyMetadata(typeof(CheckComboBox))); }

        public CheckComboBox()
        {
            Loaded += (s, e) =>
                      {
                          UpdateSelectedItems();
                          if (string.IsNullOrEmpty(SelectedItems))
                          {
                              Text = Watermark;
                          }
                      };
            SelectAllCommand = new RelayCommand<object>(parameter => Items.ToList().ForEach(i => i.IsChecked = true), parameter => Items?.Any() == true);
            SelectInverseCommand = new RelayCommand<object>(parameter => Items.ToList().ForEach(i => i.IsChecked = !i.IsChecked), parameter => Items?.Any() == true);
            ClearAllCommand = new RelayCommand<object>(parameter => Items.ToList().ForEach(i => i.IsChecked = false), parameter => Items?.Any() == true);
        }

        public RelayCommand<object> ClearAllCommand { get; }

        public new ObservableCollection<CheckBoxItem> Items { get => (ObservableCollection<CheckBoxItem>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

        public RelayCommand<object> SelectAllCommand { get; }

        public string SelectedItems { get => (string)GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }

        public RelayCommand<object> SelectInverseCommand { get; }

        public string Watermark { get => (string)GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            UpdateSelectedItems();
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CheckComboBox checkComboBox)
            {
                checkComboBox.UpdateSelectedItems();
            }
        }

        private void UpdateSelectedItems()
        {
            if (Items is null)
            {
                return;
            }
            SelectedItems = string.Join(",", Items.Where(i => i.IsChecked).Select(i => i.Name));
            if (string.IsNullOrEmpty(SelectedItems))
            {
                Text = Watermark;
                return;
            }
            Text = SelectedItems;
        }
    }
}
