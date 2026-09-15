using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class YkcRaporPdfService
    {
        public static byte[] Olustur(IEnumerable<YkcRaporKayitDto> kayitlar, bool icOperasyon)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var liste = kayitlar.ToList();

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
                            column.Item().Text("Cihaz Değişim Raporu").FontSize(16).SemiBold().FontColor("#17324D");
                            column.Item().Text($"{liste.Count} kayıt · {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor("#64748B");
                        });
                        row.ConstantItem(190).AlignRight().Text("Yetkili Servis Gaz Açma Sistemi").FontSize(9).FontColor("#475569");
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Element(x => Ozet(x, liste));
                        column.Item().Table(table => Tablo(table, liste, icOperasyon));
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

        private static void Ozet(IContainer container, IReadOnlyCollection<YkcRaporKayitDto> liste)
        {
            var tamamlanan = liste.Count(x => x.Durum == YkcDurumDegerleri.Tamamlandi);
            var kontrol = liste.Count(x => x.Durum is YkcDurumDegerleri.Atandi or YkcDurumDegerleri.SahaIsleminde);
            var bekleyen = liste.Count(x => x.Durum is YkcDurumDegerleri.TalepAlindi or YkcDurumDegerleri.AtamaBekliyor);

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                Kart("Toplam Talep", liste.Count, "#17324D");
                Kart("İnceleme / Atama", bekleyen, "#9A6700");
                Kart("Kontrol Süreci", kontrol, "#0E7490");
                Kart("Tamamlanan", tamamlanan, "#15803D");

                void Kart(string etiket, int deger, string renk)
                {
                    table.Cell().Border(1).BorderColor("#D9E2EC").Background("#F8FAFC").Padding(8).Row(row =>
                    {
                        row.RelativeItem().Text(etiket).FontSize(8).FontColor("#52657A");
                        row.AutoItem().Text(deger.ToString()).FontSize(12).SemiBold().FontColor(renk);
                    });
                }
            });
        }

        private static void Tablo(TableDescriptor table, IEnumerable<YkcRaporKayitDto> liste, bool icOperasyon)
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(.65f);
                columns.RelativeColumn(1.15f);
                columns.RelativeColumn(1.25f);
                if (icOperasyon) columns.RelativeColumn(1.25f);
                columns.RelativeColumn(icOperasyon ? 1.85f : 1.5f);
                columns.RelativeColumn(1.25f);
                if (icOperasyon) columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1f);
            });

            table.Header(header =>
            {
                Baslik(header, "Talep");
                Baslik(header, "Tesisat / Abone");
                Baslik(header, "Abone Adı Soyadı");
                if (icOperasyon) Baslik(header, "Sertifikalı Firma");
                Baslik(header, icOperasyon ? "Projedeki / Yeni Kullanılan Cihaz" : "Yeni Kullanılan Cihaz");
                Baslik(header, "Kontrol Randevusu");
                if (icOperasyon) Baslik(header, "Ekip / Bölge");
                Baslik(header, "Durum");
            });

            foreach (var kayit in liste)
            {
                Hucre(table, $"#{kayit.Id}\n{kayit.TalepTarihi:dd.MM.yyyy}");
                Hucre(table, $"Tesisat: {Deger(kayit.TesisatNo)}\nAbone: {Deger(kayit.AboneNo)}");
                Hucre(table, Deger(kayit.MusteriAdi));
                if (icOperasyon) Hucre(table, Deger(kayit.FirmaAdi));
                Hucre(table, Cihaz(kayit, icOperasyon));
                Hucre(table, Randevu(kayit));
                if (icOperasyon) Hucre(table, Atama(kayit));
                Hucre(table, Durum(kayit.Durum));
            }
        }

        private static void Baslik(TableCellDescriptor header, string value)
            => header.Cell().Background("#EAF0F6").BorderBottom(1).BorderColor("#C8D4E1").Padding(6).Text(value).SemiBold().FontColor("#253B53");

        private static void Hucre(TableDescriptor table, string value)
            => table.Cell().BorderBottom(1).BorderColor("#E2E8F0").PaddingVertical(6).PaddingHorizontal(5).Text(value).LineHeight(1.25f);

        private static string Cihaz(YkcRaporKayitDto kayit, bool icOperasyon)
        {
            var yeni = string.Join(" / ", new[] { kayit.YeniCihazTipi, kayit.YeniMarka, kayit.YeniModel, kayit.YeniKapasite }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!icOperasyon)
                return Deger(yeni);

            var eski = string.Join(" / ", new[] { kayit.EskiCihazTipi, kayit.EskiMarka, kayit.EskiKapasite }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return $"Projedeki Cihaz: {Deger(eski)}\nYeni Kullanılan Cihaz: {Deger(yeni)}";
        }

        private static string Randevu(YkcRaporKayitDto kayit)
        {
            var value = string.Join(" ", new[] { kayit.RandevuTarihi?.ToString("dd.MM.yyyy"), kayit.RandevuSaati }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(value))
                return kayit.SiradakiKontrolNo is > 1
                    ? $"{kayit.SiradakiKontrolNo}. kontrol randevusu bekleniyor"
                    : "Henüz planlanmadı";

            return kayit.SiradakiKontrolNo is > 1
                ? $"{kayit.SiradakiKontrolNo}. kontrol - {value}"
                : value;
        }

        private static string Atama(YkcRaporKayitDto kayit)
        {
            var value = string.Join(" / ", new[] { kayit.AtananEkip, kayit.Bolge }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(value) ? "Henüz atanmadı" : value;
        }

        private static string Durum(int durum) => durum switch
        {
            YkcDurumDegerleri.TalepAlindi => "İnceleme Bekleniyor",
            YkcDurumDegerleri.AtamaBekliyor => "Randevu Planlanacak",
            YkcDurumDegerleri.Atandi => "Randevu Oluşturuldu",
            YkcDurumDegerleri.SahaIsleminde => "İşlem Devam Ediyor",
            YkcDurumDegerleri.Reddedildi => "Reddedildi",
            YkcDurumDegerleri.Tamamlandi => "Tamamlandı",
            YkcDurumDegerleri.Iptal => "İptal",
            _ => "Bilinmiyor"
        };

        private static string Deger(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
