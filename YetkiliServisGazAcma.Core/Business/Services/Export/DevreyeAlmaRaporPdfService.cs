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

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().PaddingBottom(14).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(baslik).FontSize(17).SemiBold().FontColor("#213B53");
                            col.Item().PaddingTop(4).Text($"Rapor Aral\u0131\u011f\u0131: {basTarih:dd.MM.yyyy} - {bitTarih:dd.MM.yyyy}")
                                .FontSize(9).FontColor("#627588");
                        });
                        row.ConstantItem(130).AlignRight().Column(col =>
                        {
                            col.Item().AlignRight().Text("Düzenleme Tarihi").FontSize(8).FontColor("#627588");
                            col.Item().AlignRight().PaddingTop(4).Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
                                .FontSize(9).FontColor("#213B53");
                        });
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);
                        col.Item().Element(x => OzetKartlari(x, devreyeSayisi, tamamlanan));
                        col.Item().Text(listeBasligi).FontSize(11).SemiBold().FontColor("#213B53");

                        if (detayliListe)
                            DetayliListe(col, liste);
                        else
                            ServisListe(col, liste);
                    });

                    page.Footer().PaddingTop(10).BorderTop(1).BorderColor("#E3EAF0").PaddingTop(7).Row(row =>
                    {
                        row.RelativeItem().Text("Yetkili Servis Gaz A\u00e7ma Sistemi").FontSize(8).FontColor("#627588");
                        row.ConstantItem(90).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8).FontColor("#627588"));
                            text.Span("Sayfa ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static void OzetKartlari(IContainer container, int toplam, int tamamlanan)
        {
            container.BorderTop(2).BorderColor("#276EAE").Background("#F6F9FC").Padding(10).Row(row =>
            {
                row.Spacing(24);
                Ozet("Toplam Kayıt:", toplam);
                Ozet("Tamamlanan:", tamamlanan);

                void Ozet(string title, int value)
                {
                    row.AutoItem().Text(text =>
                    {
                        text.Span(title + " ").FontSize(9).FontColor("#627588");
                        text.Span(value.ToString()).FontSize(11).SemiBold().FontColor("#213B53");
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
                        r.RelativeItem().AlignRight().Text($"Devreye Alma Tarihi: {d.DevreyeAlmaTarihi:dd.MM.yyyy HH:mm}").FontSize(10).FontColor("#4B5563");
                    });

                    detail.Item().Background(satirBg).PaddingHorizontal(8).PaddingVertical(6).Text($"Durum: {durumText}").FontSize(10).FontColor(durumColor).SemiBold();
                    detail.Item().Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        Bilgi("Dağıtım Şirketi", d.Firma?.Sirket?.SirketAdi);
                        Bilgi("Yetkili Servis", d.Firma?.FirmaAdi);
                        Bilgi("Sözleşme No", d.SozlesmeNo);
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
                            t.Cell().PaddingRight(8).PaddingBottom(4).Text($"{etiket}: {Deger(deger)}").FontSize(10);
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
                col.Item().EnsureSpace(220).Border(1).BorderColor("#DCE5EC").Column(record =>
                {
                    record.Item().Background("#F6F9FC").PaddingHorizontal(12).PaddingVertical(8).Row(row =>
                    {
                        row.Spacing(16);
                        row.RelativeItem().Column(header =>
                        {
                            header.Item().Text("Abone Adı:").FontSize(8).FontColor("#627588");
                            header.Item().PaddingTop(3).Text(Deger(d.MusteriAdi)).FontSize(11).SemiBold().FontColor("#213B53");
                        });
                        row.ConstantItem(125).AlignRight().Column(header =>
                        {
                            header.Item().AlignRight().Text("Devreye Alma Tarihi:").FontSize(8).FontColor("#627588");
                            header.Item().AlignRight().PaddingTop(3).Text(d.DevreyeAlmaTarihi.ToString("dd.MM.yyyy"))
                                .FontSize(10).SemiBold().FontColor("#213B53");
                        });
                    });
                    record.Item().BorderBottom(1).BorderColor("#E3EAF0").PaddingHorizontal(12).PaddingVertical(6).Row(row =>
                    {
                        row.Spacing(14);
                        Kimlik("Tesisat No:", d.TesistatNo);
                        Kimlik("Sözleşme No:", d.SozlesmeNo);
                        row.AutoItem().Text(DurumText(d.Durum)).FontSize(9).SemiBold()
                            .FontColor(d.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi ? "#187F61" : "#213B53");

                        void Kimlik(string label, string? value)
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.Span(label + " ").FontSize(8.5f).FontColor("#627588");
                                text.Span(Deger(value)).FontSize(9).FontColor("#213B53");
                            });
                        }
                    });
                    record.Item().PaddingHorizontal(12).PaddingVertical(10).Row(row =>
                    {
                        row.Spacing(16);
                        row.RelativeItem(3).Element(x => BilgiGrubu(x, "Cihaz Bilgileri", 86,
                            ("Yakıcı Cihaz Tipi", d.CihazTipi),
                            ("Marka", d.Marka?.MarkaAdi ?? d.CihazMarka),
                            ("Model", d.CihazModeli),
                            ("Seri No", d.SeriNo),
                            ("Kapasite", string.IsNullOrWhiteSpace(d.CihazKapasite) ? null : $"{d.CihazKapasite} kcal/h")));
                        row.RelativeItem(2).BorderLeft(1).BorderColor("#E3EAF0").PaddingLeft(12)
                            .Element(x => BilgiGrubu(x, "Servis ve Teknisyen", 78,
                                ("Yetkili Servis", d.Firma?.FirmaAdi),
                                ("Teknisyen", d.TeknisyenAdi),
                                ("Yetki Belgesi No", d.TeknisyenYetkiBelgesiNo)));
                    });
                    if (!string.IsNullOrWhiteSpace(d.Adres) || !string.IsNullOrWhiteSpace(d.Notlar))
                    {
                        record.Item().BorderTop(1).BorderColor("#E3EAF0").PaddingHorizontal(12).PaddingVertical(6).Column(notes =>
                        {
                            notes.Spacing(5);
                            if (!string.IsNullOrWhiteSpace(d.Adres)) Aciklama("Adres:", d.Adres);
                            if (!string.IsNullOrWhiteSpace(d.Notlar)) Aciklama("İşlem Notu:", d.Notlar);

                            void Aciklama(string label, string value)
                            {
                                notes.Item().Row(row =>
                                {
                                    row.ConstantItem(58).Text(label).FontSize(8.5f).FontColor("#627588");
                                    row.RelativeItem().Text(value).FontSize(9).FontColor("#213B53");
                                });
                            }
                        });
                    }
                });
            }
        }

        private static void BilgiGrubu(IContainer container, string baslik, float etiketGenisligi,
            params (string Etiket, string? Deger)[] alanlar)
        {
            container.Column(group =>
            {
                group.Item().PaddingBottom(6).Text(baslik).FontSize(9.5f).SemiBold().FontColor("#213B53");
                group.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(etiketGenisligi);
                        columns.RelativeColumn();
                    });
                    foreach (var (etiket, deger) in alanlar)
                    {
                        table.Cell().PaddingRight(8).PaddingBottom(3).Text(etiket + ":").FontSize(8.5f).FontColor("#627588");
                        table.Cell().PaddingBottom(3).Text(Deger(deger)).FontSize(9).FontColor("#213B53");
                    }
                });
            });
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
