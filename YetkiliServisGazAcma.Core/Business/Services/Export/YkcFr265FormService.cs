using System.IO.Compression;
using System.Xml.Linq;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YkcFr265FormService
    {
        private const string TemplateRelativePath = "Templates/Ykc/FR265_Yakici_Cihaz_Degisim_Formu.docx";
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        public YkcFr265BelgeSonuc WordOlustur(YkcTalepDetayDto talep, YkcFr265BelgeSecenekleri? secenekler = null)
        {
            var templatePath = FindTemplatePath();
            using var output = new MemoryStream();
            secenekler ??= new YkcFr265BelgeSecenekleri();

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
                Doldur(document, talep, secenekler);

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

        private static void Doldur(XDocument document, YkcTalepDetayDto talep, YkcFr265BelgeSecenekleri secenekler)
        {
            var formTarihi = FormTarihi(talep);
            foreach (var textNode in document.Descendants(W + "t"))
            {
                if (textNode.Value.Contains("Tarih :"))
                    textNode.Value = $"Tarih : {formTarihi}";
            }

            var firmaTablosu = FindTable(document, "Sertifikalı Firma Unvanı");
            var sertifikaTablosu = FindTable(document, "Sertifika Numarası");
            var tesisatTablosu = FindTable(document, "Tesisat Numarası", "Tüketim Noktası");
            var cihazTablosu = FindTable(document, "Projedeki Cihaz", "Yeni Kullanılan Cihaz");
            var ikinciElTablosu = FindTable(document, "Takılan Yakıcı Cihaz İkinci El Cihaz Mı");
            var firmaImzaTablosu = FindTable(document, "Sertifikalı Firma Yetkilisi");
            var gorulduImzaTablosu = FindTable(document, "Yukardaki bilgileri verilen yeni cihaz", "İşlemi Yapan Gaz Dağıtım");

            SetCellText(firmaTablosu, 0, 1, talep.FirmaAdi);
            SetCellText(sertifikaTablosu, 0, 1, talep.YetkiBelgesiNo);

            SetCellText(tesisatTablosu, 0, 1, talep.MusteriAdi);
            SetCellText(tesisatTablosu, 1, 1, talep.TesisatNo);
            SetCellText(tesisatTablosu, 2, 1, talep.TuketimNoktasi);
            SetCellText(tesisatTablosu, 3, 1, talep.BaglantiNesnesi);
            SetCellText(tesisatTablosu, 4, 1, talep.Adres);

            SetCellText(cihazTablosu, 1, 1, talep.EskiCihazTipi);
            SetCellText(cihazTablosu, 1, 2, talep.YeniCihazTipi);
            SetCellText(cihazTablosu, 2, 1, talep.EskiMarka);
            SetCellText(cihazTablosu, 2, 2, talep.YeniMarka);
            SetCellText(cihazTablosu, 3, 1, talep.EskiBacaTipi);
            SetCellText(cihazTablosu, 3, 2, talep.YeniBacaTipi);
            SetCellText(cihazTablosu, 4, 1, talep.EskiKapasite);
            SetCellText(cihazTablosu, 4, 2, talep.YeniKapasite);

            SetCellText(ikinciElTablosu, 0, 1, talep.IkinciElCihazMi == true ? "☒ Evet" : "☐ Evet");
            SetCellText(ikinciElTablosu, 0, 2, talep.IkinciElCihazMi == false ? "☒ Hayır" : "☐ Hayır");

            SetCellText(firmaImzaTablosu, 1, 0, FirmaImzaMetni(talep, formTarihi, secenekler));
            KontrolleriDoldur(document, talep.Kontroller);
            if (secenekler.ImzaliNihaiMi)
            {
                GorulduImzaTablosunuDoldur(gorulduImzaTablosu, talep, secenekler, formTarihi);
                KontrolImzaTablolariniDoldur(document, talep, secenekler, formTarihi);
            }
        }

        private static XElement? FindTable(XDocument document, params string[] labels)
        {
            var labelKeys = labels.Select(TextKey).ToList();
            return document
                .Descendants(W + "tbl")
                .FirstOrDefault(table =>
                {
                    var tableKey = TextKey(string.Concat(table.Descendants(W + "t").Select(x => x.Value)));
                    return labelKeys.All(tableKey.Contains);
                });
        }

        private static string TextKey(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private static void KontrolleriDoldur(XDocument document, IReadOnlyCollection<YkcFr265KontrolDto> kontroller)
        {
            var body = document.Root?.Element(W + "body");
            if (body == null)
                return;

            var bodyElements = body.Elements().ToList();
            for (var kontrolNo = 1; kontrolNo <= 5; kontrolNo++)
            {
                var baslikIndex = bodyElements.FindIndex(x =>
                    x.Name == W + "p" &&
                    ParagraphText(x).StartsWith($"{kontrolNo}. KONTROL", StringComparison.OrdinalIgnoreCase));

                if (baslikIndex < 0)
                    continue;

                var kontrol = kontroller.FirstOrDefault(x => x.KontrolNo == kontrolNo);
                var sonucParagrafi = SonrakiParagraf(bodyElements, baslikIndex + 1);
                var aciklamaEtiketiIndex = sonucParagrafi == null ? -1 : bodyElements.IndexOf(sonucParagrafi);
                var aciklamaParagrafi = aciklamaEtiketiIndex < 0
                    ? null
                    : SonrakiParagraf(bodyElements, aciklamaEtiketiIndex + 1, "Uygun değil ise nedeni:");

                if (sonucParagrafi != null)
                    SetParagraphText(sonucParagrafi, KontrolSonucMetni(kontrol?.Sonuc));

                if (aciklamaParagrafi != null)
                    SetParagraphText(aciklamaParagrafi, KontrolAciklamaMetni(kontrol));
            }
        }

        private static XElement? SonrakiParagraf(
            IReadOnlyList<XElement> elements,
            int baslangicIndex,
            string? atlanacakMetin = null)
        {
            for (var index = baslangicIndex; index < elements.Count; index++)
            {
                var element = elements[index];
                if (element.Name == W + "tbl")
                    return null;

                if (element.Name != W + "p")
                    continue;

                var metin = ParagraphText(element);
                if (!string.IsNullOrWhiteSpace(atlanacakMetin)
                    && metin.StartsWith(atlanacakMetin, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return element;
            }

            return null;
        }

        private static string KontrolSonucMetni(string? sonuc)
        {
            return sonuc switch
            {
                YkcFr265KontrolSonucDegerleri.Uygun => "☒ Uygun      ☐ Uygun Değil",
                YkcFr265KontrolSonucDegerleri.UygunDegil => "☐ Uygun      ☒ Uygun Değil",
                YkcFr265KontrolSonucDegerleri.Uygulanmaz => "☐ Uygun      ☐ Uygun Değil      ☒ Uygulanmaz",
                _ => "☐ Uygun      ☐ Uygun Değil"
            };
        }

        private static string KontrolAciklamaMetni(YkcFr265KontrolDto? kontrol)
        {
            if (!string.IsNullOrWhiteSpace(kontrol?.Aciklama))
                return kontrol.Aciklama.Trim();

            return kontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.Uygulanmaz
                ? "Uygulanmaz"
                : "................................................................................";
        }

        private static string ParagraphText(XElement paragraph)
        {
            return string.Concat(paragraph.Descendants(W + "t").Select(x => x.Value)).Trim();
        }

        private static void SetParagraphText(XElement paragraph, string value)
        {
            foreach (var child in paragraph.Elements().Where(x => x.Name != W + "pPr").ToList())
                child.Remove();

            paragraph.Add(new XElement(W + "r",
                new XElement(W + "t",
                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                    value)));
        }

        private static void GorulduImzaTablosunuDoldur(
            XElement? table,
            YkcTalepDetayDto talep,
            YkcFr265BelgeSecenekleri secenekler,
            string formTarihi)
        {
            if (table == null)
                return;

            var dagitim = ImzaSatiri(secenekler, 2);
            var abone = ImzaSatiri(secenekler, 3);
            var satirlar = new[]
            {
                "Yukardaki bilgileri verilen yeni cihaz belirtilen adreste görülmüştür.",
                "",
                "İşlemi Yapan Gaz Dağıtım Şirketi Yetkilisi",
                $"Adı ve Soyadı: {ImzaAdSoyad(dagitim, "Gaz Dağıtım Şirketi Yetkilisi")}",
                $"Tarih: {ImzaTarihi(dagitim, secenekler, formTarihi)}",
                $"İmza: {ImzaKaydiMetni()}",
                "",
                "İşlem Yapılan Abone/Kullanıcı",
                $"Adı ve Soyadı: {ImzaAdSoyad(abone, talep.MusteriAdi)}",
                $"Tarih: {ImzaTarihi(abone, secenekler, formTarihi)}",
                $"İmza: {ImzaKaydiMetni()}"
            };

            SetCellText(table, 0, 0, string.Join(Environment.NewLine, satirlar));
        }

        private static void KontrolImzaTablolariniDoldur(
            XDocument document,
            YkcTalepDetayDto talep,
            YkcFr265BelgeSecenekleri secenekler,
            string formTarihi)
        {
            var firma = ImzaSatiri(secenekler, 1);
            var dagitim = ImzaSatiri(secenekler, 2);
            var abone = ImzaSatiri(secenekler, 3);

            var imzaTablolari = document
                .Descendants(W + "tbl")
                .Where(table =>
                {
                    var tableKey = TextKey(string.Concat(table.Descendants(W + "t").Select(x => x.Value)));
                    return tableKey.Contains(TextKey("Gaz Dağıtım Şirketi Yetkilisi"))
                        && tableKey.Contains(TextKey("Abone / Kullanıcı"))
                        && tableKey.Contains(TextKey("Sertifikalı Firma"))
                        && tableKey.Contains(TextKey("Kaşe / İmza"));
                })
                .ToList();

            foreach (var table in imzaTablolari)
            {
                SetCellText(table, 1, 0, $"Adı Soyadı{Environment.NewLine}{ImzaAdSoyad(dagitim, "Gaz Dağıtım Şirketi Yetkilisi")}");
                SetCellText(table, 1, 1, $"Adı Soyadı{Environment.NewLine}{ImzaAdSoyad(abone, talep.MusteriAdi)}");
                SetCellText(table, 1, 2, $"Firma / Yetkili{Environment.NewLine}{ImzaAdSoyad(firma, talep.FirmaAdi)}");
                SetCellText(table, 2, 0, $"Tarih{Environment.NewLine}{ImzaTarihi(dagitim, secenekler, formTarihi)}");
                SetCellText(table, 2, 1, $"Tarih{Environment.NewLine}{ImzaTarihi(abone, secenekler, formTarihi)}");
                SetCellText(table, 2, 2, $"Tarih{Environment.NewLine}{ImzaTarihi(firma, secenekler, formTarihi)}");
                SetCellText(table, 3, 0, $"İmza{Environment.NewLine}{ImzaKaydiMetni()}");
                SetCellText(table, 3, 1, $"İmza{Environment.NewLine}{ImzaKaydiMetni()}");
                SetCellText(table, 3, 2, $"Kaşe / İmza{Environment.NewLine}{ImzaKaydiMetni()}");
            }
        }

        private static string FirmaImzaMetni(YkcTalepDetayDto talep, string formTarihi, YkcFr265BelgeSecenekleri secenekler)
        {
            var firma = ImzaSatiri(secenekler, 1);
            var yetkili = ImzaAdSoyad(firma, talep.FirmaYetkiliKisi);
            var satirlar = new List<string>
            {
                "Sertifikalı Firma Yetkilisi",
                "",
                $"Adı ve Soyadı: {yetkili}",
                $"Tarih: {ImzaTarihi(firma, secenekler, formTarihi)}",
                secenekler.ImzaliNihaiMi ? $"İmza: {ImzaKaydiMetni()}" : "İmza:"
            };

            return string.Join(Environment.NewLine, satirlar);
        }

        private static YkcFr265ImzaSatiri? ImzaSatiri(YkcFr265BelgeSecenekleri secenekler, int siraNo)
        {
            return secenekler.Imzalar
                .Where(x => x.SiraNo == siraNo)
                .OrderByDescending(x => x.ImzaTarihi.HasValue)
                .FirstOrDefault();
        }

        private static string ImzaAdSoyad(YkcFr265ImzaSatiri? imza, string? fallback)
        {
            return Clean(imza?.AdSoyad) is { Length: > 0 } adSoyad
                ? adSoyad
                : Clean(fallback);
        }

        private static string ImzaTarihi(YkcFr265ImzaSatiri? imza, YkcFr265BelgeSecenekleri secenekler, string formTarihi)
        {
            var tarih = imza?.ImzaTarihi ?? secenekler.ImzaTarihi;
            return tarih?.ToString("dd.MM.yyyy") ?? formTarihi;
        }

        private static string ImzaKaydiMetni()
        {
            return "Dijital imza kaydı alındı";
        }

        private static string FormTarihi(YkcTalepDetayDto talep)
        {
            return talep.TalepTarihi.ToString("dd.MM.yyyy");
        }

        private static void SetCellText(XElement? table, int rowIndex, int cellIndex, string? value)
        {
            if (table == null)
                return;

            var rows = table.Elements(W + "tr").ToList();
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

    public class YkcFr265BelgeSecenekleri
    {
        public bool ImzaliNihaiMi { get; set; }
        public DateTime? ImzaTarihi { get; set; }
        public List<YkcFr265ImzaSatiri> Imzalar { get; set; } = new();
    }

    public class YkcFr265ImzaSatiri
    {
        public int SiraNo { get; set; }
        public string? Rol { get; set; }
        public string? AdSoyad { get; set; }
        public DateTime? ImzaTarihi { get; set; }
    }
}
