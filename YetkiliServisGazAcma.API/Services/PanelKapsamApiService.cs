using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class PanelKapsamApiService(
    AppDbContext context,
    UserManager<AppKullanici> userManager,
    SehirFirmaKoduService sehirFirmaKodlari,
    YkcYetkiService ykcYetkileri)
{
    public async Task<ReferansApiSonuc<List<PanelSirketDto>>> KullaniciSirketleriAsync(ClaimsPrincipal principal)
    {
        var kullanici = await AktifKullaniciAsync(principal);
        if (kullanici == null)
            return new(ReferansApiDurum.KimlikGerekli);

        return new(ReferansApiDurum.Basarili, await SirketleriListeleAsync(kullanici, principal));
    }

    public async Task<ReferansApiSonuc<PanelKimlikApiSonuc>> KimlikAsync(
        ClaimsPrincipal principal, int? aktifSirketId)
    {
        var kullanici = await AktifKullaniciAsync(principal);
        if (kullanici == null)
            return new(ReferansApiDurum.KimlikGerekli);
        if (aktifSirketId is int sirketId
            && !(await SirketleriListeleAsync(kullanici, principal)).Any(x => x.Id == sirketId))
            return new(ReferansApiDurum.Yasak);

        return new(ReferansApiDurum.Basarili, await KimlikOlusturAsync(kullanici, aktifSirketId));
    }

    public async Task<ReferansApiSonuc<YkcYetkiOzeti>> YkcYetkileriAsync(
        ClaimsPrincipal principal, int? aktifSirketId, CancellationToken cancellationToken = default)
    {
        var kullanici = await AktifKullaniciAsync(principal);
        if (kullanici == null)
            return new(ReferansApiDurum.KimlikGerekli);

        var sirketler = await SirketleriListeleAsync(kullanici, principal);
        if (aktifSirketId is int seciliId && !sirketler.Any(x => x.Id == seciliId))
            return new(ReferansApiDurum.Yasak);

        var sirketId = aktifSirketId ?? kullanici.SirketId ?? sirketler.FirstOrDefault()?.Id;
        if (sirketId == null && !await GenelSistemAdminMiAsync(kullanici, principal))
            return new(ReferansApiDurum.Basarili, new());

        return new(ReferansApiDurum.Basarili,
            await ykcYetkileri.OzetAsync(kullanici, sirketId, cancellationToken));
    }

    private async Task<AppKullanici?> AktifKullaniciAsync(ClaimsPrincipal principal)
    {
        var kullanici = await userManager.GetUserAsync(principal);
        return kullanici?.AktifMi == true ? kullanici : null;
    }

    private async Task<List<PanelSirketDto>> SirketleriListeleAsync(AppKullanici kullanici, ClaimsPrincipal principal)
    {
        var query = context.Dag_Sirketler.AsNoTracking().Where(x => !x.SilindiMi && x.AktifMi);
        if (!await GenelSistemAdminMiAsync(kullanici, principal))
        {
            var sirketIds = new HashSet<int>();
            if (kullanici.SirketId.HasValue)
                sirketIds.Add(kullanici.SirketId.Value);

            if (kullanici.FirmaId.HasValue)
            {
                var firmaSirketId = await context.Ys_Firmalar
                    .Where(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi)
                    .Select(x => x.SirketId)
                    .FirstOrDefaultAsync();
                if (firmaSirketId > 0)
                    sirketIds.Add(firmaSirketId);
            }

            var yetkiSirketleri = await context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi)
                .Select(x => x.SirketId)
                .Distinct()
                .ToListAsync();
            foreach (var id in yetkiSirketleri.Where(x => x > 0))
                sirketIds.Add(id);

            if (sirketIds.Count == 0)
                return [];

            query = query.Where(x => sirketIds.Contains(x.Id));
        }

        return await query.OrderBy(x => x.SirketAdi).Select(x => new PanelSirketDto
        {
            Id = x.Id,
            SirketAdi = x.SirketAdi,
            Il = x.Il,
            AktifMi = x.AktifMi
        }).ToListAsync();
    }

    private async Task<PanelKimlikApiSonuc> KimlikOlusturAsync(AppKullanici kullanici, int? aktifSirketId)
    {
        string? sirketAdi = null;
        string? sehir = null;

        // A firm's own company/city takes precedence over the selected panel company.
        if (kullanici.FirmaId.HasValue)
        {
            var firma = await context.Ys_Firmalar
                .Where(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi)
                .Select(x => new
                {
                    SirketAdi = x.Sirket != null ? x.Sirket.SirketAdi : null,
                    Sehir = x.FaaliyetIli ?? (x.Sirket != null ? x.Sirket.Il : null)
                })
                .FirstOrDefaultAsync();
            sirketAdi = firma?.SirketAdi;
            sehir = firma?.Sehir;
        }

        if (string.IsNullOrWhiteSpace(sirketAdi) && aktifSirketId.HasValue)
        {
            var sirket = await context.Dag_Sirketler
                .Where(x => x.Id == aktifSirketId.Value && !x.SilindiMi)
                .Select(x => new { x.SirketAdi, x.Il })
                .FirstOrDefaultAsync();
            sirketAdi = sirket?.SirketAdi;
            sehir = sirket?.Il;
        }

        if (string.IsNullOrWhiteSpace(sirketAdi) && kullanici.SirketId.HasValue)
        {
            var sirket = await context.Dag_Sirketler
                .Where(x => x.Id == kullanici.SirketId.Value && !x.SilindiMi)
                .Select(x => new { x.SirketAdi, x.Il })
                .FirstOrDefaultAsync();
            sirketAdi = sirket?.SirketAdi;
            sehir = sirket?.Il;
        }

        var firmaKodu = sehirFirmaKodlari.FirmaKodu(sehir);
        return new()
        {
            SirketAdi = string.IsNullOrWhiteSpace(sirketAdi) ? firmaKodu : sirketAdi,
            Sehir = sehir,
            FirmaKodu = firmaKodu
        };
    }

    private async Task<bool> GenelSistemAdminMiAsync(AppKullanici kullanici, ClaimsPrincipal principal)
    {
        if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
            || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue))
            return true;

        return principal.IsInRole(KullaniciRolAdlari.GenelSistemAdmin)
            || principal.IsInRole("SuperAdmin")
            || await userManager.IsInRoleAsync(kullanici, KullaniciRolAdlari.GenelSistemAdmin);
    }
}
