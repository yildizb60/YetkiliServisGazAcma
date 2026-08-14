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
        public const string Fr265Taslak = "FR265_TASLAK";
        public const string Fr265ImzayaGonderilen = "FR265_IMZAYA_GONDERILEN";
        public const string Fr265ImzaliNihai = "FR265_IMZALI_NIHAI";
        public const string TeknikEk = "TEKNIK_EK";
    }

    public static class YkcDepolamaTuruDegerleri
    {
        public const string Private = "PRIVATE";
        public const string LegacyWwwroot = "LEGACY_WWWROOT";
        public const string ExternalArchive = "EXTERNAL_ARCHIVE";
    }

    public static class YkcImzaDurumDegerleri
    {
        public const string Hazir = "HAZIR";
        public const string ImzayaGonderildi = "IMZAYA_GONDERILDI";
        public const string ImzaBekliyor = "IMZA_BEKLIYOR";
        public const string KismiImzali = "KISMI_IMZALI";
        public const string Tamamlandi = "TAMAMLANDI";
        public const string Hata = "HATA";
        public const string Iptal = "IPTAL";
    }

    public static class YkcImzaciDurumDegerleri
    {
        public const string Bekliyor = "BEKLIYOR";
        public const string Imzaladi = "IMZALADI";
        public const string Reddetti = "REDDETTI";
    }

    public static class YkcFr265KontrolSonucDegerleri
    {
        public const string Bekliyor = "BEKLIYOR";
        public const string Uygun = "UYGUN";
        public const string UygunDegil = "UYGUN_DEGIL";
        public const string Uygulanmaz = "UYGULANMAZ";
    }
}
