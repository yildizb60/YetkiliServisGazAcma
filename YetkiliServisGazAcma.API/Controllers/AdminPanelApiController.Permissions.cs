using Microsoft.AspNetCore.Mvc;

namespace YetkiliServisGazAcma.API.Controllers
{
    public partial class AdminPanelApiController
    {
        [HttpPost("yetkiler/liste")]
        public async Task<IActionResult> YetkilerListe([FromBody] AdminYetkiListeFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminPersonelYetkiApiService.ListeleAsync(
                kullanici,
                kapsam.sirketId,
                GenelSistemAdminMi(kullanici)));
        }

        [HttpPost("yetkiler/getir")]
        public async Task<IActionResult> YetkiGetir([FromBody] AdminYetkiGetirDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminPersonelYetkiApiService.GetirAsync(
                dto,
                kullanici,
                kapsam.sirketId,
                GenelSistemAdminMi(kullanici)));
        }

        [HttpPost("yetkiler/guncelle")]
        public async Task<IActionResult> YetkiGuncelle([FromBody] AdminYetkiGuncelleDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminPersonelYetkiApiService.GuncelleAsync(
                dto,
                kullanici,
                kapsam.sirketId,
                GenelSistemAdminMi(kullanici)));
        }

    }
}
