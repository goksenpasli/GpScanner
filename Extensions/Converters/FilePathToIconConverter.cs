using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Extensions;

public class FilePathToIconConverter : DependencyObject, IValueConverter
{
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        "IconSize",
        typeof(ShellIcon.SizeType),
        typeof(FilePathToIconConverter),
        new PropertyMetadata(ShellIcon.SizeType.large));

    public ShellIcon.SizeType IconSize { get => (ShellIcon.SizeType)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            return value is string path ? ShellIcon.GetExtensionIconBySize(path, IconSize) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}