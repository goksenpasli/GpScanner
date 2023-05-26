using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Extensions;

public class TranslateViewModel : InpcBase
{
    public static string DileÇevir(string text, string from = "auto", string to = "en")
    {
        try
        {
            WebClient wc = new();
            wc.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
            wc.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");
            wc.Encoding = Encoding.UTF8;
            string url =
                $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={from}&tl={to}&dt=t&q={Uri.EscapeUriString(text)}";
            string page = wc.DownloadString(url);
            JavaScriptSerializer JSS = new();
            object parsedObj = JSS.DeserializeObject(page);
            string çeviri = string.Empty;
            object[] data = parsedObj as object[];
            object firstnode = data.FirstOrDefault();
            for (int i = 0; i < (firstnode as object[])?.Length; i++)
            {
                çeviri += ((data.FirstOrDefault() as object[])?[i] as object[])?.ElementAtOrDefault(0).ToString();
            }

            return çeviri;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}