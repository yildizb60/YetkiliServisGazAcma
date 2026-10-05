using System.Globalization;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class YkcRaporExcelService
    {
        public static byte[] Olustur(IEnumerable<YkcRaporKayitDto> kayitlar, bool icOperasyon)
        {
            var basliklar = new List<string>
            {
                "Talep No", "Talep Tarihi", "Tesisat No", "Sözleşme No", "Abone No", "Abone Adı Soyadı"
            };

            if (icOperasyon)
                basliklar.AddRange(new[] { "Sertifikalı Firma", "Dağıtım Şirketi", "Projedeki Yakıcı Cihaz Tipi", "Projedeki Marka", "Projedeki Kapasite", "Projedeki Baca Tipi" });

            basliklar.AddRange(new[]
            {
                "Yeni Kullanılan Yakıcı Cihaz Tipi", "Yeni Kullanılan Cihaz Markası", "Yeni Kullanılan Cihaz Modeli", "Yeni Kullanılan Cihaz Kapasitesi", "Yeni Kullanılan Baca Tipi", "İkinci El Cihaz",
                "Kontrol Randevusu", "Kontrol Sırası", "İl", "İlçe", "Bölge"
            });

            if (icOperasyon)
                basliklar.AddRange(new[] { "Ekip / Personel", "Hedef Uygulama" });

            basliklar.AddRange(new[] { "Durum", "Form Durumu" });
            var satirlar = new List<IReadOnlyList<object?>>();
            foreach (var kayit in kayitlar)
            {
                var alanlar = new List<object?>
                {
                    kayit.Id.ToString(),
                    kayit.TalepTarihi,
                    kayit.TesisatNo ?? "",
                    kayit.SozlesmeNo ?? "",
                    kayit.AboneNo ?? "",
                    kayit.MusteriAdi ?? ""
                };

                if (icOperasyon)
                {
                    alanlar.AddRange(new object?[]
                    {
                        kayit.FirmaAdi ?? "",
                        kayit.SirketAdi ?? "",
                        kayit.EskiCihazTipi ?? "",
                        kayit.EskiMarka ?? "",
                        ExcelWorkbookService.KapasiteDegeri(kayit.EskiKapasite),
                        kayit.EskiBacaTipi ?? ""
                    });
                }

                alanlar.AddRange(new object?[]
                {
                    kayit.YeniCihazTipi ?? "",
                    kayit.YeniMarka ?? "",
                    kayit.YeniModel ?? "",
                    ExcelWorkbookService.KapasiteDegeri(kayit.YeniKapasite),
                    kayit.YeniBacaTipi ?? "",
                    kayit.IkinciElCihazMi == true ? "Evet" : kayit.IkinciElCihazMi == false ? "Hayır" : "",
                    Randevu(kayit),
                    kayit.SiradakiKontrolNo,
                    kayit.Il ?? "",
                    kayit.Ilce ?? "",
                    kayit.Bolge ?? ""
                });

                if (icOperasyon)
                    alanlar.AddRange(new[] { kayit.AtananEkip ?? "", YkcHedefUygulamaDegerleri.Etiket(kayit.HedefUygulama) });

                alanlar.Add(YkcDurumDegerleri.Etiket(kayit.Durum));
                alanlar.Add(kayit.ImzaliNihaiBelgeVar ? "İmzalı belge hazır" : "İmzalı belge yok");
                satirlar.Add(alanlar);
            }

            return ExcelWorkbookService.Olustur("Cihaz Değişim Raporu", basliklar, satirlar);
        }

        private static object Randevu(YkcRaporKayitDto kayit)
        {
            if (kayit.RandevuTarihi is DateTime tarih)
            {
                if (string.IsNullOrWhiteSpace(kayit.RandevuSaati))
                    return DateOnly.FromDateTime(tarih);
                if (TimeOnly.TryParseExact(kayit.RandevuSaati.Trim(), new[] { "HH:mm", "H:mm", "HH:mm:ss" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var saat))
                    return tarih.Date.Add(saat.ToTimeSpan());
            }

            var randevu = string.Join(" ", new[]
            {
                kayit.RandevuTarihi?.ToString("dd.MM.yyyy"),
                string.IsNullOrWhiteSpace(kayit.RandevuSaati) ? null : kayit.RandevuSaati
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            if (string.IsNullOrWhiteSpace(randevu))
                return kayit.SiradakiKontrolNo is > 1
                    ? $"{kayit.SiradakiKontrolNo}. kontrol randevusu bekleniyor"
                    : "Henüz planlanmadı";

            return randevu;
        }

    }
}
