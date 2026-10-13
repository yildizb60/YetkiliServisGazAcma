using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public static class AdminKullaniciKapsami
{
    public static async Task<bool> IceriyorAsync(AppDbContext context, AppKullanici yapan,
        AppKullanici hedef, int? sirketId, bool genelSistemAdminMi, CancellationToken cancellationToken = default)
    {
        if (yapan.Id == hedef.Id || (genelSistemAdminMi && !sirketId.HasValue))
            return true;
        if (!sirketId.HasValue)
            return false;

        if (hedef.KullaniciTipi is KullaniciTipiDegerleri.YetkiliServis or KullaniciTipiDegerleri.SertifikaliFirma
            && hedef.FirmaId.HasValue)
        {
            return await context.Ys_Firmalar.AnyAsync(x => x.Id == hedef.FirmaId.Value
                && !x.SilindiMi && x.SirketId == sirketId.Value, cancellationToken);
        }

        return hedef.KullaniciTipi is KullaniciTipiDegerleri.Personel or KullaniciTipiDegerleri.SirketAdmin
            && hedef.SirketId == sirketId.Value;
    }
}
