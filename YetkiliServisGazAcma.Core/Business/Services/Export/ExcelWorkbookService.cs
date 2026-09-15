using System.IO.Compression;
using System.Text;
using System.Xml;

namespace YetkiliServisGazAcma.Business.Services
{
    internal static class ExcelWorkbookService
    {
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string OfficeRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static byte[] Olustur(
            string sayfaAdi,
            IReadOnlyList<string> basliklar,
            IEnumerable<IReadOnlyList<string?>> satirlar)
        {
            ArgumentNullException.ThrowIfNull(basliklar);
            ArgumentNullException.ThrowIfNull(satirlar);

            if (basliklar.Count == 0)
                throw new ArgumentException("Excel dosyası en az bir sütun içermelidir.", nameof(basliklar));

            var kayitlar = satirlar.Select(x => x.ToArray()).ToList();
            var sutunGenislikleri = SutunGenislikleri(basliklar, kayitlar);

            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                XmlYaz(archive, "[Content_Types].xml", ContentTypes);
                XmlYaz(archive, "_rels/.rels", RootRelationships);
                XmlYaz(archive, "xl/workbook.xml", writer => Workbook(writer, TemizSayfaAdi(sayfaAdi)));
                XmlYaz(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
                XmlYaz(archive, "xl/styles.xml", Styles);
                XmlYaz(archive, "xl/worksheets/sheet1.xml", writer => Worksheet(writer, basliklar, kayitlar, sutunGenislikleri));
            }

            return stream.ToArray();
        }

        private static void ContentTypes(XmlWriter writer)
        {
            writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            ContentType(writer, "Default", "Extension", "rels", "application/vnd.openxmlformats-package.relationships+xml");
            ContentType(writer, "Default", "Extension", "xml", "application/xml");
            ContentType(writer, "Override", "PartName", "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            ContentType(writer, "Override", "PartName", "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            ContentType(writer, "Override", "PartName", "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            writer.WriteEndElement();
        }

        private static void ContentType(XmlWriter writer, string element, string key, string value, string contentType)
        {
            writer.WriteStartElement(element);
            writer.WriteAttributeString(key, value);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void RootRelationships(XmlWriter writer)
        {
            writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
            Relationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
            writer.WriteEndElement();
        }

        private static void WorkbookRelationships(XmlWriter writer)
        {
            writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
            Relationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
            Relationship(writer, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
            writer.WriteEndElement();
        }

        private static void Relationship(XmlWriter writer, string id, string type, string target)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static void Workbook(XmlWriter writer, string sayfaAdi)
        {
            writer.WriteStartElement("workbook", SpreadsheetNamespace);
            writer.WriteAttributeString("xmlns", "r", null, OfficeRelationshipNamespace);
            writer.WriteStartElement("sheets", SpreadsheetNamespace);
            writer.WriteStartElement("sheet", SpreadsheetNamespace);
            writer.WriteAttributeString("name", sayfaAdi);
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("r", "id", OfficeRelationshipNamespace, "rId1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void Styles(XmlWriter writer)
        {
            writer.WriteStartElement("styleSheet", SpreadsheetNamespace);

            writer.WriteStartElement("fonts", SpreadsheetNamespace);
            writer.WriteAttributeString("count", "2");
            Font(writer, bold: false, white: false);
            Font(writer, bold: true, white: true);
            writer.WriteEndElement();

            writer.WriteStartElement("fills", SpreadsheetNamespace);
            writer.WriteAttributeString("count", "3");
            PatternFill(writer, "none", null);
            PatternFill(writer, "gray125", null);
            PatternFill(writer, "solid", "1F5FBF");
            writer.WriteEndElement();

            writer.WriteStartElement("borders", SpreadsheetNamespace);
            writer.WriteAttributeString("count", "2");
            Border(writer, null);
            Border(writer, "D7E0EA");
            writer.WriteEndElement();

            writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace);
            writer.WriteAttributeString("count", "1");
            Xf(writer, 0, 0, 0, false, false);
            writer.WriteEndElement();

            writer.WriteStartElement("cellXfs", SpreadsheetNamespace);
            writer.WriteAttributeString("count", "3");
            Xf(writer, 0, 0, 0, false, false);
            Xf(writer, 1, 2, 1, true, true);
            Xf(writer, 0, 0, 1, true, false);
            writer.WriteEndElement();

            writer.WriteStartElement("cellStyles", SpreadsheetNamespace);
            writer.WriteAttributeString("count", "1");
            writer.WriteStartElement("cellStyle", SpreadsheetNamespace);
            writer.WriteAttributeString("name", "Normal");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteAttributeString("builtinId", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        private static void Font(XmlWriter writer, bool bold, bool white)
        {
            writer.WriteStartElement("font", SpreadsheetNamespace);
            if (bold) writer.WriteElementString("b", SpreadsheetNamespace, string.Empty);
            writer.WriteStartElement("sz", SpreadsheetNamespace);
            writer.WriteAttributeString("val", "11");
            writer.WriteEndElement();
            if (white)
            {
                writer.WriteStartElement("color", SpreadsheetNamespace);
                writer.WriteAttributeString("rgb", "FFFFFFFF");
                writer.WriteEndElement();
            }
            writer.WriteStartElement("name", SpreadsheetNamespace);
            writer.WriteAttributeString("val", "Calibri");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void PatternFill(XmlWriter writer, string patternType, string? color)
        {
            writer.WriteStartElement("fill", SpreadsheetNamespace);
            writer.WriteStartElement("patternFill", SpreadsheetNamespace);
            writer.WriteAttributeString("patternType", patternType);
            if (color != null)
            {
                writer.WriteStartElement("fgColor", SpreadsheetNamespace);
                writer.WriteAttributeString("rgb", "FF" + color);
                writer.WriteEndElement();
                writer.WriteStartElement("bgColor", SpreadsheetNamespace);
                writer.WriteAttributeString("indexed", "64");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void Border(XmlWriter writer, string? color)
        {
            writer.WriteStartElement("border", SpreadsheetNamespace);
            foreach (var side in new[] { "left", "right", "top", "bottom" })
            {
                writer.WriteStartElement(side, SpreadsheetNamespace);
                if (color != null)
                {
                    writer.WriteAttributeString("style", "thin");
                    writer.WriteStartElement("color", SpreadsheetNamespace);
                    writer.WriteAttributeString("rgb", "FF" + color);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteElementString("diagonal", SpreadsheetNamespace, string.Empty);
            writer.WriteEndElement();
        }

        private static void Xf(XmlWriter writer, int fontId, int fillId, int borderId, bool alignment, bool header)
        {
            writer.WriteStartElement("xf", SpreadsheetNamespace);
            writer.WriteAttributeString("numFmtId", "0");
            writer.WriteAttributeString("fontId", fontId.ToString());
            writer.WriteAttributeString("fillId", fillId.ToString());
            writer.WriteAttributeString("borderId", borderId.ToString());
            writer.WriteAttributeString("xfId", "0");
            if (fontId > 0) writer.WriteAttributeString("applyFont", "1");
            if (fillId > 0) writer.WriteAttributeString("applyFill", "1");
            if (borderId > 0) writer.WriteAttributeString("applyBorder", "1");
            if (alignment)
            {
                writer.WriteAttributeString("applyAlignment", "1");
                writer.WriteStartElement("alignment", SpreadsheetNamespace);
                writer.WriteAttributeString("vertical", "top");
                writer.WriteAttributeString("wrapText", "1");
                if (header) writer.WriteAttributeString("horizontal", "center");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void Worksheet(
            XmlWriter writer,
            IReadOnlyList<string> basliklar,
            IReadOnlyList<string?[]> satirlar,
            IReadOnlyList<double> sutunGenislikleri)
        {
            var sonSatir = satirlar.Count + 1;
            var sonSutun = SutunAdi(basliklar.Count);

            writer.WriteStartElement("worksheet", SpreadsheetNamespace);
            writer.WriteStartElement("dimension", SpreadsheetNamespace);
            writer.WriteAttributeString("ref", $"A1:{sonSutun}{sonSatir}");
            writer.WriteEndElement();

            writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
            writer.WriteStartElement("sheetView", SpreadsheetNamespace);
            writer.WriteAttributeString("workbookViewId", "0");
            writer.WriteStartElement("pane", SpreadsheetNamespace);
            writer.WriteAttributeString("ySplit", "1");
            writer.WriteAttributeString("topLeftCell", "A2");
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("sheetFormatPr", SpreadsheetNamespace);
            writer.WriteAttributeString("defaultRowHeight", "15");
            writer.WriteEndElement();

            writer.WriteStartElement("cols", SpreadsheetNamespace);
            for (var index = 0; index < sutunGenislikleri.Count; index++)
            {
                writer.WriteStartElement("col", SpreadsheetNamespace);
                writer.WriteAttributeString("min", (index + 1).ToString());
                writer.WriteAttributeString("max", (index + 1).ToString());
                writer.WriteAttributeString("width", sutunGenislikleri[index].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("sheetData", SpreadsheetNamespace);
            Satir(writer, 1, basliklar.Select(x => (string?)x).ToArray(), 1, basliklar.Count);
            for (var index = 0; index < satirlar.Count; index++)
                Satir(writer, index + 2, satirlar[index], 2, basliklar.Count);
            writer.WriteEndElement();

            writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
            writer.WriteAttributeString("ref", $"A1:{sonSutun}{sonSatir}");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void Satir(XmlWriter writer, int satirNo, IReadOnlyList<string?> alanlar, int stil, int sutunSayisi)
        {
            writer.WriteStartElement("row", SpreadsheetNamespace);
            writer.WriteAttributeString("r", satirNo.ToString());
            for (var index = 0; index < sutunSayisi; index++)
            {
                writer.WriteStartElement("c", SpreadsheetNamespace);
                writer.WriteAttributeString("r", $"{SutunAdi(index + 1)}{satirNo}");
                writer.WriteAttributeString("s", stil.ToString());
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is", SpreadsheetNamespace);
                writer.WriteStartElement("t", SpreadsheetNamespace);
                writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                writer.WriteString(TemizMetin(index < alanlar.Count ? alanlar[index] : null));
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static IReadOnlyList<double> SutunGenislikleri(IReadOnlyList<string> basliklar, IReadOnlyList<string?[]> satirlar)
        {
            var sonuclar = new double[basliklar.Count];
            for (var index = 0; index < basliklar.Count; index++)
            {
                var uzunluk = basliklar[index].Length;
                foreach (var satir in satirlar)
                {
                    if (index < satir.Length)
                        uzunluk = Math.Max(uzunluk, (satir[index] ?? string.Empty).Length);
                }
                sonuclar[index] = Math.Clamp(uzunluk + 2, 11, 42);
            }
            return sonuclar;
        }

        private static string SutunAdi(int sutunNo)
        {
            var sonuc = string.Empty;
            while (sutunNo > 0)
            {
                sutunNo--;
                sonuc = (char)('A' + sutunNo % 26) + sonuc;
                sutunNo /= 26;
            }
            return sonuc;
        }

        private static string TemizSayfaAdi(string value)
        {
            var temiz = new string((value ?? string.Empty)
                .Where(x => x is not '[' and not ']' and not ':' and not '*' and not '?' and not '/' and not '\\')
                .ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(temiz)) temiz = "Rapor";
            return temiz.Length <= 31 ? temiz : temiz[..31];
        }

        private static string TemizMetin(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (XmlConvert.IsXmlChar(ch)) sb.Append(ch);
            }
            return sb.ToString();
        }

        private static void XmlYaz(ZipArchive archive, string path, Action<XmlWriter> write)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var writer = XmlWriter.Create(entryStream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = false,
                CloseOutput = false
            });
            writer.WriteStartDocument();
            write(writer);
            writer.WriteEndDocument();
        }
    }
}
