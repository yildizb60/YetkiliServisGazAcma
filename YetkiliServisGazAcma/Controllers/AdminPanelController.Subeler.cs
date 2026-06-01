using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    public partial class AdminPanelController
    {
        [HttpGet("subeler")]
        public async Task<IActionResult> Subeler(string? q, int firmaId = 0)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminSubeApiClient.ListeleAsync(kullanici, aktifSirketId, q, firmaId);

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.SeciliFirmaId = firmaId;
            ViewBag.SeciliQ = q ?? "";

            if (sonuc == null)
            {
                TempData["Hata"] = "Sube verileri API uzerinden alinamadi.";
                ViewBag.Subeler = new List<Ys_Sube>();
                ViewBag.Firmalar = new List<Ys_Firma>();
                return View("~/Views/AdminPanel/Subeler.cshtml");
            }

            ViewBag.Subeler = sonuc.Subeler;
            ViewBag.Firmalar = sonuc.Firmalar;
            return View("~/Views/AdminPanel/Subeler.cshtml");
        }

        [HttpPost("subeler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeEkle(int firmaId, string subeAdi, string? il, string? ilce, string? telefon, string? adres, bool aktifMi)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminSubeApiClient.EkleAsync(kullanici, aktifSirketId, firmaId, subeAdi, il, ilce, telefon, adres, aktifMi);

            SetSubeIslemMesaji(sonuc, "Sube kaydi eklendi.");
            return Redirect("/AdminPanel/subeler");
        }

        [HttpGet("subeler/duzenle/{id:int}")]
        public async Task<IActionResult> SubeDuzenle(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminSubeApiClient.DetayAsync(kullanici, id, aktifSirketId);
            if (sonuc == null || !sonuc.Basarili || sonuc.Sube == null)
            {
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube detayi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/subeler");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Sube = sonuc.Sube;
            ViewBag.Firmalar = sonuc.Firmalar;
            return View("~/Views/AdminPanel/SubeDuzenle.cshtml");
        }

        [HttpPost("subeler/duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDuzenle(int id, int firmaId, string subeAdi, string? il, string? ilce, string? telefon, string? adres, bool aktifMi)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminSubeApiClient.GuncelleAsync(kullanici, id, aktifSirketId, firmaId, subeAdi, il, ilce, telefon, adres, aktifMi);

            SetSubeIslemMesaji(sonuc, "Sube guncellendi.");
            return Redirect("/AdminPanel/subeler");
        }

        [HttpPost("subeler/durum")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDurum(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminSubeApiClient.DurumAsync(kullanici, id, aktifSirketId);

            SetSubeIslemMesaji(sonuc, "Sube durumu guncellendi.");
            return Redirect("/AdminPanel/subeler");
        }

        [HttpPost("subeler/sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeSil(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminSubeApiClient.SilAsync(kullanici, id, aktifSirketId);

            SetSubeIslemMesaji(sonuc, "Sube kaydi silindi.");
            return Redirect("/AdminPanel/subeler");
        }

        private void SetSubeIslemMesaji(AdminSubeIslemSonuc? sonuc, string varsayilanBasari)
        {
            if (sonuc?.Basarili == true)
            {
                TempData["Basarili"] = sonuc.Mesaj ?? varsayilanBasari;
                return;
            }

            TempData["Hata"] = sonuc?.Mesaj ?? "Sube islemi API uzerinden tamamlanamadi.";
        }
    }
}
