using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/panel-kapsam")]
    [Authorize]
    public class PanelKapsamApiController : ControllerBase
    {
        private readonly PanelKapsamApiService _service;

        public PanelKapsamApiController(PanelKapsamApiService service)
        {
            _service = service;
        }

        [HttpPost("sirketler")]
        public async Task<IActionResult> KullaniciSirketleri()
        {
            return HttpSonucu(await _service.KullaniciSirketleriAsync(User));
        }

        [HttpPost("kimlik")]
        public async Task<IActionResult> PanelKimlik([FromBody] PanelKimlikIstekDto? dto)
        {
            return HttpSonucu(await _service.KimlikAsync(User, dto?.AktifSirketId));
        }

        [HttpPost("ykc-yetkileri")]
        public async Task<IActionResult> YkcYetkileri([FromBody] PanelKimlikIstekDto? dto)
        {
            return HttpSonucu(await _service.YkcYetkileriAsync(User, dto?.AktifSirketId, HttpContext.RequestAborted));
        }

        private IActionResult HttpSonucu<T>(ReferansApiSonuc<T> sonuc) where T : class
        {
            return sonuc.Durum switch
            {
                ReferansApiDurum.Basarili => Ok(sonuc.Veri),
                ReferansApiDurum.KimlikGerekli => Unauthorized(),
                ReferansApiDurum.Yasak => Forbid(),
                _ => throw new InvalidOperationException("Bilinmeyen panel kapsam sonucu")
            };
        }
    }
}
