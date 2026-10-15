using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/marka")]
    public class MarkaApiController : ControllerBase
    {
        private readonly MarkaKatalogApiService _service;

        public MarkaApiController(MarkaKatalogApiService service)
        {
            _service = service;
        }

        [HttpPost("liste")]
        [AllowAnonymous]
        public async Task<IActionResult> Liste([FromBody] MarkaListeFiltreDto? dto)
        {
            return HttpSonucu(await _service.ListeleAsync(dto, User));
        }

        [HttpPost("getir")]
        [Authorize]
        public async Task<IActionResult> Getir([FromBody] IdDto dto)
        {
            return HttpSonucu(await _service.GetirAsync(dto.Id, User));
        }

        [HttpPost("ekle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Ekle([FromBody] MarkaKaydetDto dto)
        {
            return HttpSonucu(await _service.EkleAsync(dto, User));
        }

        [HttpPost("guncelle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Guncelle([FromBody] MarkaKaydetDto dto)
        {
            return HttpSonucu(await _service.GuncelleAsync(dto, User));
        }

        [HttpPost("sil")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
        {
            return HttpSonucu(await _service.SilAsync(dto.Id, User));
        }

        private IActionResult HttpSonucu<T>(ReferansApiSonuc<T> sonuc) where T : class
        {
            return sonuc.Durum switch
            {
                ReferansApiDurum.Basarili => Ok(sonuc.Veri),
                ReferansApiDurum.KimlikGerekli => Unauthorized(),
                ReferansApiDurum.Yasak => Forbid(),
                ReferansApiDurum.Bulunamadi => NotFound(new ApiIslemSonuc { Mesaj = sonuc.Mesaj }),
                ReferansApiDurum.Gecersiz => BadRequest(new ApiIslemSonuc { Mesaj = sonuc.Mesaj }),
                _ => throw new InvalidOperationException("Bilinmeyen marka sonucu")
            };
        }
    }
}
