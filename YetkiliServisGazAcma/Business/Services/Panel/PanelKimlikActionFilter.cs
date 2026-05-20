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

        public PanelKimlikActionFilter(
            UserManager<AppKullanici> userManager,
            PanelKimlikService panelKimlikService,
            AktifSirketService aktifSirketService)
        {
            _userManager = userManager;
            _panelKimlikService = panelKimlikService;
            _aktifSirketService = aktifSirketService;
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
                    controller.ViewBag.AktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
                    controller.ViewBag.GenelSistemAdminMi = await _aktifSirketService.GenelSistemAdminMi(kullanici);
                    controller.ViewBag.SirketAdminMi = await _aktifSirketService.SirketAdminMi(kullanici);
                }
            }

            await next();
        }
    }
}
