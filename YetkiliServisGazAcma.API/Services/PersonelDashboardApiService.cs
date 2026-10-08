using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class PersonelDashboardApiService(
    AppDbContext context,
    AdminDashboardService dashboard,
    YkcTalepOkumaService talepler,
    YkcYetkiService ykcYetki)
{
    // Sirket kapsami controller tarafinda dogrulandiktan sonra cagrilir.
    public async Task<PersonelDashboardDto> GetirAsync(AppKullanici kullanici, int? sirketId, bool yonetici)
    {
        var bugun = DateTime.Today;
        var ozet = new PersonelDashboardDto
        {
            Bugun = bugun,
            AyBaslangici = new DateTime(bugun.Year, bugun.Month, 1),
        };
        ozet.AyBitisi = ozet.AyBaslangici.AddMonths(1).AddDays(-1);
        if (!kullanici.AktifMi || kullanici.ArsivlemeTarihi != null || (!yonetici && !sirketId.HasValue))
            return ozet;

        var yetkiler = await context.Dag_PersonelYetkiler.AsNoTracking()
            .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi && x.SirketId == sirketId)
            .Select(x => x.YetkiTipi).Distinct().ToListAsync();
        bool Yetkili(string yetki) => yonetici || yetkiler.Contains(YetkiTipleri.TAM_YETKI) || yetkiler.Contains(yetki);
        ozet.BelgeYetkisi = Yetkili(YetkiTipleri.YETKI_BELGESI_ONAY);
        ozet.RaporYetkisi = Yetkili(YetkiTipleri.RAPOR_GOR);
        ozet.ServisYetkisi = Yetkili(YetkiTipleri.KULLANICI_YONET);
        ozet.MarkaYetkisi = Yetkili(YetkiTipleri.MARKA_YONET);
        ozet.YkcYetkileri = await ykcYetki.OzetAsync(kullanici, sirketId);

        if (ozet.BelgeYetkisi)
        {
            ozet.OnayBekleyen = await dashboard.OnayBekleyenSayisiAsync(sirketId);
            ozet.SuresiBitecek = await dashboard.SuresiBitecekSayisiAsync(sirketId);
        }
        if (ozet.RaporYetkisi)
        {
            var kayitlar = context.Ys_DevreyeAlmalar.AsNoTracking().Where(x => !x.SilindiMi
                && x.Firma != null && !x.Firma.SilindiMi && (sirketId == null || x.Firma.SirketId == sirketId));
            var sonrakiAy = ozet.AyBaslangici.AddMonths(1);
            ozet.ToplamDevreyeAlma = await kayitlar.CountAsync();
            ozet.BuAyDevreyeAlma = await kayitlar.CountAsync(x =>
                x.DevreyeAlmaTarihi >= ozet.AyBaslangici && x.DevreyeAlmaTarihi < sonrakiAy);
        }
        if (ozet.ServisYetkisi)
        {
            ozet.AktifServis = await context.Ys_Firmalar.AsNoTracking().CountAsync(x =>
                !x.SilindiMi && x.AktifMi && (sirketId == null || x.SirketId == sirketId)
                && context.Users.Any(u => u.FirmaId == x.Id && u.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis));
        }
        if (ozet.YkcYetkileri.TalepleriGorebilir)
            ozet.Ykc = await talepler.DashboardOzetAsync(kullanici, yonetici, sirketId);

        return ozet;
    }
}
