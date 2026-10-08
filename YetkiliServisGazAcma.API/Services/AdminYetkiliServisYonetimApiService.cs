using Microsoft.EntityFrameworkCore;
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

        public async Task<AdminYetkiliServisEditorDto?> EditorAsync(int id, int? kapsamSirketId)
        {
            if (id < 0) return null;
            Ys_Firma? firma = null;
            if (id > 0)
            {
                firma = await _context.Ys_Firmalar.AsNoTracking().AsSplitQuery()
                    .Include(x => x.Sirket)
                    .Include(x => x.FirmaMarkalar!).ThenInclude(x => x.Marka)
                    .Include(x => x.FirmaKategoriler!).ThenInclude(x => x.Kategori)
                    .SingleOrDefaultAsync(x => x.Id == id && !x.SilindiMi
                        && (kapsamSirketId == null || x.SirketId == kapsamSirketId));
                if (firma == null) return null;
            }

            var markaIds = firma?.FirmaMarkalar?.Where(x => !x.SilindiMi && x.Marka is { SilindiMi: false })
                .Select(x => x.MarkaId).Distinct().ToList() ?? new List<int>();
            var kategoriIds = firma?.FirmaKategoriler?.Where(x => !x.SilindiMi
                    && x.Kategori is { SilindiMi: false, AktifMi: true })
                .Select(x => x.KategoriId).Distinct().ToList() ?? new List<int>();
            return new AdminYetkiliServisEditorDto
            {
                Servis = firma == null ? new AdminYetkiliServisDto { AktifMi = true } : AdminYetkiliServisDto.FromEntity(firma),
                Sehirler = _sehirFirmaKoduService.Sehirler(),
                SehirFirmaKodlari = _sehirFirmaKoduService.TumKodlar(),
                SeciliMarkaIds = markaIds,
                SeciliKategoriIds = kategoriIds,
                Markalar = await _context.Ys_Markalar.AsNoTracking()
                    .Where(x => !x.SilindiMi && (x.AktifMi || markaIds.Contains(x.Id)))
                    .OrderBy(x => x.MarkaAdi)
                    .Select(x => new MarkaApiDto { Id = x.Id, MarkaAdi = x.MarkaAdi, AktifMi = x.AktifMi, Aciklama = x.Aciklama })
                    .ToListAsync(),
                Kategoriler = await _context.UrunKategoriler.AsNoTracking()
                    .Where(x => !x.SilindiMi && x.AktifMi).OrderBy(x => x.SiraNo).ThenBy(x => x.Ad)
                    .Select(x => new AdminYetkiliServisKategoriDto { Id = x.Id, Ad = x.Ad, IconUrl = x.IconUrl })
                    .ToListAsync()
            };
        }

        public async Task<ApiIslemSonuc> EkleAsync(
            AdminYetkiliServisKaydetDto? dto,
            AppKullanici kullanici,
            int? kapsamSirketId)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return ApiIslemSonuc.Basarisiz("Firma adi zorunludur.");

            if (!await FirmaYetkiIliskileri.GecerliMiAsync(_context, dto.KategoriIds, dto.MarkaIds))
                return ApiIslemSonuc.Basarisiz("Geçersiz marka veya hizmet türü seçildi.");

            if (!string.IsNullOrWhiteSpace(dto.VergiNo))
            {
                var vknVar = await _context.Ys_Firmalar.AnyAsync(x =>
                    !x.SilindiMi &&
                    x.VergiNo == dto.VergiNo.Trim());

                if (vknVar)
                    return ApiIslemSonuc.Basarisiz("Bu VKN ile kayitli bir yetkili servis zaten var.");
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
                OlusturmaTipi = YetkiliServisOlusturmaTipleri.Admin,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullaniciAdi,
                SilindiMi = false
            };

            _context.Ys_Firmalar.Add(yeni);
            await _context.SaveChangesAsync();

            await FirmaYetkiIliskileri.GuncelleAsync(_context,
                yeni.Id,
                dto.KategoriIds,
                dto.MarkaIds,
                kullaniciAdi);

            await _context.SaveChangesAsync();
            return ApiIslemSonuc.BasariliSonuc("Servis kaydedildi. Giris hesabi Kullanicilar bolumunden olusturulur.");
        }

        public async Task<ApiIslemSonuc> GuncelleAsync(
            AdminYetkiliServisKaydetDto? dto,
            AppKullanici kullanici,
            int? kapsamSirketId)
        {
            if (dto == null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return ApiIslemSonuc.Basarisiz("Yetkili servis ve firma adi zorunludur.");

            if (!await FirmaYetkiIliskileri.GecerliMiAsync(_context, dto.KategoriIds, dto.MarkaIds))
                return ApiIslemSonuc.Basarisiz("Geçersiz marka veya hizmet türü seçildi.");

            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi
                    && (kapsamSirketId == null || x.SirketId == kapsamSirketId.Value));

            if (servis == null)
                return ApiIslemSonuc.Basarisiz("Yetkili servis bulunamadi.");

            if (!string.IsNullOrWhiteSpace(dto.VergiNo))
            {
                var vknVar = await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id != servis.Id &&
                    !x.SilindiMi &&
                    x.VergiNo == dto.VergiNo.Trim());

                if (vknVar)
                    return ApiIslemSonuc.Basarisiz("Bu VKN ile kayitli baska bir yetkili servis var.");
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

            await FirmaYetkiIliskileri.GuncelleAsync(_context,
                servis.Id,
                dto.KategoriIds,
                dto.MarkaIds,
                kullaniciAdi);

            await _context.SaveChangesAsync();
            return ApiIslemSonuc.BasariliSonuc("Yetkili servis guncellendi.");
        }

        public async Task<ApiIslemSonuc> SilAsync(
            AdminYetkiliServisDurumDto? dto,
            AppKullanici kullanici,
            int? kapsamSirketId)
        {
            if (dto == null || dto.Id <= 0)
                return ApiIslemSonuc.Basarisiz("Yetkili servis id zorunludur.");

            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi
                    && (kapsamSirketId == null || x.SirketId == kapsamSirketId.Value));

            if (servis == null)
                return ApiIslemSonuc.Basarisiz("Yetkili servis bulunamadi.");

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == servis.Id);

            if (devreyeAlmaVar)
                return ApiIslemSonuc.Basarisiz("Bu yetkili servis uzerinde devreye alma islemi oldugu icin silinemez.");

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = kullanici.UserName ?? "api";

            await _context.SaveChangesAsync();
            return ApiIslemSonuc.BasariliSonuc("Yetkili servis silindi.");
        }

    }
}
