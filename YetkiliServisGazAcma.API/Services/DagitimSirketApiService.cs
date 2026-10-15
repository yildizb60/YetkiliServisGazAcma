using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class DagitimSirketApiService(AppDbContext context)
{
    public async Task<List<DagitimSirketApiDto>> ListeleAsync(DagitimSirketListeFiltreDto? filtre)
    {
        var query = context.Dag_Sirketler.AsNoTracking().Where(x => !x.SilindiMi);
        if (filtre?.TumunuGetir != true)
            query = query.Where(x => x.AktifMi);
        if (filtre?.AktifMi.HasValue == true)
            query = query.Where(x => x.AktifMi == filtre.AktifMi.Value);

        return await query.OrderBy(x => x.SirketAdi).Select(x => new DagitimSirketApiDto
        {
            Id = x.Id,
            SirketAdi = x.SirketAdi,
            Il = x.Il,
            Telefon = x.Telefon,
            Email = x.Email,
            Adres = x.Adres,
            AktifMi = x.AktifMi
        }).ToListAsync();
    }

    public async Task<ReferansApiSonuc<DagitimSirketApiDto>> GetirAsync(int id, ClaimsPrincipal kullanici)
    {
        if (!await GorebilirMiAsync(id, kullanici))
            return new(ReferansApiDurum.Yasak);

        var sirket = await context.Dag_Sirketler.AsNoTracking()
            .Where(x => x.Id == id && !x.SilindiMi)
            .Select(x => new DagitimSirketApiDto
            {
                Id = x.Id,
                SirketAdi = x.SirketAdi,
                Il = x.Il,
                Telefon = x.Telefon,
                Email = x.Email,
                Adres = x.Adres,
                AktifMi = x.AktifMi
            })
            .FirstOrDefaultAsync();

        return sirket == null
            ? new(ReferansApiDurum.Bulunamadi, Mesaj: "Sirket bulunamadi")
            : new(ReferansApiDurum.Basarili, sirket);
    }

    private async Task<bool> GorebilirMiAsync(int sirketId, ClaimsPrincipal principal)
    {
        var kullaniciId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(kullaniciId))
            return false;

        var kullanici = await context.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == kullaniciId && x.AktifMi && x.ArsivlemeTarihi == null);
        if (kullanici == null)
            return false;

        if (principal.IsInRole("GenelSistemAdmin")
            || principal.IsInRole("SuperAdmin")
            || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
            || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue))
            return true;

        if (kullanici.SirketId == sirketId)
            return true;

        return kullanici.KullaniciTipi == KullaniciTipiDegerleri.Personel
            && await context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id && x.SirketId == sirketId && !x.SilindiMi
                && context.Dag_Sirketler.Any(s => s.Id == sirketId && s.AktifMi && !s.SilindiMi));
    }
}
