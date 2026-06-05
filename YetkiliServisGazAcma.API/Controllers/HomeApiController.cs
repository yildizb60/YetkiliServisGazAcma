using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/home")]
    public class HomeApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HomeApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("ozet")]
        public async Task<IActionResult> Ozet()
        {
            var servisCount = await _context.Ys_Firmalar.CountAsync(x => !x.SilindiMi && x.AktifMi);
            var devreyeCount = await _context.Ys_DevreyeAlmalar.CountAsync(x => !x.SilindiMi && x.Durum == 1);
            var yetkiBelgesiCount = await _context.Ys_YetkiBelgeleri.CountAsync(x => !x.SilindiMi && x.Durum == 1);
            var toplamIslem = await _context.Ys_DevreyeAlmalar.CountAsync(x => !x.SilindiMi);
            var zamaninda = toplamIslem == 0 ? 100.0 : Math.Round(100.0 * devreyeCount / toplamIslem, 1);

            return Ok(new HomeOzetDto
            {
                ServisCount = servisCount,
                DevreyeCount = devreyeCount,
                YetkiBelgesiCount = yetkiBelgesiCount,
                ZamanindaOran = zamaninda
            });
        }
    }

    public class HomeOzetDto
    {
        public int ServisCount { get; set; }
        public int DevreyeCount { get; set; }
        public int YetkiBelgesiCount { get; set; }
        public double ZamanindaOran { get; set; }
    }
}
