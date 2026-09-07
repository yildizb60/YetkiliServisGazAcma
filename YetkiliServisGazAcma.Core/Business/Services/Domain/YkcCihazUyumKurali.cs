using System.Globalization;

namespace YetkiliServisGazAcma.Business.Services;

public static class YkcCihazUyumKurali
{
    public static List<string> Uyarilar(string? eskiTip, string? yeniTip, string? eskiMarka, string? yeniMarka,
        string? eskiBaca, string? yeniBaca, string? eskiKapasite, string? yeniKapasite)
    {
        var sonuc = new List<string>();
        void Karsilastir(string alan, string? eski, string? yeni)
        {
            if (!string.IsNullOrWhiteSpace(eski) && !string.IsNullOrWhiteSpace(yeni)
                && !string.Equals(eski.Trim(), yeni.Trim(), StringComparison.CurrentCultureIgnoreCase))
                sonuc.Add(alan + " proje kaydıyla farklı. İncelemede kontrol edin.");
        }
        Karsilastir("Cihaz tipi", eskiTip, yeniTip);
        Karsilastir("Marka", eskiMarka, yeniMarka);
        Karsilastir("Baca tipi", eskiBaca, yeniBaca);
        if (Kapasite(eskiKapasite, out var eski) && Kapasite(yeniKapasite, out var yeni) && eski != yeni)
            sonuc.Add("Kapasite proje kaydıyla farklı. İncelemede kontrol edin.");
        return sonuc;
    }

    public static bool Kapasite(string? deger, out decimal sonuc)
        => decimal.TryParse(deger?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out sonuc) && sonuc > 0;
}
