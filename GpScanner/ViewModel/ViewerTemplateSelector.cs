using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GpScanner.ViewModel
{
    public class ViewerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Empty { get; set; }

        public DataTemplate Img { get; set; }

        public DataTemplate Pdf { get; set; }

        public DataTemplate Vid { get; set; }

        public DataTemplate Xps { get; set; }

        public DataTemplate Zip { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                if (item is string dosya)
                {
                    string[] imgext = new string[] { ".jpg", ".bmp", ".png", ".tif", ".tiff" };
                    string[] videoext = new string[] { ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg" };
                    string ext = Path.GetExtension(dosya).ToLower();
                    return ext == ".pdf"
                        ? Pdf
                        : ext == ".zip" ? Zip : ext == ".xps" ? Xps : imgext.Contains(ext) ? Img : videoext.Contains(ext) ? Vid : Empty;
                }
                return null;
            }
            return null;
        }
    }
}