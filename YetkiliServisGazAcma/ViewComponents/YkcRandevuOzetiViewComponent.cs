using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.ViewComponents;

public sealed class YkcRandevuOzetiViewComponent(
    UserManager<AppKullanici> users, YkcYetkiService yetki, YkcApiClient api,
    ILogger<YkcRandevuOzetiViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await users.GetUserAsync(HttpContext.User);
        if (user == null || user.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma
            || !(await yetki.OzetAsync(user)).TalepleriGorebilir) return Content("");
        try
        {
            var sonuc = await api.TakvimAsync(user, new YkcTakvimFiltre { Baslangic = DateTime.Today, Bitis = DateTime.Today });
            return View(sonuc);
        }
        catch (ApiIntegrationException ex)
        {
            logger.LogWarning(ex, "Ana panel randevuları alınamadı.");
            return View((YkcTakvimSonuc?)null);
        }
    }
}
