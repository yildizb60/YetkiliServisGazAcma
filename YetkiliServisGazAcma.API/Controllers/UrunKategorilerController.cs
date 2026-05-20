using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/urun-kategorileri")]
    public class UrunKategorilerController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UrunKategorilerController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("liste")]
        public async Task<IActionResult> Liste()
        {
            var list = await _context.UrunKategoriler
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .Select(x => new
                {
                    x.Id,
                    x.Ad,
                    x.IconUrl,
                    x.SiraNo,
                    x.AktifMi
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}
