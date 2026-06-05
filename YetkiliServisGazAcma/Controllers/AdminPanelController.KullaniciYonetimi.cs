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
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            return await _adminKullaniciApiClient.YonetebilirMiAsync(kullanici, aktifSirketId);
        }

        private async Task<List<Dag_Sirket>> YonetilebilirSirketler(AppKullanici kullanici)
        {
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sirketler = await _adminKullaniciApiClient.SirketSecenekleriAsync(kullanici, aktifSirketId);
            if (sirketler != null)
                return sirketler;

            return await _aktifSirketService.KullaniciSirketleriAsync(kullanici);
        }

        private async Task SyncYetkiliServisKullanicilariAsync(AppKullanici kullanici)
        {
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminKullaniciApiClient.YetkiliServisKullanicilariniSenkronizeAsync(kullanici, aktifSirketId);
            if (sonuc?.Basarili == false)
                TempData["Hata"] = sonuc.Mesaj ?? "Yetkili servis kullanicilari API uzerinden senkronize edilemedi.";
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

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dashboard = await GetAdminDashboardOzetAsync(kullanici, sirketId);
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.SuresiBitecek = dashboard?.SuresiBitecek ?? 0;
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

            await SyncYetkiliServisKullanicilariAsync(kullanici);

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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminKullaniciApiClient.KullaniciEkleAsync(
                kullanici,
                aktifSirketId,
                adSoyad,
                email,
                telefon,
                sifre,
                rol,
                sirketId,
                firmaId);

            if (sonuc?.Basarili != true)
                return await FormHata(sonuc?.Mesaj ?? "Kullanici API uzerinden olusturulamadi.");

            TempData["Basarili"] = sonuc.Mesaj ?? "Kullanici basariyla olusturuldu.";
            return Redirect("/AdminPanel/kullanicilar");
        }

        [HttpGet("kullanicilar/duzenle/{id}")]
        [HttpGet("kullanicilar/Düzenle/{id}")]
        public async Task<IActionResult> KullaniciDuzenle(string id, string? returnUrl)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await KullaniciYonetebilirMi(kullanici)) return Redirect("/AdminPanel");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var hedef = await _adminKullaniciApiClient.GetirAsync(kullanici, id, aktifSirketId);
            if (hedef == null) return Redirect("/AdminPanel/kullanicilar");

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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var hedef = await _adminKullaniciApiClient.GetirAsync(kullanici, id, aktifSirketId);
            if (hedef == null) return Redirect("/AdminPanel/kullanicilar");
            var sonuc = await _adminKullaniciApiClient.GuncelleAsync(
                kullanici,
                id,
                aktifSirketId,
                adSoyad,
                email,
                telefon,
                aktifMi,
                sirketId,
                firmaId,
                yeniSifre,
                yeniSifreTekrar);

            if (sonuc?.Basarili != true)
            {
                hedef.AdSoyad = adSoyad;
                hedef.Email = email;
                hedef.UserName = email;
                hedef.PhoneNumber = telefon;
                hedef.AktifMi = aktifMi;
                hedef.SirketId = sirketId;
                hedef.FirmaId = firmaId;

                ViewBag.Kullanici = kullanici;
                ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
                ViewBag.Hedef = hedef;
                await KullaniciFormSecenekleriHazirla(kullanici);
                ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
                ViewBag.Hata = sonuc?.Mesaj ?? "Kullanici API uzerinden guncellenemedi.";
                return View("~/Views/AdminPanel/KullaniciDuzenle.cshtml");
            }

            var hedefUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/AdminPanel/kullanicilar" : returnUrl;
            if (!hedefUrl.StartsWith("/AdminPanel", StringComparison.OrdinalIgnoreCase))
                hedefUrl = "/AdminPanel/kullanicilar";

            TempData["Basarili"] = sonuc.Mesaj ?? "Kullanici guncellendi.";
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
            AdminYetkiListeSonuc? sonuc;
            try
            {
                sonuc = await _adminKullaniciApiClient.YetkiListeAsync(kullanici, aktifSirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sonuc = null;
            }

            var yetkiIsimler = new Dictionary<string, string>
            {
                [YetkiTipleri.YETKI_BELGESI_ONAY] = "Yetki Belgesi Onay",
                [YetkiTipleri.RAPOR_GOR] = "Rapor Gor",
                [YetkiTipleri.KULLANICI_YONET] = "Kullanici Yonet",
                [YetkiTipleri.MARKA_YONET] = "Marka Yonet",
                [YetkiTipleri.TAM_YETKI] = "Tam Yetki"
            };

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Personeller = sonuc?.Personeller ?? new List<AppKullanici>();
            ViewBag.YetkiMap = sonuc?.YetkiMap ?? new Dictionary<string, List<string>>();
            ViewBag.YetkiSirketAdlariMap = sonuc?.YetkiSirketAdlariMap ?? new Dictionary<string, List<string>>();
            ViewBag.YetkiIsimler = yetkiIsimler;
            return View("~/Views/AdminPanel/Yetkiler.cshtml");
        }

        [HttpGet("yetkiler/duzenle/{id}")]
        [HttpGet("yetkiler/Düzenle/{id}")]
        public async Task<IActionResult> YetkiDuzenle(string id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            AdminYetkiDuzenleSonuc? sonuc;
            try
            {
                sonuc = await _adminKullaniciApiClient.YetkiDuzenleAsync(kullanici, id, aktifSirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return Redirect("/AdminPanel/yetkiler");
            }

            if (sonuc?.Personel == null)
            {
                TempData["Hata"] = "Personel bulunamadi veya bu personel icin yetkiniz yok.";
                return Redirect("/AdminPanel/yetkiler");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Personel = sonuc.Personel;
            ViewBag.MevcutYetkiler = sonuc.MevcutYetkiler;
            ViewBag.YetkiSirketMap = sonuc.YetkiSirketMap;
            ViewBag.Sirketler = sonuc.Sirketler;
            ViewBag.SeciliSirketIds = sonuc.SeciliSirketIds;
            return View("~/Views/AdminPanel/YetkiDuzenle.cshtml");
        }

        [HttpPost("yetkiler/duzenle/{id}")]
        [HttpPost("yetkiler/Düzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiDuzenle(string id, List<int> sirketIds, Microsoft.AspNetCore.Http.IFormCollection form)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var secilenSirketIds = (sirketIds ?? new List<int>())
                .Distinct()
                .ToList();

            var yetkiMap = new Dictionary<int, List<string>>();

            foreach (var sirketId in secilenSirketIds)
            {
                var secilenYetkiler = form[$"yetkiler_{sirketId}"]
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct()
                    .ToList();

                if (secilenYetkiler.Contains(YetkiTipleri.TAM_YETKI))
                    secilenYetkiler = new List<string> { YetkiTipleri.TAM_YETKI };

                yetkiMap[sirketId] = secilenYetkiler;
            }

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var sonuc = await _adminKullaniciApiClient.YetkiGuncelleAsync(
                    kullanici,
                    id,
                    aktifSirketId,
                    secilenSirketIds,
                    yetkiMap);

                SetKullaniciIslemMesaji(sonuc, "Yetkiler guncellendi.");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }
            return Redirect("/AdminPanel/yetkiler");
        }
    }
}
