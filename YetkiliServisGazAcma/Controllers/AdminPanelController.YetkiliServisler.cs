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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminYetkiliServisApiClient.DetayAsync(kullanici, id, aktifSirketId);
            if (sonuc?.Servis == null)
            {
                TempData["Hata"] = "Yetkili servis detayi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/yetkiliservisler");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Servis = sonuc.Servis;
            ViewBag.Sertifikalar = sonuc.Sertifikalar;
            ViewBag.Subeler = sonuc.Subeler;
            ViewBag.Devreye = sonuc.Devreye;
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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminYetkiliServisApiClient.DetayAsync(kullanici, id, aktifSirketId);
            if (sonuc?.Servis == null)
            {
                TempData["Hata"] = "Yetkili servis detayi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/yetkiliservisler");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Servis = sonuc.Servis;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.SeciliKategoriler = sonuc.Servis.FirmaKategoriler?
                .Where(x => !x.SilindiMi)
                .Select(x => x.KategoriId)
                .Distinct()
                .ToList() ?? new List<int>();
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

            var sonuc = await _adminYetkiliServisApiClient.GuncelleAsync(
                kullanici,
                id,
                firmaAdi,
                yetkiliKisi,
                telefon,
                email,
                adres,
                faaliyetIli,
                vergiNo,
                vergiDairesi,
                aktifMi,
                kategoriIds);
            SetYetkiliServisIslemMesaji(sonuc, "Yetkili servis guncellendi.");
            return Redirect("/AdminPanel/yetkiliservisler");
        }

        [HttpPost("yetkiliservisler/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisSil(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _adminYetkiliServisApiClient.SilAsync(kullanici, id);
            SetYetkiliServisIslemMesaji(sonuc, "Yetkili servis silindi.");
            return Redirect("/AdminPanel/yetkiliservisler");
        }

        private void SetYetkiliServisIslemMesaji(AdminYetkiliServisIslemSonuc? sonuc, string varsayilanBasari)
        {
            if (sonuc?.Basarili == true)
            {
                TempData["Basarili"] = sonuc.Mesaj ?? varsayilanBasari;
                return;
            }

            TempData["Hata"] = sonuc?.Mesaj ?? "Yetkili servis islemi API uzerinden tamamlanamadi.";
        }
    }
}
