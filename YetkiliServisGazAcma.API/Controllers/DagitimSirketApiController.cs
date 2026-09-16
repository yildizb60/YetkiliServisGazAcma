using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/dagitim-sirket")]
    public class DagitimSirketApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DagitimSirketApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("liste")]
        [AllowAnonymous]
        public async Task<IActionResult> Tumunu([FromBody] DagitimSirketListeFiltreDto? dto)
        {
            var query = _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (dto?.TumunuGetir != true)
                query = query.Where(x => x.AktifMi);

            if (dto?.AktifMi.HasValue == true)
                query = query.Where(x => x.AktifMi == dto.AktifMi.Value);

            var sirketler = await query
                .Select(x => new
                {
                    x.Id,
                    x.SirketAdi,
                    x.Il,
                    x.Telefon,
                    x.Email,
                    x.Adres,
                    x.AktifMi
                })
                .OrderBy(x => x.SirketAdi)
                .ToListAsync();

            return Ok(sirketler);
        }

        [HttpPost("getir")]
        [Authorize]
        public async Task<IActionResult> Getir([FromBody] IdDto dto)
        {
            if (!await DagitimSirketGorebilirMi(dto.Id))
                return Forbid();

            var sirket = await _context.Dag_Sirketler
                .Where(x => x.Id == dto.Id && !x.SilindiMi)
                .Select(x => new
                {
                    x.Id,
                    x.SirketAdi,
                    x.Il,
                    x.Telefon,
                    x.Email,
                    x.Adres,
                    x.AktifMi
                })
                .FirstOrDefaultAsync();

            if (sirket == null)
                return NotFound(new { mesaj = "Sirket bulunamadi" });

            return Ok(sirket);
        }

        private async Task<bool> GenelSistemYonetebilirMi()
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return false;

            if (User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue))
                return true;

            return false;
        }

        private async Task<bool> DagitimSirketGorebilirMi(int sirketId)
        {
            if (await GenelSistemYonetebilirMi())
                return true;

            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return false;

            if (kullanici.SirketId == sirketId)
                return true;

            return false;
        }

        private async Task<AppKullanici?> AktifKullaniciAsync()
        {
            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(kullaniciId))
                return null;

            return await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
        }
    }

    public class IdDto
    {
        public int Id { get; set; }
    }

    public class DagitimSirketListeFiltreDto
    {
        public bool TumunuGetir { get; set; }
        public bool? AktifMi { get; set; }
    }
}
