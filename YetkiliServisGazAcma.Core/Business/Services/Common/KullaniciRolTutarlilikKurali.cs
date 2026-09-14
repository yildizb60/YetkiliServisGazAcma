namespace YetkiliServisGazAcma.Business.Services;

public static class KullaniciRolTutarlilikKurali
{
    public static bool FirmaRolleriUyumlu(int kullaniciTipi, IEnumerable<string> roller)
    {
        var atananRoller = new HashSet<string>(roller, StringComparer.Ordinal);
        return (!atananRoller.Contains(KullaniciRolAdlari.YetkiliServis)
                || kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis)
            && (!atananRoller.Contains(KullaniciRolAdlari.SertifikaliFirma)
                || kullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma);
    }
}
