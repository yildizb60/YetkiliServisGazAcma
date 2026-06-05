using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ys-yetki-belgesi")]
    public class YetkiBelgesiController : Controller
    {
        private readonly YetkiBelgesiApiClient _yetkiBelgesiApiClient;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AktifSirketService _aktifSirketService;

        public YetkiBelgesiController(
            YetkiBelgesiApiClient yetkiBelgesiApiClient,
            UserManager<AppKullanici> userManager,
            AktifSirketService aktifSirketService)
        {
            _yetkiBelgesiApiClient = yetkiBelgesiApiClient;
            _userManager = userManager;
            _aktifSirketService = aktifSirketService;
        }

        [HttpGet]
        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _userManager.GetUserAsync(User);

            if (kullanici == null)
                return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            YetkiBelgesiFirmaEkraniSonuc? ekran;
            try
            {
                ekran = await _yetkiBelgesiApiClient.FirmaEkraniAsync(kullanici, firmaId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                ekran = null;
            }

            if (ekran == null)
            {
                if (!TempData.ContainsKey("Hata"))
                    TempData["Hata"] = "Yetki belgesi bilgileri API uzerinden alinamadi.";

                ekran = new YetkiBelgesiFirmaEkraniSonuc();
            }

            ViewBag.FirmaId = firmaId;
            ViewBag.Firma = ekran.Firma;
            ViewBag.Kullanici = kullanici;
            ViewBag.Bildirimler = ekran.Bildirimler;
            ViewBag.BildirimSayisi = ekran.Bildirimler.Count;
            return View("~/Views/YetkiBelgesi/Index.cshtml", ekran.Belgeler);
        }

        [HttpPost]
        [Route("yukle")]
        public async Task<IActionResult> Yukle(
            IFormFile dosya,
            DateTime bitisTarihi,
            DateTime? baslangicTarihi)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (baslangicTarihi.HasValue && baslangicTarihi.Value.Date > bitisTarihi.Date)
            {
                TempData["Hata"] = "Yetki belgesi başlangıç tarihi, bitiş tarihinden büyük olamaz.";
                return Redirect("/ys-yetki-belgesi");
            }

            var firmaId = kullanici.FirmaId ?? 0;
            try
            {
                var sonuc = await _yetkiBelgesiApiClient.YukleAsync(
                    kullanici, firmaId, dosya, bitisTarihi, baslangicTarihi);
                SetYetkiBelgesiIslemMesaji(sonuc, "Yetki belgesi yüklendi. Onay bekleniyor.", "Basarili");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return Redirect("/ys-yetki-belgesi");
        }

        [HttpPost]
        [Route("sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            try
            {
                var sonuc = await _yetkiBelgesiApiClient.SilAsync(kullanici, id);
                SetYetkiBelgesiIslemMesaji(sonuc, "Yetki belgesi silindi.", basariKey: "Basarili");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return Redirect("/ys-yetki-belgesi");
        }

        [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
        [HttpGet]
        [Route("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            YetkiBelgesiOnayEkraniSonuc? ekran;
            try
            {
                ekran = await _yetkiBelgesiApiClient.OnayEkraniAsync(kullanici, sirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return Redirect("/personel-panel");
            }

            if (ekran == null)
            {
                TempData["Hata"] = "Yetki belgesi onay bilgileri API uzerinden alinamadi veya yetkiniz yok.";
                return Redirect("/personel-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = ekran.Bekleyenler.Count;
            ViewBag.Onaylananlar = ekran.Onaylananlar;
            ViewBag.Reddedilenler = ekran.Reddedilenler;
            return View("~/Views/YetkiBelgesi/OnayBekleyenler.cshtml", ekran.Bekleyenler);
        }

        [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
        [HttpPost]
        [Route("onayla")]
        public async Task<IActionResult> Onayla(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _yetkiBelgesiApiClient.OnaylaAsync(kullanici, id);
                SetYetkiBelgesiIslemMesaji(sonuc, "Yetki belgesi onaylandi.", basariKey: "Basarili");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return Redirect("/ys-yetki-belgesi/onay-bekleyenler");
        }

        [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
        [HttpPost]
        [Route("reddet")]
        public async Task<IActionResult> Reddet(int id, string? gerekce)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _yetkiBelgesiApiClient.ReddetAsync(kullanici, id, gerekce);
                SetYetkiBelgesiIslemMesaji(sonuc, "Yetki belgesi reddedildi.", basariKey: "Hata");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return Redirect("/ys-yetki-belgesi/onay-bekleyenler");
        }

        private void SetYetkiBelgesiIslemMesaji(YetkiBelgesiIslemSonuc? sonuc, string varsayilanBasari, string basariKey)
        {
            if (sonuc?.Basarili == true)
            {
                TempData[basariKey] = sonuc.Mesaj ?? varsayilanBasari;
                return;
            }

            TempData["Hata"] = sonuc?.Mesaj ?? "Yetki belgesi islemi API uzerinden tamamlanamadi.";
        }
    }
}

