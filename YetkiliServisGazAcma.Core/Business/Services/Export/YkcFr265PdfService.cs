using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class YkcFr265PdfService
    {
        private const string Baslik = "FR265 PROJE TADİLATI GEREKTİRMEYEN YAKICI CİHAZ DEĞİŞİM FORMU";

        public static YkcFr265BelgeSonuc ImzaliNihaiOlustur(
            YkcTalepDetayDto talep,
            YkcFr265BelgeSecenekleri secenekler)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var bytes = Document.Create(document =>
            {
                document.Page(page =>
                {
                    SayfaAyarla(page);
                    page.Content().Element(container => BirinciSayfa(container, talep, secenekler));
                    page.Footer().Element(SayfaAltligi);
                });

                document.Page(page =>
                {
                    SayfaAyarla(page);
                    page.Content().Element(container => IkinciSayfa(container, talep, secenekler));
                    page.Footer().Element(SayfaAltligi);
                });
            }).GeneratePdf();

            return new YkcFr265BelgeSonuc
            {
                Bytes = bytes,
                ContentType = "application/pdf",
                DosyaAdi = $"Form_Demo_Nihai_{talep.Id}.pdf"
            };
        }

        private static void SayfaAyarla(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(1.25f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(8.5f).FontColor(Colors.Black));
        }

        private static void BirinciSayfa(
            IContainer container,
            YkcTalepDetayDto talep,
            YkcFr265BelgeSecenekleri secenekler)
        {
            container.Column(column =>
            {
                column.Spacing(7);
                column.Item().Element(c => BelgeBasligi(c, talep));
                column.Item().Element(c => FirmaBilgileri(c, talep));
                column.Item().Element(c => TesisatBilgileri(c, talep));
                column.Item().Element(c => CihazKarsilastirmasi(c, talep));
                column.Item().Element(c => IkinciElBilgisi(c, talep));
                column.Item().PaddingHorizontal(4).Text(
                    "Cihazın ikinci el olması durumunda, bu evrakla birlikte yetkili servis tarafından düzenlenecek " +
                    "“Yetkili Servis Cihaz Kontrol Raporu” bu forma eklenmelidir.")
                    .FontSize(8)
                    .LineHeight(1.2f);
                column.Item().PaddingTop(3).Text(
                    "Yukarıda bilgileri verilen abonenin (tesisatın), mevcut onaylı projesinde bulunan yakıcı cihazın " +
                    "tip, yerleşim ve kapasite değişikliği yapılmamıştır.")
                    .LineHeight(1.25f);
                column.Item().Text(
                    "Aynı yerde, aynı tip ve kapasitedeki yeni yakıcı cihaz, tesisat üzerindeki cihaz vanasına, cihaz " +
                    "bağlantı parçaları ve atık sistemleri ile tesis edilmiş olup, değişimle ilgili tüm idari ve teknik " +
                    "sorumluluk tarafımıza aittir. Mevcut onaylı projesine göre yerinde gerekli tesisat kontrollerinin " +
                    "yapılması sonrası, yakıcı cihaz ile cihaz bağlantı hattı ve atık sistemleri devreye alma işlemleri " +
                    "tarafımızca yapılacak olup, sorumluluğu tarafımıza aittir.")
                    .LineHeight(1.25f);
                column.Item().PaddingTop(3).Element(c => IlkSayfaImzalari(c, talep, secenekler));
            });
        }

        private static void IkinciSayfa(
            IContainer container,
            YkcTalepDetayDto talep,
            YkcFr265BelgeSecenekleri secenekler)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().AlignCenter().Text(Baslik).Bold().FontSize(10);

                for (var kontrolNo = 1; kontrolNo <= 5; kontrolNo++)
                {
                    var kontrol = talep.Kontroller.FirstOrDefault(x => x.KontrolNo == kontrolNo);
                    column.Item().Element(c => KontrolBlogu(c, kontrolNo, kontrol, talep, secenekler));
                }
            });
        }

        private static void BelgeBasligi(IContainer container, YkcTalepDetayDto talep)
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text(Baslik).Bold().FontSize(12);
                column.Item().PaddingTop(5).AlignRight().Text($"Tarih : {FormTarihi(talep):dd.MM.yyyy}").Bold();
            });
        }

        private static void FirmaBilgileri(IContainer container, YkcTalepDetayDto talep)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(2f);
                });

                BilgiSatiri(table, "Sertifikalı Firma Unvanı", talep.FirmaAdi);
                BilgiSatiri(table, "Sertifika Numarası", talep.YetkiBelgesiNo);
            });
        }

        private static void TesisatBilgileri(IContainer container, YkcTalepDetayDto talep)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(2f);
                });

                BilgiSatiri(table, "Adı Soyadı", talep.MusteriAdi);
                BilgiSatiri(table, "Tesisat Numarası", talep.TesisatNo);
                BilgiSatiri(table, "Tüketim Noktası (Daire/İş Yeri Numarası)", talep.TuketimNoktasi);
                BilgiSatiri(table, "Bina Kodu", talep.BaglantiNesnesi);
                BilgiSatiri(table, "Adres", talep.Adres, 34);
            });
        }

        private static void CihazKarsilastirmasi(IContainer container, YkcTalepDetayDto talep)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                BaslikHucresi(table, string.Empty);
                BaslikHucresi(table, "Projedeki Cihaz");
                BaslikHucresi(table, "Yeni Kullanılan Cihaz");
                KarsilastirmaSatiri(table, "Yakıcı Cihaz Tipi", talep.EskiCihazTipi, talep.YeniCihazTipi);
                KarsilastirmaSatiri(table, "Marka", talep.EskiMarka, talep.YeniMarka);
                KarsilastirmaSatiri(table, "Baca Tipi", talep.EskiBacaTipi, talep.YeniBacaTipi);
                KarsilastirmaSatiri(table, "Kapasite (kcal/h)", talep.EskiKapasite, talep.YeniKapasite);
            });
        }

        private static void IkinciElBilgisi(IContainer container, YkcTalepDetayDto talep)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                Hucre(table, "Takılan Yakıcı Cihaz İkinci El Cihaz Mı?", true, 28);
                Hucre(table, talep.IkinciElCihazMi == true ? "[X] Evet" : "[ ] Evet", false, 28, true);
                Hucre(table, talep.IkinciElCihazMi == false ? "[X] Hayır" : "[ ] Hayır", false, 28, true);
            });
        }

        private static void IlkSayfaImzalari(
            IContainer container,
            YkcTalepDetayDto talep,
            YkcFr265BelgeSecenekleri secenekler)
        {
            var firmaImzasi = Imza(secenekler, 1);
            var dagitimImzasi = Imza(secenekler, 2);
            var aboneImzasi = Imza(secenekler, 3);

            container.Column(column =>
            {
                column.Item().Element(c => ImzaKutusu(
                    c,
                    "Sertifikalı Firma Yetkilisi",
                    Deger(firmaImzasi?.AdSoyad, talep.FirmaYetkiliKisi),
                    ImzaTarihi(firmaImzasi, secenekler)));

                column.Item().PaddingVertical(5).Text("Yukarıdaki bilgileri verilen yeni cihaz belirtilen adreste görülmüştür.")
                    .Bold();

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => ImzaKutusu(
                        c,
                        "İşlemi Yapan Gaz Dağıtım Şirketi Yetkilisi",
                        Deger(dagitimImzasi?.AdSoyad, talep.GazDagitimYetkilisiAdi),
                        ImzaTarihi(dagitimImzasi, secenekler)));
                    row.ConstantItem(8);
                    row.RelativeItem().Element(c => ImzaKutusu(
                        c,
                        "İşlem Yapılan Abone/Kullanıcı",
                        Deger(aboneImzasi?.AdSoyad, talep.MusteriAdi),
                        ImzaTarihi(aboneImzasi, secenekler)));
                });
            });
        }

        private static void KontrolBlogu(
            IContainer container,
            int kontrolNo,
            YkcFr265KontrolDto? kontrol,
            YkcTalepDetayDto talep,
            YkcFr265BelgeSecenekleri secenekler)
        {
            var uygun = kontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun;
            var uygunDegil = kontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil;
            var kontrolKayitli = uygun || uygunDegil;
            var firmaImzasi = Imza(secenekler, 1);
            var dagitimImzasi = Imza(secenekler, 2);
            var aboneImzasi = Imza(secenekler, 3);

            container.Border(0.75f).BorderColor(Colors.Black).Padding(6).Column(column =>
            {
                column.Spacing(3);
                column.Item().Text($"{kontrolNo}. KONTROL").Bold().FontSize(9.5f);
                column.Item().Text($"{(uygun ? "[X]" : "[ ]")} Uygun      {(uygunDegil ? "[X]" : "[ ]")} Uygun Değil");
                column.Item().Text(text =>
                {
                    text.Span("Uygun değil ise nedeni: ").Bold();
                    text.Span(uygunDegil ? Deger(kontrol?.Aciklama) : string.Empty);
                });
                column.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Medium).Height(12);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    KontrolImzaHucresi(
                        table,
                        "Gaz Dağıtım Şirketi Yetkilisi",
                        kontrolKayitli ? Deger(kontrol?.KontrolEdenAdi) : string.Empty,
                        kontrolKayitli ? kontrol?.KontrolTarihi ?? dagitimImzasi?.ImzaTarihi : null,
                        "İmza",
                        kontrolKayitli);
                    KontrolImzaHucresi(
                        table,
                        "Abone / Kullanıcı",
                        kontrolKayitli ? Deger(aboneImzasi?.AdSoyad, talep.MusteriAdi) : string.Empty,
                        kontrolKayitli ? aboneImzasi?.ImzaTarihi : null,
                        "İmza",
                        kontrolKayitli);
                    KontrolImzaHucresi(
                        table,
                        "Sertifikalı Firma",
                        kontrolKayitli ? Deger(firmaImzasi?.AdSoyad, talep.FirmaYetkiliKisi, talep.FirmaAdi) : string.Empty,
                        kontrolKayitli ? firmaImzasi?.ImzaTarihi : null,
                        "Kaşe / İmza",
                        kontrolKayitli);
                });
            });
        }

        private static void BilgiSatiri(TableDescriptor table, string etiket, string? deger, float? yukseklik = null)
        {
            Hucre(table, etiket, true, yukseklik);
            Hucre(table, Deger(deger), false, yukseklik);
        }

        private static void KarsilastirmaSatiri(TableDescriptor table, string etiket, string? eski, string? yeni)
        {
            Hucre(table, etiket, true);
            Hucre(table, Deger(eski));
            Hucre(table, Deger(yeni));
        }

        private static void BaslikHucresi(TableDescriptor table, string metin)
        {
            table.Cell().Element(HucreStili).AlignCenter().AlignMiddle().Text(metin).Bold();
        }

        private static void Hucre(
            TableDescriptor table,
            string metin,
            bool kalin = false,
            float? yukseklik = null,
            bool ortala = false)
        {
            var container = table.Cell().Element(HucreStili);
            if (yukseklik.HasValue)
                container = container.MinHeight(yukseklik.Value);
            if (ortala)
                container = container.AlignCenter().AlignMiddle();

            var text = container.Text(metin);
            if (kalin)
                text.Bold();
        }

        private static IContainer HucreStili(IContainer container)
        {
            return container.Border(0.65f).BorderColor(Colors.Black).PaddingVertical(3).PaddingHorizontal(5);
        }

        private static void ImzaKutusu(IContainer container, string baslik, string adSoyad, DateTime? tarih)
        {
            container.Border(0.65f).BorderColor(Colors.Black).Padding(5).MinHeight(60).Column(column =>
            {
                column.Item().Text(baslik).Bold();
                column.Item().PaddingTop(3).Text($"Adı ve Soyadı: {adSoyad}");
                column.Item().Text($"Tarih: {(tarih.HasValue ? tarih.Value.ToString("dd.MM.yyyy HH:mm") : string.Empty)}");
                column.Item().Text("İmza: Demo onayı (gerçek e-imza değildir)");
            });
        }

        private static void KontrolImzaHucresi(
            TableDescriptor table,
            string baslik,
            string adSoyad,
            DateTime? tarih,
            string imzaEtiketi,
            bool imzali)
        {
            table.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(4).MinHeight(42).Column(column =>
            {
                column.Item().Text(baslik).Bold().FontSize(7.5f);
                column.Item().Text($"Adı Soyadı: {adSoyad}").FontSize(7.5f);
                column.Item().Text($"Tarih: {(tarih.HasValue ? tarih.Value.ToString("dd.MM.yyyy") : string.Empty)}").FontSize(7.5f);
                column.Item().Text($"{imzaEtiketi}: {(imzali ? "Demo onayı" : string.Empty)}").FontSize(7.5f);
            });
        }

        private static void SayfaAltligi(IContainer container)
        {
            container.AlignRight().Text(text =>
            {
                text.Span("Sayfa ").FontSize(7);
                text.CurrentPageNumber().FontSize(7);
                text.Span(" / ").FontSize(7);
                text.TotalPages().FontSize(7);
            });
        }

        private static YkcFr265ImzaSatiri? Imza(YkcFr265BelgeSecenekleri secenekler, int siraNo)
        {
            return secenekler.Imzalar.FirstOrDefault(x => x.SiraNo == siraNo);
        }

        private static DateTime? ImzaTarihi(YkcFr265ImzaSatiri? imza, YkcFr265BelgeSecenekleri secenekler)
        {
            return imza?.ImzaTarihi ?? secenekler.ImzaTarihi;
        }

        private static DateTime FormTarihi(YkcTalepDetayDto talep)
        {
            return talep.Fr265BelgeOlusturmaTarihi ?? talep.TalepTarihi;
        }

        private static string Deger(params string?[] degerler)
        {
            return degerler.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
        }
    }
}
