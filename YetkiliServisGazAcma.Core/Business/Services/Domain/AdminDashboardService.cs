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

        public async Task<AdminDashboardOzet> GetirAsync(int? sirketId)
        {
            var devreyeQuery = DevreyeAlmaTemelQuery(sirketId);
            var yetkiBelgesiQuery = YetkiBelgesiTemelQuery(sirketId);
            var firmaQuery = FirmaTemelQuery(sirketId);
            var now = DateTime.Now;

            return new AdminDashboardOzet
            {
                ToplamDevreyeAlma = await devreyeQuery.CountAsync(),
                ToplamFirma = await firmaQuery.CountAsync(),
                OnayBekleyen = await yetkiBelgesiQuery.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor).CountAsync(),
                SuresiBitecek = await yetkiBelgesiQuery
                    .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi
                        && x.YetkiBelgesiBitisTarihi <= now.AddDays(30)
                        && x.YetkiBelgesiBitisTarihi >= now)
                    .CountAsync(),
                ToplamSirket = sirketId.HasValue
                    ? 1
                    : await _context.Dag_Sirketler.Where(x => !x.SilindiMi && x.AktifMi).CountAsync(),
                BuAyDevreyeAlma = await devreyeQuery
                    .Where(x => x.OlusturmaTarihi.Month == now.Month
                        && x.OlusturmaTarihi.Year == now.Year)
                    .CountAsync(),
                SonYetkiBelgeleri = await yetkiBelgesiQuery
                    .Include(x => x.Firma)
                        .ThenInclude(x => x!.Sirket)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(8)
                    .ToListAsync(),
                SonDevreyeAlmalar = await devreyeQuery
                    .Include(x => x.Firma)
                    .Include(x => x.Marka)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(6)
                    .ToListAsync()
            };
        }

        public async Task<int> OnayBekleyenSayisiAsync(int? sirketId)
        {
            return await YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor)
                .CountAsync();
        }

        public async Task<int> SuresiBitecekSayisiAsync(int? sirketId)
        {
            var now = DateTime.Now;
            return await YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi
                    && x.YetkiBelgesiBitisTarihi <= now.AddDays(30)
                    && x.YetkiBelgesiBitisTarihi >= now)
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
                    && (sirketId == null || x.SirketId == sirketId));
        }
    }

    public class AdminDashboardOzet
    {
        public int ToplamDevreyeAlma { get; set; }
        public int ToplamFirma { get; set; }
        public int OnayBekleyen { get; set; }
        public int SuresiBitecek { get; set; }
        public int ToplamSirket { get; set; }
        public int BuAyDevreyeAlma { get; set; }
        public List<Ys_YetkiBelgesi> SonYetkiBelgeleri { get; set; } = new();
        public List<Ys_DevreyeAlma> SonDevreyeAlmalar { get; set; } = new();
    }
}
