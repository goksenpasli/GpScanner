using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Extensions;

public class TranslateViewModel : InpcBase
{
    public static async Task<string> DileÇevir(string text, string from = "auto", string to = "en")
    {
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            client.DefaultRequestHeaders.Add("Accept-Charset", "UTF-8");
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={from}&tl={to}&dt=t&q={Uri.EscapeUriString(text)}";
            HttpResponseMessage response = await client.GetAsync(url);
            _ = response.EnsureSuccessStatusCode();
            string page = await response.Content.ReadAsStringAsync();
            JavaScriptSerializer JSS = new();
            object parsedObj = JSS.DeserializeObject(page);
            string çeviri = string.Empty;
            object[] data = parsedObj as object[];
            object firstnode = data.FirstOrDefault();
            for(int i = 0; i < (firstnode as object[])?.Length; i++)
            {
                çeviri += ((data.FirstOrDefault() as object[])?[i] as object[])?.ElementAtOrDefault(0).ToString();
            }

            return çeviri;
        }
        catch(Exception)
        {
            return string.Empty;
        }
    }
}