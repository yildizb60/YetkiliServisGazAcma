using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class DevreyeAlmaExcelService
    {
        public static byte[] Olustur(IEnumerable<Ys_DevreyeAlma> islemler)
        {
            var basliklar = new[]
            {
                "Tesisat No", "Yetkili Servis", "Dağıtım Şirketi", "Müşteri", "Telefon", "T.C. Kimlik No",
                "Adres", "Cihaz Tipi", "Marka", "Model", "Seri No", "Kapasite", "Teknisyen",
                "Teknisyen Yetki Belgesi No", "Durum", "Devreye Alma Tarihi", "Notlar"
            };
            var satirlar = islemler.Select(i => (IReadOnlyList<string?>)new string?[]
            {
                i.TesistatNo,
                i.Firma?.FirmaAdi,
                i.Firma?.Sirket?.SirketAdi,
                i.MusteriAdi,
                i.MusteriTelefon,
                i.MusteriTcNo,
                i.Adres,
                i.CihazTipi,
                i.Marka?.MarkaAdi ?? i.CihazMarka,
                i.CihazModeli,
                i.SeriNo,
                i.CihazKapasite,
                i.TeknisyenAdi,
                i.TeknisyenYetkiBelgesiNo,
                DurumText(i.Durum),
                i.DevreyeAlmaTarihi.ToString("dd.MM.yyyy HH:mm"),
                i.Notlar
            });

            return ExcelWorkbookService.Olustur("Cihaz Devreye Alma", basliklar, satirlar);
        }

        private static string DurumText(int durum)
        {
            return durum == DevreyeAlmaDurumDegerleri.Tamamlandi ? "Tamamland\u0131" : durum == DevreyeAlmaDurumDegerleri.Iptal ? "\u0130ptal" : "Bekliyor";
        }

    }
}
