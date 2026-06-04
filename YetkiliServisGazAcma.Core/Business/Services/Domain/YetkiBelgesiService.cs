using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiBelgesiService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public YetkiBelgesiService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Firmaya ait yetki belgelerini getir
        public async Task<List<Ys_YetkiBelgesi>> FirmaninYetkiBelgeleri(int firmaId)
        {
            return await _context.Ys_YetkiBelgeleri
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();
        }

        // Onay bekleyen tüm yetki belgeleri (ÇEDAŞ personeli için)
        public async Task<List<Ys_YetkiBelgesi>> OnayBekleyenler(int? sirketId = null)
        {
            var sorgu = _context.Ys_YetkiBelgeleri
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi && x.Durum == 0);

            if (sirketId.HasValue)
                sorgu = sorgu.Where(x => x.Firma!.SirketId == sirketId.Value);

            return await sorgu
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();
        }

        // Yetki belgesi yükle
        public async Task<(bool basarili, string mesaj)> Yukle(
            int firmaId,
            IFormFile dosya,
            DateTime bitisTarihi,
            DateTime? baslangicTarihi,
            string? kullanici,
            string? publicBaseUrl = null)
        {
            var baslangic = (baslangicTarihi ?? DateTime.Now.Date).Date;
            var bitis = bitisTarihi.Date;

            if (baslangic > bitis)
                return (false, "Yetki belgesi başlangıç tarihi, bitiş tarihinden büyük olamaz.");

            // Dosya kontrolü
            if (dosya == null || dosya.Length == 0)
                return (false, "Lütfen bir dosya seçiniz.");

            // Sadece PDF ve resim kabul et
            var izinliUzantilar = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var uzanti = Path.GetExtension(dosya.FileName).ToLower();
            if (!izinliUzantilar.Contains(uzanti))
                return (false, "Sadece PDF, JPG veya PNG dosyası yükleyebilirsiniz.");

            // Dosyayı kaydet
            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;
            var klasor = Path.Combine(webRoot, "yetki-belgeleri");
            if (!Directory.Exists(klasor))
                Directory.CreateDirectory(klasor);

            var dosyaAdi = $"yb_{firmaId}_{DateTime.Now:yyyyMMddHHmmss}{uzanti}";
            var dosyaYolu = Path.Combine(klasor, dosyaAdi);

            using (var stream = new FileStream(dosyaYolu, FileMode.Create))
                await dosya.CopyToAsync(stream);

            // Önceki yetki belgelerini silmiyoruz, geçmişte görünmeleri için saklıyoruz.

            // Yeni yetki belgesi kaydı
            var yetkiBelgesi = new Ys_YetkiBelgesi
            {
                FirmaId = firmaId,
                DosyaYolu = BuildDosyaYolu(publicBaseUrl, dosyaAdi),
                YetkiBelgesiBaslangicTarihi = baslangic,
                YetkiBelgesiBitisTarihi = bitis,
                Durum = 0, // Onayda Bekliyor
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_YetkiBelgeleri.Add(yetkiBelgesi);
            await _context.SaveChangesAsync();

            return (true, "Yetki belgeniz başarıyla yüklendi. Onay bekleniyor.");
        }

        private static string BuildDosyaYolu(string? publicBaseUrl, string dosyaAdi)
        {
            var relativePath = "/yetki-belgeleri/" + dosyaAdi;
            if (string.IsNullOrWhiteSpace(publicBaseUrl))
                return relativePath;

            return publicBaseUrl.TrimEnd('/') + relativePath;
        }

        // Yetki belgesi onayla (ÇEDAŞ personeli)
        public async Task<bool> Onayla(int yetkiBelgesiId, string? kullanici)
        {
            var yetkiBelgesi = await _context.Ys_YetkiBelgeleri
                .FirstOrDefaultAsync(x => x.Id == yetkiBelgesiId);

            if (yetkiBelgesi == null) return false;

            yetkiBelgesi.Durum = 1; // Onaylandı
            yetkiBelgesi.RedGerekce = null;
            yetkiBelgesi.OnayTarihi = DateTime.Now;
            yetkiBelgesi.OnaylayanKullanici = kullanici ?? "sistem";
            yetkiBelgesi.GuncellemeTarihi = DateTime.Now;
            yetkiBelgesi.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }

        // Yetki belgesi reddet (ÇEDAŞ personeli)
        public async Task<bool> Reddet(int yetkiBelgesiId, string? gerekce, string? kullanici)
        {
            var yetkiBelgesi = await _context.Ys_YetkiBelgeleri
                .FirstOrDefaultAsync(x => x.Id == yetkiBelgesiId);

            if (yetkiBelgesi == null) return false;

            yetkiBelgesi.Durum = 2; // Reddedildi
            yetkiBelgesi.RedGerekce = string.IsNullOrWhiteSpace(gerekce) ? "Belirtilmedi." : gerekce.Trim();
            yetkiBelgesi.OnayTarihi = DateTime.Now;
            yetkiBelgesi.OnaylayanKullanici = kullanici ?? "sistem";
            yetkiBelgesi.GuncellemeTarihi = DateTime.Now;
            yetkiBelgesi.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }
    }
}


