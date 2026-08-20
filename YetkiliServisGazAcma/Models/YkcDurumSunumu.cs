using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Models;

public static class YkcDurumSunumu
{
    public static string Etiket(int durum) => durum switch
    {
        YkcDurumDegerleri.TalepAlindi => "Talep Alındı",
        YkcDurumDegerleri.AtamaBekliyor => "İnceleniyor",
        YkcDurumDegerleri.Atandi => "Randevu Oluşturuldu",
        YkcDurumDegerleri.SahaIsleminde => "İşlem Devam Ediyor",
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
