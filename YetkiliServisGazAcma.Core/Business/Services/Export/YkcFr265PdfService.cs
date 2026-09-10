using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace YetkiliServisGazAcma.Business.Services;

public static class YkcFr265PdfService
{
    public const string TasarimSurumu = "WordV2";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static YkcFr265BelgeSonuc ImzaliNihaiOlustur(YkcTalepDetayDto talep, YkcFr265BelgeSecenekleri secenekler)
        => Olustur(talep, secenekler);

    public static YkcFr265BelgeSonuc Olustur(YkcTalepDetayDto talep, YkcFr265BelgeSecenekleri? secenekler = null)
    {
        secenekler ??= new();
        QuestPDF.Settings.License = LicenseType.Community;
        var word = new YkcFr265FormService().WordOlustur(talep, secenekler);
        using var archive = new ZipArchive(new MemoryStream(word.Bytes), ZipArchiveMode.Read);
        XDocument Xml(string name)
        {
            using var stream = (archive.GetEntry(name)
                ?? throw new InvalidOperationException($"Form şablonunda {name} bulunamadı.")).Open();
            return XDocument.Load(stream);
        }

        var body = Xml("word/document.xml").Root!.Element(W + "body")!;
        var tables = body.Elements(W + "tbl").ToList();
        // This renderer targets the supplied two-page form, not arbitrary Word documents.
        if (tables.Count != 12)
            throw new InvalidOperationException("Form şablonunun tablo yapısı değişmiş. PDF eşlemesi güncellenmeli.");
        var header = Xml("word/header2.xml");
        var title = Text(header.Root!);
        var imageId = header.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed")?.Value;
        var target = Xml("word/_rels/header2.xml.rels").Root!.Elements()
            .FirstOrDefault(x => (string?)x.Attribute("Id") == imageId)?.Attribute("Target")?.Value;
        using var imageStream = (archive.GetEntry("word/" + target)
            ?? throw new InvalidOperationException("Form şablonunun logosu bulunamadı.")).Open();
        using var logo = new MemoryStream();
        imageStream.CopyTo(logo);
        var logoBytes = logo.ToArray();

        var bytes = Document.Create(document =>
        {
            for (var number = 1; number <= 2; number++)
            {
                var firstPage = number == 1;
                document.Page(page =>
                {
                    page.Size(595.3f, 841.9f);
                    page.MarginHorizontal(21);
                    page.MarginVertical(28.35f);
                    page.DefaultTextStyle(style => style.FontFamily("Calibri", "Carlito", "Arial")
                        .FontSize(10.5f).LineHeight(1).FontColor("#000000"));
                    page.Header().PaddingBottom(15).Row(row =>
                    {
                        row.ConstantItem(92).AlignCenter().AlignMiddle().Width(44.4f).Height(30).Image(logoBytes).FitUnproportionally();
                        row.RelativeItem().AlignMiddle().Text(title).FontSize(16).Bold().FontColor("#7f7f7f").AlignCenter();
                    });
                    page.Content().Column(column =>
                    {
                        if (firstPage)
                        {
                            column.Item().PaddingBottom(13).AlignRight().Text($"Tarih : {talep.TalepTarihi:dd.MM.yyyy}").Bold();
                            for (var i = 0; i < 7; i++)
                            {
                                var index = i;
                                column.Item().PaddingBottom(i < 2 ? 14 : 8)
                                    .Element(c => Table(c, tables[index], index));
                            }
                        }
                        else
                        {
                            var elements = body.Elements().ToList();
                            for (var i = 1; i <= 5; i++)
                            {
                                var index = elements.FindIndex(x => x.Name == W + "p" && Text(x).StartsWith($"{i}. KONTROL"));
                                if (index < 0) throw new InvalidOperationException("Form kontrol başlığı eksik.");
                                var paragraphs = elements.Skip(index).TakeWhile(x => x.Name != W + "tbl").ToList();
                                var tableIndex = i + 6;
                                column.Item().ShowEntire().PaddingBottom(10).Column(block =>
                                {
                                    foreach (var paragraph in paragraphs)
                                        block.Item().Element(c => Paragraph(c, paragraph));
                                    block.Item().PaddingTop(3).Element(c => Table(c, tables[tableIndex], tableIndex));
                                });
                            }
                        }
                    });
                    if (secenekler.ImzaliNihaiMi)
                        page.Footer().AlignCenter().Text("DEMO BELGESİ - Gerçek elektronik imza içermez.")
                            .FontSize(8).FontColor("#666666");
                });
            }
        }).GeneratePdf();
        return new YkcFr265BelgeSonuc
        {
            Bytes = bytes,
            ContentType = "application/pdf",
            DosyaAdi = secenekler.ImzaliNihaiMi
                ? $"Form_Demo_Nihai_{TasarimSurumu}_{talep.Id}.pdf"
                : $"Cihaz_Degisim_Formu_{talep.Id}.pdf"
        };
    }

    private static void Table(IContainer container, XElement source, int index)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var width in source.Element(W + "tblGrid")!.Elements(W + "gridCol"))
                    columns.RelativeColumn(Number(width.Attribute(W + "w"), 1));
            });
            var rows = source.Elements(W + "tr").ToList();
            for (var row = 0; row < rows.Count; row++)
            {
                uint column = 1;
                foreach (var cell in rows[row].Elements(W + "tc"))
                {
                    var properties = cell.Element(W + "tcPr");
                    var span = (uint)Number(properties?.Element(W + "gridSpan")?.Attribute(W + "val"), 1);
                    var merge = properties?.Element(W + "vMerge");
                    if (merge != null && (string?)merge.Attribute(W + "val") != "restart")
                    {
                        column += span;
                        continue;
                    }
                    var slot = table.Cell().Row((uint)row + 1).Column(column).ColumnSpan(span);
                    if (merge != null) slot = slot.RowSpan(2);
                    var height = Number(rows[row].Element(W + "trPr")?.Element(W + "trHeight")?.Attribute(W + "val"), 280) / 20;
                    var content = slot.Border(0.5f).MinHeight(height).PaddingHorizontal(index < 2 ? 1 : 5).PaddingVertical(1);
                    if (index < 2 || index == 4) content = content.AlignMiddle();
                    content.Column(block =>
                    {
                        foreach (var paragraph in cell.Elements(W + "p"))
                            block.Item().Element(c => Paragraph(c, paragraph, index >= 7));
                    });
                    column += span;
                }
            }
        });
    }

    private static void Paragraph(IContainer container, XElement paragraph, bool centered = false)
    {
        if (string.IsNullOrWhiteSpace(Text(paragraph)))
        {
            container.Height(10.5f);
            return;
        }
        if (paragraph.Descendants(W + "tab").Any())
        {
            var parts = string.Concat(paragraph.Descendants().Where(x => x.Name == W + "t" || x.Name == W + "tab")
                .Select(x => x.Name == W + "tab" ? "\t" : x.Value)).Split('\t', StringSplitOptions.RemoveEmptyEntries);
            container.Row(row =>
            {
                foreach (var part in parts) row.RelativeItem().Text(part).FontSize(10);
            });
            return;
        }
        container.Text(text =>
        {
            var alignment = (string?)paragraph.Element(W + "pPr")?.Element(W + "jc")?.Attribute(W + "val");
            if (centered || alignment == "center") text.AlignCenter();
            else if (alignment == "right") text.AlignRight();
            else if (alignment == "both") text.Justify();
            foreach (var run in paragraph.Elements(W + "r"))
            {
                var value = string.Concat(run.Elements().Where(x => x.Name == W + "t" || x.Name == W + "br")
                    .Select(x => x.Name == W + "br" ? "\n" : x.Value));
                var properties = run.Element(W + "rPr");
                var span = text.Span(value).FontSize(Math.Clamp(Number(properties?.Element(W + "sz")?.Attribute(W + "val"), 21) / 2, 9, 12));
                if (properties?.Element(W + "b") is { } bold && (string?)bold.Attribute(W + "val") != "0") span.Bold();
                if (properties?.Element(W + "i") is { } italic && (string?)italic.Attribute(W + "val") != "0") span.Italic();
                if (value.Contains('☒') || value.Contains('☐')) span.FontFamily("Segoe UI Symbol", "DejaVu Sans");
            }
        });
    }

    private static string Text(XElement element) => string.Concat(element.Descendants(W + "t").Select(x => x.Value)).Trim();
    private static float Number(XAttribute? value, float fallback) => float.TryParse(value?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : fallback;
}
