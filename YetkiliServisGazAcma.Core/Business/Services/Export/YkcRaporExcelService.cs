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
                basliklar.AddRange(new[] { "Sertifikalı Firma", "Dağıtım Şirketi", "Projedeki Cihaz Türü", "Projedeki Marka", "Projedeki Kapasite", "Projedeki Baca Tipi" });

            basliklar.AddRange(new[]
            {
                "Yeni Kullanılan Cihaz Türü", "Yeni Kullanılan Cihaz Markası", "Yeni Kullanılan Cihaz Modeli", "Yeni Kullanılan Cihaz Kapasitesi", "Yeni Kullanılan Baca Tipi", "İkinci El",
                "Kontrol Randevusu", "İl", "İlçe", "Bölge"
            });

            if (icOperasyon)
                basliklar.AddRange(new[] { "Ekip / Personel", "Hedef Uygulama" });

            basliklar.AddRange(new[] { "Durum", "Form Durumu" });
            var satirlar = new List<IReadOnlyList<string?>>();
            foreach (var kayit in kayitlar)
            {
                var alanlar = new List<string?>
                {
                    kayit.Id.ToString(),
                    kayit.TalepTarihi.ToString("dd.MM.yyyy HH:mm"),
                    kayit.TesisatNo ?? "",
                    kayit.SozlesmeNo ?? "",
                    kayit.AboneNo ?? "",
                    kayit.MusteriAdi ?? ""
                };

                if (icOperasyon)
                {
                    alanlar.AddRange(new[]
                    {
                        kayit.FirmaAdi ?? "",
                        kayit.SirketAdi ?? "",
                        kayit.EskiCihazTipi ?? "",
                        kayit.EskiMarka ?? "",
                        kayit.EskiKapasite ?? "",
                        kayit.EskiBacaTipi ?? ""
                    });
                }

                alanlar.AddRange(new[]
                {
                    kayit.YeniCihazTipi ?? "",
                    kayit.YeniMarka ?? "",
                    kayit.YeniModel ?? "",
                    kayit.YeniKapasite ?? "",
                    kayit.YeniBacaTipi ?? "",
                    kayit.IkinciElCihazMi == true ? "Evet" : kayit.IkinciElCihazMi == false ? "Hayır" : "",
                    Randevu(kayit),
                    kayit.Il ?? "",
                    kayit.Ilce ?? "",
                    kayit.Bolge ?? ""
                });

                if (icOperasyon)
                    alanlar.AddRange(new[] { kayit.AtananEkip ?? "", kayit.HedefUygulama ?? "" });

                alanlar.Add(Durum(kayit.Durum));
                alanlar.Add(kayit.ImzaliNihaiBelgeVar ? "İmzalı belge hazır" : "İmzalı belge yok");
                satirlar.Add(alanlar);
            }

            return ExcelWorkbookService.Olustur("Cihaz Değişim Raporu", basliklar, satirlar);
        }

        private static string Randevu(YkcRaporKayitDto kayit)
        {
            var randevu = string.Join(" ", new[]
            {
                kayit.RandevuTarihi?.ToString("dd.MM.yyyy"),
                string.IsNullOrWhiteSpace(kayit.RandevuSaati) ? null : kayit.RandevuSaati
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            if (string.IsNullOrWhiteSpace(randevu))
                return kayit.SiradakiKontrolNo is > 1
                    ? $"{kayit.SiradakiKontrolNo}. kontrol randevusu bekleniyor"
                    : "Henüz planlanmadı";

            return kayit.SiradakiKontrolNo is > 1
                ? $"{kayit.SiradakiKontrolNo}. kontrol - {randevu}"
                : randevu;
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

    }
}
