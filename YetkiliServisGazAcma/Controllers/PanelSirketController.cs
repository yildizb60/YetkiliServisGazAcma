using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("panel")]
    public class PanelSirketController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AktifSirketService _aktifSirketService;

        public PanelSirketController(
            UserManager<AppKullanici> userManager,
            AktifSirketService aktifSirketService)
        {
            _userManager = userManager;
            _aktifSirketService = aktifSirketService;
        }

        [HttpGet("sirket-sec")]
        public async Task<IActionResult> SirketSec(string? returnUrl)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketler = await _aktifSirketService.KullaniciSirketleriAsync(kullanici);
            if (sirketler.Count == 0 && !await _aktifSirketService.GenelSistemAdminMi(kullanici))
                return Redirect(VarsayilanUrl(kullanici));

            ViewBag.Kullanici = kullanici;
            ViewBag.Sirketler = sirketler;
            ViewBag.AktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            ViewBag.GenelSistemAdminMi = await _aktifSirketService.GenelSistemAdminMi(kullanici);
            ViewBag.ReturnUrl = GuvenliReturnUrl(returnUrl, VarsayilanUrl(kullanici));
            return View("~/Views/Panel/SirketSec.cshtml");
        }

        [HttpPost("sirket-sec")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SirketSec(int sirketId, string? returnUrl)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var degisti = await _aktifSirketService.SirketSecAsync(kullanici, sirketId);
            if (!degisti)
            {
                TempData["Hata"] = "Bu şirket için işlem yetkiniz bulunmuyor.";
                return RedirectToAction(nameof(SirketSec), new { returnUrl });
            }

            return Redirect(GuvenliReturnUrl(returnUrl, VarsayilanUrl(kullanici)));
        }

        private string GuvenliReturnUrl(string? returnUrl, string varsayilanUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return returnUrl;

            return varsayilanUrl;
        }

        private static string VarsayilanUrl(AppKullanici kullanici)
        {
            return kullanici.KullaniciTipi switch
            {
                KullaniciTipiDegerleri.YetkiliServis => "/ys-panel",
                KullaniciTipiDegerleri.Personel => "/personel-panel",
                KullaniciTipiDegerleri.SirketAdmin => "/AdminPanel",
                KullaniciTipiDegerleri.GenelSistemAdmin => "/AdminPanel",
                _ => "/giris"
            };
        }
    }
}
