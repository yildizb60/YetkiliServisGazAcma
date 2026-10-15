using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class PanelKimlikActionFilter : IAsyncActionFilter
    {
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly PanelKimlikService _panelKimlikService;
        private readonly AktifSirketService _aktifSirketService;
        private readonly PanelKapsamApiClient _panelKapsamApiClient;
        private readonly PanelGorunumService _panelGorunum;

        public PanelKimlikActionFilter(
            ApiKullaniciOturumu kullaniciOturumu,
            PanelKimlikService panelKimlikService,
            AktifSirketService aktifSirketService,
            PanelKapsamApiClient panelKapsamApiClient,
            PanelGorunumService panelGorunum)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _panelKimlikService = panelKimlikService;
            _aktifSirketService = aktifSirketService;
            _panelKapsamApiClient = panelKapsamApiClient;
            _panelGorunum = panelGorunum;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Sign-in and sign-out must remain reachable even with an expired API session.
            if (context.Controller is YetkiliServisGazAcma.Controllers.GirisController)
            {
                await next();
                return;
            }
            if (context.Controller is Controller controller)
            {
                AppKullanici? kullanici = null;
                if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                    kullanici = await _kullaniciOturumu.GetUserAsync(context.HttpContext.User);

                var kimlik = await _panelKimlikService.KullaniciIcinOlustur(kullanici);
                controller.ViewBag.PanelSirketAdi = kimlik.SirketAdi;
                controller.ViewBag.PanelLogoUrl = kimlik.LogoUrl;

                if (kullanici != null)
                {
                    controller.ViewBag.AktifSirketler = await _aktifSirketService.KullaniciSirketleriAsync(kullanici);
                    var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
                    controller.ViewBag.AktifSirketId = aktifSirketId;
                    controller.ViewBag.GenelSistemAdminMi = await _aktifSirketService.GenelSistemAdminMi(kullanici);
                    controller.ViewBag.SirketAdminMi = await _aktifSirketService.SirketAdminMi(kullanici);
                    controller.ViewBag.YkcYetkileri = await _panelKapsamApiClient.YkcYetkileriAsync(kullanici, aktifSirketId);
                }
            }

            var executed = await next();
            if (executed.Exception == null && executed.Result is ViewResult
                && context.Controller is Controller panel
                && context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var user = await _kullaniciOturumu.GetUserAsync(context.HttpContext.User);
                if (user != null)
                {
                    await _panelGorunum.HazirlaAsync(user, panel.ViewData);
                    if (_panelGorunum.HataMesaji != null) panel.TempData["Hata"] = _panelGorunum.HataMesaji;
                }
            }
        }
    }
}
