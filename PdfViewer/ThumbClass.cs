using System.Windows.Media.Imaging;
using Extensions;

namespace PdfViewer
{
    public class ThumbClass : InpcBase
    {
        public int Page
        {
            get => page;

            set
            {
                if (page != value)
                {
                    page = value;
                    OnPropertyChanged(nameof(Page));
                }
            }
        }

        public BitmapImage Thumb
        {
            get => thumb; set

            {
                if (thumb != value)
                {
                    thumb = value;
                    OnPropertyChanged(nameof(Thumb));
                }
            }
        }

        private int page;

        private BitmapImage thumb;
    }
}