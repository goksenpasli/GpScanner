using Extensions;
using System.Collections.ObjectModel;
using TwainControl;

namespace GpScanner.ViewModel
{
    public class SupportedExtensions : InpcBase
    {
        public ObservableCollection<FileCategory> FileCategories
        {
            get;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged(nameof(FileCategories));
                }
            }
        } = [ new FileCategory
        {
            Category = Translation.GetResStringValue("IMG"),
            Extensions =
            [ new() { Name = ".webp", IsChecked = true }, new() { Name = ".tiff", IsChecked = true }, new() { Name = ".tif", IsChecked = true }, new() { Name = ".jpg", IsChecked = true }, new() { Name = ".jpeg", IsChecked = true }, new()
            {
                Name = ".jpe",
                IsChecked = true
            }, new() { Name = ".png", IsChecked = true }, new() { Name = ".bmp", IsChecked = true }, new() { Name = ".jb2", IsChecked = true }, new() { Name = ".jb2zip", IsChecked = true }, new() { Name = ".cbz", IsChecked = true }, new()
            {
                Name = ".cbr",
                IsChecked = true
            }, new() { Name = ".j2k", IsChecked = true } ]
        }, new FileCategory
        {
            Category = Translation.GetResStringValue("DOCUMENT"),
            Extensions =
            [ new() { Name = ".pdf", IsChecked = true }, new() { Name = ".xps", IsChecked = true }, new() { Name = ".xml", IsChecked = true }, new() { Name = ".xsl", IsChecked = true }, new() { Name = ".xslt", IsChecked = true }, new()
            {
                Name = ".xaml",
                IsChecked = true
            }, new() { Name = ".xls", IsChecked = true }, new() { Name = ".xlsx", IsChecked = true }, new() { Name = ".xlsb", IsChecked = true }, new() { Name = ".csv", IsChecked = true }, new() { Name = ".ods", IsChecked = true }, new()
            {
                Name = ".odt",
                IsChecked = true
            }, new() { Name = ".docx", IsChecked = true }, new() { Name = ".eyp", IsChecked = true } ]
        }, new FileCategory
        {
            Category = Translation.GetResStringValue("ARCHIVE"),
            Extensions =
            [ new() { Name = ".zip", IsChecked = true, SearchInArchiveSupported = true }, new() { Name = ".rar", IsChecked = true, SearchInArchiveSupported = true }, new() { Name = ".7z", IsChecked = true, SearchInArchiveSupported = true }, new()
            {
                Name = ".tar",
                IsChecked = true,
                SearchInArchiveSupported = true
            }, new() { Name = ".arj", IsChecked = true, SearchInArchiveSupported = true }, new() { Name = ".gzip", IsChecked = true, SearchInArchiveSupported = true } ]
        }, new FileCategory
        {
            Category = Translation.GetResStringValue("VID"),
            Extensions =
            [ new() { Name = ".mp4", IsChecked = true }, new() { Name = ".wmv", IsChecked = true }, new() { Name = ".mpg", IsChecked = true }, new() { Name = ".3gp", IsChecked = true }, new() { Name = ".mov", IsChecked = true }, new()
            {
                Name = ".avi",
                IsChecked = true
            }, new() { Name = ".mpeg", IsChecked = true } ]
        }, ];
    }
}
