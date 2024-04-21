using GpScanner.ViewModel;
using System;
using System.Globalization;
using System.Windows.Data;

namespace GpScanner.Converter;

public sealed class StringToCountryFlagImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string lang ? SplashViewModel.GetFlag(lang) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
