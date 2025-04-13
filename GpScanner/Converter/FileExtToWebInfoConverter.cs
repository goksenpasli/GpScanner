using Extensions;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using TwainControl;

namespace GpScanner.Converter;

public sealed class FileExtToWebInfoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string extension ? GetData(extension) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    private string ExtractSection(string html, string startTag, string endTag)
    {
        int start = html.IndexOf(startTag);
        if (start == -1)
        {
            return "";
        }
        int end = html.IndexOf(endTag, start);
        return end == -1 ? "" : html.Substring(start, end - start + endTag.Length);
    }

    private async Task<string> GetData(string ext)
    {
        try
        {
            string url = $"https://fileinfo.com/extension/{ext.Substring(1)}";
            using HttpClient httpClient = new();
            string html = await httpClient.GetStringAsync(url);
            if (string.IsNullOrWhiteSpace(html))
            {
                return Translation.GetResStringValue("ERROR");
            }
            string description = ExtractSection(html, "<div class=\"infoBox\">", "</div>");
            return await TranslateViewModel.DileÇevirAsync(StripHtml(description), "auto", TranslationSource.Instance.CurrentCulture.TwoLetterISOLanguageName, true);
        }
        catch (Exception)
        {
            return Translation.GetResStringValue("ERROR");
        }
    }

    private string StripHtml(string html) => Regex.Replace(html, "<.*?>", "").Trim();
}