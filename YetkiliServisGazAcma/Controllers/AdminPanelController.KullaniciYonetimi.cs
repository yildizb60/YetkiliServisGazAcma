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
    public partial class AdminPanelController
    {
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
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sirketler = await _adminKullaniciApiClient.SirketSecenekleriAsync(kullanici, aktifSirketId);
            if (sirketler != null)
                return sirketler;

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
            var dashboard = await GetAdminDashboardOzetAsync(kullanici, sirketId);
            ViewBag.AdminDashboardVeriKaynagi = "API";

            if (dashboard == null)
            {
                TempData["Hata"] = "Dashboard verisi API üzerinden alınamadı.";
                dashboard = new AdminDashboardOzet();
            }

            ViewBag.ToplamDevreyeAlma = dashboard.ToplamDevreyeAlma;
            ViewBag.ToplamFirma = dashboard.ToplamFirma;
            ViewBag.OnayBekleyen = dashboard.OnayBekleyen;
            ViewBag.SuresiBitecek = dashboard.SuresiBitecek;
            ViewBag.ToplamSirket = dashboard.ToplamSirket;
            ViewBag.BuAyDevreyeAlma = dashboard.BuAyDevreyeAlma;
            ViewBag.SonYetkiBelgeleri = dashboard.SonYetkiBelgeleri;
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
            ViewBag.SuresiBitecek = await _context.Ys_YetkiBelgeleri
                .Where(x => !x.SilindiMi
                    && x.Durum == 1
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.YetkiBelgesiBitisTarihi <= DateTime.Now.AddDays(30)
                    && x.YetkiBelgesiBitisTarihi >= DateTime.Now)
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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var personeller = await _adminKullaniciApiClient.ListeleAsync(kullanici, aktifSirketId, q, "Personel", durum, sirket);
            ViewBag.AdminKullaniciVeriKaynagi = "API";

            if (personeller == null)
            {
                TempData["Hata"] = "Personel listesi API uzerinden alinamadi.";
                personeller = new List<AppKullanici>();
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

            await PersonelEkleViewBagHazirla(kullanici);
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
                ViewBag.Hata = string.Join(" ", sifreHatalari);
                await PersonelEkleViewBagHazirla(kullanici, adSoyad, email, telefon, sirketId);
                return View("~/Views/AdminPanel/PersonelEkle.cshtml");
            }

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminKullaniciApiClient.PersonelEkleAsync(kullanici, aktifSirketId, adSoyad, email, telefon, sirketId, sifre);
            if (sonuc?.Basarili == true)
            {
                TempData["Basarili"] = sonuc.Mesaj ?? "Personel basariyla olusturuldu.";
                return Redirect("/AdminPanel/personeller");
            }

            ViewBag.Hata = sonuc?.Mesaj ?? "Personel API uzerinden olusturulamadi.";
            await PersonelEkleViewBagHazirla(kullanici, adSoyad, email, telefon, sirketId);
            return View("~/Views/AdminPanel/PersonelEkle.cshtml");
        }

        private async Task PersonelEkleViewBagHazirla(
            AppKullanici kullanici,
            string? adSoyad = null,
            string? email = null,
            string? telefon = null,
            int sirketId = 0)
        {
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sirketler = await _adminKullaniciApiClient.SirketSecenekleriAsync(kullanici, aktifSirketId);

            if (sirketler == null)
            {
                ViewBag.Hata = ViewBag.Hata ?? "Sirket listesi API uzerinden alinamadi.";
                sirketler = new List<Dag_Sirket>();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Sirketler = sirketler;
            ViewBag.FormAdSoyad = adSoyad ?? "";
            ViewBag.FormEmail = email ?? "";
            ViewBag.FormTelefon = telefon ?? "";
            ViewBag.FormSirketId = sirketId;
        }

        private async Task KullaniciFormSecenekleriHazirla(AppKullanici kullanici)
        {
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sirketler = await _adminKullaniciApiClient.SirketSecenekleriAsync(kullanici, aktifSirketId);
            var firmalar = await _adminKullaniciApiClient.FirmaSecenekleriAsync(kullanici, aktifSirketId);

            if (sirketler == null)
            {
                ViewBag.Hata = ViewBag.Hata ?? "Sirket listesi API uzerinden alinamadi.";
                sirketler = new List<Dag_Sirket>();
            }

            if (firmalar == null)
            {
                ViewBag.Hata = ViewBag.Hata ?? "Firma listesi API uzerinden alinamadi.";
                firmalar = new List<Ys_Firma>();
            }

            ViewBag.Sirketler = sirketler;
            ViewBag.Firmalar = firmalar;
        }

        [HttpGet("kullanicilar")]
        public async Task<IActionResult> Kullanicilar(string? q, string? tip, string? durum, string? bagli)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            await SyncYetkiliServisKullanicilariAsync();

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kullanicilar = await _adminKullaniciApiClient.ListeleAsync(kullanici, aktifSirketId, q, tip, durum, bagli);
            ViewBag.AdminKullaniciVeriKaynagi = "API";

            if (kullanicilar == null)
            {
                TempData["Hata"] = "Kullanıcı listesi API üzerinden alınamadı.";
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
            await KullaniciFormSecenekleriHazirla(kullanici);
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
                await KullaniciFormSecenekleriHazirla(kullanici);
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
            await KullaniciFormSecenekleriHazirla(kullanici);
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
                    await KullaniciFormSecenekleriHazirla(kullanici);
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
                    await KullaniciFormSecenekleriHazirla(kullanici);
                    ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                    ViewBag.Hata = "Yetkili servis kullanıcısı için firma seçilmelidir.";
                    return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
                }

                var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
                var firmalar = await _adminKullaniciApiClient.FirmaSecenekleriAsync(kullanici, aktifSirketId);
                var firma = firmalar?.FirstOrDefault(x => x.Id == firmaId.Value);
                if (firma == null)
                {
                    ViewBag.Kullanici = kullanici;
                    ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                    ViewBag.Hedef = hedef;
                    await KullaniciFormSecenekleriHazirla(kullanici);
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
                await KullaniciFormSecenekleriHazirla(kullanici);
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
                    await KullaniciFormSecenekleriHazirla(kullanici);
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
                    await KullaniciFormSecenekleriHazirla(kullanici);
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
                    await KullaniciFormSecenekleriHazirla(kullanici);
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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminKullaniciApiClient.DurumAsync(kullanici, id, aktif, aktifSirketId, sadecePersonel: true);
            SetKullaniciIslemMesaji(sonuc, aktif ? "Personel aktif edildi." : "Personel pasiflestirildi.");
            return Redirect("/AdminPanel/personeller");
        }

        [HttpPost("personeller/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PersonelSil(string id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminKullaniciApiClient.SilAsync(kullanici, id, aktifSirketId, sadecePersonel: true);
            SetKullaniciIslemMesaji(sonuc, "Personel silindi.");
            return Redirect("/AdminPanel/personeller");
        }

        [HttpPost("kullanicilar/durum/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciDurum(string id, bool aktif)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminKullaniciApiClient.DurumAsync(kullanici, id, aktif, aktifSirketId, sadecePersonel: false);
            SetKullaniciIslemMesaji(sonuc, aktif ? "Kullanici aktif edildi." : "Kullanici pasiflestirildi.");
            return Redirect("/AdminPanel/kullanicilar");
        }

        [HttpPost("kullanicilar/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciSil(string id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminKullaniciApiClient.SilAsync(kullanici, id, aktifSirketId, sadecePersonel: false);
            SetKullaniciIslemMesaji(sonuc, "Kullanici silindi.");
            return Redirect("/AdminPanel/kullanicilar");
        }

        private void SetKullaniciIslemMesaji(AdminKullaniciIslemSonuc? sonuc, string varsayilanBasari)
        {
            if (sonuc?.Basarili == true)
            {
                TempData["Basarili"] = sonuc.Mesaj ?? varsayilanBasari;
                return;
            }

            TempData["Hata"] = sonuc?.Mesaj ?? "Kullanici islemi API uzerinden tamamlanamadi.";
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
                [YetkiTipleri.YETKI_BELGESI_ONAY] = "Yetki Belgesi Onay",
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
    }
}
