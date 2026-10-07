using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    public partial class AdminPanelApiController
    {
        [HttpPost("personel-dashboard")]
        public async Task<IActionResult> PersonelDashboard(
            [FromBody] PersonelYetkilerimIstek? dto,
            [FromServices] PersonelDashboardApiService service)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            var yonetici = GenelSistemAdminMi(kullanici) || User.IsInRole("SirketAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin;
            return Ok(await service.GetirAsync(kullanici, kapsam.sirketId, yonetici));
        }

        [HttpPost("bildirim-ozeti")]
        public async Task<IActionResult> BildirimOzeti([FromBody] AdminDashboardFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await YetkiBelgesiOnaylayabilirMi(kapsam.sirketId))
                return Ok(new PanelBildirimOzeti());

            return Ok(await _dashboardService.BildirimOzetiAsync(kapsam.sirketId));
        }

        [HttpPost("dashboard")]
        public async Task<IActionResult> Dashboard([FromBody] AdminDashboardFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();
            if (!GenelSistemAdminMi(kullanici) && !User.IsInRole("SirketAdmin")
                && kullanici.KullaniciTipi != KullaniciTipiDegerleri.SirketAdmin)
                return Forbid();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _dashboardService.GetirAsync(kapsam.sirketId));
        }
    }
}
