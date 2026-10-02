using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class SqlYkcSorguKaydiService(AppDbContext context) : IYkcSorguKaydiService
{
    private bool _expiredRecordsCleaned;

    private sealed record KaynakKaydi(
        YkcTalepKaydetDto Cihaz,
        Dictionary<string, string?> IzinliYeniCihazTipleri);

    public async Task<string> EkleAsync(string kullaniciId, YkcTalepKaydetDto kaynak)
    {
        if (!_expiredRecordsCleaned)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE TOP (200) FROM dbo.Ykc_SorguKayitlari WHERE GecerlilikTarihi <= SYSUTCDATETIME()");
            _expiredRecordsCleaned = true;
        }
        var referans = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var kayit = new Ykc_SorguKaydi
        {
            Referans = referans,
            KullaniciId = kullaniciId,
            KaynakJson = JsonSerializer.Serialize(new KaynakKaydi(kaynak, kaynak.IzinliYeniCihazTipleri)),
            GecerlilikTarihi = DateTime.UtcNow.Add(YkcSorguKaydiAyarlari.GecerlilikSuresi)
        };

        context.Ykc_SorguKayitlari.Add(kayit);
        await context.SaveChangesAsync();
        return referans;
    }

    public async Task<bool> UygulaAsync(string kullaniciId, YkcTalepKaydetDto hedef)
    {
        if (string.IsNullOrWhiteSpace(hedef.SorguReferansi)) return false;

        var kayit = await context.Ykc_SorguKayitlari.AsNoTracking()
            .Where(x => x.Referans == hedef.SorguReferansi
                && x.KullaniciId == kullaniciId
                && x.GecerlilikTarihi > DateTime.UtcNow)
            .Select(x => x.KaynakJson)
            .FirstOrDefaultAsync();
        if (kayit is null) return false;

        var kaynak = JsonSerializer.Deserialize<KaynakKaydi>(kayit);
        if (kaynak?.Cihaz is null || kaynak.IzinliYeniCihazTipleri is null) return false;
        kaynak.Cihaz.IzinliYeniCihazTipleri = new Dictionary<string, string?>(
            kaynak.IzinliYeniCihazTipleri, StringComparer.OrdinalIgnoreCase);
        return YkcSorguKaydiDogrulama.Uygula(kaynak.Cihaz, hedef);
    }
}
