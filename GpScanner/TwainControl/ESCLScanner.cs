using Extensions;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace TwainControl
{
    public static class ESCLScanner
    {
        private static readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(5) };

        public static async Task<XDocument> GetScannerCapabilitiesAsync(string eSCLUri) => await GetScannerDataAsync(eSCLUri, "ScannerCapabilities").ConfigureAwait(false);

        public static async Task<XDocument> GetScannerStatusAsync(string eSCLUri) => await GetScannerDataAsync(eSCLUri, "ScannerStatus").ConfigureAwait(false);

        public static async Task<BitmapImage> ScanDocumentAsync(string eSCLUri,
                                                                int dpi,
                                                                string inputSource = "Platen",
                                                                double pageWidth = 210.0,
                                                                double pageHeight = 297.0,
                                                                string colorMode = "Color",
                                                                string documentFormat = "image/jpeg",
                                                                int compressionFactor = 20,
                                                                int brightness = 0,
                                                                int contrast = 0,
                                                                int threshold = 128,
                                                                string duplex = "Simplex",
                                                                string multiDocumentHandling = "SingleDocument",
                                                                string fileSizeOptimization = "Normal")
        {
            try
            {
                XNamespace scanNs = "http://schemas.hp.com/imaging/escl/2011/05/03";
                XDocument payload = new(
                    new XElement(
                        scanNs + "ScanSettings",
                        new XAttribute(XNamespace.Xmlns + "scan", scanNs),
                        new XElement(scanNs + "DocumentFormat", documentFormat),
                        new XElement(scanNs + "InputSource", inputSource),
                        new XElement(scanNs + "XResolution", dpi),
                        new XElement(scanNs + "YResolution", dpi),
                        new XElement(scanNs + "ColorMode", colorMode),
                        new XElement(scanNs + "PageSize", new XElement(scanNs + "Height", pageHeight), new XElement(scanNs + "Width", pageWidth)),
                        new XElement(scanNs + "CompressionFactor", compressionFactor),
                        new XElement(scanNs + "Brightness", brightness),
                        new XElement(scanNs + "Contrast", contrast),
                        new XElement(scanNs + "Threshold", threshold),
                        new XElement(scanNs + "Duplex", duplex),
                        new XElement(scanNs + "MultipleDocumentHandling", multiDocumentHandling),
                        new XElement(scanNs + "FileSizeOptimization", fileSizeOptimization)));

                using HttpContent content = new StringContent(payload.ToString());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

                HttpResponseMessage response = await client.PostAsync($"{eSCLUri}/eSCL/ScanJobs", content).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();

                string scanJobUri = response.Headers.Location?.ToString() ?? throw new InvalidOperationException("No scan job URI received.");
                HttpResponseMessage statusResponse;
                DateTime startTime = DateTime.Now;
                TimeSpan timeout = TimeSpan.FromMinutes(2);

                do
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    statusResponse = await client.GetAsync($"{scanJobUri}/NextDocument").ConfigureAwait(false);

                    if (DateTime.Now - startTime > timeout)
                    {
                        throw new TimeoutException("Scan operation timed out.");
                    }
                }
                while (!statusResponse.IsSuccessStatusCode);

                byte[] scannedData = await statusResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return scannedData.ToBitmapImage();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An error occurred during the scanning process.", ex);
            }
        }

        private static async Task<XDocument> GetScannerDataAsync(string eSCLUri, string endpoint)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync($"{eSCLUri}/eSCL/{endpoint}").ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
                string xmlContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return XDocument.Parse(xmlContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error while retrieving scanner {endpoint.ToLower()}.", ex);
            }
        }
    }
}
