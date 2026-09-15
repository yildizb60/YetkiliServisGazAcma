namespace YetkiliServisGazAcma.Business.Services;

public static class KullaniciRolTutarlilikKurali
{
    public static bool FirmaRolleriUyumlu(int kullaniciTipi, IEnumerable<string> roller, int? sirketId = null)
    {
        var atananRoller = new HashSet<string>(roller, StringComparer.Ordinal);
        var izinliRoller = kullaniciTipi switch
        {
            KullaniciTipiDegerleri.YetkiliServis => new[] { KullaniciRolAdlari.YetkiliServis },
            KullaniciTipiDegerleri.SertifikaliFirma => new[] { KullaniciRolAdlari.SertifikaliFirma },
            KullaniciTipiDegerleri.Personel => new[] { KullaniciRolAdlari.Personel },
            KullaniciTipiDegerleri.SirketAdmin when sirketId.HasValue => new[] { KullaniciRolAdlari.SirketAdmin },
            KullaniciTipiDegerleri.SirketAdmin => new[]
            {
                KullaniciRolAdlari.GenelSistemAdmin,
                KullaniciRolAdlari.EskiSuperAdmin
            },
            KullaniciTipiDegerleri.GenelSistemAdmin => new[]
            {
                KullaniciRolAdlari.GenelSistemAdmin,
                KullaniciRolAdlari.EskiSuperAdmin
            },
            _ => Array.Empty<string>()
        };

        if (izinliRoller.Length == 0)
            return false;

        var yonetilenRoller = new HashSet<string>(new[]
        {
            KullaniciRolAdlari.GenelSistemAdmin,
            KullaniciRolAdlari.SirketAdmin,
            KullaniciRolAdlari.EskiSuperAdmin,
            KullaniciRolAdlari.Personel,
            KullaniciRolAdlari.YetkiliServis,
            KullaniciRolAdlari.SertifikaliFirma
        }, StringComparer.Ordinal);

        return !atananRoller.Any(rol => yonetilenRoller.Contains(rol) && !izinliRoller.Contains(rol, StringComparer.Ordinal));
    }
}
