namespace YetkiliServisGazAcma.Business.Services
{
    public enum KullaniciTipi
    {
        YetkiliServis = 1,
        Personel = 2,
        SirketAdmin = 3,
        GenelSistemAdmin = 4
    }

    public static class KullaniciTipiDegerleri
    {
        public const int YetkiliServis = (int)KullaniciTipi.YetkiliServis;
        public const int Personel = (int)KullaniciTipi.Personel;
        public const int SirketAdmin = (int)KullaniciTipi.SirketAdmin;
        public const int GenelSistemAdmin = (int)KullaniciTipi.GenelSistemAdmin;
    }
}
