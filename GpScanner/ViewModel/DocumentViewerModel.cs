using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Extensions;

namespace GpScanner.ViewModel
{
    public class DocumentViewerModel : InpcBase
    {
        public DocumentViewerModel()
        {
            Back = new RelayCommand<object>(parameter =>
            {
                Index--;
                PdfFilePath = DirectoryAllPdfFiles?.ElementAtOrDefault(Index);
            }, parameter => Index > 0);

            Forward = new RelayCommand<object>(parameter =>
            {
                Index++;
                PdfFilePath = DirectoryAllPdfFiles?.ElementAtOrDefault(Index);
            }, parameter => Index < DirectoryAllPdfFiles?.Count() - 1);
        }

        public ICommand Back { get; }

        public IEnumerable<string> DirectoryAllPdfFiles { get; set; }

        public ICommand Forward { get; }

        public int Index
        {
            get => ındex;

            set
            {
                if (ındex != value)
                {
                    ındex = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }

        public string PdfFilePath
        {
            get => pdfFilePath;

            set
            {
                if (pdfFilePath != value)
                {
                    pdfFilePath = value;
                    OnPropertyChanged(nameof(PdfFilePath));
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        public string Title { get => Path.GetFileName(PdfFilePath); set => title = value; }

        private int ındex;

        private string pdfFilePath;

        private string title;
    }
}