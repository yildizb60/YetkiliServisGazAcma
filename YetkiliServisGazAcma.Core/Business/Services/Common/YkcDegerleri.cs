namespace YetkiliServisGazAcma.Business.Services
{
    public enum YkcDurum
    {
        TalepAlindi = 1,
        AtamaBekliyor = 2,
        Atandi = 3,
        SahaIsleminde = 4,
        Reddedildi = 5,
        Tamamlandi = 6,
        Iptal = 7
    }

    public static class YkcDurumDegerleri
    {
        public const int TalepAlindi = (int)YkcDurum.TalepAlindi;
        public const int AtamaBekliyor = (int)YkcDurum.AtamaBekliyor;
        public const int Atandi = (int)YkcDurum.Atandi;
        public const int SahaIsleminde = (int)YkcDurum.SahaIsleminde;
        public const int Reddedildi = (int)YkcDurum.Reddedildi;
        public const int Tamamlandi = (int)YkcDurum.Tamamlandi;
        public const int Iptal = (int)YkcDurum.Iptal;

        public const int Onaylandi = SahaIsleminde;
        public const int SahadaTamamlandi = Tamamlandi;
    }

    public static class YkcHedefUygulamaDegerleri
    {
        public const string YonetimPaneli = "YONETIM_PANELI";
        public const string DogalgazMobileApp = "DOGALGAZ_MOBILE_APP";
        public const string Crm187 = "CRM187";
    }

    public static class YkcFormDosyaTuruDegerleri
    {
        public const string FirmaFormu = "FIRMA_FORMU";
        public const string SahaIslakImzaliForm = "SAHA_ISLAK_IMZALI_FORM";
    }
}
