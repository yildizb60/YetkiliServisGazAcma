using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminYetkiliServisYonetimApiService
    {
        private readonly AppDbContext _context;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public AdminYetkiliServisYonetimApiService(
            AppDbContext context,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _context = context;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        public async Task<AdminIslemSonucDto> EkleAsync(
            AdminYetkiliServisKaydetDto? dto,
            AppKullanici kullanici,
            int? kapsamSirketId)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return AdminIslemSonucDto.Basarisiz("Firma adi zorunludur.");

            if (!string.IsNullOrWhiteSpace(dto.VergiNo))
            {
                var vknVar = await _context.Ys_Firmalar.AnyAsync(x =>
                    !x.SilindiMi &&
                    x.VergiNo == dto.VergiNo.Trim());

                if (vknVar)
                    return AdminIslemSonucDto.Basarisiz("Bu VKN ile kayitli bir yetkili servis zaten var.");
            }

            var kullaniciAdi = kullanici.UserName ?? "api";
            var hedefSirketId = kapsamSirketId
                ?? await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    dto.FaaliyetIli,
                    kullaniciAdi);

            var yeni = new Ys_Firma
            {
                FirmaAdi = dto.FirmaAdi.Trim(),
                YetkiliKisi = dto.YetkiliKisi,
                Telefon = dto.Telefon,
                Email = dto.Email,
                Adres = dto.Adres,
                FaaliyetIli = dto.FaaliyetIli,
                VergiNo = dto.VergiNo,
                VergiDairesi = dto.VergiDairesi,
                SirketId = hedefSirketId,
                AktifMi = dto.AktifMi,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullaniciAdi,
                SilindiMi = false
            };

            _context.Ys_Firmalar.Add(yeni);
            await _context.SaveChangesAsync();

            await YetkiliServisIliskileriniYenileAsync(
                yeni.Id,
                dto.KategoriIds,
                dto.MarkaIds,
                kullaniciAdi,
                kategoriSil: false,
                markaSil: false);

            await _context.SaveChangesAsync();
            return AdminIslemSonucDto.BasariliSonuc("Yetkili servis eklendi.");
        }

        public async Task<AdminIslemSonucDto> GuncelleAsync(
            AdminYetkiliServisKaydetDto? dto,
            AppKullanici kullanici,
            int? kapsamSirketId)
        {
            if (dto == null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return AdminIslemSonucDto.Basarisiz("Yetkili servis ve firma adi zorunludur.");

            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi
                    && (kapsamSirketId == null || x.SirketId == kapsamSirketId.Value));

            if (servis == null)
                return AdminIslemSonucDto.Basarisiz("Yetkili servis bulunamadi.");

            if (!string.IsNullOrWhiteSpace(dto.VergiNo))
            {
                var vknVar = await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id != servis.Id &&
                    !x.SilindiMi &&
                    x.VergiNo == dto.VergiNo.Trim());

                if (vknVar)
                    return AdminIslemSonucDto.Basarisiz("Bu VKN ile kayitli baska bir yetkili servis var.");
            }

            var kullaniciAdi = kullanici.UserName ?? "api";
            var hedefSirketId = kapsamSirketId
                ?? await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    dto.FaaliyetIli,
                    kullaniciAdi);

            servis.FirmaAdi = dto.FirmaAdi.Trim();
            servis.YetkiliKisi = dto.YetkiliKisi;
            servis.Telefon = dto.Telefon;
            servis.Email = dto.Email;
            servis.Adres = dto.Adres;
            servis.FaaliyetIli = dto.FaaliyetIli;
            servis.VergiNo = dto.VergiNo;
            servis.VergiDairesi = dto.VergiDairesi;
            servis.SirketId = hedefSirketId;
            servis.AktifMi = dto.AktifMi;
            servis.GuncellemeTarihi = DateTime.Now;
            servis.GuncelleyenKullanici = kullaniciAdi;

            await YetkiliServisIliskileriniYenileAsync(
                servis.Id,
                dto.KategoriIds,
                dto.MarkaIds,
                kullaniciAdi,
                kategoriSil: dto.KategoriIds != null,
                markaSil: dto.MarkaIds != null);

            await _context.SaveChangesAsync();
            return AdminIslemSonucDto.BasariliSonuc("Yetkili servis guncellendi.");
        }

        public async Task<AdminIslemSonucDto> SilAsync(
            AdminYetkiliServisDurumDto? dto,
            AppKullanici kullanici,
            int? kapsamSirketId)
        {
            if (dto == null || dto.Id <= 0)
                return AdminIslemSonucDto.Basarisiz("Yetkili servis id zorunludur.");

            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi
                    && (kapsamSirketId == null || x.SirketId == kapsamSirketId.Value));

            if (servis == null)
                return AdminIslemSonucDto.Basarisiz("Yetkili servis bulunamadi.");

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == servis.Id);

            if (devreyeAlmaVar)
                return AdminIslemSonucDto.Basarisiz("Bu yetkili servis uzerinde devreye alma islemi oldugu icin silinemez.");

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = kullanici.UserName ?? "api";

            await _context.SaveChangesAsync();
            return AdminIslemSonucDto.BasariliSonuc("Yetkili servis silindi.");
        }

        private async Task YetkiliServisIliskileriniYenileAsync(
            int firmaId,
            List<int>? kategoriIds,
            List<int>? markaIds,
            string kullanici,
            bool kategoriSil,
            bool markaSil)
        {
            if (kategoriIds != null || kategoriSil)
            {
                var mevcutKategoriler = await _context.Ys_FirmaKategoriler
                    .Where(x => x.FirmaId == firmaId)
                    .ToListAsync();

                _context.Ys_FirmaKategoriler.RemoveRange(mevcutKategoriler);

                var gecerliKategoriIds = await _context.UrunKategoriler
                    .Where(x => !x.SilindiMi && kategoriIds != null && kategoriIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync();

                foreach (var kategoriId in gecerliKategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = firmaId,
                        KategoriId = kategoriId,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici,
                        SilindiMi = false
                    });
                }
            }

            if (markaIds != null || markaSil)
            {
                var mevcutMarkalar = await _context.Ys_FirmaMarkalar
                    .Where(x => x.FirmaId == firmaId)
                    .ToListAsync();

                _context.Ys_FirmaMarkalar.RemoveRange(mevcutMarkalar);

                var gecerliMarkaIds = await _context.Ys_Markalar
                    .Where(x => !x.SilindiMi && markaIds != null && markaIds.Contains(x.Id))
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
                        OlusturanKullanici = kullanici,
                        SilindiMi = false
                    });
                }
            }
        }
    }
}
