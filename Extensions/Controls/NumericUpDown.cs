using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Extensions
{
    public class NumericUpDown : Control
    {
        public static readonly DependencyProperty IntervalProperty =
            DependencyProperty.Register("Interval", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(1m));
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(decimal.MaxValue));
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(decimal.MinValue));
        public static readonly DependencyProperty NumericUpdownTextBoxVisibilityProperty =
            DependencyProperty.Register("NumericUpdownTextBoxVisibility", typeof(Visibility), typeof(NumericUpDown), new PropertyMetadata(Visibility.Visible));
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(decimal), typeof(NumericUpDown), new FrameworkPropertyMetadata(0m, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        static NumericUpDown() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericUpDown), new FrameworkPropertyMetadata(typeof(NumericUpDown))); }

        public decimal Interval { get => (decimal)GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }

        public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

        public decimal Maximum { get => (decimal)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

        public decimal Minimum { get => (decimal)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

        public Visibility NumericUpdownTextBoxVisibility { get => (Visibility)GetValue(NumericUpdownTextBoxVisibilityProperty); set => SetValue(NumericUpdownTextBoxVisibilityProperty, value); }

        public decimal Value { get => (decimal)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_TextBox") is TextBox textBox && GetTemplateChild("PART_UpButton") is RepeatButton upButton && GetTemplateChild("PART_DownButton") is RepeatButton downButton)
            {
                upButton.Click += UpButton_Click;
                downButton.Click += DownButton_Click;
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericUpDown numericUpDown && e.NewValue is decimal number)
            {
                numericUpDown.Value = Math.Max(numericUpDown.Minimum, Math.Min(numericUpDown.Maximum, number));
            }
        }

        private void AdjustValue(decimal delta) => Value += delta;

        private void DownButton_Click(object sender, RoutedEventArgs e) => AdjustValue(-Interval);

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
            if (e.Key == Key.Enter && decimal.TryParse((sender as TextBox)?.Text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal value))
            {
                SetValue(ValueProperty, value);
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (char.IsDigit(e.Text, e.Text.Length - 1) || e.Text == "." || e.Text == "-")
            {
                return;
            }

            e.Handled = true;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e) => AdjustValue(Interval);
    }
}
