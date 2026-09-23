using Microsoft.AspNetCore.Identity;
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
        private readonly PersonelPanelApiClient _personelPanelApiClient;

        public PanelKimlikActionFilter(
            ApiKullaniciOturumu kullaniciOturumu,
            PanelKimlikService panelKimlikService,
            AktifSirketService aktifSirketService,
            PanelKapsamApiClient panelKapsamApiClient,
            PersonelPanelApiClient personelPanelApiClient)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _panelKimlikService = panelKimlikService;
            _aktifSirketService = aktifSirketService;
            _panelKapsamApiClient = panelKapsamApiClient;
            _personelPanelApiClient = personelPanelApiClient;
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

                    if (context.HttpContext.User.IsInRole(KullaniciRolAdlari.Personel))
                    {
                        var yetkiler = await _personelPanelApiClient.YetkilerimAsync(kullanici, aktifSirketId)
                            ?? new List<string>();
                        var tamYetkili = yetkiler.Contains(YetkiTipleri.TAM_YETKI, StringComparer.OrdinalIgnoreCase);
                        bool Yetkili(string kod) => tamYetkili || yetkiler.Contains(kod, StringComparer.OrdinalIgnoreCase);

                        controller.ViewBag.YetkiBelgesi = Yetkili(YetkiTipleri.YETKI_BELGESI_ONAY);
                        controller.ViewBag.YetkiRapor = Yetkili(YetkiTipleri.RAPOR_GOR);
                        controller.ViewBag.YetkiServis = Yetkili(YetkiTipleri.KULLANICI_YONET);
                        controller.ViewBag.YetkiMarka = Yetkili(YetkiTipleri.MARKA_YONET);
                        controller.ViewBag.YetkiMarkaYonet = controller.ViewBag.YetkiMarka;
                        controller.ViewBag.YetkiYkcTalep = Yetkili(YetkiTipleri.YKC_TALEP_GOR);
                        controller.ViewBag.YetkiYkcAtama = Yetkili(YetkiTipleri.YKC_ATAMA_YAP);
                        controller.ViewBag.YetkiYkcImza = Yetkili(YetkiTipleri.YKC_FR265_IMZA_ISLEM);
                        controller.ViewBag.YetkiYkcRapor = Yetkili(YetkiTipleri.YKC_RAPOR_GOR);
                    }
                }
            }

            await next();
        }
    }
}
