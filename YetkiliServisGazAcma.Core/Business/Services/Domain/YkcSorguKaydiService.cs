using System.Security.Cryptography;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace YetkiliServisGazAcma.Business.Services;

// The client selects a short-lived reference, never supplies authoritative installation data.
public sealed class YkcSorguKaydiService : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 4096 });

    public string Ekle(string kullaniciId, YkcTalepKaydetDto kaynak)
    {
        var referans = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _cache.Set(referans, (kullaniciId, kaynak),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20), Size = 1 });
        return referans;
    }

    public bool Uygula(string kullaniciId, YkcTalepKaydetDto hedef)
    {
        if (string.IsNullOrWhiteSpace(hedef.SorguReferansi)
            || !_cache.TryGetValue(hedef.SorguReferansi, out (string KullaniciId, YkcTalepKaydetDto Kaynak) kayit)
            || kayit.KullaniciId != kullaniciId
            || !NumaraEslesiyor(kayit.Kaynak.TesisatNo, hedef.TesisatNo)
            || !NumaraEslesiyor(kayit.Kaynak.SozlesmeNo, hedef.SozlesmeNo))
            return false;

        var k = kayit.Kaynak;
        hedef.TesisatNo = k.TesisatNo;
        hedef.SozlesmeNo = k.SozlesmeNo;
        hedef.FirmaId = k.FirmaId;
        hedef.SirketId = k.SirketId;
        hedef.Vkn = k.Vkn;
        hedef.FirmaKodu = k.FirmaKodu;
        hedef.KaynakTipi = "OnlineServis";
        hedef.AboneNo = k.AboneNo;
        hedef.ProjeNo = k.ProjeNo;
        hedef.SayacNo = k.SayacNo;
        hedef.MusteriAdi = k.MusteriAdi;
        hedef.MusteriTelefon = k.MusteriTelefon;
        hedef.Il = k.Il;
        hedef.Ilce = k.Ilce;
        hedef.Bolge = k.Bolge;
        hedef.Adres = k.Adres;
        hedef.EskiCihazTipiKodu = k.EskiCihazTipiKodu;
        hedef.EskiCihazTipi = k.EskiCihazTipi;
        hedef.EskiMarkaKodu = k.EskiMarkaKodu;
        hedef.EskiMarka = k.EskiMarka;
        hedef.EskiBacaTipiKodu = k.EskiBacaTipiKodu;
        hedef.EskiBacaTipi = k.EskiBacaTipi;
        hedef.EskiKapasite = k.EskiKapasite;
        hedef.Aufnr = k.Aufnr;
        return true;
    }

    private static bool NumaraEslesiyor(string? kaynak, string? girilen)
        => long.TryParse(kaynak, NumberStyles.None, CultureInfo.InvariantCulture, out var beklenen)
            && beklenen > 0
            && long.TryParse(girilen?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var deger)
            && beklenen == deger;

    public void Dispose() => _cache.Dispose();
}
