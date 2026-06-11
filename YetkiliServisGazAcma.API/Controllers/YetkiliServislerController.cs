using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/yetkili-servisler")]
    public class YetkiliServislerController : ControllerBase
    {
        private static readonly CultureInfo TrCulture = new("tr-TR");

        private readonly AppDbContext _context;
        private readonly YetkiliServisService _yetkiliServisService;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public YetkiliServislerController(
            AppDbContext context,
            YetkiliServisService yetkiliServisService,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _context = context;
            _yetkiliServisService = yetkiliServisService;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        [HttpPost("liste")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        public async Task<IActionResult> Liste([FromBody] YetkiliServisFiltreDto? dto)
        {
            return Ok(await ListeleAsync(dto));
        }

        [HttpGet]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        public async Task<IActionResult> ListeGet([FromQuery] YetkiliServisFiltreDto? dto)
        {
            return Ok(await ListeleAsync(dto));
        }

        private async Task<List<YetkiliServisDto>> ListeleAsync(YetkiliServisFiltreDto? dto)
        {
            var il = dto?.Il;
            var ilce = dto?.Ilce;
            var markaId = dto?.MarkaId;
            var kategoriId = dto?.KategoriId;
            var sirketId = dto?.SirketId;
            var q = dto?.Q;

            var query = _context.Ys_Firmalar
                .Include(x => x.FirmaMarkalar!)
                    .ThenInclude(x => x.Marka)
                .Include(x => x.FirmaKategoriler!)
                    .ThenInclude(x => x.Kategori)
                .Include(x => x.Subeler!)
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi && x.AktifMi)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(il))
                query = query.Where(x =>
                    x.FaaliyetIli == il ||
                    (x.Subeler != null && x.Subeler.Any(s => !s.SilindiMi && s.AktifMi && s.Il == il)));

            if (!string.IsNullOrWhiteSpace(ilce))
                query = query.Where(x =>
                    x.Subeler != null && x.Subeler.Any(s => !s.SilindiMi && s.AktifMi && s.Ilce == ilce));

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x =>
                    (x.FirmaAdi ?? "").Contains(q) ||
                    (x.YetkiliKisi ?? "").Contains(q));

            if (markaId.HasValue)
                query = query.Where(x => x.FirmaMarkalar!.Any(m => !m.SilindiMi && m.MarkaId == markaId.Value));

            if (kategoriId.HasValue)
                query = query.Where(x => x.FirmaKategoriler!.Any(k => !k.SilindiMi && k.KategoriId == kategoriId.Value));

            if (sirketId.HasValue)
                query = query.Where(x => x.SirketId == sirketId.Value);

            return await query
                .OrderBy(x => x.FirmaAdi)
                .Select(x => new YetkiliServisDto
                {
                    Id = x.Id,
                    FirmaAdi = x.FirmaAdi,
                    YetkiliKisi = x.YetkiliKisi,
                    Telefon = x.Telefon,
                    Email = x.Email,
                    Adres = x.Adres,
                    FaaliyetIli = x.FaaliyetIli,
                    Ilce = x.Subeler!
                        .Where(s => !s.SilindiMi && s.AktifMi && s.Ilce != null && s.Ilce != "")
                        .Select(s => s.Ilce)
                        .FirstOrDefault(),
                    SirketId = x.SirketId,
                    SirketAdi = x.Sirket != null ? x.Sirket.SirketAdi : null,
                    Markalar = x.FirmaMarkalar!
                        .Where(m => !m.SilindiMi)
                        .Select(m => m.Marka!.MarkaAdi)
                        .Where(m => m != null)
                        .Select(m => m!)
                        .Distinct()
                        .ToList(),
                    Kategoriler = x.FirmaKategoriler!
                        .Where(k => !k.SilindiMi && k.Kategori != null)
                        .Select(k => new KategoriDto
                        {
                            Id = k.Kategori!.Id,
                            Ad = k.Kategori.Ad,
                            IconUrl = k.Kategori.IconUrl
                        })
                        .GroupBy(k => k.Id)
                        .Select(g => g.First())
                        .ToList()
                })
                .ToListAsync();
        }

        [HttpPost("filtre-secenekleri")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        public async Task<IActionResult> FiltreSecenekleri([FromBody] YetkiliServisFiltreSecenekleriIstek? dto)
        {
            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .Select(x => new YetkiliServisMarkaSecenekDto
                {
                    Id = x.Id,
                    MarkaAdi = x.MarkaAdi
                })
                .ToListAsync();

            var kategoriler = await _context.UrunKategoriler
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .Select(x => new KategoriDto
                {
                    Id = x.Id,
                    Ad = x.Ad,
                    IconUrl = x.IconUrl,
                    SiraNo = x.SiraNo,
                    AktifMi = x.AktifMi
                })
                .ToListAsync();

            var illerRaw = await _context.Ys_Firmalar
                .Where(x => !x.SilindiMi && x.AktifMi && x.FaaliyetIli != null && x.FaaliyetIli != "")
                .Select(x => x.FaaliyetIli!)
                .ToListAsync();

            var subeIllerRaw = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi
                    && x.AktifMi
                    && x.Il != null
                    && x.Il != ""
                    && _context.Ys_Firmalar.Any(f => f.Id == x.FirmaId && !f.SilindiMi && f.AktifMi))
                .Select(x => x.Il!)
                .ToListAsync();

            var dagitimIllerRaw = await _context.Dag_Sirketler
                .Where(x => x.AktifMi && x.Il != null && x.Il != "")
                .Select(x => x.Il!)
                .ToListAsync();

            var iller = illerRaw
                .Concat(subeIllerRaw)
                .Concat(dagitimIllerRaw)
                .Select(NormalizeKonum)
                .Where(GecerliKonumMu)
                .GroupBy(NormalizeKonumKey)
                .Select(g => TitleCaseTr(g.First()))
                .OrderBy(x => x)
                .ToList();

            var ilcelerQuery = _context.Ys_Subeler
                .Where(x => !x.SilindiMi
                    && x.AktifMi
                    && x.Ilce != null
                    && x.Ilce != ""
                    && _context.Ys_Firmalar.Any(f => f.Id == x.FirmaId && !f.SilindiMi && f.AktifMi));

            if (!string.IsNullOrWhiteSpace(dto?.Il))
                ilcelerQuery = ilcelerQuery.Where(x => x.Il == dto.Il);

            var ilceler = (await ilcelerQuery
                    .Select(x => x.Ilce!)
                    .ToListAsync())
                .Select(NormalizeKonum)
                .Where(GecerliKonumMu)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return Ok(new YetkiliServisFiltreSecenekleriDto
            {
                Markalar = markalar,
                Kategoriler = kategoriler,
                Iller = iller,
                Ilceler = ilceler
            });
        }

        [HttpPost("kayit")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        public async Task<IActionResult> Kayit([FromBody] YetkiliServisBasvuruDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return BadRequest(new { basarili = false, mesaj = "Firma adi zorunludur" });

            if (string.IsNullOrWhiteSpace(dto.VergiNo))
                return BadRequest(new { basarili = false, mesaj = "VKN zorunludur" });

            if (string.IsNullOrWhiteSpace(dto.Sifre))
                return BadRequest(new { basarili = false, mesaj = "Sifre zorunludur" });

            var firma = new Ys_Firma
            {
                FirmaAdi = dto.FirmaAdi,
                YetkiliKisi = dto.YetkiliKisi,
                Telefon = dto.Telefon,
                Email = dto.Email,
                Adres = dto.Adres,
                FaaliyetIli = dto.FaaliyetIli,
                VergiNo = dto.VergiNo,
                VergiDairesi = dto.VergiDairesi,
                SirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    dto.FaaliyetIli,
                    dto.Email ?? dto.VergiNo ?? "api-kayit")
            };

            var sonuc = await _yetkiliServisService.Kayit(
                firma,
                dto.Sifre,
                dto.MarkaIdleri ?? new List<int>(),
                dto.KategoriIdleri ?? new List<int>());

            if (!sonuc.basarili)
                return BadRequest(new { basarili = false, mesaj = sonuc.mesaj });

            return Ok(new { basarili = true, mesaj = sonuc.mesaj, firmaId = firma.Id });
        }

        [HttpPost("getir")]
        [Authorize]
        public async Task<IActionResult> Getir([FromBody] IdDto dto)
        {
            var servis = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.FirmaMarkalar!)
                    .ThenInclude(x => x.Marka)
                .Include(x => x.FirmaKategoriler!)
                    .ThenInclude(x => x.Kategori)
                .Where(x => x.Id == dto.Id && !x.SilindiMi)
                .Select(x => new YetkiliServisDetayDto
                {
                    Id = x.Id,
                    FirmaAdi = x.FirmaAdi,
                    YetkiliKisi = x.YetkiliKisi,
                    Telefon = x.Telefon,
                    Email = x.Email,
                    Adres = x.Adres,
                    FaaliyetIli = x.FaaliyetIli,
                    VergiNo = x.VergiNo,
                    VergiDairesi = x.VergiDairesi,
                    SirketId = x.SirketId,
                    SirketAdi = x.Sirket != null ? x.Sirket.SirketAdi : null,
                    AktifMi = x.AktifMi,
                    MarkaIds = x.FirmaMarkalar!
                        .Where(m => !m.SilindiMi)
                        .Select(m => m.MarkaId)
                        .Distinct()
                        .ToList(),
                    KategoriIds = x.FirmaKategoriler!
                        .Where(k => !k.SilindiMi)
                        .Select(k => k.KategoriId)
                        .Distinct()
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (servis == null)
                return NotFound(new { basarili = false, mesaj = "Yetkili servis bulunamadi" });

            return Ok(servis);
        }

        [HttpPost("guncelle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin")]
        public async Task<IActionResult> Guncelle([FromBody] YetkiliServisKaydetDto dto)
        {
            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi);

            if (servis == null)
                return NotFound(new { basarili = false, mesaj = "Yetkili servis bulunamadi" });

            servis.FirmaAdi = dto.FirmaAdi;
            servis.YetkiliKisi = dto.YetkiliKisi;
            servis.Telefon = dto.Telefon;
            servis.Email = dto.Email;
            servis.Adres = dto.Adres;
            servis.FaaliyetIli = dto.FaaliyetIli;
            servis.VergiNo = dto.VergiNo;
            servis.VergiDairesi = dto.VergiDairesi;
            servis.SirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                dto.FaaliyetIli,
                User.Identity?.Name ?? "api");
            servis.AktifMi = dto.AktifMi;
            servis.GuncellemeTarihi = DateTime.Now;
            servis.GuncelleyenKullanici = User.Identity?.Name ?? "api";

            if (dto.KategoriIds != null)
            {
                var mevcutKategoriler = await _context.Ys_FirmaKategoriler
                    .Where(x => x.FirmaId == servis.Id)
                    .ToListAsync();

                _context.Ys_FirmaKategoriler.RemoveRange(mevcutKategoriler);

                foreach (var kategoriId in dto.KategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = servis.Id,
                        KategoriId = kategoriId,
                        YetkiBitisTarihi = DateTime.Now.AddYears(1),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = User.Identity?.Name ?? "api",
                        SilindiMi = false
                    });
                }
            }

            if (dto.MarkaIds != null)
            {
                var mevcutMarkalar = await _context.Ys_FirmaMarkalar
                    .Where(x => x.FirmaId == servis.Id)
                    .ToListAsync();

                _context.Ys_FirmaMarkalar.RemoveRange(mevcutMarkalar);

                foreach (var markaId in dto.MarkaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = servis.Id,
                        MarkaId = markaId,
                        YetkiBitisTarihi = DateTime.Now.AddYears(1),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = User.Identity?.Name ?? "api",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { basarili = true, mesaj = "Yetkili servis guncellendi" });
        }

        [HttpPost("sil")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
        {
            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi);

            if (servis == null)
                return NotFound(new { basarili = false, mesaj = "Yetkili servis bulunamadi" });

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == dto.Id);

            if (devreyeAlmaVar)
                return BadRequest(new { basarili = false, mesaj = "Bu yetkili servis uzerinde devreye alma islemi oldugu icin silinemez" });

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = User.Identity?.Name ?? "api";
            await _context.SaveChangesAsync();

            return Ok(new { basarili = true, mesaj = "Yetkili servis silindi" });
        }

        private static string NormalizeKonum(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var trimmed = value.Trim();
            while (trimmed.Contains("  ")) trimmed = trimmed.Replace("  ", " ");
            return trimmed;
        }

        private static bool GecerliKonumMu(string? value)
        {
            var norm = NormalizeKonum(value);
            return norm.Length >= 2 && norm.Any(char.IsLetter);
        }

        private static string NormalizeKonumKey(string? value)
        {
            var norm = NormalizeKonum(value);
            if (string.IsNullOrWhiteSpace(norm)) return "";
            var lower = norm.ToLower(TrCulture);
            var keyChars = lower.Where(char.IsLetterOrDigit).ToArray();
            return new string(keyChars);
        }

        private static string TitleCaseTr(string value)
        {
            var trimmed = NormalizeKonum(value);
            if (string.IsNullOrWhiteSpace(trimmed)) return "";
            var lower = trimmed.ToLower(TrCulture);
            return TrCulture.TextInfo.ToTitleCase(lower);
        }
    }

    public class YetkiliServisDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? Ilce { get; set; }
        public int SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public List<string> Markalar { get; set; } = new();
        public List<KategoriDto> Kategoriler { get; set; } = new();
    }

    public class KategoriDto
    {
        public int Id { get; set; }
        public string? Ad { get; set; }
        public string? IconUrl { get; set; }
        public int SiraNo { get; set; }
        public bool AktifMi { get; set; }
    }

    public class YetkiliServisFiltreSecenekleriIstek
    {
        public string? Il { get; set; }
    }

    public class YetkiliServisFiltreSecenekleriDto
    {
        public List<YetkiliServisMarkaSecenekDto> Markalar { get; set; } = new();
        public List<KategoriDto> Kategoriler { get; set; } = new();
        public List<string> Iller { get; set; } = new();
        public List<string> Ilceler { get; set; } = new();
    }

    public class YetkiliServisMarkaSecenekDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class YetkiliServisFiltreDto
    {
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public int? SirketId { get; set; }
        public int? MarkaId { get; set; }
        public int? KategoriId { get; set; }
        public string? Q { get; set; }
    }

    public class YetkiliServisDetayDto : YetkiliServisDto
    {
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public bool AktifMi { get; set; }
        public List<int> MarkaIds { get; set; } = new();
        public List<int> KategoriIds { get; set; } = new();
    }

    public class YetkiliServisKaydetDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public bool AktifMi { get; set; } = true;
        public List<int>? MarkaIds { get; set; }
        public List<int>? KategoriIds { get; set; }
    }

    public class YetkiliServisBasvuruDto
    {
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public string Sifre { get; set; } = string.Empty;
        public List<int>? MarkaIdleri { get; set; }
        public List<int>? KategoriIdleri { get; set; }
    }
}
