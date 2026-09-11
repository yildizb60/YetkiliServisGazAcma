namespace YetkiliServisGazAcma.Business.Services;

public static class YkcFirmaSunumu
{
    public static void Hazirla(YkcTalepDetayDto talep, bool resmiForm)
    {
        if (!resmiForm)
        {
            talep.EskiCihaz = null;
            talep.ProjedekiCihazBilgisi = null;
            talep.EskiCihazTipi = null;
            talep.EskiCihazTipiKodu = null;
            talep.EskiMarka = null;
            talep.EskiMarkaKodu = null;
            talep.EskiBacaTipi = null;
            talep.EskiBacaTipiKodu = null;
            talep.EskiKapasite = null;
        }

        // Official form data keeps device fields, not internal routing or staff notes.
        talep.Atamalar.Clear();
        talep.AtananEkip = null;
        talep.HedefUygulama = null;
        foreach (var kayit in talep.Gecmis)
        {
            kayit.KullaniciAdi = null;
            if (kayit.IslemTipi is "AtamaYapildi" or "DurumGuncellendi")
                kayit.Aciklama = null;
        }
    }
}
