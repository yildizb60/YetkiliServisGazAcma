using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MarkaController : Controller
    {
        private readonly MarkaApiClient _markaApiClient;
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly AktifSirketService _aktifSirketService;

        public MarkaController(
            MarkaApiClient markaApiClient,
            ApiKullaniciOturumu kullaniciOturumu,
            AktifSirketService aktifSirketService)
        {
            _markaApiClient = markaApiClient;
            _kullaniciOturumu = kullaniciOturumu;
            _aktifSirketService = aktifSirketService;
        }

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _kullaniciOturumu.GetUserAsync(User);
        }

        public async Task<IActionResult> Index(string? q, string? durum)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            bool? aktifMi = durum switch { "aktif" => true, "pasif" => false, _ => null };
            var markalar = await _markaApiClient.TumunuGetirAsync(kullanici, q, aktifMi);
            ViewBag.MarkaVeriKaynagi = "API";

            if (markalar == null)
            {
                TempData["Hata"] = "Marka listesi API uzerinden alinamadi.";
                markalar = new List<MarkaApiDto>();
            }

            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliDurum = string.IsNullOrWhiteSpace(durum) ? "tumu" : durum;
            ViewBag.Kullanici = kullanici;
            ViewBag.GenelSistemAdminMi = kullanici != null && await _aktifSirketService.GenelSistemAdminMi(kullanici);
            return View(markalar);
        }

        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            return RedirectToAction(nameof(Index), new { yeni = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(MarkaKaydetDto marka)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            var sonuc = await _markaApiClient.EkleAsync(kullanici, marka);
            SetMarkaIslemMesaji(sonuc, "Marka basariyla eklendi.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            return RedirectToAction(nameof(Index), new { duzenle = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(MarkaKaydetDto marka)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            var sonuc = await _markaApiClient.GuncelleAsync(kullanici, marka);
            SetMarkaIslemMesaji(sonuc, "Marka basariyla guncellendi.");
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            var sonuc = await _markaApiClient.SilAsync(kullanici, id);
            SetMarkaIslemMesaji(
                sonuc,
                "Marka basariyla silindi.",
                "Bu marka uzerinde devreye alma veya yetkili servis kaydi oldugu icin silinemez.");
            return RedirectToAction("Index");
        }

        private void SetMarkaIslemMesaji(ApiIslemSonuc? sonuc, string varsayilanBasari, string? varsayilanHata = null)
        {
            if (sonuc?.Basarili == true)
            {
                TempData["Mesaj"] = sonuc.Mesaj ?? varsayilanBasari;
                return;
            }

            TempData["Hata"] = sonuc?.Mesaj ?? varsayilanHata ?? "Marka islemi API uzerinden tamamlanamadi.";
        }
    }
}

