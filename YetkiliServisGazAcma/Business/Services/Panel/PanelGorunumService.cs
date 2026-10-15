using Microsoft.AspNetCore.Mvc.ViewFeatures;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

// Scoped to one HTTP request; permission data is never shared across users or companies.
public sealed class PanelGorunumService(AktifSirketService sirket, PersonelPanelApiClient personel,
    AdminDashboardApiClient dashboard, YetkiliServisPanelApiClient servis)
{
    private readonly Dictionary<string, Task<List<string>>> _yetkiler = new();
    public string? HataMesaji { get; private set; }

    public async Task<bool> YetkiliMiAsync(AppKullanici kullanici, string yetki)
    {
        if (await sirket.GenelSistemAdminMi(kullanici) || await sirket.SirketAdminMi(kullanici)) return true;
        var kodlar = await YetkilerAsync(kullanici);
        return kodlar.Contains(YetkiTipleri.TAM_YETKI, StringComparer.OrdinalIgnoreCase)
            || kodlar.Contains(yetki, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<string>> YetkilerAsync(AppKullanici kullanici)
    {
        var sirketId = await sirket.AktifSirketIdAsync(kullanici);
        var key = $"{kullanici.Id}:{sirketId}";
        if (!_yetkiler.TryGetValue(key, out var task))
            _yetkiler[key] = task = GetirAsync(kullanici, sirketId);
        return await task;
    }

    private async Task<List<string>> GetirAsync(AppKullanici kullanici, int? sirketId)
    {
        try { return await personel.YetkilerimAsync(kullanici, sirketId) ?? new(); }
        catch (ApiIntegrationException ex) { HataMesaji = ex.Message; return new(); }
    }

    public async Task HazirlaAsync(AppKullanici kullanici, ViewDataDictionary viewData)
    {
        viewData["Kullanici"] = kullanici;
        if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.Personel)
        {
            var kodlar = await YetkilerAsync(kullanici);
            var tam = kodlar.Contains(YetkiTipleri.TAM_YETKI, StringComparer.OrdinalIgnoreCase);
            bool Var(string kod) => tam || kodlar.Contains(kod, StringComparer.OrdinalIgnoreCase);
            viewData["YetkiBelgesi"] = Var(YetkiTipleri.YETKI_BELGESI_ONAY);
            viewData["YetkiRapor"] = Var(YetkiTipleri.RAPOR_GOR);
            viewData["YetkiServis"] = Var(YetkiTipleri.KULLANICI_YONET);
            viewData["YetkiMarka"] = viewData["YetkiMarkaYonet"] = Var(YetkiTipleri.MARKA_YONET);
            viewData["YetkiYkcTalep"] = Var(YetkiTipleri.YKC_TALEP_GOR);
            viewData["YetkiYkcAtama"] = Var(YetkiTipleri.YKC_ATAMA_YAP);
            viewData["YetkiYkcImza"] = Var(YetkiTipleri.YKC_FR265_IMZA_ISLEM);
            viewData["YetkiYkcRapor"] = Var(YetkiTipleri.YKC_RAPOR_GOR);
            viewData["Yetkilerim"] = kodlar.Select(YetkiAdi).Distinct().ToList();
        }
        else if (await sirket.GenelSistemAdminMi(kullanici) || await sirket.SirketAdminMi(kullanici))
        {
            foreach (var key in new[] { "YetkiBelgesi", "YetkiRapor", "YetkiServis", "YetkiMarka",
                "YetkiMarkaYonet", "YetkiYkcTalep", "YetkiYkcAtama", "YetkiYkcImza", "YetkiYkcRapor" })
                viewData[key] = true;
            viewData["Yetkilerim"] = new List<string> { "Tam Yetki" };
        }

        try
        {
            if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis)
            {
                if (!viewData.ContainsKey("Bildirimler"))
                {
                    var sonuc = await servis.BildirimlerAsync(kullanici);
                    viewData["Bildirimler"] = sonuc?.Bildirimler ?? new();
                    viewData["BildirimSayisi"] = sonuc?.BildirimSayisi ?? 0;
                }
            }
            else if (kullanici.KullaniciTipi != KullaniciTipiDegerleri.SertifikaliFirma
                && (!viewData.ContainsKey("OnayBekleyen") || !viewData.ContainsKey("SuresiBitecek")))
            {
                var ozet = await dashboard.BildirimOzetiAsync(kullanici, await sirket.AktifSirketIdAsync(kullanici));
                viewData["OnayBekleyen"] = ozet?.OnayBekleyen ?? 0;
                viewData["SuresiBitecek"] = ozet?.SuresiBitecek ?? 0;
            }
        }
        catch (ApiIntegrationException ex)
        {
            HataMesaji = ex.Message;
            viewData.TryAdd("OnayBekleyen", 0);
            viewData.TryAdd("SuresiBitecek", 0);
            viewData.TryAdd("Bildirimler", new List<string>());
        }
    }

    private static string YetkiAdi(string kod) => kod switch
    {
        YetkiTipleri.TAM_YETKI => "Tam Yetki",
        YetkiTipleri.YETKI_BELGESI_ONAY => "Yetki Belgesi Onay",
        YetkiTipleri.RAPOR_GOR => "Rapor Gör",
        YetkiTipleri.KULLANICI_YONET => "Kullanıcı Yönet",
        YetkiTipleri.MARKA_YONET => "Marka Yönet",
        YetkiTipleri.YKC_TALEP_GOR => "YKC Taleplerini Gör",
        YetkiTipleri.YKC_ATAMA_YAP => "YKC Atama ve Randevu",
        YetkiTipleri.YKC_FR265_IMZA_ISLEM => "YKC FR265 ve İmza İşlemleri",
        YetkiTipleri.YKC_RAPOR_GOR => "YKC Raporlarını Gör",
        _ => kod
    };
}
