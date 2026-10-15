using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class YetkiliServisPanelYonetimApiService
    {
        private readonly AppDbContext _context;

        public YetkiliServisPanelYonetimApiService(AppDbContext context)
        {
            _context = context;
        }

        public Task<AdminSubeDto?> SubeGetirAsync(int id, AppKullanici kullanici)
        {
            if (id <= 0 || !SubeKapsamiVar(kullanici)) return Task.FromResult<AdminSubeDto?>(null);
            return SubeSorgusu(kullanici.FirmaId!.Value).SingleOrDefaultAsync(x => x.Id == id);
        }

        public Task<List<AdminSubeDto>> SubelerAsync(AppKullanici kullanici)
        {
            if (!SubeKapsamiVar(kullanici)) return Task.FromResult(new List<AdminSubeDto>());
            return SubeSorgusu(kullanici.FirmaId!.Value)
                .OrderByDescending(x => x.AktifMi).ThenBy(x => x.SubeAdi).ToListAsync();
        }

        private static bool SubeKapsamiVar(AppKullanici kullanici)
            => kullanici.FirmaId.HasValue && kullanici.AktifMi && kullanici.ArsivlemeTarihi == null
                && kullanici.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis;

        private IQueryable<AdminSubeDto> SubeSorgusu(int firmaId)
            => _context.Ys_Subeler.AsNoTracking()
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi
                    && x.Firma != null && !x.Firma.SilindiMi && x.Firma.AktifMi)
                .Select(x => new AdminSubeDto
                {
                    Id = x.Id, FirmaId = x.FirmaId, SubeAdi = x.SubeAdi, Il = x.Il, Ilce = x.Ilce,
                    Telefon = x.Telefon, Adres = x.Adres, AktifMi = x.AktifMi,
                    FirmaAdi = x.Firma!.FirmaAdi, FirmaEmail = x.Firma.Email,
                    FirmaTelefon = x.Firma.Telefon, FirmaSirketId = x.Firma.SirketId
                });


        public async Task<ApiIslemSonuc> SubeKaydetAsync(YsPanelSubeKaydetDto? dto, AppKullanici kullanici)
        {
            if (dto == null)
                return ApiIslemSonuc.Basarisiz("Sube bilgileri zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.SubeAdi))
                return ApiIslemSonuc.Basarisiz("Sube adi zorunludur.");

            var firmaId = kullanici.FirmaId!.Value;
            var sube = dto.Id > 0
                ? await _context.Ys_Subeler.FirstOrDefaultAsync(x => x.Id == dto.Id && x.FirmaId == firmaId)
                : null;

            if (dto.Id > 0 && sube == null)
                return ApiIslemSonuc.Basarisiz("Sube bulunamadi.");

            if (sube == null)
            {
                sube = new Ys_Sube
                {
                    FirmaId = firmaId,
                    OlusturanKullanici = kullanici.UserName ?? "sistem"
                };
                _context.Ys_Subeler.Add(sube);
            }
            else
            {
                sube.GuncellemeTarihi = DateTime.Now;
                sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            }

            sube.SubeAdi = dto.SubeAdi;
            sube.Il = dto.Il;
            sube.Ilce = dto.Ilce;
            sube.Telefon = dto.Telefon;
            sube.Adres = dto.Adres;
            sube.AktifMi = dto.AktifMi;
            sube.SilindiMi = false;

            await _context.SaveChangesAsync();
            return ApiIslemSonuc.BasariliSonuc(dto.Id > 0 ? "Sube guncellendi." : "Sube kaydi eklendi.");
        }

        public async Task<ApiIslemSonuc> SubeDurumAsync(YsPanelIdDto? dto, AppKullanici kullanici)
        {
            if (dto == null || dto.Id <= 0)
                return ApiIslemSonuc.Basarisiz("Sube id zorunludur.");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.FirmaId == kullanici.FirmaId!.Value);

            if (sube == null)
                return ApiIslemSonuc.Basarisiz("Sube bulunamadi.");

            sube.AktifMi = !sube.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            return ApiIslemSonuc.BasariliSonuc("Sube durumu guncellendi.");
        }

        public async Task<ApiIslemSonuc> SubeSilAsync(YsPanelIdDto? dto, AppKullanici kullanici)
        {
            if (dto == null || dto.Id <= 0)
                return ApiIslemSonuc.Basarisiz("Sube id zorunludur.");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.FirmaId == kullanici.FirmaId!.Value);

            if (sube == null)
                return ApiIslemSonuc.Basarisiz("Sube bulunamadi.");

            sube.SilindiMi = true;
            sube.SilinmeTarihi = DateTime.Now;
            sube.SilenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            return ApiIslemSonuc.BasariliSonuc("Sube kaydi silindi.");
        }

        public async Task<ApiIslemSonuc> MarkaGuncelleAsync(YsPanelMarkaGuncelleDto? dto, AppKullanici kullanici)
        {
            if (dto?.MarkaIds == null)
                return ApiIslemSonuc.Basarisiz("Marka seçimi zorunludur.");
            var ids = dto.MarkaIds.Distinct().ToList();
            if (await _context.Ys_Markalar.CountAsync(x => ids.Contains(x.Id) && !x.SilindiMi && x.AktifMi) != ids.Count)
                return ApiIslemSonuc.Basarisiz("Geçersiz marka seçildi.");
            await FirmaYetkiIliskileri.GuncelleAsync(_context, kullanici.FirmaId!.Value,
                null, ids, kullanici.UserName ?? "sistem");
            await _context.SaveChangesAsync();
            return ApiIslemSonuc.BasariliSonuc("Marka yetkileri guncellendi.");
        }

        public Task<ApiIslemSonuc> MarkaEkleAsync(YsPanelMarkaKaydetDto? dto, AppKullanici kullanici)
            => Task.FromResult(ApiIslemSonuc.Basarisiz("Ortak marka kataloğunu değiştirme yetkiniz yok."));

        public Task<ApiIslemSonuc> MarkaDuzenleAsync(YsPanelMarkaKaydetDto? dto, AppKullanici kullanici)
            => Task.FromResult(ApiIslemSonuc.Basarisiz("Ortak marka kataloğunu değiştirme yetkiniz yok."));

        public async Task<ApiIslemSonuc> MarkaSilAsync(YsPanelIdDto? dto, AppKullanici kullanici)
        {
            if (dto == null || dto.Id <= 0)
                return ApiIslemSonuc.Basarisiz("Marka id zorunludur.");

            var firmaId = kullanici.FirmaId!.Value;
            var bag = await _context.Ys_FirmaMarkalar
                .FirstOrDefaultAsync(x => x.FirmaId == firmaId && x.MarkaId == dto.Id && !x.SilindiMi);

            if (bag != null)
            {
                bag.SilindiMi = true;
                bag.SilinmeTarihi = DateTime.Now;
                bag.SilenKullanici = kullanici.UserName ?? "sistem";
                await _context.SaveChangesAsync();
            }

            return ApiIslemSonuc.BasariliSonuc("Marka yetkisi kaldirildi.");
        }
    }
}
