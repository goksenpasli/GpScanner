using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace Extensions;

public sealed class FileExtensionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return false;
        }

        string fileName = value.ToString();
        string extension = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        IEnumerable<string> allowedExtensions = parameter.ToString().Split([ ';' ], StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim().ToLowerInvariant());

        return allowedExtensions.Contains(extension.ToLowerInvariant());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}