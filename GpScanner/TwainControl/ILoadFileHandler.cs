using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TwainControl
{
    public interface ILoadFileHandler
    {
        Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber);

        int GetPageCount(string filename);

        bool IsValidFile(string filename);

        Task<BitmapFrame> LoadImageAsync(string filename);

        Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename);

        BitmapFrame LoadWebpImage(int decodeheight, string filename);

        Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename);
    }
}
