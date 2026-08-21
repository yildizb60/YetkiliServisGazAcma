using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class PanelKimlikActionFilter : IAsyncActionFilter
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly PanelKimlikService _panelKimlikService;
        private readonly AktifSirketService _aktifSirketService;
        private readonly YkcYetkiService _ykcYetkiService;

        public PanelKimlikActionFilter(
            UserManager<AppKullanici> userManager,
            PanelKimlikService panelKimlikService,
            AktifSirketService aktifSirketService,
            YkcYetkiService ykcYetkiService)
        {
            _userManager = userManager;
            _panelKimlikService = panelKimlikService;
            _aktifSirketService = aktifSirketService;
            _ykcYetkiService = ykcYetkiService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Controller is Controller controller)
            {
                AppKullanici? kullanici = null;
                if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                    kullanici = await _userManager.GetUserAsync(context.HttpContext.User);

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
                    controller.ViewBag.YkcYetkileri = await _ykcYetkiService.OzetAsync(kullanici, aktifSirketId);
                }
            }

            await next();
        }
    }
}
