using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminDashboardService
    {
        private readonly AppDbContext _context;

        public AdminDashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardApiDto> GetirAsync(int? sirketId)
        {
            var devreyeQuery = DevreyeAlmaTemelQuery(sirketId);
            var yetkiBelgesiQuery = YetkiBelgesiTemelQuery(sirketId);
            var firmaQuery = FirmaTemelQuery(sirketId);
            var now = DateTime.Now;
            var ayBasi = new DateTime(now.Year, now.Month, 1);
            var sonrakiAy = ayBasi.AddMonths(1);

            var bildirimler = await BildirimOzetiAsync(sirketId);
            return new AdminDashboardApiDto
            {
                ToplamDevreyeAlma = await devreyeQuery.CountAsync(),
                ToplamFirma = await firmaQuery.CountAsync(),
                OnayBekleyen = bildirimler.OnayBekleyen,
                SuresiBitecek = bildirimler.SuresiBitecek,
                ToplamSirket = sirketId.HasValue
                    ? 1
                    : await _context.Dag_Sirketler.Where(x => !x.SilindiMi && x.AktifMi).CountAsync(),
                BuAyDevreyeAlma = await devreyeQuery
                    .Where(x => x.DevreyeAlmaTarihi >= ayBasi && x.DevreyeAlmaTarihi < sonrakiAy)
                    .CountAsync(),
                SonYetkiBelgeleri = await yetkiBelgesiQuery
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(8)
                    .Select(x => new AdminYetkiBelgesiOzetDto
                    {
                        Id = x.Id,
                        FirmaId = x.FirmaId,
                        FirmaAdi = x.Firma!.FirmaAdi,
                        SirketAdi = x.Firma.Sirket != null ? x.Firma.Sirket.SirketAdi : null,
                        Durum = x.Durum,
                        OlusturmaTarihi = x.OlusturmaTarihi,
                        YetkiBelgesiBitisTarihi = x.YetkiBelgesiBitisTarihi
                    })
                    .ToListAsync(),
                SonDevreyeAlmalar = await devreyeQuery
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(6)
                    .Select(x => new AdminDevreyeAlmaOzetDto
                    {
                        Id = x.Id,
                        FirmaId = x.FirmaId,
                        FirmaAdi = x.Firma!.FirmaAdi,
                        MarkaAdi = x.Marka != null ? x.Marka.MarkaAdi : null,
                        MusteriAdi = x.MusteriAdi,
                        TesistatNo = x.TesistatNo,
                        DevreyeAlmaTarihi = x.DevreyeAlmaTarihi,
                        Durum = x.Durum,
                        OlusturmaTarihi = x.OlusturmaTarihi
                    })
                    .ToListAsync()
            };
        }

        public async Task<PanelBildirimOzeti> BildirimOzetiAsync(int? sirketId)
        {
            return new PanelBildirimOzeti
            {
                OnayBekleyen = await OnayBekleyenSayisiAsync(sirketId),
                SuresiBitecek = await SuresiBitecekSayisiAsync(sirketId)
            };
        }

        public async Task<int> OnayBekleyenSayisiAsync(int? sirketId)
        {
            var bugun = DateTime.Today;
            return await YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                    && x.YetkiBelgesiBitisTarihi >= bugun)
                .CountAsync();
        }

        public async Task<int> SuresiBitecekSayisiAsync(int? sirketId)
        {
            var bugun = DateTime.Today;
            var bitisHaric = bugun.AddDays(31);
            return await YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi
                    && x.YetkiBelgesiBitisTarihi < bitisHaric
                    && x.YetkiBelgesiBitisTarihi >= bugun)
                .CountAsync();
        }

        private IQueryable<Ys_DevreyeAlma> DevreyeAlmaTemelQuery(int? sirketId)
        {
            return _context.Ys_DevreyeAlmalar
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private IQueryable<Ys_YetkiBelgesi> YetkiBelgesiTemelQuery(int? sirketId)
        {
            return _context.Ys_YetkiBelgeleri
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private IQueryable<Ys_Firma> FirmaTemelQuery(int? sirketId)
        {
            return _context.Ys_Firmalar
                .Where(x => !x.SilindiMi
                    && _context.Users.Any(u =>
                        u.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis &&
                        u.FirmaId == x.Id)
                    && (sirketId == null || x.SirketId == sirketId));
        }
    }

}
