using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminRaporApiService
    {
        private readonly AppDbContext _context;

        public AdminRaporApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDevreyeAlmaListeDto> DevreyeAlmalarAsync(AdminDevreyeAlmaListeFiltreDto? dto, int? sirketId)
        {
            var query = DevreyeAlmaTemelQuery(sirketId);

            if (!string.IsNullOrWhiteSpace(dto?.TesisatNo))
                query = query.Where(x => x.TesistatNo != null && x.TesistatNo.Contains(dto.TesisatNo));
            if (!string.IsNullOrWhiteSpace(dto?.Marka))
                query = query.Where(x =>
                    (x.CihazMarka != null && x.CihazMarka.Contains(dto.Marka)) ||
                    (x.Marka != null && x.Marka.MarkaAdi != null && x.Marka.MarkaAdi.Contains(dto.Marka)));
            if (!string.IsNullOrWhiteSpace(dto?.Servis))
                query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(dto.Servis));
            if (!string.IsNullOrWhiteSpace(dto?.Il))
                query = query.Where(x => x.Firma != null && x.Firma.FaaliyetIli != null && x.Firma.FaaliyetIli.Contains(dto.Il));
            if (!string.IsNullOrWhiteSpace(dto?.Ilce))
                query = query.Where(x => _context.Ys_Subeler.Any(s => !s.SilindiMi && s.FirmaId == x.FirmaId && s.Ilce != null && s.Ilce.Contains(dto.Ilce)));
            if (dto?.Durum.HasValue == true)
                query = query.Where(x => x.Durum == dto.Durum.Value);
            if (dto?.BaslangicTarihi.HasValue == true)
                query = query.Where(x => x.OlusturmaTarihi >= dto.BaslangicTarihi.Value.Date);
            if (dto?.BitisTarihi.HasValue == true)
                query = query.Where(x => x.OlusturmaTarihi < dto.BitisTarihi.Value.Date.AddDays(1));

            var islemler = await query.OrderByDescending(x => x.OlusturmaTarihi).ToListAsync();
            var firmaIds = islemler.Select(x => x.FirmaId).Distinct().ToList();
            var subeler = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi && firmaIds.Contains(x.FirmaId))
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.MarkaAdi)
                .Select(x => new AdminMarkaSecenekDto
                {
                    Id = x.Id,
                    MarkaAdi = x.MarkaAdi
                })
                .ToListAsync();

            return new AdminDevreyeAlmaListeDto
            {
                Islemler = islemler.Select(AdminDevreyeAlmaDto.FromEntity).ToList(),
                Markalar = markalar,
                FirmaIlceleri = subeler
                    .GroupBy(x => x.FirmaId)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(s => s.Ilce).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "-")
            };
        }

        public async Task<AdminDevreyeAlmaDto?> DevreyeAlmaGetirAsync(int id, int? sirketId)
        {
            var kayit = await DevreyeAlmaTemelQuery(sirketId)
                .FirstOrDefaultAsync(x => x.Id == id);

            return kayit == null ? null : AdminDevreyeAlmaDto.FromEntity(kayit);
        }

        public async Task<AdminYetkiBelgesiUyariListeDto> YetkiBelgesiUyarilariAsync(int? sirketId)
        {
            var bugun = DateTime.Now.Date;
            var bitisSinir = bugun.AddDays(30);
            var query = YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum == 1);

            var yaklasan = await query
                .Where(x => x.YetkiBelgesiBitisTarihi >= bugun && x.YetkiBelgesiBitisTarihi <= bitisSinir)
                .OrderBy(x => x.YetkiBelgesiBitisTarihi)
                .ToListAsync();

            var gecmis = await query
                .Where(x => x.YetkiBelgesiBitisTarihi < bugun)
                .OrderByDescending(x => x.YetkiBelgesiBitisTarihi)
                .ToListAsync();

            return new AdminYetkiBelgesiUyariListeDto
            {
                Yaklasan = yaklasan.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList(),
                Gecmis = gecmis.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList()
            };
        }

        public async Task<AdminRaporOzetDto> RaporlarOzetAsync(AdminRaporOzetFiltreDto? dto, int? sirketId)
        {
            var basTarih = dto?.BaslangicTarihi?.Date ?? DateTime.Now.Date.AddDays(-30);
            var bitTarih = dto?.BitisTarihi?.Date ?? DateTime.Now.Date;
            var bitSonrasi = bitTarih.AddDays(1);
            var raporTipi = string.IsNullOrWhiteSpace(dto?.Tip) ? "devreye" : dto.Tip.Trim().ToLowerInvariant();

            var devreyeTemelQuery = DevreyeAlmaTemelQuery(sirketId)
                .Where(x => x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi);

            var yetkiBelgesiTemelQuery = YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi);

            var devreyeSayisi = await devreyeTemelQuery.CountAsync();
            var devreyeTamamlanan = await devreyeTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var devreyeBekleyen = await devreyeTemelQuery.Where(x => x.Durum == 0).CountAsync();
            var devreyeIptal = await devreyeTemelQuery.Where(x => x.Durum == 2).CountAsync();
            var yetkiBelgesiOnayli = await yetkiBelgesiTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var yetkiBelgesiBekleyen = await yetkiBelgesiTemelQuery.Where(x => x.Durum == 0).CountAsync();
            var yetkiBelgesiReddedilen = await yetkiBelgesiTemelQuery.Where(x => x.Durum == 2).CountAsync();

            var aylikBaslangic = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);
            var aylikEtiketler = Enumerable.Range(0, 6)
                .Select(i => aylikBaslangic.AddMonths(i))
                .ToList();

            var aylikHam = await devreyeTemelQuery
                .Where(x => x.OlusturmaTarihi >= aylikBaslangic)
                .GroupBy(x => new { x.OlusturmaTarihi.Year, x.OlusturmaTarihi.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var aylikMap = aylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
            var chartAylikLabels = aylikEtiketler.Select(x => x.ToString("MM.yyyy")).ToList();
            var chartAylikData = aylikEtiketler
                .Select(x => aylikMap.TryGetValue($"{x.Year:D4}-{x.Month:D2}", out var value) ? value : 0)
                .ToList();

            var chartSirket = await devreyeTemelQuery
                .Where(x => x.Firma != null && x.Firma.Sirket != null)
                .GroupBy(x => x.Firma!.Sirket!.SirketAdi)
                .Select(g => new { Sirket = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(6)
                .ToListAsync();

            var chartMarka = await devreyeTemelQuery
                .Where(x => x.Marka != null)
                .GroupBy(x => x.Marka!.MarkaAdi)
                .Select(g => new { Marka = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(6)
                .ToListAsync();

            var sonuc = new AdminRaporOzetDto
            {
                BasTarih = basTarih,
                BitTarih = bitTarih,
                RaporTipi = raporTipi,
                DevreyeSayisi = devreyeSayisi,
                DevreyeTamamlanan = devreyeTamamlanan,
                DevreyeBekleyen = devreyeBekleyen,
                DevreyeIptal = devreyeIptal,
                YetkiBelgesiOnayli = yetkiBelgesiOnayli,
                YetkiBelgesiBekleyen = yetkiBelgesiBekleyen,
                YetkiBelgesiReddedilen = yetkiBelgesiReddedilen,
                ChartAylikLabels = chartAylikLabels,
                ChartAylikData = chartAylikData,
                ChartDurumData = new List<int> { yetkiBelgesiOnayli, yetkiBelgesiBekleyen, yetkiBelgesiReddedilen },
                ChartSirketLabels = chartSirket.Select(x => x.Sirket).ToList(),
                ChartSirketData = chartSirket.Select(x => x.Sayi).ToList(),
                ChartMarkaLabels = chartMarka.Select(x => x.Marka).ToList(),
                ChartMarkaData = chartMarka.Select(x => x.Sayi).ToList(),
                Sirketler = await SirketSecenekleriAsync(sirketId)
            };

            if (raporTipi == "onayli" || raporTipi == "bekleyen" || raporTipi == "reddedilen")
            {
                var durum = raporTipi == "onayli" ? 1 : (raporTipi == "bekleyen" ? 0 : 2);
                var yetkiBelgesiIslemler = await yetkiBelgesiTemelQuery
                    .Where(x => x.Durum == durum)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                sonuc.ListeTipi = "yetkiBelgesi";
                sonuc.YetkiBelgesiIslemler = yetkiBelgesiIslemler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList();
            }
            else
            {
                var sonIslemler = await devreyeTemelQuery
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                sonuc.ListeTipi = "devreye";
                sonuc.SonIslemler = sonIslemler.Select(AdminDevreyeAlmaDto.FromEntity).ToList();
            }

            return sonuc;
        }

        private IQueryable<Ys_DevreyeAlma> DevreyeAlmaTemelQuery(int? sirketId)
        {
            return _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private IQueryable<Ys_YetkiBelgesi> YetkiBelgesiTemelQuery(int? sirketId)
        {
            return _context.Ys_YetkiBelgeleri
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private async Task<List<AdminSirketSecenekDto>> SirketSecenekleriAsync(int? sirketId)
        {
            var query = _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (sirketId.HasValue)
                query = query.Where(x => x.Id == sirketId.Value);

            return await query
                .OrderBy(x => x.SirketAdi)
                .Select(x => new AdminSirketSecenekDto
                {
                    Id = x.Id,
                    SirketAdi = x.SirketAdi
                })
                .ToListAsync();
        }
    }
}
