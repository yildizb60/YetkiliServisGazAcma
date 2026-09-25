using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class DevreyeAlmaRaporPdfService
    {
        public static byte[] AdminRaporuOlustur(IEnumerable<Ys_DevreyeAlma> islemler, DateTime basTarih, DateTime bitTarih)
        {
            return Olustur(
                "Y\u00f6netim Raporlar\u0131",
                "Se\u00e7ili Devreye Alma Detaylar\u0131",
                islemler,
                basTarih,
                bitTarih,
                detayliListe: true);
        }

        public static byte[] YetkiliServisRaporuOlustur(IEnumerable<Ys_DevreyeAlma> islemler, DateTime basTarih, DateTime bitTarih)
        {
            return Olustur(
                "Yetkili Servis Raporlar\u0131",
                "Cihaz Devreye Alma Kay\u0131tlar\u0131",
                islemler,
                basTarih,
                bitTarih,
                detayliListe: false);
        }

        private static byte[] Olustur(
            string baslik,
            string listeBasligi,
            IEnumerable<Ys_DevreyeAlma> islemler,
            DateTime basTarih,
            DateTime bitTarih,
            bool detayliListe)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var liste = islemler.ToList();
            var devreyeSayisi = liste.Count;
            var tamamlanan = liste.Count(x => x.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi);
            var bekleyen = liste.Count(x => x.Durum == DevreyeAlmaDurumDegerleri.Bekliyor);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(baslik).FontSize(16).SemiBold();
                            col.Item().Text($"Rapor Aral\u0131\u011f\u0131: {basTarih:dd.MM.yyyy} - {bitTarih:dd.MM.yyyy}")
                                .FontSize(10).FontColor("#555555");
                        });
                        row.ConstantItem(160).AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
                            .FontSize(10).FontColor("#777777");
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);
                        col.Item().Element(x => OzetKartlari(x, devreyeSayisi, tamamlanan, bekleyen, detayliListe));
                        col.Item().Text(listeBasligi).FontSize(12).SemiBold();

                        if (detayliListe)
                            DetayliListe(col, liste);
                        else
                            ServisListe(col, liste);
                    });

                    page.Footer().AlignCenter().Text("Yetkili Servis Gaz A\u00e7ma Sistemi").FontSize(9).FontColor("#888888");
                });
            });

            return document.GeneratePdf();
        }

        private static void OzetKartlari(IContainer container, int toplam, int tamamlanan, int bekleyen, bool detayliListe)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    if (detayliListe)
                        c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                Cell("Toplam \u0130\u015flem", toplam.ToString());
                Cell("Tamamlanan", tamamlanan.ToString());
                if (detayliListe)
                    Cell("Bekleyen", bekleyen.ToString());

                void Cell(string title, string value)
                {
                    table.Cell().Element(cell =>
                    {
                        cell.Border(1).BorderColor("#E5E7EB").Padding(8).Background("#F8FAFC")
                            .Column(column =>
                            {
                                column.Item().Text(title).FontSize(9).FontColor("#6B7280");
                                column.Item().Text(value).FontSize(14).SemiBold().FontColor("#111827");
                            });
                    });
                }
            });
        }

        private static void DetayliListe(ColumnDescriptor col, List<Ys_DevreyeAlma> liste)
        {
            foreach (var d in liste)
            {
                var durumText = DurumText(d.Durum);
                var durumColor = d.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi ? "#0f766e" : d.Durum == DevreyeAlmaDurumDegerleri.Iptal ? "#b42318" : "#9a6700";
                var satirBg = d.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi ? "#ecfdf3" : d.Durum == DevreyeAlmaDurumDegerleri.Iptal ? "#fff1f2" : "#fffbeb";

                col.Item().PaddingBottom(8).Border(1).BorderColor("#E5E7EB").Background("#FFFFFF").Column(detail =>
                {
                    detail.Item().Background("#F8FAFC").Padding(8).Row(r =>
                    {
                        r.RelativeItem().Text($"Tesisat No: {Deger(d.TesistatNo)}").FontSize(10).SemiBold();
                        r.RelativeItem().AlignRight().Text($"Tarih: {d.OlusturmaTarihi:dd.MM.yyyy HH:mm}").FontSize(10).FontColor("#4B5563");
                    });

                    detail.Item().Background(satirBg).PaddingHorizontal(8).PaddingVertical(6).Text($"Durum: {durumText}").FontSize(10).FontColor(durumColor).SemiBold();
                    detail.Item().Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        Bilgi("Firma Kodu", d.Firma?.Sirket?.SirketAdi);
                        Bilgi("Yetkili Servis", d.Firma?.FirmaAdi);
                        Bilgi("M\u00fc\u015fteri", d.MusteriAdi);
                        Bilgi("Telefon", d.MusteriTelefon);
                        Bilgi("TC", d.MusteriTcNo);
                        Bilgi("Adres", d.Adres);
                        Bilgi("Cihaz Tipi", d.CihazTipi);
                        Bilgi("Marka", d.Marka?.MarkaAdi ?? d.CihazMarka);
                        Bilgi("Model", d.CihazModeli);
                        Bilgi("Seri No", d.SeriNo);
                        Bilgi("Kapasite", d.CihazKapasite);
                        Bilgi("Teknisyen", d.TeknisyenAdi);
                        Bilgi("Teknisyen Yetki Belgesi No", d.TeknisyenYetkiBelgesiNo);

                        void Bilgi(string etiket, string? deger)
                        {
                            t.Cell().PaddingBottom(4).Text($"{etiket}: {Deger(deger)}").FontSize(10);
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(d.Notlar))
                        detail.Item().PaddingHorizontal(8).PaddingBottom(8).Text($"Not: {d.Notlar}").FontSize(10).FontColor("#4B5563");
                });
            }
        }

        private static void ServisListe(ColumnDescriptor col, List<Ys_DevreyeAlma> liste)
        {
            if (liste.Count == 0)
            {
                col.Item().Text("Seçilen dönemde devreye alma kaydı bulunmuyor.").FontSize(10).FontColor("#607486");
                return;
            }

            foreach (var d in liste)
            {
                col.Item().ShowEntire().PaddingBottom(7).Border(1).BorderColor("#DCE5EC").Column(card =>
                {
                    card.Item().Background("#F3F7FC").Padding(7).Row(row =>
                    {
                        row.RelativeItem().Text($"Tesisat {Deger(d.TesistatNo)} · {Deger(d.MusteriAdi)}").FontSize(10).SemiBold().FontColor("#213B53");
                        row.ConstantItem(102).AlignRight().Text(d.DevreyeAlmaTarihi.ToString("dd.MM.yyyy")).FontSize(9).FontColor("#52677A");
                    });
                    card.Item().PaddingHorizontal(7).PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text($"Yakıcı cihaz tipi: {Deger(d.CihazTipi)}").FontSize(9);
                        row.RelativeItem().Text($"Marka: {Deger(d.Marka?.MarkaAdi ?? d.CihazMarka)}").FontSize(9);
                    });
                    card.Item().PaddingHorizontal(7).PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"Model: {Deger(d.CihazModeli)}").FontSize(9);
                        row.RelativeItem().Text($"Seri No: {Deger(d.SeriNo)}").FontSize(9);
                    });
                    card.Item().PaddingHorizontal(7).PaddingTop(4).PaddingBottom(7).Row(row =>
                    {
                        row.RelativeItem().Text($"Kapasite: {Deger(d.CihazKapasite)} kcal/h").FontSize(9);
                        row.RelativeItem().Text($"Teknisyen: {Deger(d.TeknisyenAdi)}").FontSize(9);
                    });
                    card.Item().PaddingHorizontal(7).PaddingBottom(7).Text($"Teknisyen yetki belgesi no: {Deger(d.TeknisyenYetkiBelgesiNo)}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(d.Adres))
                        card.Item().PaddingHorizontal(7).PaddingBottom(7).Text($"Adres: {d.Adres}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(d.Notlar))
                        card.Item().PaddingHorizontal(7).PaddingBottom(7).Text($"Not: {d.Notlar}").FontSize(9);
                });
            }
        }

        private static string DurumText(int durum)
        {
            return durum == DevreyeAlmaDurumDegerleri.Tamamlandi ? "Tamamland\u0131" : durum == DevreyeAlmaDurumDegerleri.Iptal ? "\u0130ptal" : "Bekliyor";
        }

        private static string Deger(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
