using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TwainControl
{
    public class ViewerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Docx { get; set; }

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
                string[] archiveext = [".7z", ".arj", ".bzip2", ".cab", ".gzip", ".iso", ".lzh", ".lzma", ".ntfs", ".ppmd", ".rar", ".rar5", ".rpm", ".tar", ".vhd", ".wim", ".xar", ".xz", ".z", ".zip"];
                string[] videoext = [".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg"];
                string ext = Path.GetExtension(dosya).ToLower();
                if (ext != null)
                {
                    return ext switch
                    {
                        ".pdf" => Pdf,
                        ".eyp" => Eyp,
                        ".xps" => Xps,
                        ".docx" or ".txt" => Docx,
                        ".xml" or ".xsl" or ".xslt" or ".xaml" => Xml,
                        ".csv" or ".xls" or ".xlsx" or ".xlsb" => Xlsx,
                        _ => imgext.Contains(ext) ? Img : archiveext.Contains(ext) ? Zip : videoext.Contains(ext) ? Vid : Empty
                    };
                }
            }

            return null;
        }
    }
}
