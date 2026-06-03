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
    [Route("api/yetki-belgesi")]
    [Authorize]
    public class YetkiBelgesiApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SertifikaService _service;

        public YetkiBelgesiApiController(AppDbContext context, SertifikaService service)
        {
            _context = context;
            _service = service;
        }

        [HttpPost("firma-liste")]
        public async Task<IActionResult> FirmaListe([FromBody] IdDto dto)
        {
            var sertifikalar = await _service.FirmaninSertifikalari(dto.Id);

            return Ok(sertifikalar.Select(x => new YetkiBelgesiDto
            {
                Id = x.Id,
                FirmaId = x.FirmaId,
                DosyaYolu = x.DosyaYolu,
                Durum = x.Durum,
                YetkiBelgesiBaslangicTarihi = x.SertifikaBaslangicTarihi,
                YetkiBelgesiBitisTarihi = x.SertifikaBitisTarihi,
                OnayTarihi = x.OnayTarihi,
                OnaylayanKullanici = x.OnaylayanKullanici,
                RedGerekce = x.RedGerekce
            }));
        }

        [HttpPost("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler([FromBody] YetkiBelgesiFiltreDto? dto)
        {
            var sirketId = await KapsamSirketIdAsync(dto?.SirketId);
            if (sirketId.gecersiz)
                return Forbid();

            var sertifikalar = await _service.OnayBekleyenler(sirketId.sirketId);

            return Ok(sertifikalar.Select(x => new YetkiBelgesiDto
            {
                Id = x.Id,
                FirmaId = x.FirmaId,
                FirmaAdi = x.Firma?.FirmaAdi,
                SirketId = x.Firma?.SirketId,
                SirketAdi = x.Firma?.Sirket?.SirketAdi,
                DosyaYolu = x.DosyaYolu,
                Durum = x.Durum,
                YetkiBelgesiBaslangicTarihi = x.SertifikaBaslangicTarihi,
                YetkiBelgesiBitisTarihi = x.SertifikaBitisTarihi,
                OnayTarihi = x.OnayTarihi,
                OnaylayanKullanici = x.OnaylayanKullanici,
                RedGerekce = x.RedGerekce
            }));
        }

        [HttpPost("onayla")]
        public async Task<IActionResult> Onayla([FromBody] IdDto dto)
        {
            var sirketId = await SertifikaSirketIdAsync(dto.Id);
            if (!sirketId.HasValue)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            if (!await SertifikaOnayYetkisiVarMi(sirketId.Value))
                return Forbid();

            var sonuc = await _service.Onayla(dto.Id, User.Identity?.Name);
            if (!sonuc)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            return Ok(new { basarili = true, mesaj = "Yetki belgesi onaylandi" });
        }

        [HttpPost("reddet")]
        public async Task<IActionResult> Reddet([FromBody] YetkiBelgesiRedDto dto)
        {
            var sirketId = await SertifikaSirketIdAsync(dto.Id);
            if (!sirketId.HasValue)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            if (!await SertifikaOnayYetkisiVarMi(sirketId.Value))
                return Forbid();

            var sonuc = await _service.Reddet(dto.Id, dto.Gerekce, User.Identity?.Name);
            if (!sonuc)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

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

                return (istenenSirketId.Value, !await SertifikaOnayYetkisiVarMi(istenenSirketId.Value));
            }

            return (null, true);
        }

        private async Task<int?> SertifikaSirketIdAsync(int sertifikaId)
        {
            return await _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                .Where(x => x.Id == sertifikaId && !x.SilindiMi && x.Firma != null)
                .Select(x => (int?)x.Firma!.SirketId)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> SertifikaOnayYetkisiVarMi(int sirketId)
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
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.CERTIFIKA_ONAY));
        }
    }

    public class YetkiBelgesiFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class YetkiBelgesiRedDto
    {
        public int Id { get; set; }
        public string? Gerekce { get; set; }
    }

    public class YetkiBelgesiDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public int? SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public string? DosyaYolu { get; set; }
        public int Durum { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime? YetkiBelgesiBitisTarihi { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanKullanici { get; set; }
        public string? RedGerekce { get; set; }
    }
}
