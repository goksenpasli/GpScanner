using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DvdBurner
{
    public class ViewerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Empty { get; set; }

        public DataTemplate Img { get; set; }

        public DataTemplate Vid { get; set; }

        public DataTemplate Zip { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (!DesignerProperties.GetIsInDesignMode(container) && item is string dosya)
            {
                string[] imgext = [".jpg", ".bmp", ".png", ".tif", ".tiff"];
                string[] videoext = [".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg"];
                string ext = Path.GetExtension(dosya).ToLowerInvariant();
                if (ext != null)
                {
                    return ext switch
                    {
                        ".zip" => Zip,
                        _ => imgext.Contains(ext) ? Img : videoext.Contains(ext) ? Vid : Empty
                    };
                }
            }

            return null;
        }
    }
}
