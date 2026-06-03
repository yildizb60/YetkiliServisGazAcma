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
                OlusturmaTarihi = x.OlusturmaTarihi,
                YetkiBelgesiBaslangicTarihi = x.SertifikaBaslangicTarihi,
                YetkiBelgesiBitisTarihi = x.SertifikaBitisTarihi,
                OnayTarihi = x.OnayTarihi,
                OnaylayanKullanici = x.OnaylayanKullanici,
                RedGerekce = x.RedGerekce
            }));
        }

        [HttpPost("firma-ekrani")]
        public async Task<IActionResult> FirmaEkrani([FromBody] IdDto dto)
        {
            var firmaId = dto.Id;
            if (!await FirmaGoruntulemeYetkisiVarMi(firmaId))
                return Forbid();

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sertifikalar)
                .FirstOrDefaultAsync(x => x.Id == firmaId && !x.SilindiMi);

            if (firma == null)
                return NotFound(new { basarili = false, mesaj = "Yetkili servis bulunamadi" });

            var sertifikalar = await _service.FirmaninSertifikalari(firmaId);
            var bildirimler = await FirmaBildirimleriAsync(firmaId, firma);

            return Ok(new YetkiBelgesiFirmaEkraniDto
            {
                Firma = new YetkiBelgesiFirmaDto
                {
                    Id = firma.Id,
                    FirmaAdi = firma.FirmaAdi,
                    YetkiliKisi = firma.YetkiliKisi,
                    VergiNo = firma.VergiNo,
                    FaaliyetIli = firma.FaaliyetIli
                },
                Belgeler = sertifikalar.Select(MapYetkiBelgesi).ToList(),
                Bildirimler = bildirimler
            });
        }

        [HttpPost("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler([FromBody] YetkiBelgesiFiltreDto? dto)
        {
            var sirketId = await KapsamSirketIdAsync(dto?.SirketId);
            if (sirketId.gecersiz)
                return Forbid();

            var sertifikalar = await _service.OnayBekleyenler(sirketId.sirketId);

            return Ok(sertifikalar.Select(MapYetkiBelgesi));
        }

        [HttpPost("onay-ekrani")]
        public async Task<IActionResult> OnayEkrani([FromBody] YetkiBelgesiFiltreDto? dto)
        {
            var sirketId = await KapsamSirketIdAsync(dto?.SirketId);
            if (sirketId.gecersiz)
                return Forbid();

            var sorgu = _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId.sirketId == null || x.Firma.SirketId == sirketId.sirketId));

            var bekleyenler = await sorgu
                .Where(x => x.Durum == 0)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            var onaylananlar = await sorgu
                .Where(x => x.Durum == 1)
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            var reddedilenler = await sorgu
                .Where(x => x.Durum == 2)
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            return Ok(new YetkiBelgesiOnayEkraniDto
            {
                Bekleyenler = bekleyenler.Select(MapYetkiBelgesi).ToList(),
                Onaylananlar = onaylananlar.Select(MapYetkiBelgesi).ToList(),
                Reddedilenler = reddedilenler.Select(MapYetkiBelgesi).ToList()
            });
        }

        [HttpPost("sil")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
        {
            var sertifika = await _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi);

            if (sertifika == null)
                return NotFound(new { basarili = false, mesaj = "Yetki belgesi bulunamadi" });

            if (!await FirmaGoruntulemeYetkisiVarMi(sertifika.FirmaId))
                return Forbid();

            sertifika.SilindiMi = true;
            sertifika.SilinmeTarihi = DateTime.Now;
            sertifika.SilenKullanici = User.Identity?.Name ?? "sistem";
            await _context.SaveChangesAsync();

            return Ok(new { basarili = true, mesaj = "Yetki belgesi silindi" });
        }

        private static YetkiBelgesiDto MapYetkiBelgesi(Ys_Sertifika x)
        {
            return new YetkiBelgesiDto
            {
                Id = x.Id,
                FirmaId = x.FirmaId,
                FirmaAdi = x.Firma?.FirmaAdi,
                SirketId = x.Firma?.SirketId,
                SirketAdi = x.Firma?.Sirket?.SirketAdi,
                DosyaYolu = x.DosyaYolu,
                Durum = x.Durum,
                OlusturmaTarihi = x.OlusturmaTarihi,
                YetkiBelgesiBaslangicTarihi = x.SertifikaBaslangicTarihi,
                YetkiBelgesiBitisTarihi = x.SertifikaBitisTarihi,
                OnayTarihi = x.OnayTarihi,
                OnaylayanKullanici = x.OnaylayanKullanici,
                RedGerekce = x.RedGerekce
            };
        }

        private async Task<List<string>> FirmaBildirimleriAsync(int firmaId, Ys_Firma firma)
        {
            var bildirimler = new List<string>();
            var onayli = firma.Sertifikalar?
                .Where(x => x.Durum == 1 && !x.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .FirstOrDefault();

            var bekleyenVar = firma.Sertifikalar?.Any(x => x.Durum == 0 && !x.SilindiMi) ?? false;
            if (onayli != null)
            {
                bildirimler.Add("Yetki belgeniz onaylandı. Cihaz devreye alabilirsiniz.");
                var kalan = (onayli.SertifikaBitisTarihi.Date - DateTime.Now.Date).Days;
                if (kalan <= 30)
                    bildirimler.Add($"Yetki belgenizin bitmesine {kalan} gün kaldı. Lütfen yenileyin.");
            }

            if (bekleyenVar)
                bildirimler.Add("Yetki belgeniz onay bekliyor. Yetkili onayladıktan sonra işlem yapabilirsiniz.");

            var son7Gun = DateTime.Now.AddDays(-7);
            var sonDevreye = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonDevreye > 0)
                bildirimler.Add($"Son 7 günde {sonDevreye} cihaz devreye alındı.");

            var sonSube = await _context.Ys_Subeler
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonSube > 0)
                bildirimler.Add($"Son 7 günde {sonSube} şube kaydı eklendi.");

            return bildirimler;
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

                return await SertifikaOnayYetkisiVarMi(firmaSirketId.Value);
            }

            return false;
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
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime? YetkiBelgesiBitisTarihi { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanKullanici { get; set; }
        public string? RedGerekce { get; set; }
    }

    public class YetkiBelgesiFirmaEkraniDto
    {
        public YetkiBelgesiFirmaDto? Firma { get; set; }
        public List<YetkiBelgesiDto> Belgeler { get; set; } = new();
        public List<string> Bildirimler { get; set; } = new();
    }

    public class YetkiBelgesiFirmaDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? VergiNo { get; set; }
        public string? FaaliyetIli { get; set; }
    }

    public class YetkiBelgesiOnayEkraniDto
    {
        public List<YetkiBelgesiDto> Bekleyenler { get; set; } = new();
        public List<YetkiBelgesiDto> Onaylananlar { get; set; } = new();
        public List<YetkiBelgesiDto> Reddedilenler { get; set; } = new();
    }
}
