using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class YetkiliServisKayitDosyasi
    {
        public static byte[] ExcelOlustur(AdminYetkiliServisDetaySonuc detay)
        {
            var satirlar = Alanlar(detay)
                .Select(alan => (IReadOnlyList<string?>)new string?[] { alan.Baslik, alan.Deger });

            return ExcelWorkbookService.Olustur("Yetkili Servis", new[] { "Alan", "Bilgi" }, satirlar);
        }

        public static byte[] PdfOlustur(AdminYetkiliServisDetaySonuc detay)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var alanlar = Alanlar(detay);

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(10));
                    page.Header().PaddingBottom(14)
                        .Text("Yetkili Servis Kaydı").FontSize(18).SemiBold().FontColor("#17324D");

                    page.Content().Column(column =>
                    {
                        foreach (var alan in alanlar)
                        {
                            column.Item().BorderBottom(1).BorderColor("#E2EAF0").PaddingVertical(7).Row(row =>
                            {
                                row.ConstantItem(145).Text(alan.Baslik).FontColor("#566B7D");
                                row.RelativeItem().Text(alan.Deger).SemiBold().FontColor("#17324D");
                            });
                        }
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span("Sayfa ");
                        text.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();
        }

        private static List<(string Baslik, string Deger)> Alanlar(AdminYetkiliServisDetaySonuc detay)
        {
            var servis = detay.Servis ?? throw new ArgumentException("Yetkili servis kaydı bulunamadı.", nameof(detay));
            var ilceler = string.Join(", ", detay.Subeler
                .Select(sube => sube.Ilce?.Trim())
                .Where(ilce => !string.IsNullOrWhiteSpace(ilce))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(ilce => ilce, StringComparer.CurrentCultureIgnoreCase));
            var markalar = string.Join(", ", servis.FirmaMarkalar?
                .Where(x => !x.SilindiMi && x.Marka != null)
                .Select(x => x.Marka!.MarkaAdi)
                .Where(ad => !string.IsNullOrWhiteSpace(ad))
                .Distinct() ?? Enumerable.Empty<string?>());
            var hizmetler = string.Join(", ", servis.FirmaKategoriler?
                .Where(x => !x.SilindiMi && x.Kategori != null && !x.Kategori.SilindiMi && x.Kategori.AktifMi)
                .Select(x => x.Kategori!.Ad)
                .Where(ad => !string.IsNullOrWhiteSpace(ad))
                .Distinct() ?? Enumerable.Empty<string?>());

            return new List<(string, string)>
            {
                ("Firma adı", Deger(servis.FirmaAdi)),
                ("Yetkili ad soyad", Deger(servis.YetkiliKisi)),
                ("Telefon", Deger(servis.Telefon)),
                ("E-posta", Deger(servis.Email)),
                ("İl", Deger(servis.FaaliyetIli)),
                ("İlçe", Deger(ilceler)),
                ("Vergi no", Deger(servis.VergiNo)),
                ("Vergi dairesi", Deger(servis.VergiDairesi)),
                ("Dağıtım şirketi", Deger(servis.Sirket?.SirketAdi)),
                ("Yetkili markalar", Deger(markalar)),
                ("Hizmet türleri", Deger(hizmetler)),
                ("Adres", Deger(servis.Adres)),
                ("Durum", servis.AktifMi ? "Aktif" : "Pasif")
            };
        }

        private static string Deger(string? deger) => string.IsNullOrWhiteSpace(deger) ? "-" : deger.Trim();
    }
}
