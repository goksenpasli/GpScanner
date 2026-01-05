using Extensions;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
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
            System.Windows.Controls.Image image = new();
            BitmapFrame bitmapFrame = Path.GetExtension(picture.FileName.ToLowerInvariant()) == ".emf"
                                      ? BitmapFrame.Create(EmfFileToBitmapSource(picture.Stream).ToBitmapImage())
                                      : BitmapFrame.Create(picture.Stream, BitmapCreateOptions.None, BitmapCacheOption.None);
            bitmapFrame?.Freeze();
            image.Source = bitmapFrame;
            return new BlockUIContainer(image);
        }

        private static void DocxDataFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (d is DocxViewer viewer && e.NewValue is string uriString)
                {
                    if (string.IsNullOrWhiteSpace(uriString))
                    {
                        viewer.Fd.Document = null;
                        return;
                    }
                    if (File.Exists(uriString))
                    {
                        viewer.Fd.Document = viewer.GetFlowDocument(uriString);
                    }
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
                try
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
                catch
                {
                }
            }

            return fd;
        }

        private static BitmapSource EmfFileToBitmapSource(Stream path)
        {
            using Metafile emf = new(path);
            using System.Drawing.Bitmap bmp = new(emf.Width, emf.Height);
            bmp.SetResolution(emf.HorizontalResolution, emf.VerticalResolution);
            using System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp);
            g.DrawImage(emf, 0, 0);
            return Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private static Run GetRun(Paragraph docxParagraph, System.Windows.Documents.Paragraph wpfParagraph, FormattedText formattedText)
        {
            Run run = new(formattedText.text ?? string.Empty);

            Formatting fmt = formattedText.formatting;
            if (fmt is not null)
            {
                run.FontSize = (formattedText.formatting?.Size * 4 / 3) ?? 16;
                run.FontFamily = fmt.FontFamily is not null ? new FontFamily(fmt.FontFamily.Name) : new FontFamily("Times New Roman");

                if (fmt.FontColor.HasValue)
                {
                    Xceed.Drawing.Color c = fmt.FontColor.Value;
                    SolidColorBrush brush = new(Color.FromArgb(c.A, c.R, c.G, c.B));
                    brush.Freeze();
                    run.Foreground = brush;
                }

                if (fmt.ShadingPattern?.Fill is not null)
                {
                    Xceed.Drawing.Color c = fmt.ShadingPattern.Fill;
                    SolidColorBrush brush = new(Color.FromArgb(c.A, c.R, c.G, c.B));
                    brush.Freeze();
                    run.Background = brush;
                }

                if (fmt.Bold == true)
                {
                    run.FontWeight = FontWeights.Bold;
                }

                if (fmt.Italic == true)
                {
                    run.FontStyle = FontStyles.Italic;
                }

                TextDecorationCollection decorations = null;

                if (fmt.UnderlineStyle.HasValue)
                {
                    decorations ??= [];
                    decorations.Add(TextDecorations.Underline[0]);
                }

                if (fmt.StrikeThrough.HasValue)
                {
                    decorations ??= [];
                    decorations.Add(TextDecorations.Strikethrough[0]);
                }

                if (decorations is not null)
                {
                    run.TextDecorations = decorations;
                }

                if (fmt.Script == Script.subscript)
                {
                    run.BaselineAlignment = BaselineAlignment.Subscript;
                }
                else if (fmt.Script == Script.superscript)
                {
                    run.BaselineAlignment = BaselineAlignment.Superscript;
                }
            }

            wpfParagraph.TextAlignment = docxParagraph.Alignment switch
            {
                Alignment.both => TextAlignment.Justify,
                Alignment.center => TextAlignment.Center,
                Alignment.right => TextAlignment.Right,
                _ => TextAlignment.Left,
            };

            return run;
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
