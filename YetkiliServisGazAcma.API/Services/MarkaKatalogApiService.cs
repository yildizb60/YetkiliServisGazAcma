using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class MarkaKatalogApiService(AppDbContext context, MarkaService markalar)
{
    public async Task<ReferansApiSonuc<List<MarkaApiDto>>> ListeleAsync(
        MarkaListeFiltreDto? filtre, ClaimsPrincipal kullanici)
    {
        if (filtre?.TumunuGetir == true)
        {
            if (kullanici.Identity?.IsAuthenticated != true)
                return new(ReferansApiDurum.KimlikGerekli);
            if (!kullanici.IsInRole("SirketAdmin") && !await YonetebilirMiAsync(kullanici))
                return new(ReferansApiDurum.Yasak);
        }

        return new(ReferansApiDurum.Basarili, await markalar.ListeleAsync(filtre));
    }

    public async Task<ReferansApiSonuc<MarkaApiDto>> GetirAsync(int id, ClaimsPrincipal kullanici)
    {
        if (!await YonetebilirMiAsync(kullanici))
            return new(ReferansApiDurum.Yasak);

        var marka = await context.Ys_Markalar.AsNoTracking()
            .Where(x => x.Id == id && !x.SilindiMi)
            .Select(x => new MarkaApiDto
            {
                Id = x.Id,
                MarkaAdi = x.MarkaAdi,
                Aciklama = x.Aciklama,
                AktifMi = x.AktifMi
            })
            .FirstOrDefaultAsync();

        return marka == null
            ? new(ReferansApiDurum.Bulunamadi, Mesaj: "Marka bulunamadi")
            : new(ReferansApiDurum.Basarili, marka);
    }

    public async Task<ReferansApiSonuc<ApiIslemSonuc>> EkleAsync(MarkaKaydetDto dto, ClaimsPrincipal kullanici)
    {
        if (!await YonetebilirMiAsync(kullanici))
            return new(ReferansApiDurum.Yasak);
        if (string.IsNullOrWhiteSpace(dto.MarkaAdi))
            return new(ReferansApiDurum.Gecersiz, Mesaj: "Marka adi zorunludur");

        var marka = new Ys_Marka
        {
            MarkaAdi = dto.MarkaAdi,
            Aciklama = dto.Aciklama,
            AktifMi = dto.AktifMi
        };
        await markalar.Ekle(marka, kullanici.Identity?.Name);
        return new(ReferansApiDurum.Basarili,
            new() { Basarili = true, Mesaj = "Marka eklendi", Id = marka.Id });
    }

    public async Task<ReferansApiSonuc<ApiIslemSonuc>> GuncelleAsync(MarkaKaydetDto dto, ClaimsPrincipal kullanici)
    {
        if (!await YonetebilirMiAsync(kullanici))
            return new(ReferansApiDurum.Yasak);
        if (!dto.Id.HasValue)
            return new(ReferansApiDurum.Gecersiz, Mesaj: "Id zorunludur");

        var marka = new Ys_Marka
        {
            Id = dto.Id.Value,
            MarkaAdi = dto.MarkaAdi,
            Aciklama = dto.Aciklama,
            AktifMi = dto.AktifMi
        };
        if (!await markalar.Guncelle(marka, kullanici.Identity?.Name))
            return new(ReferansApiDurum.Bulunamadi, Mesaj: "Marka bulunamadi");

        return new(ReferansApiDurum.Basarili,
            new() { Basarili = true, Mesaj = "Marka guncellendi" });
    }

    public async Task<ReferansApiSonuc<ApiIslemSonuc>> SilAsync(int id, ClaimsPrincipal kullanici)
    {
        if (!await YonetebilirMiAsync(kullanici))
            return new(ReferansApiDurum.Yasak);
        if (await markalar.KullaniliyorMu(id))
            return new(ReferansApiDurum.Gecersiz, Mesaj: "Bu marka kullanildigi icin silinemez");
        if (!await markalar.Sil(id, kullanici.Identity?.Name))
            return new(ReferansApiDurum.Bulunamadi, Mesaj: "Marka bulunamadi");

        return new(ReferansApiDurum.Basarili,
            new() { Basarili = true, Mesaj = "Marka silindi" });
    }

    private async Task<bool> YonetebilirMiAsync(ClaimsPrincipal principal)
    {
        if (principal.IsInRole("GenelSistemAdmin") || principal.IsInRole("SuperAdmin"))
            return true;

        var kullaniciId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(kullaniciId))
            return false;

        var kullanici = await context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kullaniciId);
        if (kullanici == null)
            return false;
        if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin ||
            (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue))
            return true;

        return await context.Dag_PersonelYetkiler.AnyAsync(x =>
            x.KullaniciId == kullanici.Id && !x.SilindiMi &&
            (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.MARKA_YONET));
    }
}
