using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using TwainControl;

namespace GpScanner.Converter;

public sealed class PdfFileNameToInfoConverter : DependencyObject, IValueConverter
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.Register("IsEnabled", typeof(bool), typeof(PdfFileNameToInfoConverter), new PropertyMetadata(false));

    public bool IsEnabled { get => (bool)GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (IsEnabled && value is string filename && File.Exists(filename) && Path.GetExtension(filename.ToLower()) == ".pdf")
        {
            using PdfDocument reader = PdfReader.Open(filename, PdfDocumentOpenMode.InformationOnly);
            StringBuilder stringBuilder = new();
            _ = stringBuilder
                .Append((reader.Version / 10d).ToString("n1", CultureInfo.InvariantCulture))
                .AppendLine(reader.Info.Title)
                .Append(Translation.GetResStringValue("PAGENUMBER"))
                .Append(": ")
                .AppendLine(reader.PageCount.ToString())
                .AppendLine(reader.Info.Author)
                .Append(reader.Info.CreationDate.AddHours(DateTimeOffset.Now.Offset.Hours))
                .AppendLine()
                .Append($"{reader.FileSize / 1048576d:##.##}")
                .AppendLine(" MB");
            return stringBuilder.ToString();
        }
        else
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}