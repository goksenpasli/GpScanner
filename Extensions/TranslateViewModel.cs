using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Extensions;

public class TranslateViewModel : InpcBase
{
    private const int maxLengthAllowed = 65519;

    public static async Task<string> DileÇevirAsync(string text, string from = "auto", string to = "en")
    {
        try
        {
            if (text.Length > maxLengthAllowed)
            {
                return null;
            }
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            client.DefaultRequestHeaders.Add("Accept-Charset", "UTF-8");
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={from}&tl={to}&dt=t&q={Uri.EscapeUriString(text)}";
            HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();
            string page = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            JavaScriptSerializer JSS = new();
            object parsedObj = JSS.DeserializeObject(page);
            string çeviri = string.Empty;
            object[] data = parsedObj as object[];
            foreach (object firstnodeItem in data[0] as object[])
            {
                çeviri += (firstnodeItem as object[])?[0].ToString();
            }

            return çeviri;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}