using System.Globalization;

namespace YetkiliServisGazAcma.Business.Services;

public static class YkcCihazUyumKurali
{
    public static List<string> TalepOncesiUyarilar(YkcTalepKaydetDto cihaz)
    {
        var sonuc = new List<string>();
        void Karsilastir(string alan, string? kaynak, string? yeni, bool marka = false, bool eksikKaynakUyarisi = true)
        {
            if (string.IsNullOrWhiteSpace(yeni)) return;
            if (string.IsNullOrWhiteSpace(kaynak) || kaynak.Trim() == "-")
            {
                if (eksikKaynakUyarisi)
                    sonuc.Add($"Proje kaydında {alan.ToLowerInvariant()} bulunmadığı için karşılaştırılamadı.");
                return;
            }
            if (marka)
            {
                kaynak = MarkaKarsilastirmaDegeri(kaynak);
                yeni = MarkaKarsilastirmaDegeri(yeni);
            }
            if (!string.Equals(kaynak.Trim(), yeni.Trim(), StringComparison.OrdinalIgnoreCase))
                sonuc.Add($"{alan} proje kaydıyla farklı.");
        }

        Karsilastir("Marka", cihaz.EskiMarka, cihaz.YeniMarka, marka: true);
        Karsilastir("Baca tipi", cihaz.EskiBacaTipi, cihaz.YeniBacaTipi, eksikKaynakUyarisi: false);
        if (Kapasite(cihaz.YeniKapasite, out var yeni))
        {
            if (!Kapasite(cihaz.EskiKapasite, out var eski))
                sonuc.Add("Proje kaydında geçerli kapasite bilgisi bulunmadığı için karşılaştırılamadı.");
            else if (yeni > eski)
                sonuc.Add("Yeni cihazın kapasitesi projedeki kapasiteden yüksek. Tadilat projesi gereklidir.");
            else if (yeni < eski)
                sonuc.Add("Yeni cihazın kapasitesi projedeki kapasiteden düşük.");
        }
        return sonuc;
    }

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
        Karsilastir("Marka", MarkaKarsilastirmaDegeri(eskiMarka), MarkaKarsilastirmaDegeri(yeniMarka));
        Karsilastir("Baca tipi", eskiBaca, yeniBaca);
        if (Kapasite(eskiKapasite, out var eski) && Kapasite(yeniKapasite, out var yeni) && yeni > eski)
            sonuc.Add("Yeni cihazın kapasitesi projedeki kapasiteden yüksek. Tadilat projesi gereklidir.");
        return sonuc;
    }

    // Marka kisaltmalarindaki nokta ve bosluklar fark uyarisi olusturmaz.
    private static string MarkaKarsilastirmaDegeri(string? deger)
        => new string((deger ?? string.Empty).Where(c => c != '.' && !char.IsWhiteSpace(c)).ToArray())
            .ToUpperInvariant().Replace('İ', 'I').Replace('ı', 'I');

    public static bool Kapasite(string? deger, out decimal sonuc)
        => decimal.TryParse(deger?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out sonuc) && sonuc > 0;
}
