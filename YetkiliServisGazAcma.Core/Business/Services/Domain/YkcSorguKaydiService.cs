using System.Security.Cryptography;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace YetkiliServisGazAcma.Business.Services;

public interface IYkcSorguKaydiService
{
    Task<string> EkleAsync(string kullaniciId, YkcTalepKaydetDto kaynak);
    Task<bool> UygulaAsync(string kullaniciId, YkcTalepKaydetDto hedef);
}

internal static class YkcSorguKaydiAyarlari
{
    public static readonly TimeSpan GecerlilikSuresi = TimeSpan.FromMinutes(20);
}

// The client selects a short-lived reference, never supplies authoritative installation data.
public sealed class YkcSorguKaydiService : IYkcSorguKaydiService, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 4096 });

    public string Ekle(string kullaniciId, YkcTalepKaydetDto kaynak)
    {
        var referans = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _cache.Set(referans, (kullaniciId, kaynak),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = YkcSorguKaydiAyarlari.GecerlilikSuresi, Size = 1 });
        return referans;
    }

    public Task<string> EkleAsync(string kullaniciId, YkcTalepKaydetDto kaynak)
        => Task.FromResult(Ekle(kullaniciId, kaynak));

    public bool Uygula(string kullaniciId, YkcTalepKaydetDto hedef)
    {
        return !string.IsNullOrWhiteSpace(hedef.SorguReferansi)
            && _cache.TryGetValue(hedef.SorguReferansi, out (string KullaniciId, YkcTalepKaydetDto Kaynak) kayit)
            && kayit.KullaniciId == kullaniciId
            && YkcSorguKaydiDogrulama.Uygula(kayit.Kaynak, hedef);
    }

    public Task<bool> UygulaAsync(string kullaniciId, YkcTalepKaydetDto hedef)
        => Task.FromResult(Uygula(kullaniciId, hedef));

    public void Dispose() => _cache.Dispose();
}

internal static class YkcSorguKaydiDogrulama
{
    public static bool Uygula(YkcTalepKaydetDto kaynak, YkcTalepKaydetDto hedef)
    {
        if (!NumaraEslesiyor(kaynak.TesisatNo, hedef.TesisatNo)
            || !NumaraEslesiyor(kaynak.SozlesmeNo, hedef.SozlesmeNo)
            || string.IsNullOrWhiteSpace(hedef.YeniCihazTipi)
            || !kaynak.IzinliYeniCihazTipleri.TryGetValue(hedef.YeniCihazTipi.Trim(), out var yeniCihazTipiKodu))
            return false;

        hedef.TesisatNo = kaynak.TesisatNo;
        hedef.SozlesmeNo = kaynak.SozlesmeNo;
        hedef.FirmaId = kaynak.FirmaId;
        hedef.SirketId = kaynak.SirketId;
        hedef.Vkn = kaynak.Vkn;
        hedef.FirmaKodu = kaynak.FirmaKodu;
        hedef.KaynakTipi = "OnlineServis";
        hedef.AboneNo = kaynak.AboneNo;
        hedef.ProjeNo = kaynak.ProjeNo;
        hedef.SayacNo = kaynak.SayacNo;
        hedef.MusteriAdi = kaynak.MusteriAdi;
        hedef.MusteriTelefon = kaynak.MusteriTelefon;
        hedef.Il = kaynak.Il;
        hedef.Ilce = kaynak.Ilce;
        hedef.Bolge = kaynak.Bolge;
        hedef.Adres = kaynak.Adres;
        hedef.EskiCihazTipiKodu = kaynak.EskiCihazTipiKodu;
        hedef.EskiCihazTipi = kaynak.EskiCihazTipi;
        hedef.EskiMarkaKodu = kaynak.EskiMarkaKodu;
        hedef.EskiMarka = kaynak.EskiMarka;
        hedef.EskiBacaTipiKodu = kaynak.EskiBacaTipiKodu;
        hedef.EskiBacaTipi = kaynak.EskiBacaTipi;
        hedef.EskiKapasite = kaynak.EskiKapasite;
        hedef.YeniCihazTipi = kaynak.IzinliYeniCihazTipleri.Keys.First(x =>
            string.Equals(x, hedef.YeniCihazTipi.Trim(), StringComparison.OrdinalIgnoreCase));
        hedef.YeniCihazTipiKodu = yeniCihazTipiKodu;
        hedef.Aufnr = kaynak.Aufnr;
        return true;
    }

    private static bool NumaraEslesiyor(string? kaynak, string? girilen)
        => long.TryParse(kaynak, NumberStyles.None, CultureInfo.InvariantCulture, out var beklenen)
            && beklenen > 0
            && long.TryParse(girilen?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var deger)
            && beklenen == deger;
}
