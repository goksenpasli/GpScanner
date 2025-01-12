using SevenZipExtractor;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace TwainControl
{
    internal sealed class OdsReader
    {
        private static readonly string[,] namespaces =
        {
        {
            "table",
            "urn:oasis:names:tc:opendocument:xmlns:table:1.0"
        },
        {
            "office",
            "urn:oasis:names:tc:opendocument:xmlns:office:1.0"
        },
        {
            "style",
            "urn:oasis:names:tc:opendocument:xmlns:style:1.0"
        },
        {
            "text",
            "urn:oasis:names:tc:opendocument:xmlns:text:1.0"
        },
        {
            "draw",
            "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
        },
        {
            "fo",
            "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"
        },
        {
            "dc",
            "http://purl.org/dc/elements/1.1/"
        },
        {
            "meta",
            "urn:oasis:names:tc:opendocument:xmlns:meta:1.0"
        },
        {
            "number",
            "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0"
        },
        {
            "presentation",
            "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0"
        },
        {
            "svg",
            "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"
        },
        {
            "chart",
            "urn:oasis:names:tc:opendocument:xmlns:chart:1.0"
        },
        {
            "dr3d",
            "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0"
        },
        {
            "math",
            "http://www.w3.org/1998/Math/MathML"
        },
        {
            "form",
            "urn:oasis:names:tc:opendocument:xmlns:form:1.0"
        },
        {
            "script",
            "urn:oasis:names:tc:opendocument:xmlns:script:1.0"
        },
        {
            "ooo",
            "http://openoffice.org/2004/office"
        },
        {
            "ooow",
            "http://openoffice.org/2004/writer"
        },
        {
            "oooc",
            "http://openoffice.org/2004/calc"
        },
        {
            "dom",
            "http://www.w3.org/2001/xml-events"
        },
        {
            "xforms",
            "http://www.w3.org/2002/xforms"
        },
        {
            "xsd",
            "http://www.w3.org/2001/XMLSchema"
        },
        {
            "xsi",
            "http://www.w3.org/2001/XMLSchema-instance"
        },
        {
            "rpt",
            "http://openoffice.org/2005/report"
        },
        {
            "of",
            "urn:oasis:names:tc:opendocument:xmlns:of:1.2"
        },
        {
            "rdfa",
            "http://docs.oasis-open.org/opendocument/meta/rdfa#"
        },
        {
            "config",
            "urn:oasis:names:tc:opendocument:xmlns:config:1.0"
        }
        };

        public static async Task<DataSet> ReadOdsFile(FileStream fs, string path)
        {
            return await Task.Run(
                async () =>
                {
                    using ArchiveFile odsfile = new(fs);
                    Entry dosya = odsfile.Entries.FirstOrDefault(entry => entry.FileName == "content.xml");
                    using Stream contentStream = new MemoryStream();
                    dosya.Extract(contentStream);
                    _ = contentStream.Seek(0, SeekOrigin.Begin);
                    XmlDocument contentXml = new();
                    contentXml.Load(contentStream);
                    XmlNamespaceManager nmsManager = InitializeXmlNamespaceManager(contentXml);
                    DataSet odsFile = new(Path.GetFileName(path));
                    foreach (XmlNode tableNode in GetTableNodes(contentXml, nmsManager))
                    {
                        odsFile.Tables.Add(await GetSheetAsync(tableNode, nmsManager));
                    }
                    return odsFile;
                });
        }

        private static void GetCell(XmlNode cellNode, DataRow row, ref int cellIndex)
        {
            XmlAttribute cellRepeated = cellNode.Attributes["table:number-columns-repeated"];
            if (cellRepeated == null)
            {
                DataTable sheet = row.Table;
                while (sheet.Columns.Count <= cellIndex)
                {
                    _ = sheet.Columns.Add();
                }

                row[cellIndex] = ReadCellValue(cellNode);
                cellIndex++;
            }
            else
            {
                cellIndex += Convert.ToInt32(cellRepeated.Value, CultureInfo.InvariantCulture);
            }
        }

        private static async Task GetRowAsync(XmlNode rowNode, DataTable sheet, XmlNamespaceManager nmsManager)
        {
            await Task.Run(
                () =>
                {
                    DataRow row = sheet.NewRow();
                    XmlNodeList cellNodes = rowNode.SelectNodes("table:table-cell", nmsManager);
                    int cellIndex = 0;
                    foreach (XmlNode cellNode in cellNodes)
                    {
                        GetCell(cellNode, row, ref cellIndex);
                    }
                    sheet.Rows.Add(row);
                    if (sheet.Rows.Count == 0)
                    {
                        sheet.Rows.Add(sheet.NewRow());
                        _ = sheet.Columns.Add();
                    }
                });
        }

        private static async Task<DataTable> GetSheetAsync(XmlNode tableNode, XmlNamespaceManager nmsManager)
        {
            return await Task.Run(
                async () =>
                {
                    DataTable sheet = new(tableNode.Attributes["table:name"].Value);
                    foreach (XmlNode rowNode in tableNode.SelectNodes("table:table-row", nmsManager))
                    {
                        await GetRowAsync(rowNode, sheet, nmsManager);
                    }

                    return sheet;
                });
        }

        private static XmlNodeList GetTableNodes(XmlDocument contentXmlDocument, XmlNamespaceManager nmsManager) => contentXmlDocument.SelectNodes("/office:document-content/office:body/office:spreadsheet/table:table", nmsManager);

        private static XmlNamespaceManager InitializeXmlNamespaceManager(XmlDocument xmlDocument)
        {
            XmlNamespaceManager nmsManager = new(xmlDocument.NameTable);
            for (int i = 0; i < namespaces.GetLength(0); i++)
            {
                nmsManager.AddNamespace(namespaces[i, 0], namespaces[i, 1]);
            }

            return nmsManager;
        }

        private static string ReadCellValue(XmlNode cell)
        {
            XmlAttribute cellVal = cell.Attributes["office:value"];
            return cellVal == null ? string.IsNullOrEmpty(cell.InnerText) ? null : cell.InnerText : cellVal.Value;
        }
    }
}
