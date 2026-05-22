using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Text;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("AdminPanel")]
    public class AdminPanelController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AppDbContext _context;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
        private readonly AktifSirketService _aktifSirketService;
        private readonly AdminDashboardService _adminDashboardService;
        private readonly AdminDashboardApiClient _adminDashboardApiClient;
        private readonly AdminKullaniciApiClient _adminKullaniciApiClient;
        private readonly AdminYetkiliServisApiClient _adminYetkiliServisApiClient;

        public AdminPanelController(
            UserManager<AppKullanici> userManager,
            AppDbContext context,
            SehirFirmaKoduService sehirFirmaKoduService,
            AktifSirketService aktifSirketService,
            AdminDashboardService adminDashboardService,
            AdminDashboardApiClient adminDashboardApiClient,
            AdminKullaniciApiClient adminKullaniciApiClient,
            AdminYetkiliServisApiClient adminYetkiliServisApiClient)
        {
            _userManager = userManager;
            _context = context;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _aktifSirketService = aktifSirketService;
            _adminDashboardService = adminDashboardService;
            _adminDashboardApiClient = adminDashboardApiClient;
            _adminKullaniciApiClient = adminKullaniciApiClient;
            _adminYetkiliServisApiClient = adminYetkiliServisApiClient;
        }

        private static bool KullanilanKategoriMi(string? ad)
        {
            var key = NormalizeKategori(ad);

            return key == "kombi"
                || key.Contains("merkezikazan")
                || key.Contains("sofben")
                || key.Contains("sohben");
        }

        private static string NormalizeKategori(string? ad)
        {
            if (string.IsNullOrWhiteSpace(ad))
                return string.Empty;

            var normalized = ad.Trim().ToLower(new CultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
            var chars = normalized
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
                .ToArray();

            return new string(chars)
                .Replace("ı", "i")
                .Replace("ş", "s")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c");
        }

        private async Task<List<UrunKategori>> KullanilanKategorileriGetir()
        {
            return (await _context.UrunKategoriler
                    .Where(x => !x.SilindiMi)
                    .OrderBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .ToListAsync())
                .Where(x => KullanilanKategoriMi(x.Ad))
                .GroupBy(x => NormalizeKategori(x.Ad))
                .Select(g => g
                    .OrderByDescending(x => x.AktifMi)
                    .ThenBy(x => string.IsNullOrWhiteSpace(x.IconUrl) ? 1 : 0)
                    .ThenBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .First())
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .ToList();
        }

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<int> GetOnayBekleyenCount()
        {
            var kullanici = await GetCurrentUser();
            var sirketId = kullanici == null ? null : await _aktifSirketService.AktifSirketIdAsync(kullanici);

            return await _adminDashboardService.OnayBekleyenSayisiAsync(sirketId);
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var kullanici = await GetCurrentUser();
            var sirketId = kullanici == null ? null : await _aktifSirketService.AktifSirketIdAsync(kullanici);

            ViewBag.OnayBekleyen = await _adminDashboardService.OnayBekleyenSayisiAsync(sirketId);
            ViewBag.SuresiBitecek = await _adminDashboardService.SuresiBitecekSayisiAsync(sirketId);
            await next();
        }

        private static List<string> ValidatePassword(string sifre)
        {
            var hatalar = new List<string>();
            if (string.IsNullOrWhiteSpace(sifre))
            {
                hatalar.Add("Şifre zorunludur.");
                return hatalar;
            }

            if (sifre.Length < 6)
                hatalar.Add("Şifre en az 6 karakter olmalıdır.");

            if (!sifre.Any(char.IsLower))
                hatalar.Add("Şifre en az bir küçük harf içermelidir.");

            if (!sifre.Any(char.IsDigit))
                hatalar.Add("Şifre en az bir rakam içermelidir.");

            return hatalar;
        }

        private async Task<bool> KullaniciYonetebilirMi(AppKullanici kullanici)
        {
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici) || await _aktifSirketService.SirketAdminMi(kullanici))
                return true;

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                (aktifSirketId == null || x.SirketId == aktifSirketId) &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.KULLANICI_YONET));
        }

        private async Task<List<Dag_Sirket>> YonetilebilirSirketler(AppKullanici kullanici)
        {
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici))
            {
                return await _context.Dag_Sirketler
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .OrderBy(x => x.SirketAdi)
                    .ToListAsync();
            }

            return await _aktifSirketService.KullaniciSirketleriAsync(kullanici);
        }

        private async Task<bool> KullaniciKapsamindaMi(AppKullanici yapan, AppKullanici hedef)
        {
            if (yapan.Id == hedef.Id)
                return true;

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(yapan);
            if (await _aktifSirketService.GenelSistemAdminMi(yapan) && !aktifSirketId.HasValue)
                return true;

            if (!aktifSirketId.HasValue)
                return false;

            if (hedef.KullaniciTipi == 1 && hedef.FirmaId.HasValue)
            {
                return await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id == hedef.FirmaId.Value &&
                    !x.SilindiMi &&
                    x.SirketId == aktifSirketId.Value);
            }

            return (hedef.KullaniciTipi == 2 || hedef.KullaniciTipi == 3) && hedef.SirketId == aktifSirketId.Value;
        }

        private IQueryable<int> GetAktifYetkiliServisFirmaIdsQuery()
        {
            return _context.Users
                .Where(u => u.KullaniciTipi == 1 && u.AktifMi && u.FirmaId.HasValue)
                .Select(u => u.FirmaId!.Value)
                .Distinct();
        }

        private async Task<string?> GetYetkiliServisRolAdiAsync()
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

        // Yetkili servis firmaları ile kullanıcı hesaplarını senkron tutar.
        // Amaç: Yetkili Servisler ekranındaki her firma, Kullanıcılar ekranında da yönetilebilir olsun.
        private async Task SyncYetkiliServisKullanicilariAsync()
        {
            var yetkiliServisRolAdi = await GetYetkiliServisRolAdiAsync();
            var firmalar = await _context.Ys_Firmalar
                .Where(x => !x.SilindiMi)
                .ToListAsync();

            foreach (var firma in firmalar)
            {
                if (string.IsNullOrWhiteSpace(firma.Email))
                    continue;

                var email = firma.Email.Trim();
                var adSoyad = !string.IsNullOrWhiteSpace(firma.YetkiliKisi) ? firma.YetkiliKisi : firma.FirmaAdi;

                // Önce firmaya bağlı mevcut kullanıcıyı bul
                var kullanici = await _context.Users
                    .FirstOrDefaultAsync(u => u.FirmaId == firma.Id);

                // FirmaId yoksa e-posta üzerinden bulup bağla
                if (kullanici == null)
                {
                    kullanici = await _userManager.FindByEmailAsync(email);
                    if (kullanici != null && !kullanici.FirmaId.HasValue)
                    {
                        kullanici.FirmaId = firma.Id;
                    }
                }

                if (kullanici == null)
                {
                    // Hiç kullanıcı yoksa otomatik oluştur
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

                // Mevcut kullanıcıyı servis hesabı standardına çek
                kullanici.KullaniciTipi = 1;
                kullanici.FirmaId = firma.Id;
                kullanici.SirketId = firma.SirketId;
                kullanici.AktifMi = firma.AktifMi;

                if (string.IsNullOrWhiteSpace(kullanici.AdSoyad))
                    kullanici.AdSoyad = adSoyad;

                if (string.IsNullOrWhiteSpace(kullanici.PhoneNumber) && !string.IsNullOrWhiteSpace(firma.Telefon))
                    kullanici.PhoneNumber = firma.Telefon;

                // E-posta değişimi, sadece başka kullanıcıyla çakışmıyorsa uygulanır
                if (!string.Equals(kullanici.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    var emailSahibi = await _userManager.FindByEmailAsync(email);
                    if (emailSahibi == null || emailSahibi.Id == kullanici.Id)
                    {
                        kullanici.Email = email;
                        kullanici.UserName = email;
                    }
                }

                await _userManager.UpdateAsync(kullanici);

                if (!string.IsNullOrWhiteSpace(yetkiliServisRolAdi))
                {
                    if (!await _userManager.IsInRoleAsync(kullanici, yetkiliServisRolAdi!))
                        await _userManager.AddToRoleAsync(kullanici, yetkiliServisRolAdi!);
                }
            }
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dashboard = await _adminDashboardApiClient.GetirAsync(kullanici, sirketId);
            ViewBag.AdminDashboardVeriKaynagi = "API";

            if (dashboard == null)
            {
                TempData["Hata"] = "Admin dashboard verisi API üzerinden alınamadı. Lütfen API uygulamasının çalıştığını kontrol edin.";
                dashboard = new AdminDashboardOzet();
                ViewBag.AdminDashboardVeriKaynagi = "API erişilemedi";
            }

            ViewBag.ToplamDevreyeAlma = dashboard.ToplamDevreyeAlma;
            ViewBag.ToplamFirma = dashboard.ToplamFirma;
            ViewBag.OnayBekleyen = dashboard.OnayBekleyen;
            ViewBag.SuresiBitecek = dashboard.SuresiBitecek;
            ViewBag.ToplamSirket = dashboard.ToplamSirket;
            ViewBag.BuAyDevreyeAlma = dashboard.BuAyDevreyeAlma;
            ViewBag.SonSertifikalar = dashboard.SonSertifikalar;
            ViewBag.SonDevreyeAlmalar = dashboard.SonDevreyeAlmalar;

            ViewBag.Kullanici = kullanici;
            return View("~/Views/AdminPanel/Index.cshtml");
        }

        [HttpGet("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.SuresiBitecek = await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi
                    && x.Durum == 1
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.SertifikaBitisTarihi <= DateTime.Now.AddDays(30)
                    && x.SertifikaBitisTarihi >= DateTime.Now)
                .CountAsync();
            ViewBag.Kullanici = kullanici;
            return View("~/Views/AdminPanel/Profil.cshtml");
        }

        [HttpPost("profil-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilGuncelle(string adSoyad, string email, string telefon)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            kullanici.AdSoyad = adSoyad;
            kullanici.Email = email;
            kullanici.UserName = email;
            kullanici.PhoneNumber = telefon;

            var sonuc = await _userManager.UpdateAsync(kullanici);
            if (sonuc.Succeeded) TempData["Basarili"] = "Profil bilgileriniz başarıyla güncellendi.";
            else TempData["Hata"] = "Güncelleme sırasında hata oluştu.";

            return RedirectToAction(nameof(Profil));
        }

        [HttpPost("sifre-degistir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SifreDegistir(string mevcutSifre, string yeniSifre, string yeniSifreTekrar)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            if (yeniSifre != yeniSifreTekrar)
            {
                TempData["SifreHata"] = "Yeni şifreler eşleşmiyor.";
                return RedirectToAction(nameof(Profil));
            }

            var sonuc = await _userManager.ChangePasswordAsync(kullanici, mevcutSifre, yeniSifre);
            if (sonuc.Succeeded) TempData["SifreBasarili"] = "Şifreniz başarıyla değiştirildi.";
            else TempData["SifreHata"] = "Mevcut şifreniz yanlış.";

            return RedirectToAction(nameof(Profil));
        }

        [HttpGet("personeller")]
        public async Task<IActionResult> Personeller(string? q, string? sirket, string? durum)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var personeller = await _context.Users
                .Include(x => x.Sirket)
                .Where(x => x.KullaniciTipi == 2)
                .OrderBy(x => x.AdSoyad)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var aranacak = q.Trim();
                personeller = personeller.Where(x =>
                    (!string.IsNullOrWhiteSpace(x.AdSoyad) && x.AdSoyad.IndexOf(aranacak, StringComparison.CurrentCultureIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(x.Email) && x.Email.IndexOf(aranacak, StringComparison.CurrentCultureIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(x.PhoneNumber) && x.PhoneNumber.IndexOf(aranacak, StringComparison.CurrentCultureIgnoreCase) >= 0))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(sirket))
            {
                var sirketArama = sirket.Trim();
                personeller = personeller.Where(x =>
                    x.Sirket != null &&
                    !string.IsNullOrWhiteSpace(x.Sirket.SirketAdi) &&
                    x.Sirket.SirketAdi.IndexOf(sirketArama, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(durum))
            {
                var aktifMi = durum.Equals("Aktif", StringComparison.CurrentCultureIgnoreCase);
                personeller = personeller.Where(x => x.AktifMi == aktifMi).ToList();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Personeller = personeller;
            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliSirket = sirket ?? "";
            ViewBag.SeciliDurum = durum ?? "";
            return View("~/Views/AdminPanel/Personeller.cshtml");
        }

        [HttpGet("personeller/ekle")]
        public async Task<IActionResult> PersonelEkle()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Sirketler = await _context.Dag_Sirketler
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.SirketAdi)
                .ToListAsync();
            return View("~/Views/AdminPanel/PersonelEkle.cshtml");
        }

        [HttpPost("personeller/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PersonelEkle(string adSoyad, string email, string telefon, int sirketId, string sifre)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var sifreHatalari = ValidatePassword(sifre);
            if (sifreHatalari.Count > 0)
            {
                ViewBag.Kullanici = kullanici;
                ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                ViewBag.Sirketler = await _context.Dag_Sirketler
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .OrderBy(x => x.SirketAdi)
                    .ToListAsync();
                ViewBag.Hata = string.Join(" ", sifreHatalari);
                ViewBag.FormAdSoyad = adSoyad;
                ViewBag.FormEmail = email;
                ViewBag.FormTelefon = telefon;
                ViewBag.FormSirketId = sirketId;
                return View("~/Views/AdminPanel/PersonelEkle.cshtml");
            }

            var yeni = new AppKullanici
            {
                UserName = email,
                Email = email,
                PhoneNumber = telefon,
                AdSoyad = adSoyad,
                KullaniciTipi = 2,
                SirketId = sirketId,
                AktifMi = true,
                EmailConfirmed = true
            };

            var sonuc = await _userManager.CreateAsync(yeni, sifre);
            if (!sonuc.Succeeded)
            {
                ViewBag.Kullanici = kullanici;
                ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                ViewBag.Sirketler = await _context.Dag_Sirketler
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .OrderBy(x => x.SirketAdi)
                    .ToListAsync();
                ViewBag.Hata = string.Join(", ", sonuc.Errors.Select(x => x.Description));
                ViewBag.FormAdSoyad = adSoyad;
                ViewBag.FormEmail = email;
                ViewBag.FormTelefon = telefon;
                ViewBag.FormSirketId = sirketId;
                return View("~/Views/AdminPanel/PersonelEkle.cshtml");
            }

            await _userManager.AddToRoleAsync(yeni, "Personel");
            TempData["Basarili"] = "Personel başarıyla oluşturuldu.";
            return Redirect("/AdminPanel/personeller");
        }

        [HttpGet("kullanicilar")]
        public async Task<IActionResult> Kullanicilar(string? q, string? tip, string? durum, string? bagli)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            await SyncYetkiliServisKullanicilariAsync();

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kullanicilar = await _adminKullaniciApiClient.ListeleAsync(
                kullanici,
                aktifSirketId,
                q,
                tip,
                durum,
                bagli);

            if (kullanicilar == null)
            {
                TempData["Hata"] = "Kullanıcı listesi API üzerinden alınamadı. Lütfen API uygulamasının çalıştığını kontrol edin.";
                kullanicilar = new List<AppKullanici>();
            }

            // Yetkili servis kullanıcılarında boş kalan alanları, bağlı firma bilgisinden gösterim amaçlı tamamla
            foreach (var k in kullanicilar.Where(x => x.KullaniciTipi == 1 && x.Firma != null))
            {
                if (string.IsNullOrWhiteSpace(k.AdSoyad))
                    k.AdSoyad = !string.IsNullOrWhiteSpace(k.Firma!.YetkiliKisi) ? k.Firma.YetkiliKisi : k.Firma.FirmaAdi;

                if (string.IsNullOrWhiteSpace(k.Email) && !string.IsNullOrWhiteSpace(k.Firma!.Email))
                    k.Email = k.Firma.Email;

                if (string.IsNullOrWhiteSpace(k.PhoneNumber) && !string.IsNullOrWhiteSpace(k.Firma!.Telefon))
                    k.PhoneNumber = k.Firma.Telefon;
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Kullanicilar = kullanicilar;
            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliTip = tip ?? "";
            ViewBag.SeciliDurum = durum ?? "";
            ViewBag.SeciliBagli = bagli ?? "";
            return View("~/Views/AdminPanel/Kullanicilar.cshtml");
        }

        [HttpGet("kullanicilar/ekle")]
        public async Task<IActionResult> KullaniciEkle()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            var yonetilebilirSirketler = await YonetilebilirSirketler(kullanici);
            var yonetilebilirSirketIds = yonetilebilirSirketler.Select(x => x.Id).ToHashSet();
            ViewBag.Sirketler = yonetilebilirSirketler;
            ViewBag.Firmalar = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi && x.AktifMi && yonetilebilirSirketIds.Contains(x.SirketId))
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();
            return View("~/Views/AdminPanel/KullaniciEkle.cshtml");
        }

        [HttpPost("kullanicilar/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciEkle(string adSoyad, string email, string telefon, string sifre, string rol, int? sirketId, int? firmaId)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            async Task<IActionResult> FormHata(string mesaj)
            {
                ViewBag.Kullanici = kullanici;
                ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                var yonetilebilirSirketler = await YonetilebilirSirketler(kullanici);
                var yonetilebilirSirketIds = yonetilebilirSirketler.Select(x => x.Id).ToHashSet();
                ViewBag.Sirketler = yonetilebilirSirketler;
                ViewBag.Firmalar = await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .Where(x => !x.SilindiMi && x.AktifMi && yonetilebilirSirketIds.Contains(x.SirketId))
                    .OrderBy(x => x.FirmaAdi)
                    .ToListAsync();
                ViewBag.Hata = mesaj;
                ViewBag.FormAdSoyad = adSoyad;
                ViewBag.FormEmail = email;
                ViewBag.FormTelefon = telefon;
                ViewBag.FormRol = rol;
                ViewBag.FormSirketId = sirketId ?? 0;
                ViewBag.FormFirmaId = firmaId ?? 0;
                return View("~/Views/AdminPanel/KullaniciEkle.cshtml");
            }

            rol = (rol ?? "").Trim();
            if (string.Equals(rol, "Servis", StringComparison.OrdinalIgnoreCase))
                rol = "YetkiliServis";
            if (string.Equals(rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                rol = "SirketAdmin";

            var gecerliRoller = new[] { "GenelSistemAdmin", "SirketAdmin", "Personel", "YetkiliServis" };
            if (!gecerliRoller.Any(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase)))
                return await FormHata("Rol seçilmelidir.");

            rol = gecerliRoller.First(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase));

            var genelSistemAdmin = await _aktifSirketService.GenelSistemAdminMi(kullanici);
            if (rol == "GenelSistemAdmin" && !genelSistemAdmin)
                return await FormHata("Genel Sistem Admini sadece genel sistem admini tarafından oluşturulabilir.");

            var sifreHatalari = ValidatePassword(sifre);
            if (sifreHatalari.Count > 0)
                return await FormHata(string.Join(" ", sifreHatalari));

            int kullaniciTipi = rol == "GenelSistemAdmin" ? 4 : rol == "SirketAdmin" ? 3 : rol == "Personel" ? 2 : 1;

            if ((kullaniciTipi == 3 || kullaniciTipi == 2 || kullaniciTipi == 1) && (!sirketId.HasValue || sirketId.Value <= 0))
            {
                var mesaj = kullaniciTipi == 1
                    ? "Yetkili servis için bağlı dağıtım şirketi seçilmelidir."
                    : kullaniciTipi == 2
                        ? "Personel için şirket seçilmelidir."
                        : "Şirket admini için şirket seçilmelidir.";
                return await FormHata(mesaj);
            }

            if (kullaniciTipi == 3 || kullaniciTipi == 2 || kullaniciTipi == 1)
            {
                var yonetilebilirSirketIds = (await YonetilebilirSirketler(kullanici)).Select(x => x.Id).ToHashSet();
                var sirketVar = yonetilebilirSirketIds.Contains(sirketId!.Value);
                if (!sirketVar)
                    return await FormHata("Seçilen şirket bulunamadı, aktif değil veya bu şirket için yetkiniz yok.");
            }

            // Mantıksal olarak bu alanlar rol bazlıdır; yanlış geleni server tarafında sıfırla.
            if (kullaniciTipi != 3 && kullaniciTipi != 2 && kullaniciTipi != 1)
                sirketId = null;
            if (kullaniciTipi != 1)
                firmaId = null;

            Ys_Firma? firma = null;
            if (kullaniciTipi == 1)
            {
                // Yetkili servis kullanıcıları bu ekrandan şirket seçimi ile oluşturulur;
                // firma kaydı otomatik üretilip kullanıcıya bağlanır.
                firmaId = null;
            }

            var yeni = new AppKullanici
            {
                UserName = email,
                Email = email,
                PhoneNumber = telefon,
                AdSoyad = adSoyad,
                KullaniciTipi = kullaniciTipi,
                SirketId = (kullaniciTipi == 3 || kullaniciTipi == 2 || kullaniciTipi == 1) ? sirketId : null,
                FirmaId = null,
                AktifMi = true,
                EmailConfirmed = true
            };

            var sonuc = await _userManager.CreateAsync(yeni, sifre);
            if (!sonuc.Succeeded)
                return await FormHata(string.Join(", ", sonuc.Errors.Select(x => x.Description)));

            if (kullaniciTipi == 1)
            {
                try
                {
                    firma = new Ys_Firma
                    {
                        FirmaAdi = adSoyad,
                        YetkiliKisi = adSoyad,
                        Telefon = telefon,
                        Email = email,
                        SirketId = sirketId!.Value,
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
                    return await FormHata("Yetkili servis kaydı oluşturulurken hata oluştu. Lütfen tekrar deneyin.");
                }
            }

            var atanacakRol = rol;
            if (rol == "YetkiliServis")
            {
                var ysRol = await GetYetkiliServisRolAdiAsync();
                if (string.IsNullOrWhiteSpace(ysRol))
                    return await FormHata("Yetkili Servis rolü sistemde bulunamadı.");
                atanacakRol = ysRol!;
            }

            var rolVarMi = await _context.Set<IdentityRole>()
                .AnyAsync(r => r.Name != null && r.Name.ToLower() == atanacakRol.ToLower());
            if (!rolVarMi)
                return await FormHata($"Rol bulunamadı: {atanacakRol}");

            await _userManager.AddToRoleAsync(yeni, atanacakRol);
            if (rol == "GenelSistemAdmin")
                await _userManager.AddToRoleAsync(yeni, KullaniciRolAdlari.EskiSuperAdmin);

            TempData["Basarili"] = "Kullanici başarıyla oluşturuldu.";
            return Redirect("/AdminPanel/kullanicilar");
        }

        [HttpGet("kullanicilar/duzenle/{id}")]
        [HttpGet("kullanicilar/Düzenle/{id}")]
        public async Task<IActionResult> KullaniciDuzenle(string id, string? returnUrl)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var hedef = await _context.Users
                .Include(x => x.Sirket)
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (hedef == null) return Redirect("/AdminPanel/kullanicilar");
            if (!await KullaniciKapsamindaMi(kullanici, hedef)) return Redirect("/AdminPanel/kullanicilar");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Hedef = hedef;
            var yonetilebilirSirketler = await YonetilebilirSirketler(kullanici);
            var yonetilebilirSirketIds = yonetilebilirSirketler.Select(x => x.Id).ToHashSet();
            ViewBag.Sirketler = yonetilebilirSirketler;
            ViewBag.Firmalar = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi && x.AktifMi && yonetilebilirSirketIds.Contains(x.SirketId))
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
            return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
        }

        [HttpPost("kullanicilar/duzenle/{id}")]
        [HttpPost("kullanicilar/Düzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciDuzenle(string id, string adSoyad, string email, string telefon, bool aktifMi, int? sirketId, int? firmaId, string? yeniSifre, string? yeniSifreTekrar, string? returnUrl)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (hedef == null) return Redirect("/AdminPanel/kullanicilar");
            if (!await KullaniciKapsamindaMi(kullanici, hedef)) return Redirect("/AdminPanel/kullanicilar");

            if (hedef.KullaniciTipi == 3 || hedef.KullaniciTipi == 2)
            {
                if (!sirketId.HasValue || sirketId.Value <= 0)
                {
                    ViewBag.Kullanici = kullanici;
                    ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                    ViewBag.Hedef = hedef;
                    ViewBag.Sirketler = await _context.Dag_Sirketler
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.SirketAdi)
                        .ToListAsync();
                    ViewBag.Firmalar = await _context.Ys_Firmalar
                        .Include(x => x.Sirket)
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.FirmaAdi)
                        .ToListAsync();
                    ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                    ViewBag.Hata = hedef.KullaniciTipi == 2
                        ? "Personel için şirket seçilmelidir."
                        : "Şirket admini için şirket seçilmelidir.";
                    return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
                }

                hedef.SirketId = sirketId;
                hedef.FirmaId = null;
            }
            else if (hedef.KullaniciTipi == 1)
            {
                if (!firmaId.HasValue || firmaId.Value <= 0)
                {
                    ViewBag.Kullanici = kullanici;
                    ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                    ViewBag.Hedef = hedef;
                    ViewBag.Sirketler = await _context.Dag_Sirketler
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.SirketAdi)
                        .ToListAsync();
                    ViewBag.Firmalar = await _context.Ys_Firmalar
                        .Include(x => x.Sirket)
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.FirmaAdi)
                        .ToListAsync();
                    ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                    ViewBag.Hata = "Yetkili servis kullanıcısı için firma seçilmelidir.";
                    return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
                }

                var firma = await _context.Ys_Firmalar
                    .FirstOrDefaultAsync(x => x.Id == firmaId.Value && !x.SilindiMi);
                if (firma == null)
                {
                    ViewBag.Kullanici = kullanici;
                    ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                    ViewBag.Hedef = hedef;
                    ViewBag.Sirketler = await _context.Dag_Sirketler
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.SirketAdi)
                        .ToListAsync();
                    ViewBag.Firmalar = await _context.Ys_Firmalar
                        .Include(x => x.Sirket)
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.FirmaAdi)
                        .ToListAsync();
                    ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                    ViewBag.Hata = "Seçilen firma bulunamadı.";
                    return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
                }

                hedef.FirmaId = firma.Id;
                hedef.SirketId = firma.SirketId;
            }
            else
            {
                hedef.SirketId = null;
                hedef.FirmaId = null;
            }

            hedef.AdSoyad = adSoyad;
            hedef.Email = email;
            hedef.UserName = email;
            hedef.PhoneNumber = telefon;
            hedef.AktifMi = aktifMi;

            var sonuc = await _userManager.UpdateAsync(hedef);
            if (!sonuc.Succeeded)
            {
                ViewBag.Kullanici = kullanici;
                ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                ViewBag.Hedef = hedef;
                ViewBag.Sirketler = await _context.Dag_Sirketler
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .OrderBy(x => x.SirketAdi)
                    .ToListAsync();
                ViewBag.Firmalar = await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .OrderBy(x => x.FirmaAdi)
                    .ToListAsync();
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                ViewBag.Hata = string.Join(", ", sonuc.Errors.Select(x => x.Description));
                return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
            }

            if (!string.IsNullOrWhiteSpace(yeniSifre) && !string.IsNullOrWhiteSpace(yeniSifreTekrar))
            {
                if (yeniSifre != yeniSifreTekrar)
                {
                    ViewBag.Kullanici = kullanici;
                    ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                    ViewBag.Hedef = hedef;
                    ViewBag.Sirketler = await _context.Dag_Sirketler
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.SirketAdi)
                        .ToListAsync();
                    ViewBag.Firmalar = await _context.Ys_Firmalar
                        .Include(x => x.Sirket)
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.FirmaAdi)
                        .ToListAsync();
                    ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                    ViewBag.Hata = "Yeni şifreler eşleşmiyor.";
                    return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
                }

                var sifreHatalari = ValidatePassword(yeniSifre ?? "");
                if (sifreHatalari.Count > 0)
                {
                    ViewBag.Kullanici = kullanici;
                    ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                    ViewBag.Hedef = hedef;
                    ViewBag.Sirketler = await _context.Dag_Sirketler
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.SirketAdi)
                        .ToListAsync();
                    ViewBag.Firmalar = await _context.Ys_Firmalar
                        .Include(x => x.Sirket)
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.FirmaAdi)
                        .ToListAsync();
                    ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                    ViewBag.Hata = string.Join(" ", sifreHatalari);
                    return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(hedef);
                var sifreSonuc = await _userManager.ResetPasswordAsync(hedef, token, yeniSifre ?? "");
                if (!sifreSonuc.Succeeded)
                {
                    ViewBag.Kullanici = kullanici;
                    ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                    ViewBag.Hedef = hedef;
                    ViewBag.Sirketler = await _context.Dag_Sirketler
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.SirketAdi)
                        .ToListAsync();
                    ViewBag.Firmalar = await _context.Ys_Firmalar
                        .Include(x => x.Sirket)
                        .Where(x => !x.SilindiMi && x.AktifMi)
                        .OrderBy(x => x.FirmaAdi)
                        .ToListAsync();
                    ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                    ViewBag.Hata = string.Join(", ", sifreSonuc.Errors.Select(x => x.Description));
                    return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
                }
            }

            var hedefUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
            if (!hedefUrl.StartsWith("/AdminPanel", StringComparison.OrdinalIgnoreCase))
                hedefUrl = "/AdminPanel/kullanicilar";

            TempData["Basarili"] = "Kullanici güncellendi.";
            return Redirect(hedefUrl);
        }

        [HttpPost("personeller/durum/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PersonelDurum(string id, bool aktif)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == id && x.KullaniciTipi == 2);
            if (hedef == null)
            {
                TempData["Hata"] = "Personel bulunamadı.";
                return Redirect("/AdminPanel/personeller");
            }

            hedef.AktifMi = aktif;
            await _userManager.UpdateAsync(hedef);

            TempData["Basarili"] = aktif ? "Personel aktif edildi." : "Personel pasifleştirildi.";
            return Redirect("/AdminPanel/personeller");
        }

        [HttpPost("personeller/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PersonelSil(string id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == id && x.KullaniciTipi == 2);
            if (hedef == null)
            {
                TempData["Hata"] = "Personel bulunamadı.";
                return Redirect("/AdminPanel/personeller");
            }

            if (kullanici.Id == hedef.Id)
            {
                TempData["Hata"] = "Kendi hesabınızı silemezsiniz.";
                return Redirect("/AdminPanel/personeller");
            }

            var sonuc = await _userManager.DeleteAsync(hedef);
            if (!sonuc.Succeeded)
            {
                TempData["Hata"] = string.Join(", ", sonuc.Errors.Select(x => x.Description));
                return Redirect("/AdminPanel/personeller");
            }

            TempData["Basarili"] = "Personel silindi.";
            return Redirect("/AdminPanel/personeller");
        }

        [HttpPost("kullanicilar/durum/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciDurum(string id, bool aktif)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (hedef == null) return Redirect("/AdminPanel/kullanicilar");
            if (!await KullaniciKapsamindaMi(kullanici, hedef)) return Redirect("/AdminPanel/kullanicilar");

            hedef.AktifMi = aktif;
            await _userManager.UpdateAsync(hedef);

            TempData["Basarili"] = aktif ? "Kullanici aktif edildi." : "Kullanici pasifleştirildi.";
            return Redirect("/AdminPanel/kullanicilar");
        }

        [HttpPost("kullanicilar/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciSil(string id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (hedef == null)
            {
                TempData["Hata"] = "Kullanıcı bulunamadı.";
                return Redirect("/AdminPanel/kullanicilar");
            }
            if (!await KullaniciKapsamindaMi(kullanici, hedef)) return Redirect("/AdminPanel/kullanicilar");

            if (kullanici.Id == hedef.Id)
            {
                TempData["Hata"] = "Kendi hesabınızı silemezsiniz.";
                return Redirect("/AdminPanel/kullanicilar");
            }

            var sonuc = await _userManager.DeleteAsync(hedef);
            if (!sonuc.Succeeded)
            {
                TempData["Hata"] = string.Join(", ", sonuc.Errors.Select(x => x.Description));
                return Redirect("/AdminPanel/kullanicilar");
            }

            TempData["Basarili"] = "Kullanıcı silindi.";
            return Redirect("/AdminPanel/kullanicilar");
        }

        [HttpGet("yetkiler")]
        public async Task<IActionResult> Yetkiler()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var genelSistemAdmin = await _aktifSirketService.GenelSistemAdminMi(kullanici);
            var personeller = await _context.Users
                .Include(x => x.Sirket)
                .Where(x => x.KullaniciTipi == 2
                    && (genelSistemAdmin && !aktifSirketId.HasValue
                        || (aktifSirketId.HasValue
                            && (x.SirketId == aktifSirketId.Value
                                || _context.Dag_PersonelYetkiler.Any(y => y.KullaniciId == x.Id && y.SirketId == aktifSirketId.Value && !y.SilindiMi)))))
                .OrderBy(x => x.AdSoyad)
                .ToListAsync();

            var personelIds = personeller.Select(x => x.Id).ToList();
            var yetkiQuery = _context.Dag_PersonelYetkiler
                .Where(x => personelIds.Contains(x.KullaniciId) && !x.SilindiMi)
                .AsQueryable();

            if (aktifSirketId.HasValue)
                yetkiQuery = yetkiQuery.Where(x => x.SirketId == aktifSirketId.Value);

            var yetkiKayitlari = await yetkiQuery
                .Include(x => x.Sirket)
                .ToListAsync();

            var yetkiMap = yetkiKayitlari
                .GroupBy(x => x.KullaniciId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.YetkiTipi)
                        .Where(x => x != YetkiTipleri.DAGITIM_SIRKET_YONET)
                        .Distinct()
                        .ToList());

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
                if (!yetkiSirketAdlariMap.ContainsKey(personel.Id) && personel.Sirket != null && !string.IsNullOrWhiteSpace(personel.Sirket.SirketAdi))
                    yetkiSirketAdlariMap[personel.Id] = new List<string> { personel.Sirket.SirketAdi };
            }

            foreach (var key in yetkiMap.Keys.ToList())
            {
                var mevcut = yetkiMap[key];
                if (mevcut.Contains(YetkiTipleri.TAM_YETKI))
                    yetkiMap[key] = new List<string> { YetkiTipleri.TAM_YETKI };
            }

            var yetkiIsimler = new Dictionary<string, string>
            {
                [YetkiTipleri.CERTIFIKA_ONAY] = "Yetki Belgesi Onay",
                [YetkiTipleri.RAPOR_GOR] = "Rapor Gör",
                [YetkiTipleri.KULLANICI_YONET] = "Kullanıcı Yönet",
                [YetkiTipleri.MARKA_YONET] = "Marka Yönet",
                [YetkiTipleri.TAM_YETKI] = "Tam Yetki"
            };

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Personeller = personeller;
            ViewBag.YetkiMap = yetkiMap;
            ViewBag.YetkiSirketAdlariMap = yetkiSirketAdlariMap;
            ViewBag.YetkiIsimler = yetkiIsimler;
            return View("~/Views/AdminPanel/Yetkiler.cshtml");
        }

        [HttpGet("yetkiler/duzenle/{id}")]
        [HttpGet("yetkiler/Düzenle/{id}")]
        public async Task<IActionResult> YetkiDuzenle(string id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var personel = await _context.Users.Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == id && x.KullaniciTipi == 2);
            if (personel == null) return Redirect("/AdminPanel/yetkiler");
            if (!await KullaniciKapsamindaMi(kullanici, personel)) return Redirect("/AdminPanel/yetkiler");

            var sirketler = await YonetilebilirSirketler(kullanici);
            var sirketIds = sirketler.Select(x => x.Id).ToHashSet();
            var mevcutKayitlar = await _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == personel.Id)
                .Where(x => sirketIds.Contains(x.SirketId))
                .ToListAsync();

            var yetkiSirketMap = mevcutKayitlar
                .GroupBy(x => x.SirketId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.YetkiTipi).Distinct().ToList());

            var mevcut = mevcutKayitlar
                .Select(x => x.YetkiTipi)
                .Distinct()
                .ToList();

            if (mevcut.Contains(YetkiTipleri.TAM_YETKI))
                mevcut = new List<string> { YetkiTipleri.TAM_YETKI };

            var seciliSirketIds = mevcutKayitlar
                .Select(x => x.SirketId)
                .Distinct()
                .ToList();

            if (seciliSirketIds.Count == 0 && personel.SirketId.HasValue && sirketIds.Contains(personel.SirketId.Value))
                seciliSirketIds.Add(personel.SirketId.Value);

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Personel = personel;
            ViewBag.MevcutYetkiler = mevcut;
            ViewBag.YetkiSirketMap = yetkiSirketMap;
            ViewBag.Sirketler = sirketler;
            ViewBag.SeciliSirketIds = seciliSirketIds;
            return View("~/Views/AdminPanel/YetkiDuzenle.cshtml");
        }

        [HttpPost("yetkiler/duzenle/{id}")]
        [HttpPost("yetkiler/Düzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiDuzenle(string id, List<int> sirketIds, Microsoft.AspNetCore.Http.IFormCollection form)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var personel = await _context.Users.FirstOrDefaultAsync(x => x.Id == id && x.KullaniciTipi == 2);
            if (personel == null) return Redirect("/AdminPanel/yetkiler");
            if (!await KullaniciKapsamindaMi(kullanici, personel)) return Redirect("/AdminPanel/yetkiler");

            var yonetilebilirSirketIds = (await YonetilebilirSirketler(kullanici)).Select(x => x.Id).ToHashSet();
            var secilenSirketIds = (sirketIds ?? new List<int>())
                .Where(x => yonetilebilirSirketIds.Contains(x))
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
                var secilenYetkiler = form[$"yetkiler_{sirketId}"]
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct()
                    .ToList();

                if (secilenYetkiler.Contains(YetkiTipleri.TAM_YETKI))
                    secilenYetkiler = new List<string> { YetkiTipleri.TAM_YETKI };

                foreach (var y in secilenYetkiler)
                {
                    _context.Dag_PersonelYetkiler.Add(new Dag_PersonelYetki
                    {
                        KullaniciId = personel.Id,
                        SirketId = sirketId,
                        YetkiTipi = y,
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Yetkiler güncellendi.";
            return Redirect("/AdminPanel/yetkiler");
        }

        [HttpGet("yetkiliservisler")]
        public async Task<IActionResult> YetkiliServisler(string? q, string? il, int? durum, string? devreyeSiralama)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var listeSonuc = await _adminYetkiliServisApiClient.ListeleAsync(
                    kullanici,
                    aktifSirketId,
                    q,
                    il,
                    durum,
                    devreyeSiralama);

            if (listeSonuc == null)
            {
                TempData["Hata"] = "Yetkili servis listesi API üzerinden alınamadı. Lütfen API uygulamasının çalıştığını kontrol edin.";
                listeSonuc = new AdminYetkiliServisListeSonuc();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.YetkiliServisler = listeSonuc.Servisler;
            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliIl = il ?? "";
            ViewBag.SeciliDurum = durum;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.DevreyeSayilari = listeSonuc.DevreyeSayilari;
            ViewBag.SeciliDevreyeSiralama = devreyeSiralama ?? "";
            return View("~/Views/AdminPanel/YetkiliServisler.cshtml");
        }

        [HttpGet("yetkiliservisler/ekle")]
        public async Task<IActionResult> YetkiliServisEkle()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.Markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();
            return View("~/Views/AdminPanel/YetkiliServisEkle.cshtml");
        }

        [HttpGet("yetkiliservisler/detay/{id}")]
        public async Task<IActionResult> YetkiliServisDetay(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var detay = await _adminYetkiliServisApiClient.DetayAsync(kullanici, id, aktifSirketId);

            if (detay?.Servis == null)
            {
                TempData["Hata"] = "Yetkili servis detayı API üzerinden alınamadı. Lütfen API uygulamasının çalıştığını kontrol edin.";
                return Redirect("/AdminPanel/yetkiliservisler");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Servis = detay.Servis;
            ViewBag.Sertifikalar = detay.Sertifikalar;
            ViewBag.Subeler = detay.Subeler;
            ViewBag.Devreye = detay.Devreye;
            return View("~/Views/AdminPanel/YetkiliServisDetay.cshtml");
        }

        [HttpPost("yetkiliservisler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisEkle(string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, List<int> kategoriIds, List<int> markaIds)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(firmaAdi))
            {
                TempData["Hata"] = "Firma adı zorunludur.";
                return Redirect("/AdminPanel/yetkiliservisler/ekle");
            }

            var sirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                faaliyetIli,
                kullanici.UserName ?? "sistem");
            var kullanilanKategoriIds = (await KullanilanKategorileriGetir())
                .Select(x => x.Id)
                .ToHashSet();
            kategoriIds = kategoriIds?
                .Where(kullanilanKategoriIds.Contains)
                .Distinct()
                .ToList() ?? new List<int>();

            var yeni = new Ys_Firma
            {
                FirmaAdi = firmaAdi,
                YetkiliKisi = yetkiliKisi,
                Telefon = telefon,
                Email = email,
                Adres = adres,
                FaaliyetIli = faaliyetIli,
                VergiNo = vergiNo,
                VergiDairesi = vergiDairesi,
                SirketId = sirketId,
                AktifMi = true,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_Firmalar.Add(yeni);
            await _context.SaveChangesAsync();

            if (kategoriIds != null && kategoriIds.Count > 0)
            {
                foreach (var kid in kategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = yeni.Id,
                        KategoriId = kid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
                await _context.SaveChangesAsync();
            }

            if (markaIds != null && markaIds.Count > 0)
            {
                foreach (var mid in markaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = yeni.Id,
                        MarkaId = mid,
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
                await _context.SaveChangesAsync();
            }

            TempData["Basarili"] = "Yetkili servis başarıyla eklendi.";
            return Redirect("/AdminPanel/yetkiliservisler");
        }

        [HttpGet("yetkiliservis-duzenle/{id}")]
        [HttpGet("yetkiliservisler/duzenle/{id}")]
        [HttpGet("yetkiliservisler/Düzenle/{id}")]
        public async Task<IActionResult> YetkiliServisDuzenle(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var servis = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (servis == null) return Redirect("/AdminPanel/yetkiliservisler");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Servis = servis;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.SeciliKategoriler = await _context.Ys_FirmaKategoriler
                .Where(x => x.FirmaId == servis.Id && !x.SilindiMi)
                .Select(x => x.KategoriId)
                .ToListAsync();
            return View("~/Views/AdminPanel/YetkiliServisDuzenle.cshtml");
        }

        [HttpPost("yetkiliservis-duzenle/{id}")]
        [HttpPost("yetkiliservisler/duzenle/{id}")]
        [HttpPost("yetkiliservisler/Düzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisDuzenle(int id, string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, bool aktifMi, List<int> kategoriIds)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var servis = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (servis == null) return Redirect("/AdminPanel/yetkiliservisler");

            var sirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                faaliyetIli,
                kullanici.UserName ?? "sistem");

            servis.FirmaAdi = firmaAdi;
            servis.YetkiliKisi = yetkiliKisi;
            servis.Telefon = telefon;
            servis.Email = email;
            servis.Adres = adres;
            servis.FaaliyetIli = faaliyetIli;
            servis.VergiNo = vergiNo;
            servis.VergiDairesi = vergiDairesi;
            servis.SirketId = sirketId;
            servis.AktifMi = aktifMi;
            servis.GuncellemeTarihi = DateTime.Now;
            servis.GuncelleyenKullanici = kullanici.UserName ?? "sistem";

            var mevcut = await _context.Ys_FirmaKategoriler
                .Where(x => x.FirmaId == servis.Id)
                .ToListAsync();
            _context.Ys_FirmaKategoriler.RemoveRange(mevcut);

            if (kategoriIds != null && kategoriIds.Count > 0)
            {
                foreach (var kid in kategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = servis.Id,
                        KategoriId = kid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Yetkili servis güncellendi.";
            return Redirect("/AdminPanel/yetkiliservisler");
        }

        [HttpPost("yetkiliservisler/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisSil(int id)
        {
            var servis = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (servis == null) return Redirect("/AdminPanel/yetkiliservisler");

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == id);

            if (devreyeAlmaVar)
            {
                TempData["Hata"] = "Bu yetkili servis üzerinde devreye alma işlemi olduğu için silinemez.";
                return Redirect("/AdminPanel/yetkiliservisler");
            }

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = User.Identity?.Name ?? "sistem";
            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Yetkili servis silindi.";
            return Redirect("/AdminPanel/yetkiliservisler");
        }

        [HttpGet("devreyealmalar")]
        public async Task<IActionResult> DevreyeAlmalar(string? marka, string? servis, string? il, string? durum, DateTime? bas, DateTime? bit)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var query = _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(marka))
                query = query.Where(x => x.Marka != null && x.Marka.MarkaAdi != null && x.Marka.MarkaAdi.Contains(marka));
            if (!string.IsNullOrWhiteSpace(servis))
                query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(servis));
            if (!string.IsNullOrWhiteSpace(il))
                query = query.Where(x => x.Firma != null && x.Firma.FaaliyetIli != null && x.Firma.FaaliyetIli.Contains(il));
            if (int.TryParse(durum, out var durumNo))
                query = query.Where(x => x.Durum == durumNo);
            if (bas.HasValue)
                query = query.Where(x => x.OlusturmaTarihi >= bas.Value.Date);
            if (bit.HasValue)
                query = query.Where(x => x.OlusturmaTarihi < bit.Value.Date.AddDays(1));

            var islemler = await query.OrderByDescending(x => x.OlusturmaTarihi).ToListAsync();
            var firmaIds = islemler.Select(x => x.FirmaId).Distinct().ToList();
            var subeler = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi && firmaIds.Contains(x.FirmaId))
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Markalar = await _context.Ys_Markalar.Where(x => !x.SilindiMi).OrderBy(x => x.MarkaAdi).ToListAsync();
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.SeciliIl = il ?? "";
            ViewBag.FirmaIlceleri = subeler
                .GroupBy(x => x.FirmaId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(s => s.Ilce).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "-");
            return View("~/Views/AdminPanel/DevreyeAlmalar.cshtml", islemler);
        }

        [HttpGet("devreyealmalar/detay/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaDetay(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var kayit = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi);

            if (kayit == null) return Redirect("/AdminPanel/devreyealmalar");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            return View("~/Views/AdminPanel/DevreyeAlmaDetay.cshtml", kayit);
        }

        [HttpGet("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit, string? tip, int? sirketId)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
            var bitTarih = bit?.Date ?? DateTime.Now.Date;
            var bitSonrasi = bitTarih.AddDays(1);
            var raporTipi = string.IsNullOrWhiteSpace(tip) ? "devreye" : tip.Trim().ToLowerInvariant();

            var devreyeTemelQuery = _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.OlusturmaTarihi >= basTarih
                    && x.OlusturmaTarihi < bitSonrasi);

            var sertifikaTemelQuery = _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.OlusturmaTarihi >= basTarih
                    && x.OlusturmaTarihi < bitSonrasi);

            if (sirketId.HasValue && sirketId.Value > 0)
            {
                devreyeTemelQuery = devreyeTemelQuery.Where(x => x.Firma != null && x.Firma.SirketId == sirketId.Value);
                sertifikaTemelQuery = sertifikaTemelQuery.Where(x => x.Firma != null && x.Firma.SirketId == sirketId.Value);
            }

            var devreyeSayisi = await devreyeTemelQuery
                .CountAsync();

            var sertifikaOnayli = await sertifikaTemelQuery
                .Where(x => x.Durum == 1)
                .CountAsync();

            var sertifikaBekleyen = await sertifikaTemelQuery
                .Where(x => x.Durum == 0)
                .CountAsync();

            var sertifikaReddedilen = await sertifikaTemelQuery
                .Where(x => x.Durum == 2)
                .CountAsync();

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

            if (raporTipi == "onayli" || raporTipi == "bekleyen" || raporTipi == "reddedilen")
            {
                var durum = raporTipi == "onayli" ? 1 : (raporTipi == "bekleyen" ? 0 : 2);
                var sertifikaIslemler = await sertifikaTemelQuery
                    .Where(x => x.Durum == durum)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                ViewBag.SertifikaIslemler = sertifikaIslemler;
                ViewBag.ListeTipi = "sertifika";
            }
            else
            {
                var sonIslemler = await devreyeTemelQuery
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                ViewBag.SonIslemler = sonIslemler;
                ViewBag.ListeTipi = "devreye";
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.BasTarih = basTarih;
            ViewBag.BitTarih = bitTarih;
            ViewBag.DevreyeSayisi = devreyeSayisi;
            ViewBag.SertifikaOnayli = sertifikaOnayli;
            ViewBag.SertifikaBekleyen = sertifikaBekleyen;
            ViewBag.SertifikaReddedilen = sertifikaReddedilen;
            ViewBag.RaporTipi = raporTipi;
            ViewBag.SeciliSirketId = sirketId;
            ViewBag.Sirketler = await _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.SirketAdi)
                .ToListAsync();
            ViewBag.ChartAylikLabels = chartAylikLabels;
            ViewBag.ChartAylikData = chartAylikData;
            ViewBag.ChartDurumData = new List<int> { sertifikaOnayli, sertifikaBekleyen, sertifikaReddedilen };
            ViewBag.ChartSirketLabels = chartSirket.Select(x => x.Sirket).ToList();
            ViewBag.ChartSirketData = chartSirket.Select(x => x.Sayi).ToList();
            return View("~/Views/AdminPanel/Raporlar.cshtml");
        }

        [HttpGet("raporlar/pdf")]
        public async Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            List<Ys_DevreyeAlma> sonIslemler;
            DateTime basTarih;
            DateTime bitTarih;

            if (ids != null && ids.Count > 0)
            {
                sonIslemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                    .Include(x => x.Marka)
                    .Where(x => !x.SilindiMi && ids.Contains(x.Id))
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .ToListAsync();

                basTarih = sonIslemler.Count > 0 ? sonIslemler.Min(x => x.OlusturmaTarihi).Date : DateTime.Now.Date;
                bitTarih = sonIslemler.Count > 0 ? sonIslemler.Max(x => x.OlusturmaTarihi).Date : DateTime.Now.Date;
            }
            else
            {
                basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
                bitTarih = bit?.Date ?? DateTime.Now.Date;
                var bitSonrasi = bitTarih.AddDays(1);

                sonIslemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                    .Include(x => x.Marka)
                    .Where(x => !x.SilindiMi && x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(20)
                    .ToListAsync();
            }

            var devreyeSayisi = sonIslemler.Count;
            var tamamlanan = sonIslemler.Count(x => x.Durum == 1);
            var bekleyen = sonIslemler.Count(x => x.Durum == 0);

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Yönetim Raporları").FontSize(16).SemiBold();
                            col.Item().Text($"Rapor Aralığı: {basTarih:dd.MM.yyyy} - {bitTarih:dd.MM.yyyy}")
                                .FontSize(10).FontColor("#555555");
                        });
                        row.ConstantItem(160).AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
                            .FontSize(10).FontColor("#777777");
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                            });

                            void Cell(string title, string value)
                            {
                                table.Cell().Element(cell =>
                                {
                                    cell.Border(1).BorderColor("#E5E7EB").Padding(8).Background("#F8FAFC")
                                        .Column(column =>
                                        {
                                            column.Item().Text(title).FontSize(9).FontColor("#6B7280");
                                            column.Item().Text(value).FontSize(14).SemiBold().FontColor("#111827");
                                        });
                                });
                            }

                            Cell("Toplam İşlem", devreyeSayisi.ToString());
                            Cell("Tamamlanan", tamamlanan.ToString());
                            Cell("Bekleyen", bekleyen.ToString());
                        });

                        col.Item().Text("Seçili Devreye Alma Detayları").FontSize(12).SemiBold();

                        foreach (var d in sonIslemler)
                        {
                            var durumText = d.Durum == 1 ? "Tamamlandı" : d.Durum == 2 ? "İptal" : "Bekliyor";
                            var durumColor = d.Durum == 1 ? "#0f766e" : d.Durum == 2 ? "#b42318" : "#9a6700";
                            var satirBg = d.Durum == 1 ? "#ecfdf3" : d.Durum == 2 ? "#fff1f2" : "#fffbeb";
                            col.Item().PaddingBottom(8).Border(1).BorderColor("#E5E7EB").Background("#FFFFFF").Column(detail =>
                            {
                                detail.Item().Background("#F8FAFC").Padding(8).Row(r =>
                                {
                                    r.RelativeItem().Text($"Tesisat No: {d.TesistatNo ?? "-"}").FontSize(10).SemiBold();
                                    r.RelativeItem().AlignRight().Text($"Tarih: {d.OlusturmaTarihi:dd.MM.yyyy HH:mm}").FontSize(10).FontColor("#4B5563");
                                });
                                detail.Item().Background(satirBg).PaddingHorizontal(8).PaddingVertical(6).Text($"Durum: {durumText}").FontSize(10).FontColor(durumColor).SemiBold();
                                detail.Item().Padding(8).Table(t =>
                                {
                                    t.ColumnsDefinition(c =>
                                    {
                                        c.RelativeColumn();
                                        c.RelativeColumn();
                                    });

                                    void Bilgi(string etiket, string deger)
                                    {
                                        t.Cell().PaddingBottom(4).Text($"{etiket}: {deger}").FontSize(10);
                                    }

                                    Bilgi("Firma Kodu", d.Firma?.Sirket?.SirketAdi ?? "-");
                                    Bilgi("Yetkili Servis", d.Firma?.FirmaAdi ?? "-");
                                    Bilgi("Müşteri", d.MusteriAdi ?? "-");
                                    Bilgi("Telefon", d.MusteriTelefon ?? "-");
                                    Bilgi("TC", d.MusteriTcNo ?? "-");
                                    Bilgi("Adres", d.Adres ?? "-");
                                    Bilgi("Cihaz Tipi", d.CihazTipi ?? "-");
                                    Bilgi("Marka", d.Marka?.MarkaAdi ?? d.CihazMarka ?? "-");
                                    Bilgi("Model", d.CihazModeli ?? "-");
                                    Bilgi("Seri No", d.SeriNo ?? "-");
                                    Bilgi("Kapasite", d.CihazKapasite ?? "-");
                                    Bilgi("Teknisyen", d.TeknisyenAdi ?? "-");
                                    Bilgi("Teknisyen Yetki Belgesi No", d.TeknisyenSertifikaNo ?? "-");
                                });

                                if (!string.IsNullOrWhiteSpace(d.Notlar))
                                    detail.Item().PaddingHorizontal(8).PaddingBottom(8).Text($"Not: {d.Notlar}").FontSize(10).FontColor("#4B5563");
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text("Yetkili Servis Gaz Açma Sistemi").FontSize(9).FontColor("#888888");
                });
            });

            var pdfBytes = document.GeneratePdf();
            var dosyaAdi = $"raporlar_{basTarih:yyyyMMdd}_{bitTarih:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", dosyaAdi);
        }

        [HttpGet("raporlar/pdf-toplu")]
        public async Task<IActionResult> RaporlarPdfToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarPdf(bas, bit, null);
        }

        [HttpGet("raporlar/excel")]
        public async Task<IActionResult> RaporlarExcel(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            List<Ys_DevreyeAlma> islemler;
            DateTime basTarih;
            DateTime bitTarih;

            if (ids != null && ids.Count > 0)
            {
                islemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                    .Include(x => x.Marka)
                    .Where(x => !x.SilindiMi && ids.Contains(x.Id))
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .ToListAsync();

                basTarih = islemler.Count > 0 ? islemler.Min(x => x.OlusturmaTarihi).Date : DateTime.Now.Date;
                bitTarih = islemler.Count > 0 ? islemler.Max(x => x.OlusturmaTarihi).Date : DateTime.Now.Date;
            }
            else
            {
                basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
                bitTarih = bit?.Date ?? DateTime.Now.Date;
                var bitSonrasi = bitTarih.AddDays(1);

                islemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                    .Include(x => x.Marka)
                    .Where(x => !x.SilindiMi && x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .ToListAsync();
            }

            var bytes = DevreyeAlmaExcelService.Olustur(islemler);
            var dosyaAdi = $"raporlar_{basTarih:yyyyMMdd}_{bitTarih:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=windows-1254", dosyaAdi);
        }

        [HttpGet("raporlar/excel-toplu")]
        public async Task<IActionResult> RaporlarExcelToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarExcel(bas, bit, null);
        }

        [HttpGet("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var bekleyenler = await _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum == 0
                    && x.Firma != null
                    && !x.Firma.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Bekleyenler = bekleyenler;
            return View("~/Views/AdminPanel/OnayBekleyenler.cshtml");
        }

        [HttpGet("yetki-belgesi-uyarilari")]
        public async Task<IActionResult> SertifikaUyarilari()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var bugun = DateTime.Now.Date;
            var bitisSinir = bugun.AddDays(30);

            var yaklasan = await _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum == 1
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.SertifikaBitisTarihi >= bugun
                    && x.SertifikaBitisTarihi <= bitisSinir)
                .OrderBy(x => x.SertifikaBitisTarihi)
                .ToListAsync();

            var gecmis = await _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum == 1
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.SertifikaBitisTarihi < bugun)
                .OrderByDescending(x => x.SertifikaBitisTarihi)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Yaklasan = yaklasan;
            ViewBag.Gecmis = gecmis;
            return View("~/Views/AdminPanel/SertifikaUyarilari.cshtml");
        }

        [HttpGet("subeler")]
        public async Task<IActionResult> Subeler(string? q, int firmaId = 0)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifFirmaIdQuery = GetAktifYetkiliServisFirmaIdsQuery();

            var firmalar = await _context.Ys_Firmalar
                .Where(x => !x.SilindiMi && x.AktifMi && aktifFirmaIdQuery.Contains(x.Id))
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();

            var sorgu = _context.Ys_Subeler
                .Include(x => x.Firma)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.Firma.AktifMi
                    && aktifFirmaIdQuery.Contains(x.FirmaId));

            if (firmaId > 0)
                sorgu = sorgu.Where(x => x.FirmaId == firmaId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim().ToLower();
                if (q.Length == 1)
                {
                    var likeStart = $"{q}%";
                    sorgu = sorgu.Where(x =>
                        !string.IsNullOrWhiteSpace(x.SubeAdi) &&
                        EF.Functions.Like(x.SubeAdi.ToLower(), likeStart));
                }
                else
                {
                    var likeAny = $"%{q}%";
                    sorgu = sorgu.Where(x =>
                        (!string.IsNullOrWhiteSpace(x.SubeAdi) && EF.Functions.Like(x.SubeAdi.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Il) && EF.Functions.Like(x.Il.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Ilce) && EF.Functions.Like(x.Ilce.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Telefon) && EF.Functions.Like(x.Telefon.ToLower(), likeAny)) ||
                        (!string.IsNullOrWhiteSpace(x.Adres) && EF.Functions.Like(x.Adres.ToLower(), likeAny)) ||
                        (x.Firma != null && !string.IsNullOrWhiteSpace(x.Firma.FirmaAdi) && EF.Functions.Like(x.Firma.FirmaAdi.ToLower(), likeAny)));
                }
            }

            var subeler = await sorgu
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Subeler = subeler;
            ViewBag.Firmalar = firmalar;
            ViewBag.SeciliFirmaId = firmaId;
            ViewBag.SeciliQ = q ?? "";
            return View("~/Views/AdminPanel/Subeler.cshtml");
        }

        [HttpPost("subeler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeEkle(int firmaId, string subeAdi, string? il, string? ilce, string? telefon, string? adres, bool aktifMi)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            if (firmaId <= 0 || string.IsNullOrWhiteSpace(subeAdi))
            {
                TempData["Hata"] = "Firma ve şube adı zorunludur.";
                return Redirect("/AdminPanel/subeler");
            }

            var gecerliFirma = await _context.Ys_Firmalar
                .AnyAsync(x => x.Id == firmaId
                    && !x.SilindiMi
                    && x.AktifMi
                    && _context.Users.Any(u => u.KullaniciTipi == 1 && u.AktifMi && u.FirmaId == x.Id));
            if (!gecerliFirma)
            {
                TempData["Hata"] = "Seçilen firma aktif yetkili servis kullanıcısına sahip değil.";
                return Redirect("/AdminPanel/subeler");
            }

            var yeni = new Ys_Sube
            {
                FirmaId = firmaId,
                SubeAdi = subeAdi,
                Il = il,
                Ilce = ilce,
                Telefon = telefon,
                Adres = adres,
                AktifMi = aktifMi,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_Subeler.Add(yeni);
            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Şube kaydı eklendi.";
            return Redirect("/AdminPanel/subeler");
        }

        [HttpGet("subeler/duzenle/{id:int}")]
        public async Task<IActionResult> SubeDuzenle(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifFirmaIdQuery = GetAktifYetkiliServisFirmaIdsQuery();

            var sube = await _context.Ys_Subeler
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.Firma.AktifMi
                    && aktifFirmaIdQuery.Contains(x.FirmaId));
            if (sube == null) return Redirect("/AdminPanel/subeler");

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Sube = sube;
            ViewBag.Firmalar = await _context.Ys_Firmalar
                .Where(x => !x.SilindiMi && x.AktifMi && aktifFirmaIdQuery.Contains(x.Id))
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();
            return View("~/Views/AdminPanel/SubeDuzenle.cshtml");
        }

        [HttpPost("subeler/duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDuzenle(int id, int firmaId, string subeAdi, string? il, string? ilce, string? telefon, string? adres, bool aktifMi)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifFirmaIdQuery = GetAktifYetkiliServisFirmaIdsQuery();
            var sube = await _context.Ys_Subeler
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.Firma.AktifMi
                    && aktifFirmaIdQuery.Contains(x.FirmaId));
            if (sube == null) return Redirect("/AdminPanel/subeler");

            var hedefFirmaGecerli = await _context.Ys_Firmalar
                .AnyAsync(x => x.Id == firmaId
                    && !x.SilindiMi
                    && x.AktifMi
                    && aktifFirmaIdQuery.Contains(x.Id));
            if (!hedefFirmaGecerli)
            {
                TempData["Hata"] = "Seçilen firma aktif yetkili servis kullanıcısına sahip değil.";
                return Redirect("/AdminPanel/subeler");
            }

            sube.FirmaId = firmaId;
            sube.SubeAdi = subeAdi;
            sube.Il = il;
            sube.Ilce = ilce;
            sube.Telefon = telefon;
            sube.Adres = adres;
            sube.AktifMi = aktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Şube güncellendi.";
            return Redirect("/AdminPanel/subeler");
        }

        [HttpPost("subeler/durum")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDurum(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifFirmaIdQuery = GetAktifYetkiliServisFirmaIdsQuery();
            var sube = await _context.Ys_Subeler
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.Firma.AktifMi
                    && aktifFirmaIdQuery.Contains(x.FirmaId));
            if (sube == null)
            {
                TempData["Hata"] = "Şube bulunamadı.";
                return Redirect("/AdminPanel/subeler");
            }

            sube.AktifMi = !sube.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Şube durumu güncellendi.";
            return Redirect("/AdminPanel/subeler");
        }

        [HttpPost("subeler/sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeSil(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifFirmaIdQuery = GetAktifYetkiliServisFirmaIdsQuery();
            var sube = await _context.Ys_Subeler
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.Firma.AktifMi
                    && aktifFirmaIdQuery.Contains(x.FirmaId));
            if (sube == null)
            {
                TempData["Hata"] = "Şube bulunamadı.";
                return Redirect("/AdminPanel/subeler");
            }

            sube.SilindiMi = true;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Şube kaydı silindi.";
            return Redirect("/AdminPanel/subeler");
        }
    }
}


