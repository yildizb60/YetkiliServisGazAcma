using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Entities;
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

        public async Task<YsPanelIslemSonucDto> SubeKaydetAsync(YsPanelSubeKaydetDto? dto, AppKullanici kullanici)
        {
            if (dto == null)
                return YsPanelIslemSonucDto.Basarisiz("Sube bilgileri zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.SubeAdi))
                return YsPanelIslemSonucDto.Basarisiz("Sube adi zorunludur.");

            var firmaId = kullanici.FirmaId!.Value;
            var sube = dto.Id > 0
                ? await _context.Ys_Subeler.FirstOrDefaultAsync(x => x.Id == dto.Id && x.FirmaId == firmaId)
                : null;

            if (dto.Id > 0 && sube == null)
                return YsPanelIslemSonucDto.Basarisiz("Sube bulunamadi.");

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
            return YsPanelIslemSonucDto.BasariliSonuc(dto.Id > 0 ? "Sube guncellendi." : "Sube kaydi eklendi.");
        }

        public async Task<YsPanelIslemSonucDto> SubeDurumAsync(YsPanelIdDto? dto, AppKullanici kullanici)
        {
            if (dto == null || dto.Id <= 0)
                return YsPanelIslemSonucDto.Basarisiz("Sube id zorunludur.");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.FirmaId == kullanici.FirmaId!.Value);

            if (sube == null)
                return YsPanelIslemSonucDto.Basarisiz("Sube bulunamadi.");

            sube.AktifMi = !sube.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            return YsPanelIslemSonucDto.BasariliSonuc("Sube durumu guncellendi.");
        }

        public async Task<YsPanelIslemSonucDto> SubeSilAsync(YsPanelIdDto? dto, AppKullanici kullanici)
        {
            if (dto == null || dto.Id <= 0)
                return YsPanelIslemSonucDto.Basarisiz("Sube id zorunludur.");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.FirmaId == kullanici.FirmaId!.Value);

            if (sube == null)
                return YsPanelIslemSonucDto.Basarisiz("Sube bulunamadi.");

            sube.SilindiMi = true;
            sube.SilinmeTarihi = DateTime.Now;
            sube.SilenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            return YsPanelIslemSonucDto.BasariliSonuc("Sube kaydi silindi.");
        }

        public async Task<YsPanelIslemSonucDto> MarkaGuncelleAsync(YsPanelMarkaGuncelleDto? dto, AppKullanici kullanici)
        {
            var firmaId = kullanici.FirmaId!.Value;
            var mevcut = await _context.Ys_FirmaMarkalar
                .Where(x => x.FirmaId == firmaId)
                .ToListAsync();
            _context.Ys_FirmaMarkalar.RemoveRange(mevcut);

            if (dto?.MarkaIds?.Count > 0)
            {
                var gecerliMarkaIds = await _context.Ys_Markalar
                    .Where(x => !x.SilindiMi && x.AktifMi && dto.MarkaIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync();

                foreach (var markaId in gecerliMarkaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = firmaId,
                        MarkaId = markaId,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            return YsPanelIslemSonucDto.BasariliSonuc("Marka yetkileri guncellendi.");
        }

        public async Task<YsPanelIslemSonucDto> MarkaEkleAsync(YsPanelMarkaKaydetDto? dto, AppKullanici kullanici)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.MarkaAdi))
                return YsPanelIslemSonucDto.Basarisiz("Marka adi zorunludur.");

            var temizAdi = dto.MarkaAdi.Trim();
            var mevcutMarka = await _context.Ys_Markalar
                .FirstOrDefaultAsync(x => !x.SilindiMi && x.MarkaAdi != null && x.MarkaAdi.ToLower() == temizAdi.ToLower());

            if (mevcutMarka == null)
            {
                mevcutMarka = new Ys_Marka
                {
                    MarkaAdi = temizAdi,
                    Aciklama = dto.Aciklama,
                    AktifMi = true,
                    OlusturanKullanici = kullanici.UserName ?? "sistem"
                };
                _context.Ys_Markalar.Add(mevcutMarka);
                await _context.SaveChangesAsync();
            }

            var firmaId = kullanici.FirmaId!.Value;
            var bag = await _context.Ys_FirmaMarkalar
                .FirstOrDefaultAsync(x => x.FirmaId == firmaId && x.MarkaId == mevcutMarka.Id);
            if (bag == null)
            {
                _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                {
                    FirmaId = firmaId,
                    MarkaId = mevcutMarka.Id,
                    YetkiBitisTarihi = DateTime.Now.AddYears(5),
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = kullanici.UserName ?? "sistem",
                    SilindiMi = false
                });
            }
            else
            {
                bag.SilindiMi = false;
                bag.SilinmeTarihi = null;
                bag.SilenKullanici = null;
            }

            await _context.SaveChangesAsync();
            return YsPanelIslemSonucDto.BasariliSonuc("Marka eklendi.");
        }

        public async Task<YsPanelIslemSonucDto> MarkaDuzenleAsync(YsPanelMarkaKaydetDto? dto, AppKullanici kullanici)
        {
            if (dto == null || dto.Id <= 0)
                return YsPanelIslemSonucDto.Basarisiz("Marka id zorunludur.");

            var firmaId = kullanici.FirmaId!.Value;
            var bag = await _context.Ys_FirmaMarkalar
                .Include(x => x.Marka)
                .FirstOrDefaultAsync(x => x.FirmaId == firmaId && x.MarkaId == dto.Id && !x.SilindiMi);

            if (bag?.Marka == null)
                return YsPanelIslemSonucDto.Basarisiz("Marka bulunamadi.");

            if (!string.IsNullOrWhiteSpace(dto.MarkaAdi))
                bag.Marka.MarkaAdi = dto.MarkaAdi.Trim();

            bag.Marka.Aciklama = dto.Aciklama;
            bag.Marka.GuncellemeTarihi = DateTime.Now;
            bag.Marka.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            return YsPanelIslemSonucDto.BasariliSonuc("Marka guncellendi.");
        }

        public async Task<YsPanelIslemSonucDto> MarkaSilAsync(YsPanelIdDto? dto, AppKullanici kullanici)
        {
            if (dto == null || dto.Id <= 0)
                return YsPanelIslemSonucDto.Basarisiz("Marka id zorunludur.");

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

            return YsPanelIslemSonucDto.BasariliSonuc("Marka yetkisi kaldirildi.");
        }
    }
}
