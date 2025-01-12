using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xceed.Document.NET;
using Xceed.Words.NET;
using Color = System.Windows.Media.Color;
using FormattedText = Xceed.Document.NET.FormattedText;
using Paragraph = Xceed.Document.NET.Paragraph;
using Run = System.Windows.Documents.Run;

namespace TwainControl
{
    /// <summary>
    /// Interaction logic for DocxViewer.xaml
    /// </summary>
    public partial class DocxViewer : UserControl
    {
        public static readonly DependencyProperty DocxDataFilePathProperty = DependencyProperty.Register("DocxDataFilePath", typeof(string), typeof(DocxViewer), new PropertyMetadata(null, DocxDataFilePathChanged));

        public DocxViewer() { InitializeComponent(); }

        public string DocxDataFilePath { get => (string)GetValue(DocxDataFilePathProperty); set => SetValue(DocxDataFilePathProperty, value); }

        protected override void OnDrop(DragEventArgs e)
        {
            if ((e?.Data?.GetData(DataFormats.FileDrop) is string[] droppedfiles) && (droppedfiles?.Length > 0))
            {
                if (Path.GetExtension(droppedfiles[0]).ToLowerInvariant() is ".docx" or ".txt" or ".xml" or ".xsl" or ".xslt" or ".xaml" or ".log" or ".odt")
                {
                    DocxDataFilePath = droppedfiles[0];
                }
            }
        }

        private static BlockUIContainer BlockUIContainerGetPicture(Picture picture)
        {
            System.Windows.Controls.Image image = new() { Source = BitmapFrame.Create(picture.Stream, BitmapCreateOptions.None, BitmapCacheOption.None) };
            return new BlockUIContainer(image);
        }

        private static void DocxDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (d is DocxViewer viewer && e.NewValue is string uriString)
                {
                    if (!File.Exists(uriString))
                    {
                        return;
                    }
                    viewer.Fd.Document = viewer.GetFlowDocument(uriString);
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex?.Message);
            }
        }

        private static FlowDocument DocxFlowDocument(DocX document)
        {
            FlowDocument fd = new();
            foreach (Paragraph docxparagraph in document?.Paragraphs)
            {
                System.Windows.Documents.Paragraph paragraph = new();
                foreach (FormattedText formattedText in docxparagraph?.MagicText)
                {
                    paragraph.Inlines.Add(GetRun(docxparagraph, paragraph, formattedText));
                    fd.Blocks.Add(paragraph);
                }

                if (docxparagraph?.Pictures?.Count > 0)
                {
                    foreach (Picture picture in docxparagraph?.Pictures)
                    {
                        fd.Blocks.Add(BlockUIContainerGetPicture(picture));
                    }
                }
            }

            return fd;
        }

        private static Run GetRun(Paragraph docxparagraph, System.Windows.Documents.Paragraph paragraph, FormattedText formattedText)
        {
            Run inline = new(formattedText.text)
            {
                FontSize = formattedText.formatting?.Size * 4 / 3 ?? 16,
                FontFamily = formattedText.formatting?.FontFamily is null ? new System.Windows.Media.FontFamily("Times New Roman") : new System.Windows.Media.FontFamily(formattedText.formatting?.FontFamily.Name)
            };
            if (formattedText?.formatting is not null)
            {
                if (formattedText.formatting.FontColor.HasValue)
                {
                    SolidColorBrush sb = new(Color.FromArgb(formattedText.formatting.FontColor.Value.A, formattedText.formatting.FontColor.Value.R, formattedText.formatting.FontColor.Value.G, formattedText.formatting.FontColor.Value.B));
                    sb.Freeze();
                    inline.Foreground = sb;
                }

                if (formattedText.formatting.ShadingPattern is not null)
                {
                    SolidColorBrush sb = new(Color.FromArgb(formattedText.formatting.ShadingPattern.Fill.A, formattedText.formatting.ShadingPattern.Fill.R, formattedText.formatting.ShadingPattern.Fill.G, formattedText.formatting.ShadingPattern.Fill.B));
                    sb.Freeze();
                    inline.Background = sb;
                }

                if (formattedText.formatting.Bold == true)
                {
                    inline.FontWeight = FontWeights.Bold;
                }

                if (formattedText.formatting.Italic == true)
                {
                    inline.FontStyle = FontStyles.Italic;
                }

                if (formattedText.formatting.StrikeThrough.HasValue)
                {
                    inline.TextDecorations = TextDecorations.Strikethrough;
                }

                if (formattedText.formatting.UnderlineStyle.HasValue)
                {
                    inline.TextDecorations = TextDecorations.Underline;
                }

                if (formattedText.formatting.Script.HasValue)
                {
                    if (formattedText.formatting.Script == Script.subscript)
                    {
                        inline.BaselineAlignment = BaselineAlignment.Subscript;
                    }

                    if (formattedText.formatting.Script == Script.superscript)
                    {
                        inline.BaselineAlignment = BaselineAlignment.Superscript;
                    }
                }
            }

            paragraph.TextAlignment = docxparagraph.Alignment switch
            {
                Alignment.both => TextAlignment.Justify,
                Alignment.center => TextAlignment.Center,
                Alignment.left => TextAlignment.Left,
                Alignment.right => TextAlignment.Right,
                _ => TextAlignment.Left,
            };

            return inline;
        }

        private FlowDocument GetFlowDocument(string uriString)
        {
            if (Path.GetExtension(uriString.ToLowerInvariant()) == ".docx")
            {
                using FileStream fileStream = new(uriString, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using DocX document = DocX.Load(fileStream);
                return DocxFlowDocument(document);
            }
            if (Path.GetExtension(uriString.ToLowerInvariant()) is ".txt" or ".xml" or ".xsl" or ".xslt" or ".xaml" or ".log")
            {
                System.Windows.Documents.Paragraph paragraph = new();
                paragraph.Inlines.Add(File.ReadAllText(uriString));
                return new FlowDocument(paragraph);
            }
            if (Path.GetExtension(uriString.ToLowerInvariant()) is ".odt")
            {
                System.Windows.Documents.Paragraph paragraph = new();
                paragraph.Inlines.Add(OdtReader.ParseOdtFile(uriString));
                return new FlowDocument(paragraph);
            }
            return null;
        }
    }
}
