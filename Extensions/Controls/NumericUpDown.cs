using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Extensions
{
    public class NumericUpDown : Control, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ContentTextProperty = DependencyProperty.Register("ContentText", typeof(string), typeof(NumericUpDown), new PropertyMetadata("0", OnValueChanged));
        public static readonly DependencyProperty IntervalProperty = DependencyProperty.Register("Interval", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(1m));
        public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(decimal.MaxValue));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(decimal.MinValue));
        public static readonly DependencyProperty NumericUpdownTextBoxVisibilityProperty = DependencyProperty.Register("NumericUpdownTextBoxVisibility", typeof(Visibility), typeof(NumericUpDown), new PropertyMetadata(Visibility.Visible));
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(Orientation), typeof(NumericUpDown), new PropertyMetadata(Orientation.Horizontal));
        public static readonly DependencyProperty StringFormatProperty = DependencyProperty.Register("StringFormat", typeof(string), typeof(NumericUpDown), new PropertyMetadata(null));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(decimal), typeof(NumericUpDown), new FrameworkPropertyMetadata(0m, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        private bool mouseSelectAllText = true;

        static NumericUpDown() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericUpDown), new FrameworkPropertyMetadata(typeof(NumericUpDown))); }

        public NumericUpDown()
        {
            NumberIncrease = new RelayCommand<object>(parameter => AdjustValue(Interval), parameter => Value < Maximum);
            NumberDecrease = new RelayCommand<object>(parameter => AdjustValue(-Interval), parameter => Value > Minimum);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ContentText { get => (string)GetValue(ContentTextProperty); set => SetValue(ContentTextProperty, value); }

        public decimal Interval { get => (decimal)GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }

        public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

        public decimal Maximum { get => (decimal)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

        public decimal Minimum { get => (decimal)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

        public bool MouseSelectAllText
        {
            get => mouseSelectAllText;
            set
            {
                if (mouseSelectAllText != value)
                {
                    mouseSelectAllText = value;
                    OnPropertyChanged(nameof(MouseSelectAllText));
                }
            }
        }

        public RelayCommand<object> NumberDecrease { get; }

        public RelayCommand<object> NumberIncrease { get; }

        public Visibility NumericUpdownTextBoxVisibility { get => (Visibility)GetValue(NumericUpdownTextBoxVisibilityProperty); set => SetValue(NumericUpdownTextBoxVisibilityProperty, value); }

        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

        public string StringFormat { get => (string)GetValue(StringFormatProperty); set => SetValue(StringFormatProperty, value); }

        public decimal Value { get => (decimal)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_TextBox") is TextBox textBox)
            {
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                if (MouseSelectAllText && !IsReadOnly)
                {
                    textBox.PreviewMouseUp += TextBox_PreviewMouseUp;
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericUpDown numericUpDown)
            {
                if (e.NewValue is decimal number)
                {
                    numericUpDown.Value = Math.Max(numericUpDown.Minimum, Math.Min(numericUpDown.Maximum, number));
                    numericUpDown.UpdateText(numericUpDown.Value);
                }
                if (e.NewValue is string stringnumber && decimal.TryParse(stringnumber, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
                {
                    numericUpDown.UpdateText(value);
                }
            }
        }

        private void AdjustValue(decimal delta) => Value += delta;

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
            if (e.Key == Key.Up)
            {
                AdjustValue(Interval);
            }
            if (e.Key == Key.Down)
            {
                AdjustValue(-Interval);
            }
            if (e.Key == Key.Enter && decimal.TryParse((sender as TextBox)?.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
            {
                SetValue(ValueProperty, value);
            }
        }

        private void TextBox_PreviewMouseUp(object sender, MouseButtonEventArgs e) => (sender as TextBox)?.SelectAll();

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (char.IsDigit(e.Text, e.Text.Length - 1) || e.Text == CultureInfo.InvariantCulture.NumberFormat.CurrencyDecimalSeparator || e.Text == "-")
            {
                return;
            }

            e.Handled = true;
        }

        private void UpdateText(decimal value)
        {
            if (!string.IsNullOrWhiteSpace(StringFormat))
            {
                try
                {
                    ContentText = string.Format(StringFormat, value);
                    Value = value;
                }
                catch (FormatException)
                {
                    ContentText = value.ToString();
                    Value = value;
                }
            }
            else
            {
                ContentText = value.ToString();
                Value = value;
            }
        }
    }
}
