using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Extensions;

public class NumericUpDownControl : ScrollBar
{
    static NumericUpDownControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericUpDownControl), new FrameworkPropertyMetadata(typeof(NumericUpDownControl)));
        MaximumProperty.OverrideMetadata(typeof(NumericUpDownControl), new FrameworkPropertyMetadata(double.MaxValue));
        MinimumProperty.OverrideMetadata(typeof(NumericUpDownControl), new FrameworkPropertyMetadata(double.MinValue));
    }

    public DateTime? DateValue { get => (DateTime?)GetValue(DateValueProperty); set => SetValue(DateValueProperty, value); }

    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    public Visibility NumericUpDownButtonsVisibility { get => (Visibility)GetValue(NumericUpDownButtonsVisibilityProperty); set => SetValue(NumericUpDownButtonsVisibilityProperty, value); }

    public Visibility NumericUpdownTextBoxVisibility { get => (Visibility)GetValue(NumericUpdownTextBoxVisibilityProperty); set => SetValue(NumericUpdownTextBoxVisibilityProperty, value); }

    public Mode ShowMode { get => (Mode)GetValue(ShowModeProperty); set => SetValue(ShowModeProperty, value); }

    public double Text { get => (double)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if(!IsReadOnly)
        {
            if(e.Key is not ((>= Key.NumPad0 and <= Key.NumPad9) or (>= Key.D0 and <= Key.D9) or Key.OemComma
                or Key.Back or Key.Tab or Key.Enter or Key.Left or Key.Right))
            {
                e.Handled = true;
            }

            switch(e.Key)
            {
                case Key.Up:
                    if(ShowMode == Mode.DateTimeMode && DateValue.HasValue && DateValue < DateTime.MaxValue)
                    {
                        DateValue = DateValue.Value.AddDays(1);
                    }

                    break;

                case Key.Down:
                    if(ShowMode == Mode.DateTimeMode && DateValue.HasValue && DateValue > DateTime.MinValue)
                    {
                        DateValue = DateValue.Value.AddDays(-1);
                    }

                    break;
            }
        }

        base.OnKeyDown(e);
    }

    private static void ModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if((Mode)e.NewValue == Mode.DateTimeMode && d is NumericUpDownControl numericUpDownControl)
        {
            numericUpDownControl.SmallChange = 1;
            numericUpDownControl.LargeChange = 1;
        }
    }

    public static readonly DependencyProperty DateValueProperty = DependencyProperty.Register("DateValue", typeof(DateTime?), typeof(NumericUpDownControl), new PropertyMetadata(null));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(NumericUpDownControl), new PropertyMetadata(false));

    public static readonly DependencyProperty NumericUpDownButtonsVisibilityProperty =
        DependencyProperty.Register("NumericUpDownButtonsVisibility", typeof(Visibility), typeof(NumericUpDownControl), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty NumericUpdownTextBoxVisibilityProperty =
        DependencyProperty.Register("NumericUpdownTextBoxVisibility", typeof(Visibility), typeof(NumericUpDownControl), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty ShowModeProperty = DependencyProperty.Register("ShowMode", typeof(Mode), typeof(NumericUpDownControl), new PropertyMetadata(Mode.NumberMode, ModeChanged));

    [Browsable(false)]
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        "Text",
        typeof(double),
        typeof(NumericUpDownControl),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public enum Mode
    {
        NumberMode = 0,

        CurrencyMode = 1,

        DateTimeMode = 2
    }
}