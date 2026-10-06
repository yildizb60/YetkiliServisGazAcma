using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Models;

public static class YkcDurumSunumu
{
    public static string Etiket(int durum) => YkcDurumDegerleri.Etiket(durum);

    public static YkcFr265KontrolDto? SonUygunsuzKontrol(YkcTalepDetayDto talep)
    {
        if (talep.Durum is not (YkcDurumDegerleri.AtamaBekliyor or YkcDurumDegerleri.Atandi or YkcDurumDegerleri.SahaIsleminde))
            return null;

        var sonKontrol = talep.Kontroller
            .Where(x => x.Sonuc is YkcFr265KontrolSonucDegerleri.Uygun or YkcFr265KontrolSonucDegerleri.UygunDegil)
            .OrderByDescending(x => x.KontrolNo).FirstOrDefault();
        return sonKontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil ? sonKontrol : null;
    }

    public static string CssSinifi(int durum) => durum switch
    {
        YkcDurumDegerleri.Tamamlandi => "df-pill-success",
        YkcDurumDegerleri.Reddedildi or YkcDurumDegerleri.Iptal => "df-pill-danger",
        YkcDurumDegerleri.Atandi or YkcDurumDegerleri.SahaIsleminde => "df-pill-primary",
        _ => "df-pill-warning"
    };
}
