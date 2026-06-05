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
    public class AdminPanelApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AdminDashboardService _dashboardService;
        private readonly AdminYetkiliServisListeService _yetkiliServisListeService;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
        private readonly AdminSubeApiService _adminSubeApiService;
        private readonly AdminRaporApiService _adminRaporApiService;
        private readonly AdminYetkiBelgesiOnayApiService _adminYetkiBelgesiOnayApiService;

        public AdminPanelApiController(
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            AdminDashboardService dashboardService,
            AdminYetkiliServisListeService yetkiliServisListeService,
            SehirFirmaKoduService sehirFirmaKoduService,
            AdminSubeApiService adminSubeApiService,
            AdminRaporApiService adminRaporApiService,
            AdminYetkiBelgesiOnayApiService adminYetkiBelgesiOnayApiService)
        {
            _context = context;
            _userManager = userManager;
            _dashboardService = dashboardService;
            _yetkiliServisListeService = yetkiliServisListeService;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _adminSubeApiService = adminSubeApiService;
            _adminRaporApiService = adminRaporApiService;
            _adminYetkiBelgesiOnayApiService = adminYetkiBelgesiOnayApiService;
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
                SonYetkiBelgeleri = ozet.SonYetkiBelgeleri.Select(x => new AdminYetkiBelgesiOzetDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    FirmaAdi = x.Firma?.FirmaAdi,
                    SirketAdi = x.Firma?.Sirket?.SirketAdi,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    YetkiBelgesiBitisTarihi = x.YetkiBelgesiBitisTarihi
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

        [HttpPost("kullanicilar/sirket-secenekleri")]
        public async Task<IActionResult> KullaniciSirketSecenekleri([FromBody] AdminKullaniciSirketSecenekFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await SirketSecenekleriAsync(kapsam.sirketId));
        }

        [HttpPost("kullanicilar/firma-secenekleri")]
        public async Task<IActionResult> KullaniciFirmaSecenekleri([FromBody] AdminKullaniciFirmaSecenekFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await FirmaSecenekleriAsync(kapsam.sirketId));
        }

        [HttpPost("kullanicilar/yetkili-servis-senkronize")]
        public async Task<IActionResult> YetkiliServisKullanicilariniSenkronize([FromBody] AdminKullaniciSenkronFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            await YetkiliServisKullanicilariniSenkronizeAsync(kapsam.sirketId);
            return Ok(AdminIslemSonucDto.BasariliSonuc("Yetkili servis kullanicilari senkronize edildi."));
        }

        [HttpPost("kullanicilar/yonetim-yetkisi")]
        public async Task<IActionResult> KullaniciYonetimYetkisi([FromBody] AdminKullaniciYonetimYetkiDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(new AdminKullaniciYonetimYetkiSonucDto
            {
                YetkiliMi = await KullaniciYonetebilirMi(kullanici, kapsam.sirketId)
            });
        }

        [HttpPost("kullanicilar/getir")]
        public async Task<IActionResult> KullaniciGetir([FromBody] AdminKullaniciGetirDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return NotFound();

            var hedef = await _context.Users
                .Include(x => x.Sirket)
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);

            if (hedef == null)
                return NotFound();

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            return Ok(MapKullanici(hedef));
        }

        [HttpPost("kullanicilar/guncelle")]
        public async Task<IActionResult> KullaniciGuncelle([FromBody] AdminKullaniciGuncelleDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.KapsamSirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici id zorunludur."));

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (hedef == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici bulunamadi."));

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            if ((hedef.KullaniciTipi == 3 || hedef.KullaniciTipi == 2) && (!dto.SirketId.HasValue || dto.SirketId.Value <= 0))
            {
                return Ok(AdminIslemSonucDto.Basarisiz(hedef.KullaniciTipi == 2
                    ? "Personel icin sirket secilmelidir."
                    : "Sirket admini icin sirket secilmelidir."));
            }

            if (hedef.KullaniciTipi == 3 || hedef.KullaniciTipi == 2)
            {
                if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId!.Value, kapsam.sirketId))
                    return Forbid();

                hedef.SirketId = dto.SirketId;
                hedef.FirmaId = null;
            }
            else if (hedef.KullaniciTipi == 1)
            {
                if (!dto.FirmaId.HasValue || dto.FirmaId.Value <= 0)
                    return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis kullanicisi icin firma secilmelidir."));

                var firma = await _context.Ys_Firmalar
                    .FirstOrDefaultAsync(x => x.Id == dto.FirmaId.Value && !x.SilindiMi);
                if (firma == null)
                    return Ok(AdminIslemSonucDto.Basarisiz("Secilen firma bulunamadi."));

                if (!await SirketYonetimKapsamindaMi(kullanici, firma.SirketId, kapsam.sirketId))
                    return Forbid();

                hedef.FirmaId = firma.Id;
                hedef.SirketId = firma.SirketId;
            }
            else
            {
                hedef.SirketId = null;
                hedef.FirmaId = null;
            }

            hedef.AdSoyad = dto.AdSoyad;
            hedef.Email = dto.Email;
            hedef.UserName = dto.Email;
            hedef.PhoneNumber = dto.Telefon;
            hedef.AktifMi = dto.AktifMi;

            var sonuc = await _userManager.UpdateAsync(hedef);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            if (!string.IsNullOrWhiteSpace(dto.YeniSifre) || !string.IsNullOrWhiteSpace(dto.YeniSifreTekrar))
            {
                if (dto.YeniSifre != dto.YeniSifreTekrar)
                    return Ok(AdminIslemSonucDto.Basarisiz("Yeni sifreler eslesmiyor."));

                var sifreHatalari = ValidatePassword(dto.YeniSifre);
                if (sifreHatalari.Count > 0)
                    return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));

                var token = await _userManager.GeneratePasswordResetTokenAsync(hedef);
                var sifreSonuc = await _userManager.ResetPasswordAsync(hedef, token, dto.YeniSifre ?? "");
                if (!sifreSonuc.Succeeded)
                    return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sifreSonuc.Errors.Select(x => x.Description))));
            }

            return Ok(AdminIslemSonucDto.BasariliSonuc("Kullanici guncellendi."));
        }

        [HttpPost("kullanicilar/ekle")]
        public async Task<IActionResult> KullaniciEkle([FromBody] AdminKullaniciKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.KapsamSirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici bilgileri zorunludur."));

            var rol = (dto.Rol ?? "").Trim();
            if (string.Equals(rol, "Servis", StringComparison.OrdinalIgnoreCase))
                rol = "YetkiliServis";
            if (string.Equals(rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                rol = "SirketAdmin";

            var gecerliRoller = new[] { "GenelSistemAdmin", "SirketAdmin", "Personel", "YetkiliServis" };
            if (!gecerliRoller.Any(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase)))
                return Ok(AdminIslemSonucDto.Basarisiz("Rol secilmelidir."));

            rol = gecerliRoller.First(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase));

            var genelSistemAdmin = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == 4;
            if (rol == "GenelSistemAdmin" && !genelSistemAdmin)
                return Ok(AdminIslemSonucDto.Basarisiz("Genel Sistem Admini sadece genel sistem admini tarafindan olusturulabilir."));

            var sifreHatalari = ValidatePassword(dto.Sifre);
            if (sifreHatalari.Count > 0)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));

            var kullaniciTipi = rol == "GenelSistemAdmin" ? 4 : rol == "SirketAdmin" ? 3 : rol == "Personel" ? 2 : 1;
            if ((kullaniciTipi == 3 || kullaniciTipi == 2 || kullaniciTipi == 1) && (!dto.SirketId.HasValue || dto.SirketId.Value <= 0))
            {
                var mesaj = kullaniciTipi == 1
                    ? "Yetkili servis icin bagli dagitim sirketi secilmelidir."
                    : kullaniciTipi == 2
                        ? "Personel icin sirket secilmelidir."
                        : "Sirket admini icin sirket secilmelidir.";
                return Ok(AdminIslemSonucDto.Basarisiz(mesaj));
            }

            if (kullaniciTipi == 3 || kullaniciTipi == 2 || kullaniciTipi == 1)
            {
                if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId!.Value, kapsam.sirketId))
                    return Forbid();
            }

            var email = (dto.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return Ok(AdminIslemSonucDto.Basarisiz("E-posta zorunludur."));

            var mevcut = await _userManager.FindByEmailAsync(email);
            if (mevcut != null)
                return Ok(AdminIslemSonucDto.Basarisiz("Bu e-posta ile kayitli bir kullanici zaten var."));

            var yeni = new AppKullanici
            {
                UserName = email,
                Email = email,
                PhoneNumber = dto.Telefon,
                AdSoyad = dto.AdSoyad,
                KullaniciTipi = kullaniciTipi,
                SirketId = (kullaniciTipi == 3 || kullaniciTipi == 2 || kullaniciTipi == 1) ? dto.SirketId : null,
                FirmaId = null,
                AktifMi = true,
                EmailConfirmed = true
            };

            var createSonuc = await _userManager.CreateAsync(yeni, dto.Sifre ?? string.Empty);
            if (!createSonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", createSonuc.Errors.Select(x => x.Description))));

            Ys_Firma? firma = null;
            if (kullaniciTipi == 1)
            {
                try
                {
                    firma = new Ys_Firma
                    {
                        FirmaAdi = dto.AdSoyad,
                        YetkiliKisi = dto.AdSoyad,
                        Telefon = dto.Telefon,
                        Email = email,
                        SirketId = dto.SirketId!.Value,
                        AktifMi = true
                    };

                    _context.Ys_Firmalar.Add(firma);
                    await _context.SaveChangesAsync();

                    yeni.FirmaId = firma.Id;
                    yeni.SirketId = firma.SirketId;
                    await _userManager.UpdateAsync(yeni);
                }
                catch
                {
                    await _userManager.DeleteAsync(yeni);
                    return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis kaydi olusturulurken hata olustu. Lutfen tekrar deneyin."));
                }
            }

            var atanacakRol = rol;
            if (rol == "YetkiliServis")
            {
                var ysRol = await YetkiliServisRolAdiAsync();
                if (string.IsNullOrWhiteSpace(ysRol))
                {
                    await _userManager.DeleteAsync(yeni);
                    if (firma != null)
                    {
                        _context.Ys_Firmalar.Remove(firma);
                        await _context.SaveChangesAsync();
                    }

                    return Ok(AdminIslemSonucDto.Basarisiz("Yetkili Servis rolu sistemde bulunamadi."));
                }

                atanacakRol = ysRol!;
            }

            var rolVarMi = await _context.Set<IdentityRole>()
                .AnyAsync(r => r.Name != null && r.Name.ToLower() == atanacakRol.ToLower());
            if (!rolVarMi)
            {
                await _userManager.DeleteAsync(yeni);
                if (firma != null)
                {
                    _context.Ys_Firmalar.Remove(firma);
                    await _context.SaveChangesAsync();
                }

                return Ok(AdminIslemSonucDto.Basarisiz($"Rol bulunamadi: {atanacakRol}"));
            }

            var rolSonuc = await _userManager.AddToRoleAsync(yeni, atanacakRol);
            if (!rolSonuc.Succeeded)
            {
                await _userManager.DeleteAsync(yeni);
                if (firma != null)
                {
                    _context.Ys_Firmalar.Remove(firma);
                    await _context.SaveChangesAsync();
                }

                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", rolSonuc.Errors.Select(x => x.Description))));
            }

            if (rol == "GenelSistemAdmin")
                await _userManager.AddToRoleAsync(yeni, KullaniciRolAdlari.EskiSuperAdmin);

            return Ok(AdminIslemSonucDto.BasariliSonuc("Kullanici basariyla olusturuldu."));
        }

        [HttpPost("personeller/ekle")]
        public async Task<IActionResult> PersonelEkle([FromBody] AdminPersonelKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.KapsamSirketId ?? dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Personel bilgileri zorunludur."));

            if (dto.SirketId <= 0)
                return Ok(AdminIslemSonucDto.Basarisiz("Personel icin sirket secilmelidir."));

            if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId, kapsam.sirketId))
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.AdSoyad))
                return Ok(AdminIslemSonucDto.Basarisiz("Ad soyad zorunludur."));

            if (string.IsNullOrWhiteSpace(dto.Email))
                return Ok(AdminIslemSonucDto.Basarisiz("E-posta zorunludur."));

            var sifreHatalari = ValidatePassword(dto.Sifre);
            if (sifreHatalari.Count > 0)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));

            var email = dto.Email.Trim();
            var mevcut = await _userManager.FindByEmailAsync(email);
            if (mevcut != null)
                return Ok(AdminIslemSonucDto.Basarisiz("Bu e-posta ile kayitli bir kullanici zaten var."));

            var yeni = new AppKullanici
            {
                UserName = email,
                Email = email,
                PhoneNumber = dto.Telefon,
                AdSoyad = dto.AdSoyad.Trim(),
                KullaniciTipi = 2,
                SirketId = dto.SirketId,
                AktifMi = true,
                EmailConfirmed = true
            };

            var sonuc = await _userManager.CreateAsync(yeni, dto.Sifre ?? string.Empty);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            var rolSonuc = await _userManager.AddToRoleAsync(yeni, "Personel");
            if (!rolSonuc.Succeeded)
            {
                await _userManager.DeleteAsync(yeni);
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", rolSonuc.Errors.Select(x => x.Description))));
            }

            return Ok(AdminIslemSonucDto.BasariliSonuc("Personel basariyla olusturuldu."));
        }

        [HttpPost("kullanicilar/durum")]
        public async Task<IActionResult> KullaniciDurum([FromBody] AdminKullaniciDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici id zorunludur."));

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (hedef == null || (dto.SadecePersonel && hedef.KullaniciTipi != 2))
                return Ok(AdminIslemSonucDto.Basarisiz(dto.SadecePersonel ? "Personel bulunamadi." : "Kullanici bulunamadi."));

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            hedef.AktifMi = dto.AktifMi;
            var sonuc = await _userManager.UpdateAsync(hedef);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            return Ok(AdminIslemSonucDto.BasariliSonuc(dto.AktifMi ? "Kullanici aktif edildi." : "Kullanici pasiflestirildi."));
        }

        [HttpPost("kullanicilar/sil")]
        public async Task<IActionResult> KullaniciSil([FromBody] AdminKullaniciSilDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici id zorunludur."));

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (hedef == null || (dto.SadecePersonel && hedef.KullaniciTipi != 2))
                return Ok(AdminIslemSonucDto.Basarisiz(dto.SadecePersonel ? "Personel bulunamadi." : "Kullanici bulunamadi."));

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            if (kullanici.Id == hedef.Id)
                return Ok(AdminIslemSonucDto.Basarisiz("Kendi hesabinizi silemezsiniz."));

            var sonuc = await _userManager.DeleteAsync(hedef);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            return Ok(AdminIslemSonucDto.BasariliSonuc(dto.SadecePersonel ? "Personel silindi." : "Kullanici silindi."));
        }

        [HttpPost("yetkiler/liste")]
        public async Task<IActionResult> YetkilerListe([FromBody] AdminYetkiListeFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var genelSistemAdmin = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == 4
                || (kullanici.KullaniciTipi == 3 && !kullanici.SirketId.HasValue);

            var personelQuery = _context.Users
                .Include(x => x.Sirket)
                .Where(x => x.KullaniciTipi == 2)
                .AsQueryable();

            if (!(genelSistemAdmin && !kapsam.sirketId.HasValue))
            {
                if (!kapsam.sirketId.HasValue)
                    return Ok(new AdminYetkiListeDto());

                personelQuery = personelQuery.Where(x =>
                    x.SirketId == kapsam.sirketId.Value ||
                    _context.Dag_PersonelYetkiler.Any(y =>
                        y.KullaniciId == x.Id &&
                        y.SirketId == kapsam.sirketId.Value &&
                        !y.SilindiMi));
            }

            var personeller = await personelQuery
                .OrderBy(x => x.AdSoyad)
                .ToListAsync();

            var personelIds = personeller.Select(x => x.Id).ToList();
            var yetkiQuery = _context.Dag_PersonelYetkiler
                .Include(x => x.Sirket)
                .Where(x => personelIds.Contains(x.KullaniciId) && !x.SilindiMi)
                .AsQueryable();

            if (kapsam.sirketId.HasValue)
                yetkiQuery = yetkiQuery.Where(x => x.SirketId == kapsam.sirketId.Value);

            var yetkiKayitlari = await yetkiQuery.ToListAsync();
            var yetkiMap = yetkiKayitlari
                .GroupBy(x => x.KullaniciId)
                .ToDictionary(
                    g => g.Key,
                    g => NormalizeYetkiListesi(g.Select(x => x.YetkiTipi)));

            var yetkiSirketAdlariMap = yetkiKayitlari
                .Where(x => x.Sirket != null && !string.IsNullOrWhiteSpace(x.Sirket.SirketAdi))
                .GroupBy(x => x.KullaniciId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Sirket!.SirketAdi!)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList());

            foreach (var personel in personeller)
            {
                if (!yetkiSirketAdlariMap.ContainsKey(personel.Id)
                    && personel.Sirket != null
                    && !string.IsNullOrWhiteSpace(personel.Sirket.SirketAdi))
                {
                    yetkiSirketAdlariMap[personel.Id] = new List<string> { personel.Sirket.SirketAdi };
                }
            }

            return Ok(new AdminYetkiListeDto
            {
                Personeller = personeller.Select(MapKullanici).ToList(),
                YetkiMap = yetkiMap,
                YetkiSirketAdlariMap = yetkiSirketAdlariMap
            });
        }

        [HttpPost("yetkiler/getir")]
        public async Task<IActionResult> YetkiGetir([FromBody] AdminYetkiGetirDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(new AdminYetkiDuzenleDto());

            var personel = await _context.Users
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.KullaniciTipi == 2);

            if (personel == null || !await KullaniciKapsamindaMi(kullanici, personel, kapsam.sirketId))
                return Ok(new AdminYetkiDuzenleDto());

            var sirketler = await YonetilebilirSirketlerAsync(kullanici, kapsam.sirketId);
            var sirketIds = sirketler.Select(x => x.Id).ToHashSet();
            var mevcutKayitlar = await _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == personel.Id)
                .Where(x => sirketIds.Contains(x.SirketId))
                .ToListAsync();

            var yetkiSirketMap = mevcutKayitlar
                .GroupBy(x => x.SirketId)
                .ToDictionary(
                    g => g.Key,
                    g => NormalizeYetkiListesi(g.Select(x => x.YetkiTipi)));

            var mevcut = NormalizeYetkiListesi(mevcutKayitlar.Select(x => x.YetkiTipi));
            var seciliSirketIds = mevcutKayitlar
                .Select(x => x.SirketId)
                .Distinct()
                .ToList();

            if (seciliSirketIds.Count == 0 && personel.SirketId.HasValue && sirketIds.Contains(personel.SirketId.Value))
                seciliSirketIds.Add(personel.SirketId.Value);

            return Ok(new AdminYetkiDuzenleDto
            {
                Personel = MapKullanici(personel),
                Sirketler = sirketler,
                MevcutYetkiler = mevcut,
                YetkiSirketMap = yetkiSirketMap,
                SeciliSirketIds = seciliSirketIds
            });
        }

        [HttpPost("yetkiler/guncelle")]
        public async Task<IActionResult> YetkiGuncelle([FromBody] AdminYetkiGuncelleDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(AdminIslemSonucDto.Basarisiz("Personel id zorunludur."));

            var personel = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id && x.KullaniciTipi == 2);
            if (personel == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Personel bulunamadi."));

            if (!await KullaniciKapsamindaMi(kullanici, personel, kapsam.sirketId))
                return Forbid();

            var yonetilebilirSirketIds = (await YonetilebilirSirketlerAsync(kullanici, kapsam.sirketId))
                .Select(x => x.Id)
                .ToHashSet();

            var secilenSirketIds = (dto.SirketIds ?? new List<int>())
                .Where(yonetilebilirSirketIds.Contains)
                .Distinct()
                .ToList();

            if (secilenSirketIds.Count == 0 && personel.SirketId.HasValue && yonetilebilirSirketIds.Contains(personel.SirketId.Value))
                secilenSirketIds.Add(personel.SirketId.Value);

            var mevcut = await _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == personel.Id)
                .Where(x => yonetilebilirSirketIds.Contains(x.SirketId))
                .ToListAsync();

            _context.Dag_PersonelYetkiler.RemoveRange(mevcut);

            foreach (var sirketId in secilenSirketIds)
            {
                dto.Yetkiler.TryGetValue(sirketId, out var secilenYetkiler);
                secilenYetkiler = NormalizeYetkiListesi(secilenYetkiler ?? new List<string>());

                foreach (var yetki in secilenYetkiler)
                {
                    _context.Dag_PersonelYetkiler.Add(new Dag_PersonelYetki
                    {
                        KullaniciId = personel.Id,
                        SirketId = sirketId,
                        YetkiTipi = yetki,
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "api",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(AdminIslemSonucDto.BasariliSonuc("Yetkiler guncellendi."));
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
                    VergiDairesi = x.VergiDairesi,
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
                    VergiDairesi = sonuc.Servis.VergiDairesi,
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
                        .ToList() ?? new List<AdminYetkiliServisKategoriDto>(),
                    Markalar = sonuc.Servis.FirmaMarkalar?
                        .Where(x => !x.SilindiMi && x.Marka != null)
                        .Select(x => new AdminYetkiliServisMarkaDto
                        {
                            Id = x.Marka!.Id,
                            MarkaAdi = x.Marka.MarkaAdi
                        })
                        .GroupBy(x => x.Id)
                        .Select(x => x.First())
                        .ToList() ?? new List<AdminYetkiliServisMarkaDto>()
                },
                YetkiBelgeleri = sonuc.YetkiBelgeleri.Select(x => new AdminYetkiliServisYetkiBelgesiDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    YetkiBelgesiBaslangicTarihi = x.YetkiBelgesiBaslangicTarihi,
                    YetkiBelgesiBitisTarihi = x.YetkiBelgesiBitisTarihi
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

        [HttpPost("yetkili-servisler/ekle")]
        public async Task<IActionResult> YetkiliServisEkle([FromBody] AdminYetkiliServisKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return Ok(AdminIslemSonucDto.Basarisiz("Firma adi zorunludur."));

            if (!string.IsNullOrWhiteSpace(dto.VergiNo))
            {
                var vknVar = await _context.Ys_Firmalar.AnyAsync(x =>
                    !x.SilindiMi &&
                    x.VergiNo == dto.VergiNo.Trim());

                if (vknVar)
                    return Ok(AdminIslemSonucDto.Basarisiz("Bu VKN ile kayitli bir yetkili servis zaten var."));
            }

            var hedefSirketId = kapsam.sirketId
                ?? await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    dto.FaaliyetIli,
                    kullanici.UserName ?? "api");

            var yeni = new Ys_Firma
            {
                FirmaAdi = dto.FirmaAdi.Trim(),
                YetkiliKisi = dto.YetkiliKisi,
                Telefon = dto.Telefon,
                Email = dto.Email,
                Adres = dto.Adres,
                FaaliyetIli = dto.FaaliyetIli,
                VergiNo = dto.VergiNo,
                VergiDairesi = dto.VergiDairesi,
                SirketId = hedefSirketId,
                AktifMi = dto.AktifMi,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName ?? "api",
                SilindiMi = false
            };

            _context.Ys_Firmalar.Add(yeni);
            await _context.SaveChangesAsync();

            await YetkiliServisIliskileriniYenileAsync(
                yeni.Id,
                dto.KategoriIds,
                dto.MarkaIds,
                kullanici.UserName ?? "api",
                kategoriSil: false,
                markaSil: false);

            await _context.SaveChangesAsync();
            return Ok(AdminIslemSonucDto.BasariliSonuc("Yetkili servis eklendi."));
        }

        [HttpPost("yetkili-servisler/guncelle")]
        public async Task<IActionResult> YetkiliServisGuncelle([FromBody] AdminYetkiliServisKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis ve firma adi zorunludur."));

            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi
                    && (kapsam.sirketId == null || x.SirketId == kapsam.sirketId.Value));

            if (servis == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis bulunamadi."));

            if (!string.IsNullOrWhiteSpace(dto.VergiNo))
            {
                var vknVar = await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id != servis.Id &&
                    !x.SilindiMi &&
                    x.VergiNo == dto.VergiNo.Trim());

                if (vknVar)
                    return Ok(AdminIslemSonucDto.Basarisiz("Bu VKN ile kayitli baska bir yetkili servis var."));
            }

            var hedefSirketId = kapsam.sirketId
                ?? await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    dto.FaaliyetIli,
                    kullanici.UserName ?? "api");

            servis.FirmaAdi = dto.FirmaAdi.Trim();
            servis.YetkiliKisi = dto.YetkiliKisi;
            servis.Telefon = dto.Telefon;
            servis.Email = dto.Email;
            servis.Adres = dto.Adres;
            servis.FaaliyetIli = dto.FaaliyetIli;
            servis.VergiNo = dto.VergiNo;
            servis.VergiDairesi = dto.VergiDairesi;
            servis.SirketId = hedefSirketId;
            servis.AktifMi = dto.AktifMi;
            servis.GuncellemeTarihi = DateTime.Now;
            servis.GuncelleyenKullanici = kullanici.UserName ?? "api";

            await YetkiliServisIliskileriniYenileAsync(
                servis.Id,
                dto.KategoriIds,
                dto.MarkaIds,
                kullanici.UserName ?? "api",
                kategoriSil: dto.KategoriIds != null,
                markaSil: dto.MarkaIds != null);

            await _context.SaveChangesAsync();
            return Ok(AdminIslemSonucDto.BasariliSonuc("Yetkili servis guncellendi."));
        }

        [HttpPost("yetkili-servisler/sil")]
        public async Task<IActionResult> YetkiliServisSil([FromBody] AdminYetkiliServisDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || dto.Id <= 0)
                return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis id zorunludur."));

            var servis = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.SilindiMi
                    && (kapsam.sirketId == null || x.SirketId == kapsam.sirketId.Value));

            if (servis == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis bulunamadi."));

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == servis.Id);

            if (devreyeAlmaVar)
                return Ok(AdminIslemSonucDto.Basarisiz("Bu yetkili servis uzerinde devreye alma islemi oldugu icin silinemez."));

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = kullanici.UserName ?? "api";

            await _context.SaveChangesAsync();
            return Ok(AdminIslemSonucDto.BasariliSonuc("Yetkili servis silindi."));
        }

        [HttpPost("yetki-belgeleri/onay-listesi")]
        public async Task<IActionResult> YetkiBelgesiOnayListesi([FromBody] AdminYetkiBelgesiOnayFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminYetkiBelgesiOnayApiService.ListeleAsync(kapsam.sirketId));
        }

        [HttpPost("yetki-belgeleri/onay-gecmisi")]
        public async Task<IActionResult> YetkiBelgesiOnayGecmisi([FromBody] AdminYetkiBelgesiOnayGecmisiFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminYetkiBelgesiOnayApiService.GecmisAsync(dto, kapsam.sirketId));
        }

        [HttpPost("subeler/liste")]
        public async Task<IActionResult> Subeler([FromBody] AdminSubeListeFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminSubeApiService.ListeleAsync(dto, kapsam.sirketId));
        }

        [HttpPost("subeler/getir")]
        public async Task<IActionResult> SubeGetir([FromBody] AdminSubeGetirFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminSubeApiService.GetirAsync(dto, kapsam.sirketId));
        }

        [HttpPost("subeler/ekle")]
        public async Task<IActionResult> SubeEkle([FromBody] AdminSubeKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminSubeApiService.EkleAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("subeler/guncelle")]
        public async Task<IActionResult> SubeGuncelle([FromBody] AdminSubeKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminSubeApiService.GuncelleAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("subeler/durum")]
        public async Task<IActionResult> SubeDurum([FromBody] AdminSubeDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminSubeApiService.DurumDegistirAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("subeler/sil")]
        public async Task<IActionResult> SubeSil([FromBody] AdminSubeDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminSubeApiService.SilAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("devreye-almalar/liste")]
        public async Task<IActionResult> DevreyeAlmalar([FromBody] AdminDevreyeAlmaListeFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminRaporApiService.DevreyeAlmalarAsync(dto, kapsam.sirketId));
        }

        [HttpPost("devreye-almalar/getir")]
        public async Task<IActionResult> DevreyeAlmaGetir([FromBody] AdminDevreyeAlmaGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Devreye alma id zorunludur" });

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var kayit = await _adminRaporApiService.DevreyeAlmaGetirAsync(dto.Id, kapsam.sirketId);
            if (kayit == null)
                return NotFound(new { basarili = false, mesaj = "Devreye alma kaydi bulunamadi" });

            return Ok(kayit);
        }

        [HttpPost("yetki-belgeleri/uyarilar")]
        public async Task<IActionResult> YetkiBelgesiUyarilari([FromBody] AdminYetkiBelgesiUyariFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminRaporApiService.YetkiBelgesiUyarilariAsync(kapsam.sirketId));
        }

        [HttpPost("raporlar/ozet")]
        public async Task<IActionResult> RaporlarOzet([FromBody] AdminRaporOzetFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(await _adminRaporApiService.RaporlarOzetAsync(dto, kapsam.sirketId));
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
                        KullaniciTipi = 1,
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

                servisKullanicisi.KullaniciTipi = 1;
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

        private async Task<List<AdminSirketSecenekDto>> YonetilebilirSirketlerAsync(AppKullanici kullanici, int? kapsamSirketId)
        {
            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == 4
                || (kullanici.KullaniciTipi == 3 && !kullanici.SirketId.HasValue);

            var query = _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (!(genelSistemAdminMi && !kapsamSirketId.HasValue))
            {
                if (kapsamSirketId.HasValue)
                {
                    query = query.Where(x => x.Id == kapsamSirketId.Value);
                }
                else if (kullanici.SirketId.HasValue)
                {
                    query = query.Where(x => x.Id == kullanici.SirketId.Value);
                }
                else
                {
                    var yetkiliSirketIds = await _context.Dag_PersonelYetkiler
                        .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi)
                        .Select(x => x.SirketId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(x => yetkiliSirketIds.Contains(x.Id));
                }
            }

            return await query
                .OrderBy(x => x.SirketAdi)
                .Select(x => new AdminSirketSecenekDto
                {
                    Id = x.Id,
                    SirketAdi = x.SirketAdi
                })
                .ToListAsync();
        }

        private static List<string> NormalizeYetkiListesi(IEnumerable<string?> yetkiler)
        {
            var liste = yetkiler
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != YetkiTipleri.DAGITIM_SIRKET_YONET)
                .Select(x => x!)
                .Distinct()
                .ToList();

            return liste.Contains(YetkiTipleri.TAM_YETKI)
                ? new List<string> { YetkiTipleri.TAM_YETKI }
                : liste;
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

        private async Task<bool> KullaniciKapsamindaMi(AppKullanici yapan, AppKullanici hedef, int? sirketId)
        {
            if (yapan.Id == hedef.Id)
                return true;

            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || yapan.KullaniciTipi == 4
                || (yapan.KullaniciTipi == 3 && !yapan.SirketId.HasValue);

            if (genelSistemAdminMi && !sirketId.HasValue)
                return true;

            if (!sirketId.HasValue)
                return false;

            if (hedef.KullaniciTipi == 1 && hedef.FirmaId.HasValue)
            {
                return await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id == hedef.FirmaId.Value &&
                    !x.SilindiMi &&
                    x.SirketId == sirketId.Value);
            }

            return (hedef.KullaniciTipi == 2 || hedef.KullaniciTipi == 3) && hedef.SirketId == sirketId.Value;
        }

        private async Task<bool> SirketYonetimKapsamindaMi(AppKullanici yapan, int hedefSirketId, int? kapsamSirketId)
        {
            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || yapan.KullaniciTipi == 4
                || (yapan.KullaniciTipi == 3 && !yapan.SirketId.HasValue);

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

        private async Task YetkiliServisIliskileriniYenileAsync(
            int firmaId,
            List<int>? kategoriIds,
            List<int>? markaIds,
            string kullanici,
            bool kategoriSil,
            bool markaSil)
        {
            if (kategoriIds != null || kategoriSil)
            {
                var mevcutKategoriler = await _context.Ys_FirmaKategoriler
                    .Where(x => x.FirmaId == firmaId)
                    .ToListAsync();

                _context.Ys_FirmaKategoriler.RemoveRange(mevcutKategoriler);

                var gecerliKategoriIds = await _context.UrunKategoriler
                    .Where(x => !x.SilindiMi && kategoriIds != null && kategoriIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync();

                foreach (var kategoriId in gecerliKategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = firmaId,
                        KategoriId = kategoriId,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici,
                        SilindiMi = false
                    });
                }
            }

            if (markaIds != null || markaSil)
            {
                var mevcutMarkalar = await _context.Ys_FirmaMarkalar
                    .Where(x => x.FirmaId == firmaId)
                    .ToListAsync();

                _context.Ys_FirmaMarkalar.RemoveRange(mevcutMarkalar);

                var gecerliMarkaIds = await _context.Ys_Markalar
                    .Where(x => !x.SilindiMi && markaIds != null && markaIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync();

                foreach (var markaId in gecerliMarkaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = firmaId,
                        MarkaId = markaId,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici,
                        SilindiMi = false
                    });
                }
            }
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
