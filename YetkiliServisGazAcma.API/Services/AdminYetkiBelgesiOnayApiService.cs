using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminYetkiBelgesiOnayApiService
    {
        private readonly AppDbContext _context;

        public AdminYetkiBelgesiOnayApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminYetkiBelgesiOnayListeDto> ListeleAsync(int? sirketId)
        {
            var query = YetkiBelgesiTemelQuery(sirketId);

            var bekleyenler = await query
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            var onaylananlar = await query
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi)
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            var reddedilenler = await query
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Reddedildi)
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            return new AdminYetkiBelgesiOnayListeDto
            {
                Bekleyenler = bekleyenler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList(),
                Onaylananlar = onaylananlar.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList(),
                Reddedilenler = reddedilenler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList()
            };
        }

        public async Task<AdminYetkiBelgesiOnayGecmisiListeDto> GecmisAsync(AdminYetkiBelgesiOnayGecmisiFiltreDto? dto, int? sirketId)
        {
            var query = YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum != YetkiBelgesiDurumDegerleri.OnaydaBekliyor);

            if (dto?.BaslangicTarihi.HasValue == true)
            {
                var baslangic = dto.BaslangicTarihi.Value.Date;
                query = query.Where(x => x.OnayTarihi.HasValue && x.OnayTarihi.Value >= baslangic);
            }

            if (dto?.BitisTarihi.HasValue == true)
            {
                var bitis = dto.BitisTarihi.Value.Date.AddDays(1);
                query = query.Where(x => x.OnayTarihi.HasValue && x.OnayTarihi.Value < bitis);
            }

            if (dto?.Durum.HasValue == true && (dto.Durum.Value == 1 || dto.Durum.Value == 2))
                query = query.Where(x => x.Durum == dto.Durum.Value);

            if (!string.IsNullOrWhiteSpace(dto?.Q))
            {
                var q = dto.Q.Trim();
                query = query.Where(x =>
                    (x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(q)) ||
                    (x.Firma != null && x.Firma.Sirket != null && x.Firma.Sirket.SirketAdi != null && x.Firma.Sirket.SirketAdi.Contains(q)) ||
                    (x.OnaylayanKullanici != null && x.OnaylayanKullanici.Contains(q)));
            }

            var islemler = await query
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .ToListAsync();

            return new AdminYetkiBelgesiOnayGecmisiListeDto
            {
                Islemler = islemler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList()
            };
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
    }
}
