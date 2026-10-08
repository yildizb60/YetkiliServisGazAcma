using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public sealed class YetkiliServisKayitYonetimApiService(
        AppDbContext context, SehirFirmaKoduService sehirFirmaKoduService)
    {
        private readonly AppDbContext _context = context;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService = sehirFirmaKoduService;

        public async Task<YetkiliServisKayitYonetimSonuc> GetirAsync(ClaimsPrincipal principal, int id)
        {
            var kullanici = await AktifKullaniciAsync(principal);
            if (kullanici == null)
                return new(YetkiliServisKayitYonetimDurumu.OturumGecersiz);

            var servis = await YetkiliServisKapsami(kullanici, principal, yonetim: false)
                .Include(x => x.Sirket)
                .Include(x => x.FirmaMarkalar!)
                    .ThenInclude(x => x.Marka)
                .Include(x => x.FirmaKategoriler!)
                    .ThenInclude(x => x.Kategori)
                .Where(x => x.Id == id && !x.SilindiMi)
                .Select(x => new YetkiliServisDetayDto
                {
                    Id = x.Id,
                    FirmaAdi = x.FirmaAdi,
                    YetkiliKisi = x.YetkiliKisi,
                    Telefon = x.Telefon,
                    Email = x.Email,
                    Adres = x.Adres,
                    FaaliyetIli = x.FaaliyetIli,
                    VergiNo = x.VergiNo,
                    VergiDairesi = x.VergiDairesi,
                    SirketId = x.SirketId,
                    SirketAdi = x.Sirket != null ? x.Sirket.SirketAdi : null,
                    AktifMi = x.AktifMi,
                    MarkaIds = x.FirmaMarkalar!
                        .Where(m => !m.SilindiMi)
                        .Select(m => m.MarkaId)
                        .Distinct()
                        .ToList(),
                    KategoriIds = x.FirmaKategoriler!
                        .Where(k => !k.SilindiMi && k.Kategori != null && !k.Kategori.SilindiMi && k.Kategori.AktifMi)
                        .Select(k => k.KategoriId)
                        .Distinct()
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (servis == null)
                return new(YetkiliServisKayitYonetimDurumu.Bulunamadi, "Yetkili servis bulunamadi");

            return new(YetkiliServisKayitYonetimDurumu.Basarili, Servis: servis);
        }

        public async Task<YetkiliServisKayitYonetimSonuc> GuncelleAsync(ClaimsPrincipal principal, YetkiliServisKaydetDto dto)
        {
            var kullanici = await AktifKullaniciAsync(principal);
            if (kullanici == null)
                return new(YetkiliServisKayitYonetimDurumu.OturumGecersiz);

            var servis = await YetkiliServisKapsami(kullanici, principal, yonetim: true)
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi);

            if (servis == null)
                return new(YetkiliServisKayitYonetimDurumu.Bulunamadi, "Yetkili servis bulunamadi");

            if (!await FirmaYetkiIliskileri.GecerliMiAsync(_context, dto.KategoriIds, dto.MarkaIds))
                return new(YetkiliServisKayitYonetimDurumu.GecersizIstek, "Geçersiz marka veya hizmet türü seçildi.");

            servis.FirmaAdi = dto.FirmaAdi;
            servis.YetkiliKisi = dto.YetkiliKisi;
            servis.Telefon = dto.Telefon;
            servis.Email = dto.Email;
            servis.Adres = dto.Adres;
            servis.FaaliyetIli = dto.FaaliyetIli;
            servis.VergiNo = dto.VergiNo;
            servis.VergiDairesi = dto.VergiDairesi;
            if (GenelSistemAdminMi(kullanici, principal))
                servis.SirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    dto.FaaliyetIli,
                    principal.Identity?.Name ?? "api");
            servis.AktifMi = dto.AktifMi;
            servis.GuncellemeTarihi = DateTime.Now;
            servis.GuncelleyenKullanici = principal.Identity?.Name ?? "api";

            await FirmaYetkiIliskileri.GuncelleAsync(_context, servis.Id,
                dto.KategoriIds, dto.MarkaIds, principal.Identity?.Name ?? "api");

            await _context.SaveChangesAsync();
            return new(YetkiliServisKayitYonetimDurumu.Basarili, "Yetkili servis guncellendi");
        }

        public async Task<YetkiliServisKayitYonetimSonuc> SilAsync(ClaimsPrincipal principal, int id)
        {
            var kullanici = await AktifKullaniciAsync(principal);
            if (kullanici == null)
                return new(YetkiliServisKayitYonetimDurumu.OturumGecersiz);

            var servis = await YetkiliServisKapsami(kullanici, principal, yonetim: true)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);

            if (servis == null)
                return new(YetkiliServisKayitYonetimDurumu.Bulunamadi, "Yetkili servis bulunamadi");

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == id);

            if (devreyeAlmaVar)
                return new(YetkiliServisKayitYonetimDurumu.GecersizIstek, "Bu yetkili servis uzerinde devreye alma islemi oldugu icin silinemez");

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = principal.Identity?.Name ?? "api";
            await _context.SaveChangesAsync();

            return new(YetkiliServisKayitYonetimDurumu.Basarili, "Yetkili servis silindi");
        }

        private Task<AppKullanici?> AktifKullaniciAsync(ClaimsPrincipal principal)
        {
            var kullaniciId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kullaniciId
                && x.AktifMi && x.ArsivlemeTarihi == null);
        }

        private static bool GenelSistemAdminMi(AppKullanici kullanici, ClaimsPrincipal principal)
            => principal.IsInRole("GenelSistemAdmin") || principal.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue);

        private IQueryable<Ys_Firma> YetkiliServisKapsami(AppKullanici kullanici, ClaimsPrincipal principal, bool yonetim)
        {
            var query = _context.Ys_Firmalar.Where(x => !x.SilindiMi);
            if (GenelSistemAdminMi(kullanici, principal))
                return query;

            if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin)
                return query.Where(x => x.SirketId == kullanici.SirketId);

            if (!yonetim && kullanici.KullaniciTipi == KullaniciTipiDegerleri.Personel)
                return query.Where(x => _context.Dag_PersonelYetkiler.Any(y =>
                    y.KullaniciId == kullanici.Id && !y.SilindiMi && y.SirketId == x.SirketId
                    && (y.YetkiTipi == YetkiTipleri.TAM_YETKI || y.YetkiTipi == YetkiTipleri.KULLANICI_YONET)));

            if (!yonetim && kullanici.KullaniciTipi is KullaniciTipiDegerleri.YetkiliServis or KullaniciTipiDegerleri.SertifikaliFirma)
                return query.Where(x => x.Id == kullanici.FirmaId);

            return query.Where(x => false);
        }
    }

    public enum YetkiliServisKayitYonetimDurumu
    {
        Basarili,
        OturumGecersiz,
        Bulunamadi,
        GecersizIstek
    }

    public sealed record YetkiliServisKayitYonetimSonuc(
        YetkiliServisKayitYonetimDurumu Durum,
        string? Mesaj = null,
        YetkiliServisDetayDto? Servis = null);
}
