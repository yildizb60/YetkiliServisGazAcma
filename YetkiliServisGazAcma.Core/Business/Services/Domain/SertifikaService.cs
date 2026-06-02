using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class SertifikaService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SertifikaService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Firmaya ait yetki belgelerini getir
        public async Task<List<Ys_Sertifika>> FirmaninSertifikalari(int firmaId)
        {
            return await _context.Ys_Sertifikalar
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();
        }

        // Onay bekleyen tüm yetki belgeleri (ÇEDAŞ personeli için)
        public async Task<List<Ys_Sertifika>> OnayBekleyenler(int? sirketId = null)
        {
            var sorgu = _context.Ys_Sertifikalar
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
            string? kullanici)
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
            var klasor = Path.Combine(_env.WebRootPath, "sertifikalar");
            if (!Directory.Exists(klasor))
                Directory.CreateDirectory(klasor);

            var dosyaAdi = $"sert_{firmaId}_{DateTime.Now:yyyyMMddHHmmss}{uzanti}";
            var dosyaYolu = Path.Combine(klasor, dosyaAdi);

            using (var stream = new FileStream(dosyaYolu, FileMode.Create))
                await dosya.CopyToAsync(stream);

            // Önceki yetki belgelerini silmiyoruz, geçmişte görünmeleri için saklıyoruz.

            // Yeni yetki belgesi kaydı
            var sertifika = new Ys_Sertifika
            {
                FirmaId = firmaId,
                DosyaYolu = "/sertifikalar/" + dosyaAdi,
                SertifikaBaslangicTarihi = baslangic,
                SertifikaBitisTarihi = bitis,
                Durum = 0, // Onayda Bekliyor
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_Sertifikalar.Add(sertifika);
            await _context.SaveChangesAsync();

            return (true, "Yetki belgeniz başarıyla yüklendi. Onay bekleniyor.");
        }

        // Yetki belgesi onayla (ÇEDAŞ personeli)
        public async Task<bool> Onayla(int sertifikaId, string? kullanici)
        {
            var sertifika = await _context.Ys_Sertifikalar
                .FirstOrDefaultAsync(x => x.Id == sertifikaId);

            if (sertifika == null) return false;

            sertifika.Durum = 1; // Onaylandı
            sertifika.OnayTarihi = DateTime.Now;
            sertifika.OnaylayanKullanici = kullanici ?? "sistem";
            sertifika.GuncellemeTarihi = DateTime.Now;
            sertifika.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }

        // Yetki belgesi reddet (ÇEDAŞ personeli)
        public async Task<bool> Reddet(int sertifikaId, string? gerekce, string? kullanici)
        {
            var sertifika = await _context.Ys_Sertifikalar
                .FirstOrDefaultAsync(x => x.Id == sertifikaId);

            if (sertifika == null) return false;

            sertifika.Durum = 2; // Reddedildi
            sertifika.RedGerekce = gerekce;
            sertifika.GuncellemeTarihi = DateTime.Now;
            sertifika.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }
    }
}


