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
        private readonly DagitimSirketService _service;

        public DagitimSirketApiController(AppDbContext context, DagitimSirketService service)
        {
            _context = context;
            _service = service;
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

        [HttpPost("ekle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Ekle([FromBody] DagitimSirketKaydetDto dto)
        {
            if (!await GenelSistemYonetebilirMi())
                return Forbid();

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
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Guncelle([FromBody] DagitimSirketKaydetDto dto)
        {
            if (!dto.Id.HasValue)
                return BadRequest(new { basarili = false, mesaj = "Id zorunludur" });

            if (!await DagitimSirketYonetebilirMi(dto.Id.Value))
                return Forbid();

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
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
        {
            if (!await GenelSistemYonetebilirMi())
                return Forbid();

            var sonuc = await _service.Sil(dto.Id, User.Identity?.Name);
            if (!sonuc)
                return NotFound(new { basarili = false, mesaj = "Sirket bulunamadi" });

            return Ok(new { basarili = true, mesaj = "Sirket silindi" });
        }

        private async Task<bool> GenelSistemYonetebilirMi()
        {
            if (User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || User.IsInRole("SirketAdmin"))
                return true;

            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return false;

            if (kullanici.KullaniciTipi == 4 || kullanici.KullaniciTipi == 3)
                return true;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.DAGITIM_SIRKET_YONET));
        }

        private async Task<bool> DagitimSirketYonetebilirMi(int sirketId)
        {
            if (await GenelSistemYonetebilirMi())
                return true;

            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return false;

            if ((User.IsInRole("SirketAdmin") || kullanici.KullaniciTipi == 3)
                && kullanici.SirketId == sirketId)
                return true;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                x.SirketId == sirketId &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.DAGITIM_SIRKET_YONET));
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
