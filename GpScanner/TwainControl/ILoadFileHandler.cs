using PdfiumViewer;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TwainControl
{
    public interface ILoadFileHandler
    {
        int GetPageCount(string filename);

        IEnumerable<PdfCharacterInformation> GetPdfCharacters();

        bool IsValidFile(string filename);

        Task<BitmapFrame> LoadImageAsync(string filename);

        Task<BitmapImage> LoadPdfAsync(string filename, int pageNumber);

        Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename);

        Task<BitmapFrame> LoadWebpImage(int decodeheight, string filename, bool fullresolution = true);

        Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename);
    }
}
