using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class YetkiBelgesiRaporExcelService
    {
        public static byte[] Olustur(IEnumerable<Ys_YetkiBelgesi> belgeler, string raporBasligi)
        {
            var satirlar = belgeler.Select(belge => (IReadOnlyList<string?>)new[]
            {
                belge.Id.ToString(),
                belge.Firma?.FirmaAdi ?? "",
                belge.Firma?.VergiNo ?? "",
                belge.Firma?.Sirket?.SirketAdi ?? "",
                belge.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm"),
                belge.YetkiBelgesiBaslangicTarihi?.ToString("dd.MM.yyyy") ?? "",
                belge.YetkiBelgesiBitisTarihi.ToString("dd.MM.yyyy"),
                Durum(belge),
                belge.OnayTarihi?.ToString("dd.MM.yyyy HH:mm") ?? "",
                belge.OnaylayanKullanici ?? "",
                belge.RedGerekce ?? ""
            }).ToList();

            return ExcelWorkbookService.Olustur(
                raporBasligi,
                new[]
                {
                    "Belge No", "Yetkili Servis", "Vergi No", "Dağıtım Şirketi", "Yükleme Tarihi",
                    "Başlangıç Tarihi", "Bitiş Tarihi", "Sonuç", "Sonuç Tarihi", "İşlemi Yapan", "Açıklama"
                },
                satirlar);
        }

        internal static string Durum(Ys_YetkiBelgesi belge)
        {
            if (belge.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                && belge.YetkiBelgesiBitisTarihi.Date < DateTime.Today)
                return "Süresi Doldu";

            return belge.Durum switch
            {
                YetkiBelgesiDurumDegerleri.Onaylandi => "Onaylandı",
                YetkiBelgesiDurumDegerleri.Reddedildi => "Reddedildi",
                _ => "Onay Bekliyor"
            };
        }
    }

    public static class YetkiBelgesiRaporPdfService
    {
        public static byte[] Olustur(IEnumerable<Ys_YetkiBelgesi> belgeler, string raporBasligi)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var liste = belgeler.ToList();

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8));

                    page.Header().PaddingBottom(12).Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text(raporBasligi).FontSize(16).SemiBold().FontColor("#17324D");
                            column.Item().Text($"{liste.Count} kayıt · {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor("#64748B");
                        });
                        row.ConstantItem(190).AlignRight().Text("Yetkili Servis Gaz Açma Sistemi").FontSize(9).FontColor("#475569");
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(.55f);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.25f);
                            columns.RelativeColumn(.85f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(.95f);
                            columns.RelativeColumn(1.15f);
                            columns.RelativeColumn(1.45f);
                        });

                        table.Header(header =>
                        {
                            Baslik(header, "Belge");
                            Baslik(header, "Yetkili Servis");
                            Baslik(header, "Dağıtım Şirketi");
                            Baslik(header, "Yüklendi");
                            Baslik(header, "Geçerlilik");
                            Baslik(header, "Sonuç");
                            Baslik(header, "İşlemi Yapan");
                            Baslik(header, "Açıklama");
                        });

                        foreach (var belge in liste)
                        {
                            Hucre(table, $"#{belge.Id}");
                            Hucre(table, Deger(belge.Firma?.FirmaAdi));
                            Hucre(table, Deger(belge.Firma?.Sirket?.SirketAdi));
                            Hucre(table, belge.OlusturmaTarihi.ToString("dd.MM.yyyy\nHH:mm"));
                            Hucre(table, $"{belge.YetkiBelgesiBaslangicTarihi?.ToString("dd.MM.yyyy") ?? "-"}\n{belge.YetkiBelgesiBitisTarihi:dd.MM.yyyy}");
                            Hucre(table, YetkiBelgesiRaporExcelService.Durum(belge));
                            Hucre(table, Deger(belge.OnaylayanKullanici));
                            Hucre(table, Deger(belge.RedGerekce));
                        }
                    });

                    page.Footer().DefaultTextStyle(x => x.FontSize(8).FontColor("#64748B")).AlignRight().Text(text =>
                    {
                        text.Span("Sayfa ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        private static void Baslik(TableCellDescriptor header, string value)
            => header.Cell().Background("#EAF0F6").BorderBottom(1).BorderColor("#C8D4E1").Padding(6).Text(value).SemiBold().FontColor("#253B53");

        private static void Hucre(TableDescriptor table, string value)
            => table.Cell().BorderBottom(1).BorderColor("#E2E8F0").PaddingVertical(6).PaddingHorizontal(5).Text(value).LineHeight(1.25f);

        private static string Deger(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
