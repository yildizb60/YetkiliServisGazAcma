using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class YetkiBelgesiSilmeApiService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<string?> SilAsync(Ys_YetkiBelgesi yetkiBelgesi, string? kullaniciAdi)
    {
        if (!YetkiBelgesiService.SilinebilirMi(yetkiBelgesi))
            return "Onaylanan yetki belgesi silinemez.";

        var simdi = DateTime.Now;
        var silen = kullaniciAdi ?? "sistem";
        var silinen = await _context.Ys_YetkiBelgeleri
            .Where(x => x.Id == yetkiBelgesi.Id && !x.SilindiMi
                && x.Durum != YetkiBelgesiDurumDegerleri.Onaylandi)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.SilindiMi, true)
                .SetProperty(x => x.SilinmeTarihi, simdi)
                .SetProperty(x => x.SilenKullanici, silen));

        if (silinen != 1)
            return "Yetki belgesi artık silinemez. Listeyi yenileyin.";

        return null;
    }
}
