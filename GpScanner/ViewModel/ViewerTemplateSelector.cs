﻿using System.IO;
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
            if (item is string dosya)
            {
                string[] imgext = new string[] { ".jpg", ".bmp", ".png", ".tif", ".tiff" };
                string[] videoext = new string[] { ".mp4", ".3gp", ".wmv", ".mpg", ".mov", ".avi", ".mpeg" };
                string ext = Path.GetExtension(dosya).ToLower();
                if (ext == ".pdf")
                {
                    return Pdf;
                }
                if (ext == ".zip")
                {
                    return Zip;
                }
                if (ext == ".xps")
                {
                    return Xps;
                }
                if (imgext.Contains(ext))
                {
                    return Img;
                }
                if (videoext.Contains(ext))
                {
                    return Vid;
                }
                return Empty;
            }
            return null;
        }
    }
}