using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.ViewComponents;

public sealed class YkcRandevuOzetiModel
{
    public DateTime Tarih { get; init; } = DateTime.Today;
    public YkcTakvimFiltre Filtre { get; init; } = new();
    public YkcTakvimSonuc? Gun { get; init; }
    public List<YkcTakvimGunOzeti> AyGunleri { get; init; } = new();
    public bool FirmaGorunumu { get; init; }
    public string Gorunum { get; init; } = "ay";
}

public sealed class YkcRandevuOzetiViewComponent(
    ApiKullaniciOturumu users, PanelKapsamApiClient yetki, YkcApiClient api, AktifSirketService sirket,
    ILogger<YkcRandevuOzetiViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await users.GetUserAsync(HttpContext.User);
        if (user == null) return Content("");
        var firmaGorunumu = user.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma || user.FirmaId.HasValue;
        var query = HttpContext.Request.Query;
        var gorunum = firmaGorunumu ? "ay" : query["takvimGorunum"].ToString().Trim().ToLowerInvariant() switch
        {
            "gun" => "gun",
            "yil" => "yil",
            _ => "ay"
        };
        var tarih = DateTime.TryParseExact(query["takvimTarih"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value) && value.Year is >= 2000 and <= 2100 ? value.Date : DateTime.Today;
        string? Filter(string key) => query[key].ToString().Trim() is { Length: > 0 and <= 120 } text ? text : null;
        var filtre = new YkcTakvimFiltre {
            Baslangic = tarih, Bitis = tarih,
            Il = firmaGorunumu ? null : Filter("takvimIl"),
            Bolge = firmaGorunumu ? null : Filter("takvimBolge"),
            Personel = firmaGorunumu ? null : Filter("takvimPersonel"),
            Musteri = Filter("takvimAbone"), TesisatNo = Filter("takvimTesisat")
        };
        try
        {
            if (!(await yetki.YkcYetkileriAsync(user, await sirket.AktifSirketIdAsync(user))).TalepleriGorebilir)
                return Content("");
            var gun = await api.TakvimAsync(user, filtre);
            var ayBasi = new DateTime(tarih.Year, tarih.Month, 1);
            var ay = await api.TakvimAsync(user, new YkcTakvimFiltre {
                Baslangic = ayBasi, Bitis = ayBasi.AddMonths(1).AddDays(-1), Il = filtre.Il,
                Bolge = filtre.Bolge, Personel = filtre.Personel, Musteri = filtre.Musteri, TesisatNo = filtre.TesisatNo
            });
            return View(new YkcRandevuOzetiModel { Tarih = tarih, Filtre = filtre, Gun = gun, AyGunleri = ay?.Gunler ?? new(), FirmaGorunumu = firmaGorunumu, Gorunum = gorunum });
        }
        catch (ApiIntegrationException ex)
        {
            logger.LogWarning(ex, "Ana panel randevuları alınamadı.");
            return View(new YkcRandevuOzetiModel { Tarih = tarih, Filtre = filtre, FirmaGorunumu = firmaGorunumu, Gorunum = gorunum });
        }
    }
}
