using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Models;

public static class YkcDurumSunumu
{
    public static string Etiket(int durum) => durum switch
    {
        YkcDurumDegerleri.TalepAlindi => "Beklemede",
        YkcDurumDegerleri.AtamaBekliyor => "İncelemede",
        YkcDurumDegerleri.Atandi => "Randevu Verildi",
        YkcDurumDegerleri.SahaIsleminde => "Saha İşleminde",
        YkcDurumDegerleri.Reddedildi => "Reddedildi",
        YkcDurumDegerleri.Tamamlandi => "Tamamlandı",
        YkcDurumDegerleri.Iptal => "İptal",
        _ => "Bilinmiyor"
    };

    public static string CssSinifi(int durum) => durum switch
    {
        YkcDurumDegerleri.Tamamlandi => "df-pill-success",
        YkcDurumDegerleri.Reddedildi or YkcDurumDegerleri.Iptal => "df-pill-danger",
        YkcDurumDegerleri.Atandi or YkcDurumDegerleri.SahaIsleminde => "df-pill-primary",
        _ => "df-pill-warning"
    };
}
