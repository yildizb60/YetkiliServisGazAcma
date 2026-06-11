namespace YetkiliServisGazAcma.Business.Services
{
    public enum DevreyeAlmaDurum
    {
        Bekliyor = 0,
        Tamamlandi = 1,
        Iptal = 2
    }

    public static class DevreyeAlmaDurumDegerleri
    {
        public const int Bekliyor = (int)DevreyeAlmaDurum.Bekliyor;
        public const int Tamamlandi = (int)DevreyeAlmaDurum.Tamamlandi;
        public const int Iptal = (int)DevreyeAlmaDurum.Iptal;
    }

    public enum YetkiBelgesiDurum
    {
        OnaydaBekliyor = 0,
        Onaylandi = 1,
        Reddedildi = 2
    }

    public static class YetkiBelgesiDurumDegerleri
    {
        public const int OnaydaBekliyor = (int)YetkiBelgesiDurum.OnaydaBekliyor;
        public const int Onaylandi = (int)YetkiBelgesiDurum.Onaylandi;
        public const int Reddedildi = (int)YetkiBelgesiDurum.Reddedildi;
    }
}
