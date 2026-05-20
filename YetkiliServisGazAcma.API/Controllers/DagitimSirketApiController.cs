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
        private readonly DagitimSirketService _service;

        public DagitimSirketApiController(AppDbContext context, DagitimSirketService service)
        {
            _context = context;
            _service = service;
        }

        // Herkese açık — liste
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

        // Token gerekli — tek kayıt
        [HttpPost("getir")]
        [Authorize]
        public async Task<IActionResult> Getir([FromBody] IdDto dto)
        {
            var sirket = await _context.Dag_Sirketler
                .Where(x => x.Id == dto.Id && !x.SilindiMi)
                .Select(x => new
                {
                    x.Id,
                    x.SirketAdi,
                    x.Il,
                    x.Telefon,
                    x.Email,
                    x.Adres
                })
                .FirstOrDefaultAsync();

            if (sirket == null)
                return NotFound(new { mesaj = "Sirket bulunamadı" });

            return Ok(sirket);
        }

        [HttpPost("ekle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin")]
        public async Task<IActionResult> Ekle([FromBody] DagitimSirketKaydetDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SirketAdi))
                return BadRequest(new { basarili = false, mesaj = "Sirket adi zorunludur" });

            var sirket = new Dag_Sirket
            {
                SirketAdi = dto.SirketAdi,
                Il = dto.Il,
                Telefon = dto.Telefon,
                Email = dto.Email,
                Adres = dto.Adres,
                AktifMi = dto.AktifMi
            };

            await _service.Ekle(sirket, User.Identity?.Name);
            return Ok(new { basarili = true, mesaj = "Sirket eklendi", id = sirket.Id });
        }

        [HttpPost("guncelle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin")]
        public async Task<IActionResult> Guncelle([FromBody] DagitimSirketKaydetDto dto)
        {
            if (!dto.Id.HasValue)
                return BadRequest(new { basarili = false, mesaj = "Id zorunludur" });

            var sirket = new Dag_Sirket
            {
                Id = dto.Id.Value,
                SirketAdi = dto.SirketAdi,
                Il = dto.Il,
                Telefon = dto.Telefon,
                Email = dto.Email,
                Adres = dto.Adres,
                AktifMi = dto.AktifMi
            };

            var sonuc = await _service.Guncelle(sirket, User.Identity?.Name);
            if (!sonuc)
                return NotFound(new { basarili = false, mesaj = "Sirket bulunamadi" });

            return Ok(new { basarili = true, mesaj = "Sirket guncellendi" });
        }

        [HttpPost("sil")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
        {
            var sonuc = await _service.Sil(dto.Id, User.Identity?.Name);
            if (!sonuc)
                return NotFound(new { basarili = false, mesaj = "Sirket bulunamadi" });

            return Ok(new { basarili = true, mesaj = "Sirket silindi" });
        }
    }

    public class IdDto
    {
        public int Id { get; set; }
    }

    public class DagitimSirketKaydetDto
    {
        public int? Id { get; set; }
        public string? SirketAdi { get; set; }
        public string? Il { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; } = true;
    }

    public class DagitimSirketListeFiltreDto
    {
        public bool TumunuGetir { get; set; }
        public bool? AktifMi { get; set; }
    }
}
