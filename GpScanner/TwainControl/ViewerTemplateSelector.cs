using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TwainControl
{
    public class ViewerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Empty { get; set; }

        public DataTemplate Eyp { get; set; }

        public DataTemplate Img { get; set; }

        public DataTemplate Pdf { get; set; }

        public DataTemplate Vid { get; set; }

        public DataTemplate Xlsx { get; set; }

        public DataTemplate Xml { get; set; }

        public DataTemplate Xps { get; set; }

        public DataTemplate Zip { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (!DesignerProperties.GetIsInDesignMode(container) && item is string dosya)
            {
                string[] imgext = [".jpg", ".jpeg", ".bmp", ".png", ".tif", ".tiff", ".tıf", ".tıff"];
                string[] videoext = [".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg"];
                string ext = Path.GetExtension(dosya).ToLower();
                if (ext != null)
                {
                    return ext switch
                    {
                        ".pdf" => Pdf,
                        ".eyp" => Eyp,
                        ".zip" => Zip,
                        ".xps" => Xps,
                        ".xml" or ".xsl" or ".xslt" or ".xaml" => Xml,
                        ".csv" or ".xls" or ".xlsx" or ".xlsb" => Xlsx,
                        _ => imgext.Contains(ext) ? Img : videoext.Contains(ext) ? Vid : Empty
                    };
                }
            }

            return null;
        }
    }
}
