using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminSubeApiService
    {
        private readonly AppDbContext _context;

        public AdminSubeApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminSubeListeDto> ListeleAsync(AdminSubeListeFiltreDto? dto, int? sirketId)
        {
            var firmalar = await SubeFirmaQuery(sirketId)
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();

            var query = SubeTemelQuery(sirketId);

            if (dto?.FirmaId > 0)
                query = query.Where(x => x.FirmaId == dto.FirmaId);

            if (!string.IsNullOrWhiteSpace(dto?.Q))
            {
                var q = dto.Q.Trim().ToLower();
                if (q.Length == 1)
                {
                    var likeStart = $"{q}%";
                    query = query.Where(x =>
                        !string.IsNullOrWhiteSpace(x.SubeAdi) &&
                        EF.Functions.Like(x.SubeAdi.ToLower(), likeStart));
                }
                else
                {
                    var likeAny = $"%{q}%";
                    query = query.Where(x =>
                        (!string.IsNullOrWhiteSpace(x.SubeAdi) && EF.Functions.Like(x.SubeAdi.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Il) && EF.Functions.Like(x.Il.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Ilce) && EF.Functions.Like(x.Ilce.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Telefon) && EF.Functions.Like(x.Telefon.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Adres) && EF.Functions.Like(x.Adres.ToLower(), likeAny)) ||
                        (x.Firma != null && !string.IsNullOrWhiteSpace(x.Firma.FirmaAdi) && EF.Functions.Like(x.Firma.FirmaAdi.ToLower(), likeAny)));
                }
            }

            var subeler = await query
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            return new AdminSubeListeDto
            {
                Subeler = subeler.Select(AdminSubeDto.FromEntity).ToList(),
                Firmalar = firmalar.Select(AdminSubeFirmaDto.FromEntity).ToList()
            };
        }

        public async Task<AdminIslemSonucDto> GetirAsync(AdminSubeGetirFiltreDto? dto, int? sirketId)
        {
            if (dto == null || dto.Id <= 0)
                return AdminIslemSonucDto.Basarisiz("Sube id zorunludur.");

            var sube = await SubeTemelQuery(sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);

            if (sube == null)
                return AdminIslemSonucDto.Basarisiz("Sube bulunamadi.");

            var firmalar = await SubeFirmaQuery(sirketId)
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();

            return new AdminSubeDetayDto
            {
                Basarili = true,
                Sube = AdminSubeDto.FromEntity(sube),
                Firmalar = firmalar.Select(AdminSubeFirmaDto.FromEntity).ToList()
            };
        }

        public async Task<AdminIslemSonucDto> EkleAsync(AdminSubeKaydetDto? dto, int? sirketId, string kullaniciAdi)
        {
            if (dto == null || dto.FirmaId <= 0 || string.IsNullOrWhiteSpace(dto.SubeAdi))
                return AdminIslemSonucDto.Basarisiz("Firma ve sube adi zorunludur.");

            var gecerliFirma = await SubeFirmaQuery(sirketId).AnyAsync(x => x.Id == dto.FirmaId);
            if (!gecerliFirma)
                return AdminIslemSonucDto.Basarisiz("Secilen firma aktif yetkili servis kullanicisina sahip degil.");

            var yeni = new Ys_Sube
            {
                FirmaId = dto.FirmaId,
                SubeAdi = dto.SubeAdi.Trim(),
                Il = dto.Il,
                Ilce = dto.Ilce,
                Telefon = dto.Telefon,
                Adres = dto.Adres,
                AktifMi = dto.AktifMi,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullaniciAdi,
                SilindiMi = false
            };

            _context.Ys_Subeler.Add(yeni);
            await _context.SaveChangesAsync();

            return AdminIslemSonucDto.BasariliSonuc("Sube kaydi eklendi.");
        }

        public async Task<AdminIslemSonucDto> GuncelleAsync(AdminSubeKaydetDto? dto, int? sirketId, string kullaniciAdi)
        {
            if (dto == null || dto.Id <= 0 || dto.FirmaId <= 0 || string.IsNullOrWhiteSpace(dto.SubeAdi))
                return AdminIslemSonucDto.Basarisiz("Firma ve sube adi zorunludur.");

            var sube = await SubeTemelQuery(sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (sube == null)
                return AdminIslemSonucDto.Basarisiz("Sube bulunamadi.");

            var hedefFirmaGecerli = await SubeFirmaQuery(sirketId).AnyAsync(x => x.Id == dto.FirmaId);
            if (!hedefFirmaGecerli)
                return AdminIslemSonucDto.Basarisiz("Secilen firma aktif yetkili servis kullanicisina sahip degil.");

            sube.FirmaId = dto.FirmaId;
            sube.SubeAdi = dto.SubeAdi.Trim();
            sube.Il = dto.Il;
            sube.Ilce = dto.Ilce;
            sube.Telefon = dto.Telefon;
            sube.Adres = dto.Adres;
            sube.AktifMi = dto.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullaniciAdi;

            await _context.SaveChangesAsync();

            return AdminIslemSonucDto.BasariliSonuc("Sube guncellendi.");
        }

        public async Task<AdminIslemSonucDto> DurumDegistirAsync(AdminSubeDurumDto? dto, int? sirketId, string kullaniciAdi)
        {
            if (dto == null || dto.Id <= 0)
                return AdminIslemSonucDto.Basarisiz("Sube id zorunludur.");

            var sube = await SubeTemelQuery(sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (sube == null)
                return AdminIslemSonucDto.Basarisiz("Sube bulunamadi.");

            sube.AktifMi = !sube.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullaniciAdi;
            await _context.SaveChangesAsync();

            return AdminIslemSonucDto.BasariliSonuc("Sube durumu guncellendi.");
        }

        public async Task<AdminIslemSonucDto> SilAsync(AdminSubeDurumDto? dto, int? sirketId, string kullaniciAdi)
        {
            if (dto == null || dto.Id <= 0)
                return AdminIslemSonucDto.Basarisiz("Sube id zorunludur.");

            var sube = await SubeTemelQuery(sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (sube == null)
                return AdminIslemSonucDto.Basarisiz("Sube bulunamadi.");

            sube.SilindiMi = true;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullaniciAdi;
            await _context.SaveChangesAsync();

            return AdminIslemSonucDto.BasariliSonuc("Sube kaydi silindi.");
        }

        private IQueryable<int> AktifYetkiliServisFirmaIdsQuery()
        {
            return _context.Users
                .Where(u => u.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis && u.AktifMi && u.FirmaId.HasValue)
                .Select(u => u.FirmaId!.Value)
                .Distinct();
        }

        private IQueryable<Ys_Firma> SubeFirmaQuery(int? sirketId)
        {
            var aktifFirmaIds = AktifYetkiliServisFirmaIdsQuery();
            var query = _context.Ys_Firmalar
                .Where(x => !x.SilindiMi && x.AktifMi && aktifFirmaIds.Contains(x.Id));

            if (sirketId.HasValue)
                query = query.Where(x => x.SirketId == sirketId.Value);

            return query;
        }

        private IQueryable<Ys_Sube> SubeTemelQuery(int? sirketId)
        {
            var aktifFirmaIds = AktifYetkiliServisFirmaIdsQuery();
            var query = _context.Ys_Subeler
                .Include(x => x.Firma)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.Firma.AktifMi
                    && aktifFirmaIds.Contains(x.FirmaId));

            if (sirketId.HasValue)
                query = query.Where(x => x.Firma != null && x.Firma.SirketId == sirketId.Value);

            return query;
        }
    }
}
