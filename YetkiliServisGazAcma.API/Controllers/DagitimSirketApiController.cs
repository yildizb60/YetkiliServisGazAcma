using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/dagitim-sirket")]
    public class DagitimSirketApiController : ControllerBase
    {
        private readonly DagitimSirketApiService _service;

        public DagitimSirketApiController(DagitimSirketApiService service)
        {
            _service = service;
        }

        [HttpPost("liste")]
        [AllowAnonymous]
        public async Task<IActionResult> Tumunu([FromBody] DagitimSirketListeFiltreDto? dto)
        {
            return Ok(await _service.ListeleAsync(dto));
        }

        [HttpPost("getir")]
        [Authorize]
        public async Task<IActionResult> Getir([FromBody] IdDto dto)
        {
            var sonuc = await _service.GetirAsync(dto.Id, User);
            return sonuc.Durum switch
            {
                ReferansApiDurum.Basarili => Ok(sonuc.Veri),
                ReferansApiDurum.Yasak => Forbid(),
                ReferansApiDurum.Bulunamadi => NotFound(new { mesaj = sonuc.Mesaj }),
                _ => throw new InvalidOperationException("Bilinmeyen sirket sonucu")
            };
        }
    }

    public class IdDto
    {
        public int Id { get; set; }
    }
}
