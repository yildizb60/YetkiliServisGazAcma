using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class UrunKategoriKatalogApiService(AppDbContext context)
{
    public Task<List<UrunKategoriApiDto>> AktifleriListeleAsync()
    {
        return context.UrunKategoriler.AsNoTracking()
            .Where(x => !x.SilindiMi && x.AktifMi)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Ad)
            .Select(x => new UrunKategoriApiDto
            {
                Id = x.Id,
                Ad = x.Ad,
                IconUrl = x.IconUrl,
                SiraNo = x.SiraNo,
                AktifMi = x.AktifMi
            })
            .ToListAsync();
    }
}
