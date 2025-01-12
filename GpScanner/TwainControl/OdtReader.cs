using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace TwainControl
{
    public sealed class OdtReader
    {
        public static string ParseOdtFile(string filePath)
        {
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(filePath);
                ZipArchiveEntry contentEntry = archive.GetEntry("content.xml") ?? throw new InvalidDataException("The ODT file does not contain the required content.xml.");
                using Stream contentStream = contentEntry.Open();
                StringBuilder textContent = new();
                XmlReaderSettings settings = new() { IgnoreWhitespace = true, IgnoreComments = true };
                using (XmlReader reader = XmlReader.Create(contentStream, settings))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "text:p")
                        {
                            string paragraphText = ReadElementContentWithNested(reader);
                            _ = textContent.AppendLine(paragraphText);
                        }
                    }
                }

                return textContent.ToString().Trim();
            }
            catch (InvalidDataException ex)
            {
                throw new ArgumentException("The file is not a valid ODT file.", ex);
            }
            catch (XmlException ex)
            {
                throw new ArgumentException("Failed to parse the XML content of the ODT file.", ex);
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while parsing the ODT file.", ex);
            }
        }

        private static string ReadElementContentWithNested(XmlReader reader)
        {
            StringBuilder elementContent = new();

            if (!reader.IsEmptyElement)
            {
                int depth = reader.Depth;
                while (reader.Read() && (reader.Depth > depth || reader.NodeType != XmlNodeType.EndElement))
                {
                    if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
                    {
                        _ = elementContent.Append(reader.Value);
                    }
                    else if (reader.NodeType == XmlNodeType.Element)
                    {
                        _ = elementContent.Append(ReadElementContentWithNested(reader));
                    }
                }
            }
            return elementContent.ToString();
        }
    }
}
