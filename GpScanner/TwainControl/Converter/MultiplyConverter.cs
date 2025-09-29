using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class MultiplyConverter : IValueConverter
{
    public double Factor { get; set; } = 1.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Multiply(value, targetType, parameter, culture, divide: false);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Multiply(value, targetType, parameter, culture, divide: true);

    private static bool TryToDouble(object value, CultureInfo culture, out double d)
    {
        switch (value)
        {
            case null:
                d = 0;
                return false;
            case double dd:
                d = dd;
                return true;
            case float f:
                d = f;
                return true;
            case decimal m:
                d = (double)m;
                return true;
            case int i:
                d = i;
                return true;
            case long l:
                d = l;
                return true;
            case short s:
                d = s;
                return true;
            case string str:
                return double.TryParse(str, NumberStyles.Any, culture, out d);
            default:
                try
                {
                    d = System.Convert.ToDouble(value, culture);
                    return true;
                }
                catch
                {
                    d = 0;
                    return false;
                }
        }
    }

    private object Multiply(object input, Type targetType, object parameter, CultureInfo culture, bool divide)
    {
        if (input is null)
        {
            return null;
        }

        if (!TryToDouble(input, culture, out double val))
        {
            return input;
        }

        double factor = TryToDouble(parameter, culture, out double p) ? p : Factor;
        if (factor == 0 && divide)
        {
            return Binding.DoNothing;
        }

        double result = divide ? val / factor : val * factor;

        return targetType switch
        {
            Type t when t == typeof(int) => (int)Math.Round(result),
            Type t when t == typeof(float) => (float)result,
            Type t when t == typeof(decimal) => (decimal)result,
            _ => result,
        };
    }
}