using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Infrastructure;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/yetki-belgesi")]
    [Authorize]
    public class YetkiBelgesiApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly YetkiBelgesiOkumaApiService _okuma;
        private readonly YetkiBelgesiSilmeApiService _silme;
        private readonly YetkiBelgesiService _service;
        private readonly ILogger<YetkiBelgesiApiController> _logger;
        private readonly IWebHostEnvironment _environment;

        public YetkiBelgesiApiController(
            AppDbContext context,
            YetkiBelgesiService service,
            ILogger<YetkiBelgesiApiController> logger,
            IWebHostEnvironment environment,
            YetkiBelgesiOkumaApiService okuma,
            YetkiBelgesiSilmeApiService silme)
        {
            _context = context;
            _okuma = okuma;
            _silme = silme;
            _service = service;
            _logger = logger;
            _environment = environment;
        }

        [HttpPost("firma-liste")]
        public async Task<IActionResult> FirmaListe([FromBody] IdDto dto)
        {
            if (!await FirmaGoruntulemeYetkisiVarMi(dto.Id))
                return Forbid();

            return Ok(await _okuma.FirmaListeAsync(dto.Id));
        }

        [HttpPost("firma-ekrani")]
        public async Task<IActionResult> FirmaEkrani([FromBody] IdDto dto)
        {
            var firmaId = dto.Id;
            if (!await FirmaGoruntulemeYetkisiVarMi(firmaId))
                return Forbid();

            var sonuc = await _okuma.FirmaEkraniAsync(firmaId);
            return sonuc == null
                ? NotFound(new { basarili = false, mesaj = "Yetkili servis bulunamadi" })
                : Ok(sonuc);
        }

        [HttpPost("yukle")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Yukle([FromForm] YetkiBelgesiYukleDto dto)
        {
            if (dto.BitisTarihi == default)
                return BadRequest(new { basarili = false, mesaj = "Yetki belgesi bitiş tarihi zorunludur." });

            if (dto.Dosya == null || dto.Dosya.Length == 0)
                return BadRequest(new { basarili = false, mesaj = "Lütfen bir dosya seçiniz." });

            if (!await FirmaBelgesiYonetebilirMi(dto.FirmaId))
                return Forbid();

            var publicBaseUrl = $"{Request.Scheme}://{Request.Host}";
            var sonuc = await _service.Yukle(
                dto.FirmaId,
                dto.Dosya,
                dto.BitisTarihi,
                dto.BaslangicTarihi,
                User.Identity?.Name,
                publicBaseUrl);

            if (!sonuc.basarili)
                return BadRequest(new { basarili = false, mesaj = sonuc.mesaj });

            return Ok(new { basarili = true, mesaj = sonuc.mesaj });
        }

        [HttpPost("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler([FromBody] YetkiBelgesiFiltreDto? dto)
        {
            var sirketId = await KapsamSirketIdAsync(dto?.SirketId);
            if (sirketId.gecersiz)
                return Forbid();

            return Ok(await _okuma.OnayBekleyenlerAsync(sirketId.sirketId));
        }

        [HttpPost("onay-ekrani")]
        public async Task<IActionResult> OnayEkrani([FromBody] YetkiBelgesiFiltreDto? dto)
        {
            var sirketId = await KapsamSirketIdAsync(dto?.SirketId);
            if (sirketId.gecersiz)
                return Forbid();

            return Ok(await _okuma.OnayEkraniAsync(sirketId.sirketId, dto, HttpContext.RequestAborted));
        }

        [HttpPost("sil")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
        {
            var yetkiBelgesi = await _context.Ys_YetkiBelgeleri
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi);

            if (yetkiBelgesi == null)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            if (!await FirmaBelgesiYonetebilirMi(yetkiBelgesi.FirmaId))
                return Forbid();

            var hata = await _silme.SilAsync(yetkiBelgesi, User.Identity?.Name);
            return hata != null
                ? Conflict(new { basarili = false, mesaj = hata })
                : Ok(new { basarili = true, mesaj = "Yetki belgesi silindi" });
        }

        [HttpPost("dosya-indir")]
        public async Task<IActionResult> DosyaIndir([FromBody] IdDto dto)
        {
            var yetkiBelgesi = await _context.Ys_YetkiBelgeleri
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi);

            if (yetkiBelgesi == null)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            if (!await FirmaGoruntulemeYetkisiVarMi(yetkiBelgesi.FirmaId))
                return Forbid();

            // Demo content is development-only and follows the same authorization as uploaded documents.
            if (_environment.IsDevelopment()
                && string.Equals(yetkiBelgesi.DosyaYolu, TestDataSeed.DemoYetkiBelgesiDosyaYolu, StringComparison.Ordinal))
            {
                using var resource = typeof(TestDataSeed).Assembly.GetManifestResourceStream("YetkiliServisGazAcma.API.DemoYetkiBelgesi.html");
                if (resource != null)
                {
                    using var content = new MemoryStream();
                    await resource.CopyToAsync(content);
                    Response.Headers.CacheControl = "private, no-store";
                    return File(content.ToArray(), "text/html; charset=utf-8", "Demo_Yetki_Belgesi.html");
                }
            }

            var dosya = _service.DosyaGetir(yetkiBelgesi);
            if (dosya == null)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi dosyasi bulunamadi" });

            Response.Headers.CacheControl = "private, no-store";
            return PhysicalFile(dosya.FizikselYol, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("onayla")]
        public async Task<IActionResult> Onayla([FromBody] IdDto dto)
        {
            var sirketId = await YetkiBelgesiSirketIdAsync(dto.Id);
            if (!sirketId.HasValue)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            if (!await YetkiBelgesiOnayYetkisiVarMi(sirketId.Value))
                return Forbid();

            var sonuc = await _service.Onayla(dto.Id, User.Identity?.Name);
            if (!sonuc)
            {
                _logger.LogWarning("Yetki belgesi onaylanamadi. BelgeId: {BelgeId}, Kullanici: {Kullanici}", dto.Id, User.Identity?.Name);
                return BadRequest(new { basarili = false, mesaj = "Belge suresi dolmus veya daha once degerlendirilmis; onaylanamaz." });
            }

            _logger.LogInformation("Yetki belgesi onaylandi. BelgeId: {BelgeId}, Kullanici: {Kullanici}", dto.Id, User.Identity?.Name);
            return Ok(new { basarili = true, mesaj = "Yetki belgesi onaylandi" });
        }

        [HttpPost("reddet")]
        public async Task<IActionResult> Reddet([FromBody] YetkiBelgesiRedDto dto)
        {
            var sirketId = await YetkiBelgesiSirketIdAsync(dto.Id);
            if (!sirketId.HasValue)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            if (!await YetkiBelgesiOnayYetkisiVarMi(sirketId.Value))
                return Forbid();

            var sonuc = await _service.Reddet(dto.Id, dto.Gerekce, User.Identity?.Name);
            if (!sonuc)
            {
                _logger.LogWarning("Yetki belgesi reddedilemedi. BelgeId: {BelgeId}, Kullanici: {Kullanici}", dto.Id, User.Identity?.Name);
                return BadRequest(new { basarili = false, mesaj = "Belge daha once degerlendirilmis; yeniden reddedilemez." });
            }

            _logger.LogInformation("Yetki belgesi reddedildi. BelgeId: {BelgeId}, Kullanici: {Kullanici}", dto.Id, User.Identity?.Name);
            return Ok(new { basarili = true, mesaj = "Yetki belgesi reddedildi" });
        }

        private async Task<(int? sirketId, bool gecersiz)> KapsamSirketIdAsync(int? istenenSirketId)
        {
            if (User.IsInRole("GenelSistemAdmin") || User.IsInRole("SuperAdmin"))
                return (istenenSirketId, false);

            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var kullanici = await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
            if (kullanici == null)
                return (null, true);

            if (User.IsInRole("SirketAdmin"))
            {
                if (!kullanici.SirketId.HasValue)
                    return (null, true);

                if (istenenSirketId.HasValue && istenenSirketId.Value != kullanici.SirketId.Value)
                    return (null, true);

                return (kullanici.SirketId.Value, false);
            }

            if (User.IsInRole("Personel"))
            {
                if (!istenenSirketId.HasValue)
                    return (null, true);

                return (istenenSirketId.Value, !await YetkiBelgesiOnayYetkisiVarMi(istenenSirketId.Value));
            }

            return (null, true);
        }

        private async Task<int?> YetkiBelgesiSirketIdAsync(int yetkiBelgesiId)
        {
            return await _context.Ys_YetkiBelgeleri
                .Include(x => x.Firma)
                .Where(x => x.Id == yetkiBelgesiId && !x.SilindiMi && x.Firma != null)
                .Select(x => (int?)x.Firma!.SirketId)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> YetkiBelgesiOnayYetkisiVarMi(int sirketId)
        {
            if (User.IsInRole("GenelSistemAdmin") || User.IsInRole("SuperAdmin"))
                return true;

            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var kullanici = await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
            if (kullanici == null)
                return false;

            if (User.IsInRole("SirketAdmin"))
                return kullanici.SirketId == sirketId;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                x.SirketId == sirketId &&
                !x.SilindiMi &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.YETKI_BELGESI_ONAY));
        }

        private async Task<bool> FirmaBelgesiYonetebilirMi(int firmaId)
        {
            if (!User.IsInRole("YetkiliServis"))
                return false;

            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await _context.Users.AnyAsync(x => x.Id == kullaniciId
                && x.AktifMi
                && x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis
                && x.FirmaId == firmaId);
        }

        private async Task<bool> FirmaGoruntulemeYetkisiVarMi(int firmaId)
        {
            if (User.IsInRole("GenelSistemAdmin") || User.IsInRole("SuperAdmin"))
                return true;

            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var kullanici = await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
            if (kullanici == null)
                return false;

            if (kullanici.FirmaId == firmaId)
                return true;

            if (User.IsInRole("SirketAdmin") || User.IsInRole("Personel"))
            {
                var firmaSirketId = await _context.Ys_Firmalar
                    .Where(x => x.Id == firmaId && !x.SilindiMi)
                    .Select(x => (int?)x.SirketId)
                    .FirstOrDefaultAsync();

                if (!firmaSirketId.HasValue)
                    return false;

                if (User.IsInRole("SirketAdmin"))
                    return kullanici.SirketId == firmaSirketId.Value;

                return await YetkiBelgesiOnayYetkisiVarMi(firmaSirketId.Value);
            }

            return false;
        }
    }



    public class YetkiBelgesiYukleDto
    {
        public int FirmaId { get; set; }
        public IFormFile? Dosya { get; set; }
        public DateTime BitisTarihi { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
    }




}
