using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    public partial class AdminPanelApiController
    {
        [HttpPost("personeller/ekle")]
        public async Task<IActionResult> PersonelEkle([FromBody] AdminPersonelKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.KapsamSirketId ?? dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var sonuc = await _kullaniciYonetim.PersonelEkleAsync(dto, kullanici, kapsam.sirketId, GenelSistemAdminMi(kullanici));
            return sonuc.Yetkisiz ? Forbid() : Ok(sonuc.Sonuc);
        }

    }
}
