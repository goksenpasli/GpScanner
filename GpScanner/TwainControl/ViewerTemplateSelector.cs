using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TwainControl
{
    public class ViewerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Cbr { get; set; }

        public DataTemplate Docx { get; set; }

        public DataTemplate Empty { get; set; }

        public DataTemplate Eyp { get; set; }

        public DataTemplate Img { get; set; }

        public DataTemplate Jb2 { get; set; }

        public DataTemplate Jb2Zip { get; set; }

        public DataTemplate Pdf { get; set; }

        public DataTemplate Vid { get; set; }

        public DataTemplate Webp { get; set; }

        public DataTemplate Xlsx { get; set; }

        public DataTemplate Xml { get; set; }

        public DataTemplate Xps { get; set; }

        public DataTemplate Zip { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (!DesignerProperties.GetIsInDesignMode(container) && item is string dosya)
            {
                string[] imgext = [".jpg", ".jpeg", ".bmp", ".png", ".tif", ".tiff"];
                string[] archiveext = [".7z", ".arj", ".bzip2", ".cab", ".gzip", ".iso", ".lzh", ".lzma", ".ntfs", ".ppmd", ".rar", ".rar5", ".rpm", ".tar", ".vhd", ".wim", ".xar", ".xz", ".z", ".zip", ".gz"];
                string[] videoext = [".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg"];
                string ext = Path.GetExtension(dosya).ToLowerInvariant();
                if (ext is not null)
                {
                    return ext switch
                    {
                        ".pdf" => Pdf,
                        ".eyp" => Eyp,
                        ".xps" => Xps,
                        ".webp" => Webp,
                        ".jb2" => Jb2,
                        ".jb2zip" => Jb2Zip,
                        ".cbr" or ".cbz" => Cbr,
                        ".docx" or ".txt" or ".odt" => Docx,
                        ".xml" or ".xsl" or ".xslt" or ".xaml" => Xml,
                        ".csv" or ".xls" or ".xlsx" or ".xlsb" or ".ods" => Xlsx,
                        _ => imgext.Contains(ext) ? Img : archiveext.Contains(ext) ? Zip : videoext.Contains(ext) ? Vid : Empty
                    };
                }
            }

            return null;
        }
    }
}
