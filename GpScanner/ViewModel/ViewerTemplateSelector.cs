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

        public DataTemplate Xml { get; set; }

        public DataTemplate Xps { get; set; }

        public DataTemplate Zip { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && item is string dosya)
            {
                string[] imgext = new string[] { ".jpg", ".bmp", ".png", ".tif", ".tiff", ".tıf", ".tıff" };
                string[] videoext = new string[] { ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg" };
                string ext = Path.GetExtension(dosya).ToLower();
                return ext == ".pdf"
                    ? Pdf
                    : ext == ".zip"
                    ? Zip
                    : ext == ".xps" ? Xps : ext is ".xml" or ".xsl" or ".xslt" or ".xaml" ? Xml : imgext.Contains(ext) ? Img : videoext.Contains(ext) ? Vid : Empty;
            }
            return null;
        }
    }
}