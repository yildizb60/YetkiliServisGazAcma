using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/home")]
    public class HomeApiController : ControllerBase
    {
        private readonly HomeOzetApiService _service;

        public HomeApiController(HomeOzetApiService service)
        {
            _service = service;
        }

        [HttpPost("ozet")]
        public async Task<IActionResult> Ozet()
        {
            return Ok(await _service.GetirAsync(HttpContext.RequestAborted));
        }
    }

}
