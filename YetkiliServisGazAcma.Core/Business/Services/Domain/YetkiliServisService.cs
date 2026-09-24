using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;

        public YetkiliServisService(
            AppDbContext context,
            UserManager<AppKullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Tüm yetkili servisleri getir
        public async Task<List<Ys_Firma>> TumunuGetir(int? sirketId = null)
        {
            var sorgu = _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi);

            // Eğer sirketId verilmişse sadece o Sirketin servislerini getir
            if (sirketId.HasValue)
                sorgu = sorgu.Where(x => x.SirketId == sirketId.Value);

            return await sorgu.OrderBy(x => x.FirmaAdi).ToListAsync();
        }

        // ID ile getir
        public async Task<Ys_Firma?> IdIleGetir(int id)
        {
            return await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.FirmaMarkalar!)
                    .ThenInclude(x => x.Marka)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
        }

        // VKN ile getir
        public async Task<Ys_Firma?> VknIleGetir(string vkn)
        {
            return await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.VergiNo == vkn && !x.SilindiMi);
        }

        // Yeni kayıt
        public async Task<(bool basarili, string mesaj)> Kayit(
            Ys_Firma firma,
            string sifre,
            List<int> markaIdleri,
            List<int> kategoriIdleri,
            string? ilce = null)
        {
            var secilenKategoriIds = kategoriIdleri?.Distinct().ToList() ?? new List<int>();
            if (secilenKategoriIds.Count > 0)
            {
                var gecerliKategoriSayisi = await _context.UrunKategoriler
                    .CountAsync(x => secilenKategoriIds.Contains(x.Id) && !x.SilindiMi && x.AktifMi);
                if (gecerliKategoriSayisi != secilenKategoriIds.Count)
                    return (false, "Geçersiz hizmet türü seçildi.");
            }

            // VKN kontrolü — aynı VKN ile kayıt var mı?
            var mevcutFirma = await VknIleGetir(firma.VergiNo!);
            if (mevcutFirma != null)
                return (false, "Bu VKN ile zaten kayıt bulunmaktadır.");

            // Firma kaydı
            firma.OlusturmaTarihi = DateTime.Now;
            firma.OlusturanKullanici = firma.VergiNo;
            firma.OlusturmaTipi = YetkiliServisOlusturmaTipleri.Kayit;
            firma.SilindiMi = false;
            firma.AktifMi = true;

            _context.Ys_Firmalar.Add(firma);
            await _context.SaveChangesAsync();

            // Kullanici hesabı oluştur (VKN = Kullanici adı)
            var kullanici = new AppKullanici
            {
                UserName = firma.VergiNo,
                Email = firma.Email,
                PhoneNumber = firma.Telefon?.Trim(),
                AdSoyad = firma.YetkiliKisi,
                KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, // Yetkili Servis
                FirmaId = firma.Id,
                SirketId = firma.SirketId,
                AktifMi = true,
                EmailConfirmed = true
            };

            var sonuc = await _userManager.CreateAsync(kullanici, sifre);
            if (!sonuc.Succeeded)
            {
                // Kullanici oluşturulamazsa firmayı da sil
                _context.Ys_Firmalar.Remove(firma);
                await _context.SaveChangesAsync();
                return (false, string.Join(", ", sonuc.Errors.Select(x => x.Description)));
            }

            await _userManager.AddToRoleAsync(kullanici, "YetkiliServis");

            // Markaları ata
            foreach (var markaId in markaIdleri)
            {
                _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                {
                    FirmaId = firma.Id,
                    MarkaId = markaId,
                    YetkiBitisTarihi = DateTime.Now.AddYears(1),
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = firma.VergiNo,
                    SilindiMi = false
                });
            }

            // Kategorileri ata
            if (secilenKategoriIds.Count > 0)
            {
                foreach (var kategoriId in secilenKategoriIds)
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = firma.Id,
                        KategoriId = kategoriId,
                        YetkiBitisTarihi = DateTime.Now.AddYears(1),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = firma.VergiNo,
                        SilindiMi = false
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(ilce))
            {
                _context.Ys_Subeler.Add(new Ys_Sube
                {
                    FirmaId = firma.Id,
                    SubeAdi = "Merkez",
                    Il = firma.FaaliyetIli,
                    Ilce = ilce.Trim(),
                    Telefon = firma.Telefon,
                    Adres = firma.Adres,
                    AktifMi = true,
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = firma.VergiNo,
                    SilindiMi = false
                });
            }

            await _context.SaveChangesAsync();
            return (true, "Kayıt başarıyla tamamlandı.");
        }
    }
}
