using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ic-tesisat")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
    public class IcTesisatApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;

        public IcTesisatApiController(AppDbContext context, UserManager<AppKullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost("devreye-almalar/liste")]
        public async Task<IActionResult> DevreyeAlmaListesi([FromBody] IcTesisatDevreyeAlmaFiltreDto? dto)
        {
            dto ??= new IcTesisatDevreyeAlmaFiltreDto();

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadi." });

            var roller = await _userManager.GetRolesAsync(kullanici);
            var genelYetkili = roller.Contains("GenelSistemAdmin") || roller.Contains("SuperAdmin");

            var query = _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (!genelYetkili)
            {
                if (!kullanici.SirketId.HasValue)
                    return Forbid();

                query = query.Where(x => x.Firma != null && x.Firma.SirketId == kullanici.SirketId.Value);
            }
            else if (dto.SirketId.HasValue)
            {
                query = query.Where(x => x.Firma != null && x.Firma.SirketId == dto.SirketId.Value);
            }

            if (!string.IsNullOrWhiteSpace(dto.TesisatNo))
                query = query.Where(x => x.TesistatNo != null && x.TesistatNo.Contains(dto.TesisatNo));

            if (!string.IsNullOrWhiteSpace(dto.YetkiliServis))
                query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(dto.YetkiliServis));

            if (!string.IsNullOrWhiteSpace(dto.Il))
                query = query.Where(x => x.Firma != null && x.Firma.FaaliyetIli != null && x.Firma.FaaliyetIli.Contains(dto.Il));

            if (!string.IsNullOrWhiteSpace(dto.Ilce))
            {
                query = query.Where(x => _context.Ys_Subeler
                    .Any(s => !s.SilindiMi
                        && s.FirmaId == x.FirmaId
                        && s.Ilce != null
                        && s.Ilce.Contains(dto.Ilce)));
            }

            if (!string.IsNullOrWhiteSpace(dto.Marka))
            {
                query = query.Where(x =>
                    (x.CihazMarka != null && x.CihazMarka.Contains(dto.Marka)) ||
                    (x.Marka != null && x.Marka.MarkaAdi != null && x.Marka.MarkaAdi.Contains(dto.Marka)));
            }

            if (dto.BaslangicTarihi.HasValue)
                query = query.Where(x => x.DevreyeAlmaTarihi >= dto.BaslangicTarihi.Value.Date);

            if (dto.BitisTarihi.HasValue)
                query = query.Where(x => x.DevreyeAlmaTarihi < dto.BitisTarihi.Value.Date.AddDays(1));

            var toplam = await query.CountAsync();
            var sayfa = Math.Max(dto.Sayfa, 1);
            var sayfaBoyutu = Math.Clamp(dto.SayfaBoyutu <= 0 ? 100 : dto.SayfaBoyutu, 1, 500);

            var islemler = await query
                .OrderByDescending(x => x.DevreyeAlmaTarihi)
                .ThenByDescending(x => x.Id)
                .Skip((sayfa - 1) * sayfaBoyutu)
                .Take(sayfaBoyutu)
                .ToListAsync();

            var firmaIds = islemler.Select(x => x.FirmaId).Distinct().ToList();
            var subeIlceleri = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi && firmaIds.Contains(x.FirmaId))
                .OrderBy(x => x.SubeAdi)
                .Select(x => new
                {
                    x.FirmaId,
                    x.Ilce
                })
                .ToListAsync();

            var ilceler = subeIlceleri
                .GroupBy(x => x.FirmaId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(s => s.Ilce).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "");

            return Ok(new IcTesisatDevreyeAlmaListeDto
            {
                Toplam = toplam,
                Sayfa = sayfa,
                SayfaBoyutu = sayfaBoyutu,
                Islemler = islemler.Select(x => new IcTesisatDevreyeAlmaDto
                {
                    Id = x.Id,
                    TesisatNo = x.TesistatNo,
                    YetkiliServis = x.Firma?.FirmaAdi,
                    Il = x.Firma?.FaaliyetIli,
                    Ilce = ilceler.TryGetValue(x.FirmaId, out var ilce) ? ilce : "",
                    Tarih = x.DevreyeAlmaTarihi,
                    Marka = x.CihazMarka ?? x.Marka?.MarkaAdi,
                    CihazTipi = x.CihazTipi,
                    CihazModeli = x.CihazModeli,
                    CihazKapasite = x.CihazKapasite,
                    MusteriAdi = x.MusteriAdi,
                    Durum = x.Durum
                }).ToList()
            });
        }
    }

    public class IcTesisatDevreyeAlmaFiltreDto
    {
        public int? SirketId { get; set; }
        public string? TesisatNo { get; set; }
        public string? YetkiliServis { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? Marka { get; set; }
        public int Sayfa { get; set; } = 1;
        public int SayfaBoyutu { get; set; } = 100;
    }

    public class IcTesisatDevreyeAlmaListeDto
    {
        public int Toplam { get; set; }
        public int Sayfa { get; set; }
        public int SayfaBoyutu { get; set; }
        public List<IcTesisatDevreyeAlmaDto> Islemler { get; set; } = new();
    }

    public class IcTesisatDevreyeAlmaDto
    {
        public int Id { get; set; }
        public string? TesisatNo { get; set; }
        public string? YetkiliServis { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public DateTime Tarih { get; set; }
        public string? Marka { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazModeli { get; set; }
        public string? CihazKapasite { get; set; }
        public string? MusteriAdi { get; set; }
        public int Durum { get; set; }
    }
}
