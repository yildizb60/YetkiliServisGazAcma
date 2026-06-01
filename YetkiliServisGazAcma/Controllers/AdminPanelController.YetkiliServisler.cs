using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Text;

namespace YetkiliServisGazAcma.Controllers
{
    public partial class AdminPanelController
    {
        [HttpGet("yetkiliservisler")]
        public async Task<IActionResult> YetkiliServisler(string? q, string? il, int? durum, string? devreyeSiralama)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var apiSonuc = await _adminYetkiliServisApiClient.ListeleAsync(kullanici, aktifSirketId, q, il, durum, devreyeSiralama);
            ViewBag.AdminYetkiliServisVeriKaynagi = "API";

            var servisler = apiSonuc?.Servisler ?? new List<Ys_Firma>();
            var devreyeSayilari = apiSonuc?.DevreyeSayilari ?? new Dictionary<int, int>();

            if (apiSonuc == null)
                TempData["Hata"] = "Yetkili servis listesi API üzerinden alınamadı.";

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.YetkiliServisler = servisler;
            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliIl = il ?? "";
            ViewBag.SeciliDurum = durum;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.DevreyeSayilari = devreyeSayilari;
            ViewBag.SeciliDevreyeSiralama = devreyeSiralama ?? "";
            return View("~/Views/AdminPanel/YetkiliServisler.cshtml");
        }

        [HttpGet("yetkiliservisler/ekle")]
        public async Task<IActionResult> YetkiliServisEkle()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.Markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();
            return View("~/Views/AdminPanel/YetkiliServisEkle.cshtml");
        }

        [HttpGet("yetkiliservisler/detay/{id}")]
        public async Task<IActionResult> YetkiliServisDetay(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var servis = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.FirmaKategoriler!)
                    .ThenInclude(x => x.Kategori)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (servis == null) return Redirect("/AdminPanel/yetkiliservisler");

            var sertifikalar = await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi && x.FirmaId == id)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .Take(8)
                .ToListAsync();

            var subeler = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi && x.FirmaId == id)
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            var devreye = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi && x.FirmaId == id)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .Take(10)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Servis = servis;
            ViewBag.Sertifikalar = sertifikalar;
            ViewBag.Subeler = subeler;
            ViewBag.Devreye = devreye;
            return View("~/Views/AdminPanel/YetkiliServisDetay.cshtml");
        }

        [HttpPost("yetkiliservisler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisEkle(string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, List<int> kategoriIds, List<int> markaIds)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(firmaAdi))
            {
                TempData["Hata"] = "Firma adı zorunludur.";
                return Redirect("/AdminPanel/yetkiliservisler/ekle");
            }

            var sirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                faaliyetIli,
                kullanici.UserName ?? "sistem");
            var kullanilanKategoriIds = (await KullanilanKategorileriGetir())
                .Select(x => x.Id)
                .ToHashSet();
            kategoriIds = kategoriIds?
                .Where(kullanilanKategoriIds.Contains)
                .Distinct()
                .ToList() ?? new List<int>();

            var yeni = new Ys_Firma
            {
                FirmaAdi = firmaAdi,
                YetkiliKisi = yetkiliKisi,
                Telefon = telefon,
                Email = email,
                Adres = adres,
                FaaliyetIli = faaliyetIli,
                VergiNo = vergiNo,
                VergiDairesi = vergiDairesi,
                SirketId = sirketId,
                AktifMi = true,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_Firmalar.Add(yeni);
            await _context.SaveChangesAsync();

            if (kategoriIds != null && kategoriIds.Count > 0)
            {
                foreach (var kid in kategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = yeni.Id,
                        KategoriId = kid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
                await _context.SaveChangesAsync();
            }

            if (markaIds != null && markaIds.Count > 0)
            {
                foreach (var mid in markaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = yeni.Id,
                        MarkaId = mid,
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
                await _context.SaveChangesAsync();
            }

            TempData["Basarili"] = "Yetkili servis başarıyla eklendi.";
            return Redirect("/AdminPanel/yetkiliservisler");
        }

        [HttpGet("yetkiliservis-duzenle/{id}")]
        [HttpGet("yetkiliservisler/duzenle/{id}")]
        [HttpGet("yetkiliservisler/Düzenle/{id}")]
        public async Task<IActionResult> YetkiliServisDuzenle(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var servis = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (servis == null) return Redirect("/AdminPanel/yetkiliservisler");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Servis = servis;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.SeciliKategoriler = await _context.Ys_FirmaKategoriler
                .Where(x => x.FirmaId == servis.Id && !x.SilindiMi)
                .Select(x => x.KategoriId)
                .ToListAsync();
            return View("~/Views/AdminPanel/YetkiliServisDuzenle.cshtml");
        }

        [HttpPost("yetkiliservis-duzenle/{id}")]
        [HttpPost("yetkiliservisler/duzenle/{id}")]
        [HttpPost("yetkiliservisler/Düzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisDuzenle(int id, string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, bool aktifMi, List<int> kategoriIds)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var servis = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (servis == null) return Redirect("/AdminPanel/yetkiliservisler");

            var sirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                faaliyetIli,
                kullanici.UserName ?? "sistem");

            servis.FirmaAdi = firmaAdi;
            servis.YetkiliKisi = yetkiliKisi;
            servis.Telefon = telefon;
            servis.Email = email;
            servis.Adres = adres;
            servis.FaaliyetIli = faaliyetIli;
            servis.VergiNo = vergiNo;
            servis.VergiDairesi = vergiDairesi;
            servis.SirketId = sirketId;
            servis.AktifMi = aktifMi;
            servis.GuncellemeTarihi = DateTime.Now;
            servis.GuncelleyenKullanici = kullanici.UserName ?? "sistem";

            var mevcut = await _context.Ys_FirmaKategoriler
                .Where(x => x.FirmaId == servis.Id)
                .ToListAsync();
            _context.Ys_FirmaKategoriler.RemoveRange(mevcut);

            if (kategoriIds != null && kategoriIds.Count > 0)
            {
                foreach (var kid in kategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = servis.Id,
                        KategoriId = kid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Yetkili servis güncellendi.";
            return Redirect("/AdminPanel/yetkiliservisler");
        }

        [HttpPost("yetkiliservisler/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisSil(int id)
        {
            var servis = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (servis == null) return Redirect("/AdminPanel/yetkiliservisler");

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == id);

            if (devreyeAlmaVar)
            {
                TempData["Hata"] = "Bu yetkili servis üzerinde devreye alma işlemi olduğu için silinemez.";
                return Redirect("/AdminPanel/yetkiliservisler");
            }

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = User.Identity?.Name ?? "sistem";
            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Yetkili servis silindi.";
            return Redirect("/AdminPanel/yetkiliservisler");
        }
    }
}
