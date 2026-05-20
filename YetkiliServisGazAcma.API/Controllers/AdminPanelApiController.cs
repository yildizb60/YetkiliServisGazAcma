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
    [Route("api/admin-panel")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
    public class AdminPanelApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminDashboardService _dashboardService;
        private readonly AdminYetkiliServisListeService _yetkiliServisListeService;

        public AdminPanelApiController(
            AppDbContext context,
            AdminDashboardService dashboardService,
            AdminYetkiliServisListeService yetkiliServisListeService)
        {
            _context = context;
            _dashboardService = dashboardService;
            _yetkiliServisListeService = yetkiliServisListeService;
        }

        [HttpPost("dashboard")]
        public async Task<IActionResult> Dashboard([FromBody] AdminDashboardFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var ozet = await _dashboardService.GetirAsync(kapsam.sirketId);

            return Ok(new AdminDashboardApiDto
            {
                ToplamDevreyeAlma = ozet.ToplamDevreyeAlma,
                ToplamFirma = ozet.ToplamFirma,
                OnayBekleyen = ozet.OnayBekleyen,
                SuresiBitecek = ozet.SuresiBitecek,
                ToplamSirket = ozet.ToplamSirket,
                BuAyDevreyeAlma = ozet.BuAyDevreyeAlma,
                SonSertifikalar = ozet.SonSertifikalar.Select(x => new AdminSertifikaOzetDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    FirmaAdi = x.Firma?.FirmaAdi,
                    SirketAdi = x.Firma?.Sirket?.SirketAdi,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    SertifikaBitisTarihi = x.SertifikaBitisTarihi
                }).ToList(),
                SonDevreyeAlmalar = ozet.SonDevreyeAlmalar.Select(x => new AdminDevreyeAlmaOzetDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    FirmaAdi = x.Firma?.FirmaAdi,
                    MarkaAdi = x.Marka?.MarkaAdi,
                    TesistatNo = x.TesistatNo,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi
                }).ToList()
            });
        }

        [HttpPost("kullanicilar/liste")]
        public async Task<IActionResult> Kullanicilar([FromBody] AdminKullaniciListeFiltreDto? dto)
        {
            var yapan = await AktifKullaniciAsync();
            if (yapan == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(yapan, kapsam.sirketId))
                return Forbid();

            var genelSistemAdmin = User.IsInRole("GenelSistemAdmin") || User.IsInRole("SuperAdmin");
            var kullaniciQuery = _context.Users
                .Include(x => x.Sirket)
                .Include(x => x.Firma)
                .AsQueryable();

            if (!genelSistemAdmin || kapsam.sirketId.HasValue)
            {
                kullaniciQuery = kullaniciQuery.Where(x =>
                    x.Id == yapan.Id ||
                    ((x.KullaniciTipi == 2 || x.KullaniciTipi == 3) && kapsam.sirketId.HasValue && x.SirketId == kapsam.sirketId.Value) ||
                    (x.KullaniciTipi == 1 && x.Firma != null && kapsam.sirketId.HasValue && x.Firma.SirketId == kapsam.sirketId.Value));
            }

            var kullanicilar = await kullaniciQuery
                .OrderBy(x => x.AdSoyad)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(dto?.Q))
            {
                var aranacak = dto.Q.Trim();
                kullanicilar = kullanicilar
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.AdSoyad) && x.AdSoyad.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.Email) && x.Email.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.PhoneNumber) && x.PhoneNumber.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(dto?.Tip))
            {
                kullanicilar = dto.Tip switch
                {
                    "GenelSistemAdmin" => kullanicilar.Where(x => x.KullaniciTipi == 4).ToList(),
                    "SirketAdmin" => kullanicilar.Where(x => x.KullaniciTipi == 3).ToList(),
                    "SuperAdmin" => kullanicilar.Where(x => x.KullaniciTipi == 4).ToList(),
                    "Personel" => kullanicilar.Where(x => x.KullaniciTipi == 2).ToList(),
                    "Servis" => kullanicilar.Where(x => x.KullaniciTipi == 1).ToList(),
                    _ => kullanicilar
                };
            }

            if (!string.IsNullOrWhiteSpace(dto?.Durum))
            {
                var aktifMi = dto.Durum.Equals("Aktif", StringComparison.OrdinalIgnoreCase);
                kullanicilar = kullanicilar.Where(x => x.AktifMi == aktifMi).ToList();
            }

            if (!string.IsNullOrWhiteSpace(dto?.Bagli))
            {
                var aranacak = dto.Bagli.Trim();
                kullanicilar = kullanicilar
                    .Where(x =>
                        (x.KullaniciTipi == 1 && x.Firma != null && !string.IsNullOrWhiteSpace(x.Firma.FirmaAdi) &&
                         x.Firma.FirmaAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        ((x.KullaniciTipi == 2 || x.KullaniciTipi == 3) && x.Sirket != null && !string.IsNullOrWhiteSpace(x.Sirket.SirketAdi) &&
                         x.Sirket.SirketAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)))
                    .ToList();
            }

            return Ok(kullanicilar.Select(x => new AdminKullaniciListeDto
            {
                Id = x.Id,
                AdSoyad = !string.IsNullOrWhiteSpace(x.AdSoyad)
                    ? x.AdSoyad
                    : x.Firma?.YetkiliKisi ?? x.Firma?.FirmaAdi,
                Email = !string.IsNullOrWhiteSpace(x.Email) ? x.Email : x.Firma?.Email,
                PhoneNumber = !string.IsNullOrWhiteSpace(x.PhoneNumber) ? x.PhoneNumber : x.Firma?.Telefon,
                KullaniciTipi = x.KullaniciTipi,
                AktifMi = x.AktifMi,
                SirketId = x.SirketId,
                SirketAdi = x.Sirket?.SirketAdi,
                FirmaId = x.FirmaId,
                FirmaAdi = x.Firma?.FirmaAdi,
                FirmaYetkiliKisi = x.Firma?.YetkiliKisi,
                FirmaEmail = x.Firma?.Email,
                FirmaTelefon = x.Firma?.Telefon
            }).ToList());
        }

        [HttpPost("yetkili-servisler/liste")]
        public async Task<IActionResult> YetkiliServisler([FromBody] AdminYetkiliServisListeFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var sonuc = await _yetkiliServisListeService.ListeleAsync(new AdminYetkiliServisListeFiltre
            {
                SirketId = kapsam.sirketId,
                Q = dto?.Q,
                Il = dto?.Il,
                Durum = dto?.Durum,
                DevreyeSiralama = dto?.DevreyeSiralama
            });

            return Ok(new AdminYetkiliServisListeDto
            {
                Servisler = sonuc.Servisler.Select(x => new AdminYetkiliServisDto
                {
                    Id = x.Id,
                    FirmaAdi = x.FirmaAdi,
                    YetkiliKisi = x.YetkiliKisi,
                    VergiNo = x.VergiNo,
                    Telefon = x.Telefon,
                    Email = x.Email,
                    Adres = x.Adres,
                    FaaliyetIli = x.FaaliyetIli,
                    AktifMi = x.AktifMi,
                    SirketId = x.SirketId,
                    SirketAdi = x.Sirket?.SirketAdi
                }).ToList(),
                DevreyeSayilari = sonuc.DevreyeSayilari
            });
        }

        [HttpPost("yetkili-servisler/getir")]
        public async Task<IActionResult> YetkiliServisGetir([FromBody] AdminYetkiliServisGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Yetkili servis id zorunludur" });

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var sonuc = await _yetkiliServisListeService.GetirAsync(dto.Id, kapsam.sirketId);
            if (sonuc.Servis == null)
                return NotFound(new { basarili = false, mesaj = "Yetkili servis bulunamadi" });

            return Ok(new AdminYetkiliServisDetayDto
            {
                Servis = new AdminYetkiliServisDto
                {
                    Id = sonuc.Servis.Id,
                    FirmaAdi = sonuc.Servis.FirmaAdi,
                    YetkiliKisi = sonuc.Servis.YetkiliKisi,
                    VergiNo = sonuc.Servis.VergiNo,
                    Telefon = sonuc.Servis.Telefon,
                    Email = sonuc.Servis.Email,
                    Adres = sonuc.Servis.Adres,
                    FaaliyetIli = sonuc.Servis.FaaliyetIli,
                    AktifMi = sonuc.Servis.AktifMi,
                    SirketId = sonuc.Servis.SirketId,
                    SirketAdi = sonuc.Servis.Sirket?.SirketAdi,
                    Kategoriler = sonuc.Servis.FirmaKategoriler?
                        .Where(x => !x.SilindiMi && x.Kategori != null)
                        .Select(x => new AdminYetkiliServisKategoriDto
                        {
                            Id = x.Kategori!.Id,
                            Ad = x.Kategori.Ad,
                            IconUrl = x.Kategori.IconUrl
                        })
                        .GroupBy(x => x.Id)
                        .Select(x => x.First())
                        .ToList() ?? new List<AdminYetkiliServisKategoriDto>()
                },
                Sertifikalar = sonuc.Sertifikalar.Select(x => new AdminYetkiliServisSertifikaDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    SertifikaBaslangicTarihi = x.SertifikaBaslangicTarihi,
                    SertifikaBitisTarihi = x.SertifikaBitisTarihi
                }).ToList(),
                Subeler = sonuc.Subeler.Select(x => new AdminYetkiliServisSubeDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    SubeAdi = x.SubeAdi,
                    Il = x.Il,
                    Ilce = x.Ilce,
                    Telefon = x.Telefon
                }).ToList(),
                Devreye = sonuc.Devreye.Select(x => new AdminYetkiliServisDevreyeDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    TesistatNo = x.TesistatNo,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    MarkaAdi = x.Marka?.MarkaAdi
                }).ToList()
            });
        }

        private async Task<(int? sirketId, bool gecersiz)> KapsamSirketIdAsync(int? istenenSirketId)
        {
            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var kullanici = await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
            if (kullanici == null)
                return (null, true);

            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == 4
                || (kullanici.KullaniciTipi == 3 && !kullanici.SirketId.HasValue);

            if (genelSistemAdminMi)
                return (istenenSirketId, false);

            var sirketAdminMi = User.IsInRole("SirketAdmin")
                || (kullanici.KullaniciTipi == 3 && kullanici.SirketId.HasValue);

            if (sirketAdminMi)
            {
                if (!kullanici.SirketId.HasValue)
                    return (null, true);

                if (istenenSirketId.HasValue && istenenSirketId.Value != kullanici.SirketId.Value)
                    return (null, true);

                return (kullanici.SirketId.Value, false);
            }

            var yetkiQuery = _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi);

            if (istenenSirketId.HasValue)
            {
                var yetkiliMi = await yetkiQuery.AnyAsync(x => x.SirketId == istenenSirketId.Value);
                return (istenenSirketId.Value, !yetkiliMi);
            }

            var ilkSirketId = await yetkiQuery
                .OrderBy(x => x.SirketId)
                .Select(x => (int?)x.SirketId)
                .FirstOrDefaultAsync();

            return (ilkSirketId, !ilkSirketId.HasValue);
        }

        private async Task<AppKullanici?> AktifKullaniciAsync()
        {
            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(kullaniciId))
                return null;

            return await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
        }

        private async Task<bool> KullaniciYonetebilirMi(AppKullanici kullanici, int? sirketId)
        {
            if (User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || User.IsInRole("SirketAdmin")
                || kullanici.KullaniciTipi == 4
                || kullanici.KullaniciTipi == 3)
                return true;

            if (sirketId == null)
                return false;

            if (kullanici.SirketId == sirketId)
                return true;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                x.SirketId == sirketId.Value &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.KULLANICI_YONET));
        }
    }

    public class AdminDashboardFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminKullaniciListeFiltreDto
    {
        public int? SirketId { get; set; }
        public string? Q { get; set; }
        public string? Tip { get; set; }
        public string? Durum { get; set; }
        public string? Bagli { get; set; }
    }

    public class AdminYetkiliServisListeFiltreDto
    {
        public int? SirketId { get; set; }
        public string? Q { get; set; }
        public string? Il { get; set; }
        public int? Durum { get; set; }
        public string? DevreyeSiralama { get; set; }
    }

    public class AdminYetkiliServisGetirFiltreDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
    }

    public class AdminYetkiliServisListeDto
    {
        public List<AdminYetkiliServisDto> Servisler { get; set; } = new();
        public Dictionary<int, int> DevreyeSayilari { get; set; } = new();
    }

    public class AdminYetkiliServisDetayDto
    {
        public AdminYetkiliServisDto? Servis { get; set; }
        public List<AdminYetkiliServisSertifikaDto> Sertifikalar { get; set; } = new();
        public List<AdminYetkiliServisSubeDto> Subeler { get; set; } = new();
        public List<AdminYetkiliServisDevreyeDto> Devreye { get; set; } = new();
    }

    public class AdminYetkiliServisDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? VergiNo { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public bool AktifMi { get; set; }
        public int SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public List<AdminYetkiliServisKategoriDto> Kategoriler { get; set; } = new();
    }

    public class AdminYetkiliServisKategoriDto
    {
        public int Id { get; set; }
        public string? Ad { get; set; }
        public string? IconUrl { get; set; }
    }

    public class AdminYetkiliServisSertifikaDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? SertifikaBaslangicTarihi { get; set; }
        public DateTime SertifikaBitisTarihi { get; set; }
    }

    public class AdminYetkiliServisSubeDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
    }

    public class AdminYetkiliServisDevreyeDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? TesistatNo { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class AdminKullaniciListeDto
    {
        public string Id { get; set; } = string.Empty;
        public string? AdSoyad { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public int KullaniciTipi { get; set; }
        public bool AktifMi { get; set; }
        public int? SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public int? FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? FirmaYetkiliKisi { get; set; }
        public string? FirmaEmail { get; set; }
        public string? FirmaTelefon { get; set; }
    }

    public class AdminDashboardApiDto
    {
        public int ToplamDevreyeAlma { get; set; }
        public int ToplamFirma { get; set; }
        public int OnayBekleyen { get; set; }
        public int SuresiBitecek { get; set; }
        public int ToplamSirket { get; set; }
        public int BuAyDevreyeAlma { get; set; }
        public List<AdminSertifikaOzetDto> SonSertifikalar { get; set; } = new();
        public List<AdminDevreyeAlmaOzetDto> SonDevreyeAlmalar { get; set; } = new();
    }

    public class AdminSertifikaOzetDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? SirketAdi { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime SertifikaBitisTarihi { get; set; }
    }

    public class AdminDevreyeAlmaOzetDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? MarkaAdi { get; set; }
        public string? TesistatNo { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
    }
}
