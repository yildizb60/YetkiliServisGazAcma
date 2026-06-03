using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AdminDashboardService _dashboardService;
        private readonly AdminYetkiliServisListeService _yetkiliServisListeService;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public AdminPanelApiController(
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            AdminDashboardService dashboardService,
            AdminYetkiliServisListeService yetkiliServisListeService,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _context = context;
            _userManager = userManager;
            _dashboardService = dashboardService;
            _yetkiliServisListeService = yetkiliServisListeService;
            _sehirFirmaKoduService = sehirFirmaKoduService;
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

            var query = _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (kapsam.sirketId == null || x.Firma.SirketId == kapsam.sirketId))
                .AsQueryable();

            var bekleyenler = await query
                .Where(x => x.Durum == 0)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            var onaylananlar = await query
                .Where(x => x.Durum == 1)
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            var reddedilenler = await query
                .Where(x => x.Durum == 2)
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            return Ok(new AdminYetkiBelgesiOnayListeDto
            {
                Bekleyenler = bekleyenler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList(),
                Onaylananlar = onaylananlar.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList(),
                Reddedilenler = reddedilenler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList()
            });
        }

        [HttpPost("subeler/liste")]
        public async Task<IActionResult> Subeler([FromBody] AdminSubeListeFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var firmalar = await SubeFirmaQuery(kapsam.sirketId)
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();

            var query = SubeTemelQuery(kapsam.sirketId);

            if (dto?.FirmaId > 0)
                query = query.Where(x => x.FirmaId == dto.FirmaId);

            if (!string.IsNullOrWhiteSpace(dto?.Q))
            {
                var q = dto.Q.Trim().ToLower();
                if (q.Length == 1)
                {
                    var likeStart = $"{q}%";
                    query = query.Where(x =>
                        !string.IsNullOrWhiteSpace(x.SubeAdi) &&
                        EF.Functions.Like(x.SubeAdi.ToLower(), likeStart));
                }
                else
                {
                    var likeAny = $"%{q}%";
                    query = query.Where(x =>
                        (!string.IsNullOrWhiteSpace(x.SubeAdi) && EF.Functions.Like(x.SubeAdi.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Il) && EF.Functions.Like(x.Il.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Ilce) && EF.Functions.Like(x.Ilce.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Telefon) && EF.Functions.Like(x.Telefon.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Adres) && EF.Functions.Like(x.Adres.ToLower(), likeAny)) ||
                        (x.Firma != null && !string.IsNullOrWhiteSpace(x.Firma.FirmaAdi) && EF.Functions.Like(x.Firma.FirmaAdi.ToLower(), likeAny)));
                }
            }

            var subeler = await query
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            return Ok(new AdminSubeListeDto
            {
                Subeler = subeler.Select(AdminSubeDto.FromEntity).ToList(),
                Firmalar = firmalar.Select(AdminSubeFirmaDto.FromEntity).ToList()
            });
        }

        [HttpPost("subeler/getir")]
        public async Task<IActionResult> SubeGetir([FromBody] AdminSubeGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return Ok(AdminIslemSonucDto.Basarisiz("Sube id zorunludur."));

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var sube = await SubeTemelQuery(kapsam.sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);

            if (sube == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Sube bulunamadi."));

            var firmalar = await SubeFirmaQuery(kapsam.sirketId)
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();

            return Ok(new AdminSubeDetayDto
            {
                Basarili = true,
                Sube = AdminSubeDto.FromEntity(sube),
                Firmalar = firmalar.Select(AdminSubeFirmaDto.FromEntity).ToList()
            });
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

            if (dto == null || dto.FirmaId <= 0 || string.IsNullOrWhiteSpace(dto.SubeAdi))
                return Ok(AdminIslemSonucDto.Basarisiz("Firma ve sube adi zorunludur."));

            var gecerliFirma = await SubeFirmaQuery(kapsam.sirketId).AnyAsync(x => x.Id == dto.FirmaId);
            if (!gecerliFirma)
                return Ok(AdminIslemSonucDto.Basarisiz("Secilen firma aktif yetkili servis kullanicisina sahip degil."));

            var yeni = new Ys_Sube
            {
                FirmaId = dto.FirmaId,
                SubeAdi = dto.SubeAdi.Trim(),
                Il = dto.Il,
                Ilce = dto.Ilce,
                Telefon = dto.Telefon,
                Adres = dto.Adres,
                AktifMi = dto.AktifMi,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_Subeler.Add(yeni);
            await _context.SaveChangesAsync();

            return Ok(AdminIslemSonucDto.BasariliSonuc("Sube kaydi eklendi."));
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

            if (dto == null || dto.Id <= 0 || dto.FirmaId <= 0 || string.IsNullOrWhiteSpace(dto.SubeAdi))
                return Ok(AdminIslemSonucDto.Basarisiz("Firma ve sube adi zorunludur."));

            var sube = await SubeTemelQuery(kapsam.sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (sube == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Sube bulunamadi."));

            var hedefFirmaGecerli = await SubeFirmaQuery(kapsam.sirketId).AnyAsync(x => x.Id == dto.FirmaId);
            if (!hedefFirmaGecerli)
                return Ok(AdminIslemSonucDto.Basarisiz("Secilen firma aktif yetkili servis kullanicisina sahip degil."));

            sube.FirmaId = dto.FirmaId;
            sube.SubeAdi = dto.SubeAdi.Trim();
            sube.Il = dto.Il;
            sube.Ilce = dto.Ilce;
            sube.Telefon = dto.Telefon;
            sube.Adres = dto.Adres;
            sube.AktifMi = dto.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";

            await _context.SaveChangesAsync();

            return Ok(AdminIslemSonucDto.BasariliSonuc("Sube guncellendi."));
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

            if (dto == null || dto.Id <= 0)
                return Ok(AdminIslemSonucDto.Basarisiz("Sube id zorunludur."));

            var sube = await SubeTemelQuery(kapsam.sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (sube == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Sube bulunamadi."));

            sube.AktifMi = !sube.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            return Ok(AdminIslemSonucDto.BasariliSonuc("Sube durumu guncellendi."));
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

            if (dto == null || dto.Id <= 0)
                return Ok(AdminIslemSonucDto.Basarisiz("Sube id zorunludur."));

            var sube = await SubeTemelQuery(kapsam.sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (sube == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Sube bulunamadi."));

            sube.SilindiMi = true;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            return Ok(AdminIslemSonucDto.BasariliSonuc("Sube kaydi silindi."));
        }

        [HttpPost("devreye-almalar/liste")]
        public async Task<IActionResult> DevreyeAlmalar([FromBody] AdminDevreyeAlmaListeFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var query = DevreyeAlmaTemelQuery(kapsam.sirketId);

            if (!string.IsNullOrWhiteSpace(dto?.Marka))
                query = query.Where(x => x.Marka != null && x.Marka.MarkaAdi != null && x.Marka.MarkaAdi.Contains(dto.Marka));
            if (!string.IsNullOrWhiteSpace(dto?.Servis))
                query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(dto.Servis));
            if (!string.IsNullOrWhiteSpace(dto?.Il))
                query = query.Where(x => x.Firma != null && x.Firma.FaaliyetIli != null && x.Firma.FaaliyetIli.Contains(dto.Il));
            if (dto?.Durum.HasValue == true)
                query = query.Where(x => x.Durum == dto.Durum.Value);
            if (dto?.BaslangicTarihi.HasValue == true)
                query = query.Where(x => x.OlusturmaTarihi >= dto.BaslangicTarihi.Value.Date);
            if (dto?.BitisTarihi.HasValue == true)
                query = query.Where(x => x.OlusturmaTarihi < dto.BitisTarihi.Value.Date.AddDays(1));

            var islemler = await query.OrderByDescending(x => x.OlusturmaTarihi).ToListAsync();
            var firmaIds = islemler.Select(x => x.FirmaId).Distinct().ToList();
            var subeler = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi && firmaIds.Contains(x.FirmaId))
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.MarkaAdi)
                .Select(x => new AdminMarkaSecenekDto
                {
                    Id = x.Id,
                    MarkaAdi = x.MarkaAdi
                })
                .ToListAsync();

            return Ok(new AdminDevreyeAlmaListeDto
            {
                Islemler = islemler.Select(AdminDevreyeAlmaDto.FromEntity).ToList(),
                Markalar = markalar,
                FirmaIlceleri = subeler
                    .GroupBy(x => x.FirmaId)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(s => s.Ilce).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "-")
            });
        }

        [HttpPost("devreye-almalar/getir")]
        public async Task<IActionResult> DevreyeAlmaGetir([FromBody] AdminDevreyeAlmaGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Devreye alma id zorunludur" });

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var kayit = await DevreyeAlmaTemelQuery(kapsam.sirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);

            if (kayit == null)
                return NotFound(new { basarili = false, mesaj = "Devreye alma kaydi bulunamadi" });

            return Ok(AdminDevreyeAlmaDto.FromEntity(kayit));
        }

        [HttpPost("yetki-belgeleri/uyarilar")]
        public async Task<IActionResult> YetkiBelgesiUyarilari([FromBody] AdminYetkiBelgesiUyariFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var bugun = DateTime.Now.Date;
            var bitisSinir = bugun.AddDays(30);
            var query = SertifikaTemelQuery(kapsam.sirketId)
                .Where(x => x.Durum == 1);

            var yaklasan = await query
                .Where(x => x.SertifikaBitisTarihi >= bugun && x.SertifikaBitisTarihi <= bitisSinir)
                .OrderBy(x => x.SertifikaBitisTarihi)
                .ToListAsync();

            var gecmis = await query
                .Where(x => x.SertifikaBitisTarihi < bugun)
                .OrderByDescending(x => x.SertifikaBitisTarihi)
                .ToListAsync();

            return Ok(new AdminYetkiBelgesiUyariListeDto
            {
                Yaklasan = yaklasan.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList(),
                Gecmis = gecmis.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList()
            });
        }

        [HttpPost("raporlar/ozet")]
        public async Task<IActionResult> RaporlarOzet([FromBody] AdminRaporOzetFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var basTarih = dto?.BaslangicTarihi?.Date ?? DateTime.Now.Date.AddDays(-30);
            var bitTarih = dto?.BitisTarihi?.Date ?? DateTime.Now.Date;
            var bitSonrasi = bitTarih.AddDays(1);
            var raporTipi = string.IsNullOrWhiteSpace(dto?.Tip) ? "devreye" : dto.Tip.Trim().ToLowerInvariant();

            var devreyeTemelQuery = DevreyeAlmaTemelQuery(kapsam.sirketId)
                .Where(x => x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi);

            var sertifikaTemelQuery = SertifikaTemelQuery(kapsam.sirketId)
                .Where(x => x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi);

            var devreyeSayisi = await devreyeTemelQuery.CountAsync();
            var devreyeTamamlanan = await devreyeTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var devreyeBekleyen = await devreyeTemelQuery.Where(x => x.Durum == 0).CountAsync();
            var devreyeIptal = await devreyeTemelQuery.Where(x => x.Durum == 2).CountAsync();
            var sertifikaOnayli = await sertifikaTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var sertifikaBekleyen = await sertifikaTemelQuery.Where(x => x.Durum == 0).CountAsync();
            var sertifikaReddedilen = await sertifikaTemelQuery.Where(x => x.Durum == 2).CountAsync();

            var aylikBaslangic = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);
            var aylikEtiketler = Enumerable.Range(0, 6)
                .Select(i => aylikBaslangic.AddMonths(i))
                .ToList();

            var aylikHam = await devreyeTemelQuery
                .Where(x => x.OlusturmaTarihi >= aylikBaslangic)
                .GroupBy(x => new { x.OlusturmaTarihi.Year, x.OlusturmaTarihi.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var aylikMap = aylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
            var chartAylikLabels = aylikEtiketler.Select(x => x.ToString("MM.yyyy")).ToList();
            var chartAylikData = aylikEtiketler
                .Select(x => aylikMap.TryGetValue($"{x.Year:D4}-{x.Month:D2}", out var value) ? value : 0)
                .ToList();

            var chartSirket = await devreyeTemelQuery
                .Where(x => x.Firma != null && x.Firma.Sirket != null)
                .GroupBy(x => x.Firma!.Sirket!.SirketAdi)
                .Select(g => new { Sirket = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(6)
                .ToListAsync();

            var chartMarka = await devreyeTemelQuery
                .Where(x => x.Marka != null)
                .GroupBy(x => x.Marka!.MarkaAdi)
                .Select(g => new { Marka = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(6)
                .ToListAsync();

            var sonuc = new AdminRaporOzetDto
            {
                BasTarih = basTarih,
                BitTarih = bitTarih,
                RaporTipi = raporTipi,
                DevreyeSayisi = devreyeSayisi,
                DevreyeTamamlanan = devreyeTamamlanan,
                DevreyeBekleyen = devreyeBekleyen,
                DevreyeIptal = devreyeIptal,
                SertifikaOnayli = sertifikaOnayli,
                SertifikaBekleyen = sertifikaBekleyen,
                SertifikaReddedilen = sertifikaReddedilen,
                ChartAylikLabels = chartAylikLabels,
                ChartAylikData = chartAylikData,
                ChartDurumData = new List<int> { sertifikaOnayli, sertifikaBekleyen, sertifikaReddedilen },
                ChartSirketLabels = chartSirket.Select(x => x.Sirket).ToList(),
                ChartSirketData = chartSirket.Select(x => x.Sayi).ToList(),
                ChartMarkaLabels = chartMarka.Select(x => x.Marka).ToList(),
                ChartMarkaData = chartMarka.Select(x => x.Sayi).ToList(),
                Sirketler = await SirketSecenekleriAsync(kapsam.sirketId)
            };

            if (raporTipi == "onayli" || raporTipi == "bekleyen" || raporTipi == "reddedilen")
            {
                var durum = raporTipi == "onayli" ? 1 : (raporTipi == "bekleyen" ? 0 : 2);
                var sertifikaIslemler = await sertifikaTemelQuery
                    .Where(x => x.Durum == durum)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                sonuc.ListeTipi = "sertifika";
                sonuc.SertifikaIslemler = sertifikaIslemler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList();
            }
            else
            {
                var sonIslemler = await devreyeTemelQuery
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                sonuc.ListeTipi = "devreye";
                sonuc.SonIslemler = sonIslemler.Select(AdminDevreyeAlmaDto.FromEntity).ToList();
            }

            return Ok(sonuc);
        }

        private IQueryable<Ys_DevreyeAlma> DevreyeAlmaTemelQuery(int? sirketId)
        {
            return _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private IQueryable<Ys_Sertifika> SertifikaTemelQuery(int? sirketId)
        {
            return _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private IQueryable<int> AktifYetkiliServisFirmaIdsQuery()
        {
            return _context.Users
                .Where(u => u.KullaniciTipi == 1 && u.AktifMi && u.FirmaId.HasValue)
                .Select(u => u.FirmaId!.Value)
                .Distinct();
        }

        private IQueryable<Ys_Firma> SubeFirmaQuery(int? sirketId)
        {
            var aktifFirmaIds = AktifYetkiliServisFirmaIdsQuery();
            var query = _context.Ys_Firmalar
                .Where(x => !x.SilindiMi && x.AktifMi && aktifFirmaIds.Contains(x.Id));

            if (sirketId.HasValue)
                query = query.Where(x => x.SirketId == sirketId.Value);

            return query;
        }

        private IQueryable<Ys_Sube> SubeTemelQuery(int? sirketId)
        {
            var aktifFirmaIds = AktifYetkiliServisFirmaIdsQuery();
            var query = _context.Ys_Subeler
                .Include(x => x.Firma)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.Firma.AktifMi
                    && aktifFirmaIds.Contains(x.FirmaId));

            if (sirketId.HasValue)
                query = query.Where(x => x.Firma != null && x.Firma.SirketId == sirketId.Value);

            return query;
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

    public class AdminDevreyeAlmaListeFiltreDto
    {
        public int? SirketId { get; set; }
        public string? Marka { get; set; }
        public string? Servis { get; set; }
        public string? Il { get; set; }
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
        public int SertifikaOnayli { get; set; }
        public int SertifikaBekleyen { get; set; }
        public int SertifikaReddedilen { get; set; }
        public List<string?> ChartSirketLabels { get; set; } = new();
        public List<int> ChartSirketData { get; set; } = new();
        public List<string> ChartAylikLabels { get; set; } = new();
        public List<int> ChartAylikData { get; set; } = new();
        public List<int> ChartDurumData { get; set; } = new();
        public List<string?> ChartMarkaLabels { get; set; } = new();
        public List<int> ChartMarkaData { get; set; } = new();
        public List<AdminDevreyeAlmaDto> SonIslemler { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> SertifikaIslemler { get; set; } = new();
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
        public string? TeknisyenSertifikaNo { get; set; }
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
                TeknisyenSertifikaNo = devreyeAlma.TeknisyenSertifikaNo,
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
        public string? VergiDairesi { get; set; }
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
        public DateTime? SertifikaBaslangicTarihi { get; set; }
        public DateTime SertifikaBitisTarihi { get; set; }
        public string? DosyaYolu { get; set; }
        public string? OnaylayanKullanici { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? RedGerekce { get; set; }

        public static AdminYetkiBelgesiOnayDto FromEntity(Ys_Sertifika sertifika)
        {
            return new AdminYetkiBelgesiOnayDto
            {
                Id = sertifika.Id,
                FirmaId = sertifika.FirmaId,
                FirmaAdi = sertifika.Firma?.FirmaAdi,
                VergiNo = sertifika.Firma?.VergiNo,
                SirketAdi = sertifika.Firma?.Sirket?.SirketAdi,
                Durum = sertifika.Durum,
                OlusturmaTarihi = sertifika.OlusturmaTarihi,
                SertifikaBaslangicTarihi = sertifika.SertifikaBaslangicTarihi,
                SertifikaBitisTarihi = sertifika.SertifikaBitisTarihi,
                DosyaYolu = sertifika.DosyaYolu,
                OnaylayanKullanici = sertifika.OnaylayanKullanici,
                OnayTarihi = sertifika.OnayTarihi,
                RedGerekce = sertifika.RedGerekce
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
