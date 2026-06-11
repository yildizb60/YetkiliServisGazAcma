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
    [Route("api/marka")]
    public class MarkaApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly MarkaService _service;

        public MarkaApiController(AppDbContext context, MarkaService service)
        {
            _context = context;
            _service = service;
        }

        [HttpPost("liste")]
        [AllowAnonymous]
        public async Task<IActionResult> Liste([FromBody] MarkaListeFiltreDto? dto)
        {
            var query = _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (dto?.TumunuGetir != true)
                query = query.Where(x => x.AktifMi);

            if (dto?.AktifMi.HasValue == true)
                query = query.Where(x => x.AktifMi == dto.AktifMi.Value);

            var markalar = await query
                .Select(x => new
                {
                    x.Id,
                    x.MarkaAdi,
                    x.Aciklama,
                    x.AktifMi
                })
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();

            return Ok(markalar);
        }

        [HttpPost("getir")]
        [Authorize]
        public async Task<IActionResult> Getir([FromBody] IdDto dto)
        {
            var marka = await _context.Ys_Markalar
                .Where(x => x.Id == dto.Id && !x.SilindiMi)
                .Select(x => new
                {
                    x.Id,
                    x.MarkaAdi,
                    x.Aciklama,
                    x.AktifMi
                })
                .FirstOrDefaultAsync();

            if (marka == null)
                return NotFound(new { mesaj = "Marka bulunamadi" });

            return Ok(marka);
        }

        [HttpPost("ekle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Ekle([FromBody] MarkaKaydetDto dto)
        {
            if (!await MarkaYonetebilirMi())
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.MarkaAdi))
                return BadRequest(new { basarili = false, mesaj = "Marka adi zorunludur" });

            var marka = new Ys_Marka
            {
                MarkaAdi = dto.MarkaAdi,
                Aciklama = dto.Aciklama,
                AktifMi = dto.AktifMi
            };

            await _service.Ekle(marka, User.Identity?.Name);
            return Ok(new { basarili = true, mesaj = "Marka eklendi", id = marka.Id });
        }

        [HttpPost("guncelle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Guncelle([FromBody] MarkaKaydetDto dto)
        {
            if (!await MarkaYonetebilirMi())
                return Forbid();

            if (!dto.Id.HasValue)
                return BadRequest(new { basarili = false, mesaj = "Id zorunludur" });

            var marka = new Ys_Marka
            {
                Id = dto.Id.Value,
                MarkaAdi = dto.MarkaAdi,
                Aciklama = dto.Aciklama,
                AktifMi = dto.AktifMi
            };

            var sonuc = await _service.Guncelle(marka, User.Identity?.Name);
            if (!sonuc)
                return NotFound(new { basarili = false, mesaj = "Marka bulunamadi" });

            return Ok(new { basarili = true, mesaj = "Marka guncellendi" });
        }

        [HttpPost("sil")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
        {
            if (!await MarkaYonetebilirMi())
                return Forbid();

            if (await _service.KullaniliyorMu(dto.Id))
                return BadRequest(new { basarili = false, mesaj = "Bu marka kullanildigi icin silinemez" });

            var sonuc = await _service.Sil(dto.Id, User.Identity?.Name);
            if (!sonuc)
                return NotFound(new { basarili = false, mesaj = "Marka bulunamadi" });

            return Ok(new { basarili = true, mesaj = "Marka silindi" });
        }

        private async Task<bool> MarkaYonetebilirMi()
        {
            if (User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || User.IsInRole("SirketAdmin"))
                return true;

            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(kullaniciId))
                return false;

            var kullanici = await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
            if (kullanici == null)
                return false;

            if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin)
                return true;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.MARKA_YONET));
        }
    }

    public class MarkaKaydetDto
    {
        public int? Id { get; set; }
        public string? MarkaAdi { get; set; }
        public string? Aciklama { get; set; }
        public bool AktifMi { get; set; } = true;
    }

    public class MarkaListeFiltreDto
    {
        public bool TumunuGetir { get; set; }
        public bool? AktifMi { get; set; }
    }
}
