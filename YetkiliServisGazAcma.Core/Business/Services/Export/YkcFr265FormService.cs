using System.IO.Compression;
using System.Xml.Linq;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YkcFr265FormService
    {
        private const string TemplateRelativePath = "Templates/Ykc/FR265_Yakici_Cihaz_Degisim_Formu.docx";
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        public YkcFr265BelgeSonuc WordOlustur(YkcTalepDetayDto talep)
        {
            var templatePath = FindTemplatePath();
            using var output = new MemoryStream();

            using (var template = File.OpenRead(templatePath))
            {
                template.CopyTo(output);
            }

            using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
            {
                var documentEntry = archive.GetEntry("word/document.xml")
                    ?? throw new InvalidOperationException("FR265 sablonu icinde word/document.xml bulunamadi.");

                string xml;
                using (var reader = new StreamReader(documentEntry.Open()))
                {
                    xml = reader.ReadToEnd();
                }

                var document = XDocument.Parse(xml);
                Doldur(document, talep);
                KagitKontrolBloklariniKaldir(document);

                documentEntry.Delete();
                var updatedEntry = archive.CreateEntry("word/document.xml", CompressionLevel.Optimal);
                using var writer = new StreamWriter(updatedEntry.Open());
                document.Save(writer, SaveOptions.DisableFormatting);
            }

            return new YkcFr265BelgeSonuc
            {
                Bytes = output.ToArray(),
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DosyaAdi = $"FR265_Cihaz_Degisim_Talebi_{SafeFilePart(talep.TesisatNo ?? talep.Id.ToString())}_{talep.Id}.docx"
            };
        }

        private static void Doldur(XDocument document, YkcTalepDetayDto talep)
        {
            var formTarihi = FormTarihi(talep);
            foreach (var textNode in document.Descendants(W + "t"))
            {
                if (textNode.Value.Contains("Tarih :"))
                    textNode.Value = $"Tarih : {formTarihi}";
            }

            var tables = document.Descendants(W + "tbl").ToList();

            SetCellText(tables, 0, 0, 1, talep.FirmaAdi);
            SetCellText(tables, 1, 0, 1, talep.YetkiBelgesiNo);

            SetCellText(tables, 2, 0, 1, talep.MusteriAdi);
            SetCellText(tables, 2, 1, 1, talep.TesisatNo);
            SetCellText(tables, 2, 2, 1, talep.TuketimNoktasi);
            SetCellText(tables, 2, 3, 1, talep.BaglantiNesnesi);
            SetCellText(tables, 2, 4, 1, talep.Adres);

            SetCellText(tables, 3, 1, 1, talep.EskiCihazTipi);
            SetCellText(tables, 3, 1, 2, talep.YeniCihazTipi);
            SetCellText(tables, 3, 2, 1, talep.EskiMarka);
            SetCellText(tables, 3, 2, 2, talep.YeniMarka);
            SetCellText(tables, 3, 3, 1, talep.EskiBacaTipi);
            SetCellText(tables, 3, 3, 2, talep.YeniBacaTipi);
            SetCellText(tables, 3, 4, 1, talep.EskiKapasite);
            SetCellText(tables, 3, 4, 2, talep.YeniKapasite);

            SetCellText(tables, 4, 0, 1, talep.IkinciElCihazMi == true ? "☒ Evet" : "☐ Evet");
            SetCellText(tables, 4, 0, 2, talep.IkinciElCihazMi == false ? "☒ Hayır" : "☐ Hayır");

            SetCellText(tables, 5, 1, 0, FirmaImzaMetni(talep, formTarihi));
        }

        private static void KagitKontrolBloklariniKaldir(XDocument document)
        {
            var kontrolTablolari = document
                .Descendants(W + "tbl")
                .Where(table => TableText(table).Contains("KONTROL", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var table in kontrolTablolari)
                table.Remove();
        }

        private static string TableText(XElement table)
        {
            return string.Concat(table.Descendants(W + "t").Select(x => x.Value));
        }

        private static string FirmaImzaMetni(YkcTalepDetayDto talep, string formTarihi)
        {
            var yetkili = Clean(talep.FirmaYetkiliKisi);
            var satirlar = new List<string>
            {
                "Sertifikali Firma Yetkilisi",
                "",
                $"Adi ve Soyadi: {yetkili}",
                $"Tarih: {formTarihi}",
                "Imza:"
            };

            return string.Join(Environment.NewLine, satirlar);
        }

        private static string FormTarihi(YkcTalepDetayDto talep)
        {
            return talep.TalepTarihi.ToString("dd.MM.yyyy");
        }

        private static void SetCellText(IReadOnlyList<XElement> tables, int tableIndex, int rowIndex, int cellIndex, string? value)
        {
            if (tableIndex >= tables.Count)
                return;

            var rows = tables[tableIndex].Elements(W + "tr").ToList();
            if (rowIndex >= rows.Count)
                return;

            var cells = rows[rowIndex].Elements(W + "tc").ToList();
            if (cellIndex >= cells.Count)
                return;

            var cell = cells[cellIndex];
            foreach (var child in cell.Elements().Where(x => x.Name != W + "tcPr").ToList())
                child.Remove();

            var lines = SplitLines(value);
            foreach (var line in lines)
                cell.Add(Paragraph(line));
        }

        private static XElement Paragraph(string text)
        {
            return new XElement(W + "p",
                new XElement(W + "r",
                    new XElement(W + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        text)));
        }

        private static List<string> SplitLines(string? value)
        {
            var clean = Clean(value);
            if (string.IsNullOrWhiteSpace(clean))
                return new List<string> { string.Empty };

            return clean
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .ToList();
        }

        private static string Clean(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string FindTemplatePath()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, TemplateRelativePath),
                Path.Combine(Directory.GetCurrentDirectory(), TemplateRelativePath)
            };

            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                candidates.Add(Path.Combine(directory.FullName, "YetkiliServisGazAcma.Core", TemplateRelativePath));
                directory = directory.Parent;
            }

            var templatePath = candidates.FirstOrDefault(File.Exists);
            if (templatePath == null)
                throw new FileNotFoundException("FR265 Word sablonu bulunamadi.", TemplateRelativePath);

            return templatePath;
        }

        private static string SafeFilePart(string value)
        {
            var chars = value
                .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                .ToArray();

            return chars.Length == 0 ? "Talep" : new string(chars);
        }
    }

    public class YkcFr265BelgeSonuc
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string DosyaAdi { get; set; } = "FR265_Cihaz_Degisim_Formu.docx";
    }
}
