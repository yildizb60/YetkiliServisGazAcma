using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/admin-panel")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
    public partial class AdminPanelApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AdminDashboardService _dashboardService;
        private readonly AdminYetkiliServisListeService _yetkiliServisListeService;
        private readonly AdminYetkiliServisYonetimApiService _adminYetkiliServisYonetimApiService;
        private readonly AdminSubeApiService _adminSubeApiService;
        private readonly AdminRaporApiService _adminRaporApiService;
        private readonly AdminYetkiBelgesiOnayApiService _adminYetkiBelgesiOnayApiService;
        private readonly AdminPersonelYetkiApiService _adminPersonelYetkiApiService;
        private readonly DevreyeAlmaExportApiService _devreyeAlmaExportApiService;
        private readonly ILogger<AdminPanelApiController> _logger;

        public AdminPanelApiController(
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            AdminDashboardService dashboardService,
            AdminYetkiliServisListeService yetkiliServisListeService,
            AdminYetkiliServisYonetimApiService adminYetkiliServisYonetimApiService,
            AdminSubeApiService adminSubeApiService,
            AdminRaporApiService adminRaporApiService,
            AdminYetkiBelgesiOnayApiService adminYetkiBelgesiOnayApiService,
            AdminPersonelYetkiApiService adminPersonelYetkiApiService,
            DevreyeAlmaExportApiService devreyeAlmaExportApiService,
            ILogger<AdminPanelApiController> logger)
        {
            _context = context;
            _userManager = userManager;
            _dashboardService = dashboardService;
            _yetkiliServisListeService = yetkiliServisListeService;
            _adminYetkiliServisYonetimApiService = adminYetkiliServisYonetimApiService;
            _adminSubeApiService = adminSubeApiService;
            _adminRaporApiService = adminRaporApiService;
            _adminYetkiBelgesiOnayApiService = adminYetkiBelgesiOnayApiService;
            _adminPersonelYetkiApiService = adminPersonelYetkiApiService;
            _devreyeAlmaExportApiService = devreyeAlmaExportApiService;
            _logger = logger;
        }

        private async Task<List<AdminSirketSecenekDto>> SirketSecenekleriAsync(int? sirketId)
        {
            var query = _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (sirketId.HasValue)
                query = query.Where(x => x.Id == sirketId.Value);

            return await query
                .OrderBy(x => x.SirketAdi)
                .Select(x => new AdminSirketSecenekDto
                {
                    Id = x.Id,
                    SirketAdi = x.SirketAdi
                })
                .ToListAsync();
        }

        private async Task<List<AdminFirmaSecenekDto>> FirmaSecenekleriAsync(int? sirketId)
        {
            var query = _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi && x.AktifMi)
                .AsQueryable();

            if (sirketId.HasValue)
                query = query.Where(x => x.SirketId == sirketId.Value);

            return await query
                .OrderBy(x => x.FirmaAdi)
                .Select(x => new AdminFirmaSecenekDto
                {
                    Id = x.Id,
                    FirmaAdi = x.FirmaAdi,
                    SirketId = x.SirketId,
                    SirketAdi = x.Sirket != null ? x.Sirket.SirketAdi : null
                })
                .ToListAsync();
        }

        private async Task<string?> YetkiliServisRolAdiAsync()
        {
            var tumRoller = await _context.Set<IdentityRole>()
                .Select(r => r.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToListAsync();

            var adaylar = new[] { "YetkiliServis", "SERVIS", "Servis" };

            foreach (var aday in adaylar)
            {
                var eslesen = tumRoller.FirstOrDefault(r =>
                    string.Equals(r, aday, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(eslesen))
                    return eslesen;
            }

            return tumRoller.FirstOrDefault(r =>
                r!.Contains("yetkili", StringComparison.OrdinalIgnoreCase) &&
                r.Contains("servis", StringComparison.OrdinalIgnoreCase));
        }

        private async Task YetkiliServisKullanicilariniSenkronizeAsync(int? sirketId)
        {
            var yetkiliServisRolAdi = await YetkiliServisRolAdiAsync();
            var firmalarQuery = _context.Ys_Firmalar
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (sirketId.HasValue)
                firmalarQuery = firmalarQuery.Where(x => x.SirketId == sirketId.Value);

            var firmalar = await firmalarQuery.ToListAsync();

            foreach (var firma in firmalar)
            {
                if (string.IsNullOrWhiteSpace(firma.Email))
                    continue;

                var email = firma.Email.Trim();
                var adSoyad = !string.IsNullOrWhiteSpace(firma.YetkiliKisi) ? firma.YetkiliKisi : firma.FirmaAdi;

                var servisKullanicisi = await _context.Users
                    .FirstOrDefaultAsync(u => u.FirmaId == firma.Id);

                if (servisKullanicisi == null)
                {
                    servisKullanicisi = await _userManager.FindByEmailAsync(email);
                    if (servisKullanicisi != null && !servisKullanicisi.FirmaId.HasValue)
                    {
                        servisKullanicisi.FirmaId = firma.Id;
                    }
                }

                if (servisKullanicisi == null)
                {
                    var yeni = new AppKullanici
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        AdSoyad = adSoyad,
                        PhoneNumber = firma.Telefon,
                        KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis,
                        FirmaId = firma.Id,
                        SirketId = firma.SirketId,
                        AktifMi = firma.AktifMi
                    };

                    var createResult = await _userManager.CreateAsync(yeni, "Servis123!");
                    if (createResult.Succeeded && !string.IsNullOrWhiteSpace(yetkiliServisRolAdi))
                    {
                        await _userManager.AddToRoleAsync(yeni, yetkiliServisRolAdi!);
                    }

                    continue;
                }

                servisKullanicisi.KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis;
                servisKullanicisi.FirmaId = firma.Id;
                servisKullanicisi.SirketId = firma.SirketId;
                servisKullanicisi.AktifMi = firma.AktifMi;

                if (string.IsNullOrWhiteSpace(servisKullanicisi.AdSoyad))
                    servisKullanicisi.AdSoyad = adSoyad;

                if (string.IsNullOrWhiteSpace(servisKullanicisi.PhoneNumber) && !string.IsNullOrWhiteSpace(firma.Telefon))
                    servisKullanicisi.PhoneNumber = firma.Telefon;

                if (!string.Equals(servisKullanicisi.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    var emailSahibi = await _userManager.FindByEmailAsync(email);
                    if (emailSahibi == null || emailSahibi.Id == servisKullanicisi.Id)
                    {
                        servisKullanicisi.Email = email;
                        servisKullanicisi.UserName = email;
                    }
                }

                await _userManager.UpdateAsync(servisKullanicisi);

                if (!string.IsNullOrWhiteSpace(yetkiliServisRolAdi))
                {
                    if (!await _userManager.IsInRoleAsync(servisKullanicisi, yetkiliServisRolAdi!))
                        await _userManager.AddToRoleAsync(servisKullanicisi, yetkiliServisRolAdi!);
                }
            }
        }

        private async Task<(int? sirketId, bool gecersiz)> KapsamSirketIdAsync(int? istenenSirketId)
        {
            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var kullanici = await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
            if (kullanici == null)
                return (null, true);

            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue);

            if (genelSistemAdminMi)
                return (istenenSirketId, false);

            var sirketAdminMi = User.IsInRole("SirketAdmin")
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && kullanici.SirketId.HasValue);

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

        private bool GenelSistemAdminMi(AppKullanici kullanici)
        {
            return User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue);
        }

        private static AdminKullaniciListeDto MapKullanici(AppKullanici kullanici)
        {
            return new AdminKullaniciListeDto
            {
                Id = kullanici.Id,
                AdSoyad = kullanici.AdSoyad,
                Email = kullanici.Email,
                PhoneNumber = kullanici.PhoneNumber,
                KullaniciTipi = kullanici.KullaniciTipi,
                AktifMi = kullanici.AktifMi,
                SirketId = kullanici.SirketId,
                SirketAdi = kullanici.Sirket?.SirketAdi,
                FirmaId = kullanici.FirmaId,
                FirmaAdi = kullanici.Firma?.FirmaAdi,
                FirmaYetkiliKisi = kullanici.Firma?.YetkiliKisi,
                FirmaEmail = kullanici.Firma?.Email,
                FirmaTelefon = kullanici.Firma?.Telefon
            };
        }

        private async Task<bool> KullaniciYonetebilirMi(AppKullanici kullanici, int? sirketId)
        {
            if (User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || User.IsInRole("SirketAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin)
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

        private async Task<bool> KullaniciKapsamindaMi(AppKullanici yapan, AppKullanici hedef, int? sirketId)
        {
            if (yapan.Id == hedef.Id)
                return true;

            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || yapan.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (yapan.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !yapan.SirketId.HasValue);

            if (genelSistemAdminMi && !sirketId.HasValue)
                return true;

            if (!sirketId.HasValue)
                return false;

            if ((hedef.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                 hedef.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) &&
                hedef.FirmaId.HasValue)
            {
                return await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id == hedef.FirmaId.Value &&
                    !x.SilindiMi &&
                    x.SirketId == sirketId.Value);
            }

            return (hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && hedef.SirketId == sirketId.Value;
        }

        private async Task<bool> SirketYonetimKapsamindaMi(AppKullanici yapan, int hedefSirketId, int? kapsamSirketId)
        {
            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || yapan.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (yapan.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !yapan.SirketId.HasValue);

            if (genelSistemAdminMi && !kapsamSirketId.HasValue)
                return true;

            if (kapsamSirketId.HasValue)
                return hedefSirketId == kapsamSirketId.Value;

            if (yapan.SirketId == hedefSirketId)
                return true;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == yapan.Id &&
                !x.SilindiMi &&
                x.SirketId == hedefSirketId &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.KULLANICI_YONET));
        }

        private static List<string> ValidatePassword(string? sifre)
        {
            var hatalar = new List<string>();
            if (string.IsNullOrWhiteSpace(sifre))
            {
                hatalar.Add("Sifre zorunludur.");
                return hatalar;
            }

            if (sifre.Length < 6)
                hatalar.Add("Sifre en az 6 karakter olmalidir.");

            if (!sifre.Any(char.IsLower))
                hatalar.Add("Sifre en az bir kucuk harf icermelidir.");

            if (!sifre.Any(char.IsDigit))
                hatalar.Add("Sifre en az bir rakam icermelidir.");

            return hatalar;
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

    public class AdminKullaniciSirketSecenekFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminKullaniciFirmaSecenekFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminKullaniciSenkronFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminKullaniciYonetimYetkiDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminKullaniciYonetimYetkiSonucDto
    {
        public bool YetkiliMi { get; set; }
    }

    public class AdminKullaniciGetirDto
    {
        public string Id { get; set; } = string.Empty;
        public int? SirketId { get; set; }
    }

    public class AdminKullaniciGuncelleDto
    {
        public string Id { get; set; } = string.Empty;
        public int? KapsamSirketId { get; set; }
        public string? AdSoyad { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public bool AktifMi { get; set; }
        public int? SirketId { get; set; }
        public int? FirmaId { get; set; }
        public string? YeniSifre { get; set; }
        public string? YeniSifreTekrar { get; set; }
    }

    public class AdminKullaniciKaydetDto
    {
        public int? KapsamSirketId { get; set; }
        public string? AdSoyad { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public string? Sifre { get; set; }
        public string? Rol { get; set; }
        public int? SirketId { get; set; }
        public int? FirmaId { get; set; }
    }

    public class AdminPersonelKaydetDto
    {
        public int? KapsamSirketId { get; set; }
        public string? AdSoyad { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public int SirketId { get; set; }
        public string? Sifre { get; set; }
    }

    public class AdminKullaniciDurumDto
    {
        public string Id { get; set; } = string.Empty;
        public int? SirketId { get; set; }
        public bool AktifMi { get; set; }
        public bool SadecePersonel { get; set; }
    }

    public class AdminKullaniciSilDto
    {
        public string Id { get; set; } = string.Empty;
        public int? SirketId { get; set; }
        public bool SadecePersonel { get; set; }
    }

    public class AdminYetkiListeFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminYetkiGetirDto
    {
        public string Id { get; set; } = string.Empty;
        public int? SirketId { get; set; }
    }

    public class AdminYetkiGuncelleDto
    {
        public string Id { get; set; } = string.Empty;
        public int? SirketId { get; set; }
        public List<int> SirketIds { get; set; } = new();
        public Dictionary<int, List<string>> Yetkiler { get; set; } = new();
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

    public class AdminYetkiliServisKaydetDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
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

    public class AdminYetkiliServisDurumDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
    }

    public class AdminYetkiBelgesiOnayFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminYetkiBelgesiOnayGecmisiFiltreDto
    {
        public int? SirketId { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? Q { get; set; }
        public int? Durum { get; set; }
    }

    public class AdminDevreyeAlmaListeFiltreDto
    {
        public int? SirketId { get; set; }
        public string? TesisatNo { get; set; }
        public string? Marka { get; set; }
        public string? Servis { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public int? Durum { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
    }

    public class AdminDevreyeAlmaGetirFiltreDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
    }

    public class AdminDevreyeAlmaRaporExportFiltreDto
    {
        public int? SirketId { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public List<int>? Ids { get; set; }
    }

    public class AdminYetkiBelgesiUyariFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminRaporOzetFiltreDto
    {
        public int? SirketId { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? Tip { get; set; }
    }

    public class AdminSubeListeFiltreDto
    {
        public int? SirketId { get; set; }
        public int FirmaId { get; set; }
        public string? Q { get; set; }
    }

    public class AdminSubeGetirFiltreDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
    }

    public class AdminSubeKaydetDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
        public int FirmaId { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; }
    }

    public class AdminSubeDurumDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
    }

    public class AdminIslemSonucDto
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }

        public static AdminIslemSonucDto BasariliSonuc(string mesaj)
        {
            return new AdminIslemSonucDto { Basarili = true, Mesaj = mesaj };
        }

        public static AdminIslemSonucDto Basarisiz(string mesaj)
        {
            return new AdminIslemSonucDto { Basarili = false, Mesaj = mesaj };
        }
    }

    public class AdminSubeListeDto
    {
        public List<AdminSubeDto> Subeler { get; set; } = new();
        public List<AdminSubeFirmaDto> Firmalar { get; set; } = new();
    }

    public class AdminSubeDetayDto : AdminIslemSonucDto
    {
        public AdminSubeDto? Sube { get; set; }
        public List<AdminSubeFirmaDto> Firmalar { get; set; } = new();
    }

    public class AdminSubeDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; }
        public string? FirmaAdi { get; set; }
        public string? FirmaEmail { get; set; }
        public string? FirmaTelefon { get; set; }
        public int? FirmaSirketId { get; set; }

        public static AdminSubeDto FromEntity(Ys_Sube sube)
        {
            return new AdminSubeDto
            {
                Id = sube.Id,
                FirmaId = sube.FirmaId,
                SubeAdi = sube.SubeAdi,
                Il = sube.Il,
                Ilce = sube.Ilce,
                Telefon = sube.Telefon,
                Adres = sube.Adres,
                AktifMi = sube.AktifMi,
                FirmaAdi = sube.Firma?.FirmaAdi,
                FirmaEmail = sube.Firma?.Email,
                FirmaTelefon = sube.Firma?.Telefon,
                FirmaSirketId = sube.Firma?.SirketId
            };
        }
    }

    public class AdminSubeFirmaDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public int SirketId { get; set; }

        public static AdminSubeFirmaDto FromEntity(Ys_Firma firma)
        {
            return new AdminSubeFirmaDto
            {
                Id = firma.Id,
                FirmaAdi = firma.FirmaAdi,
                Email = firma.Email,
                Telefon = firma.Telefon,
                SirketId = firma.SirketId
            };
        }
    }

    public class AdminDevreyeAlmaListeDto
    {
        public List<AdminDevreyeAlmaDto> Islemler { get; set; } = new();
        public List<AdminMarkaSecenekDto> Markalar { get; set; } = new();
        public Dictionary<int, string> FirmaIlceleri { get; set; } = new();
    }

    public class AdminYetkiBelgesiUyariListeDto
    {
        public List<AdminYetkiBelgesiOnayDto> Yaklasan { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> Gecmis { get; set; } = new();
    }

    public class AdminYetkiListeDto
    {
        public List<AdminKullaniciListeDto> Personeller { get; set; } = new();
        public Dictionary<string, List<string>> YetkiMap { get; set; } = new();
        public Dictionary<string, List<string>> YetkiSirketAdlariMap { get; set; } = new();
    }

    public class AdminYetkiDuzenleDto
    {
        public AdminKullaniciListeDto? Personel { get; set; }
        public List<AdminSirketSecenekDto> Sirketler { get; set; } = new();
        public List<string> MevcutYetkiler { get; set; } = new();
        public Dictionary<int, List<string>> YetkiSirketMap { get; set; } = new();
        public List<int> SeciliSirketIds { get; set; } = new();
    }

    public class AdminYetkiBelgesiOnayGecmisiListeDto
    {
        public List<AdminYetkiBelgesiOnayDto> Islemler { get; set; } = new();
    }

    public class AdminRaporOzetDto
    {
        public DateTime BasTarih { get; set; }
        public DateTime BitTarih { get; set; }
        public string RaporTipi { get; set; } = "devreye";
        public string ListeTipi { get; set; } = "devreye";
        public int DevreyeSayisi { get; set; }
        public int DevreyeTamamlanan { get; set; }
        public int DevreyeBekleyen { get; set; }
        public int DevreyeIptal { get; set; }
        public int YetkiBelgesiOnayli { get; set; }
        public int YetkiBelgesiBekleyen { get; set; }
        public int YetkiBelgesiReddedilen { get; set; }
        public List<string?> ChartSirketLabels { get; set; } = new();
        public List<int> ChartSirketData { get; set; } = new();
        public List<string> ChartAylikLabels { get; set; } = new();
        public List<int> ChartAylikData { get; set; } = new();
        public List<int> ChartDurumData { get; set; } = new();
        public List<string?> ChartMarkaLabels { get; set; } = new();
        public List<int> ChartMarkaData { get; set; } = new();
        public List<AdminDevreyeAlmaDto> SonIslemler { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> YetkiBelgesiIslemler { get; set; } = new();
        public List<AdminSirketSecenekDto> Sirketler { get; set; } = new();
    }

    public class AdminMarkaSecenekDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class AdminSirketSecenekDto
    {
        public int Id { get; set; }
        public string? SirketAdi { get; set; }
    }

    public class AdminFirmaSecenekDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public int SirketId { get; set; }
        public string? SirketAdi { get; set; }
    }

    public class AdminDevreyeAlmaDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int? MarkaId { get; set; }
        public string? TesistatNo { get; set; }
        public string? AboneNo { get; set; }
        public string? UygunlukBelgeNo { get; set; }
        public DateTime? UygunlukTarihi { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTcNo { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Adres { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazModeli { get; set; }
        public string? CihazKapasite { get; set; }
        public string? SeriNo { get; set; }
        public string? TeknisyenAdi { get; set; }
        public string? TeknisyenYetkiBelgesiNo { get; set; }
        public DateTime DevreyeAlmaTarihi { get; set; }
        public string? Notlar { get; set; }
        public int Durum { get; set; }
        public string? PdfYolu { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string? FirmaAdi { get; set; }
        public string? FirmaFaaliyetIli { get; set; }
        public string? FirmaAdres { get; set; }
        public int FirmaSirketId { get; set; }
        public string? SirketAdi { get; set; }
        public string? MarkaAdi { get; set; }

        public static AdminDevreyeAlmaDto FromEntity(Ys_DevreyeAlma devreyeAlma)
        {
            return new AdminDevreyeAlmaDto
            {
                Id = devreyeAlma.Id,
                FirmaId = devreyeAlma.FirmaId,
                MarkaId = devreyeAlma.MarkaId,
                TesistatNo = devreyeAlma.TesistatNo,
                AboneNo = devreyeAlma.AboneNo,
                UygunlukBelgeNo = devreyeAlma.UygunlukBelgeNo,
                UygunlukTarihi = devreyeAlma.UygunlukTarihi,
                MusteriAdi = devreyeAlma.MusteriAdi,
                MusteriTcNo = devreyeAlma.MusteriTcNo,
                MusteriTelefon = devreyeAlma.MusteriTelefon,
                Adres = devreyeAlma.Adres,
                CihazTipi = devreyeAlma.CihazTipi,
                CihazMarka = devreyeAlma.CihazMarka,
                CihazModeli = devreyeAlma.CihazModeli,
                CihazKapasite = devreyeAlma.CihazKapasite,
                SeriNo = devreyeAlma.SeriNo,
                TeknisyenAdi = devreyeAlma.TeknisyenAdi,
                TeknisyenYetkiBelgesiNo = devreyeAlma.TeknisyenYetkiBelgesiNo,
                DevreyeAlmaTarihi = devreyeAlma.DevreyeAlmaTarihi,
                Notlar = devreyeAlma.Notlar,
                Durum = devreyeAlma.Durum,
                PdfYolu = devreyeAlma.PdfYolu,
                OlusturmaTarihi = devreyeAlma.OlusturmaTarihi,
                FirmaAdi = devreyeAlma.Firma?.FirmaAdi,
                FirmaFaaliyetIli = devreyeAlma.Firma?.FaaliyetIli,
                FirmaAdres = devreyeAlma.Firma?.Adres,
                FirmaSirketId = devreyeAlma.Firma?.SirketId ?? 0,
                SirketAdi = devreyeAlma.Firma?.Sirket?.SirketAdi,
                MarkaAdi = devreyeAlma.Marka?.MarkaAdi
            };
        }
    }

    public class AdminYetkiliServisListeDto
    {
        public List<AdminYetkiliServisDto> Servisler { get; set; } = new();
        public Dictionary<int, int> DevreyeSayilari { get; set; } = new();
    }

    public class AdminYetkiliServisDetayDto
    {
        public AdminYetkiliServisDto? Servis { get; set; }
        public List<AdminYetkiliServisYetkiBelgesiDto> YetkiBelgeleri { get; set; } = new();
        public List<AdminYetkiliServisSubeDto> Subeler { get; set; } = new();
        public List<AdminYetkiliServisDevreyeDto> Devreye { get; set; } = new();
    }

    public class AdminYetkiliServisDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public bool AktifMi { get; set; }
        public int SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public List<AdminYetkiliServisKategoriDto> Kategoriler { get; set; } = new();
        public List<AdminYetkiliServisMarkaDto> Markalar { get; set; } = new();
    }

    public class AdminYetkiliServisKategoriDto
    {
        public int Id { get; set; }
        public string? Ad { get; set; }
        public string? IconUrl { get; set; }
    }

    public class AdminYetkiliServisMarkaDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class AdminYetkiliServisYetkiBelgesiDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
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

    public class AdminYetkiBelgesiOnayListeDto
    {
        public List<AdminYetkiBelgesiOnayDto> Bekleyenler { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> Onaylananlar { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> Reddedilenler { get; set; } = new();
    }

    public class AdminYetkiBelgesiOnayDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? VergiNo { get; set; }
        public string? SirketAdi { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
        public string? DosyaYolu { get; set; }
        public string? OnaylayanKullanici { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? RedGerekce { get; set; }

        public static AdminYetkiBelgesiOnayDto FromEntity(Ys_YetkiBelgesi yetkiBelgesi)
        {
            return new AdminYetkiBelgesiOnayDto
            {
                Id = yetkiBelgesi.Id,
                FirmaId = yetkiBelgesi.FirmaId,
                FirmaAdi = yetkiBelgesi.Firma?.FirmaAdi,
                VergiNo = yetkiBelgesi.Firma?.VergiNo,
                SirketAdi = yetkiBelgesi.Firma?.Sirket?.SirketAdi,
                Durum = yetkiBelgesi.Durum,
                OlusturmaTarihi = yetkiBelgesi.OlusturmaTarihi,
                YetkiBelgesiBaslangicTarihi = yetkiBelgesi.YetkiBelgesiBaslangicTarihi,
                YetkiBelgesiBitisTarihi = yetkiBelgesi.YetkiBelgesiBitisTarihi,
                DosyaYolu = yetkiBelgesi.DosyaYolu,
                OnaylayanKullanici = yetkiBelgesi.OnaylayanKullanici,
                OnayTarihi = yetkiBelgesi.OnayTarihi,
                RedGerekce = yetkiBelgesi.RedGerekce
            };
        }
    }

    public class AdminDashboardApiDto
    {
        public int ToplamDevreyeAlma { get; set; }
        public int ToplamFirma { get; set; }
        public int OnayBekleyen { get; set; }
        public int SuresiBitecek { get; set; }
        public int ToplamSirket { get; set; }
        public int BuAyDevreyeAlma { get; set; }
        public List<AdminYetkiBelgesiOzetDto> SonYetkiBelgeleri { get; set; } = new();
        public List<AdminDevreyeAlmaOzetDto> SonDevreyeAlmalar { get; set; } = new();
    }

    public class AdminYetkiBelgesiOzetDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? SirketAdi { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
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
