using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public static class PersonelYetkiYonetimKurali
{
    public static bool YonetebilirMi(AppKullanici kullanici, int? sirketId)
        => kullanici.AktifMi && kullanici.ArsivlemeTarihi == null
            && (kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin
                    && (!kullanici.SirketId.HasValue || kullanici.SirketId == sirketId)));
}
