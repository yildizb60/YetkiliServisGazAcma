using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/urun-kategorileri")]
    public class UrunKategorilerController : ControllerBase
    {
        private readonly UrunKategoriKatalogApiService _service;

        public UrunKategorilerController(UrunKategoriKatalogApiService service)
        {
            _service = service;
        }

        [HttpPost("liste")]
        public async Task<IActionResult> Liste([FromBody] UrunKategoriListeFiltreDto? dto)
        {
            return Ok(await _service.AktifleriListeleAsync());
        }
    }

    public class UrunKategoriListeFiltreDto
    {
        public bool TumunuGetir { get; set; }
    }
}
