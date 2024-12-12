using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Data;
using TwainControl;

namespace GpScanner.Converter;

public sealed class TesseractDataFilesContentLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filename)
        {
            string adress = $"https://github.com/tesseract-ocr/tessdata_best/raw/main/{filename}";
            if (Uri.IsWellFormedUriString(adress, UriKind.Absolute))
            {
                return FetchContentLengthAsync(adress);
            }
        }
        return "Invalid URL";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    private async Task<string> FetchContentLengthAsync(string url)
    {
        try
        {
            using HttpClient client = new();
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537");

            HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            _ = response.EnsureSuccessStatusCode();

            double? contentLength = response.Content.Headers.ContentLength / 1_048_576d;
            return contentLength.HasValue ? $"{contentLength.Value:N2} MB" : Translation.GetResStringValue("ERROR");
        }
        catch (Exception)
        {
            return Translation.GetResStringValue("ERROR");
        }
    }
}
