using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class HomeOzetApiService(AppDbContext context)
{
    public async Task<HomeOzetCevap> GetirAsync(CancellationToken cancellationToken = default)
    {
        var servisCount = await context.Ys_Firmalar.CountAsync(x => !x.SilindiMi && x.AktifMi, cancellationToken);
        var devreyeCount = await context.Ys_DevreyeAlmalar.CountAsync(x => !x.SilindiMi
            && x.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi, cancellationToken);
        var yetkiBelgesiCount = await context.Ys_YetkiBelgeleri.CountAsync(x => !x.SilindiMi
            && x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi, cancellationToken);
        var toplamIslem = await context.Ys_DevreyeAlmalar.CountAsync(x => !x.SilindiMi, cancellationToken);
        return new HomeOzetCevap
        {
            ServisCount = servisCount,
            DevreyeCount = devreyeCount,
            YetkiBelgesiCount = yetkiBelgesiCount,
            TamamlanmaOrani = toplamIslem == 0 ? 0.0 : Math.Round(100.0 * devreyeCount / toplamIslem, 1)
        };
    }
}
