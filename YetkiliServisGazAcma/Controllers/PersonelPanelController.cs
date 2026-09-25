using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("personel-panel")]
    public class PersonelPanelController : Controller
    {
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly SehirFirmaKodlari _sehirFirmaKoduService;
        private readonly AktifSirketService _aktifSirketService;
        private readonly AdminDashboardApiClient _adminDashboardApiClient;
        private readonly PersonelPanelApiClient _personelPanelApiClient;
        private readonly AdminYetkiBelgesiOnayApiClient _yetkiBelgesiOnayApiClient;
        private readonly AdminRaporApiClient _adminRaporApiClient;
        private readonly YetkiBelgesiApiClient _yetkiBelgesiApiClient;
        private readonly DagitimSirketApiClient _dagitimSirketApiClient;
        private readonly MarkaApiClient _markaApiClient;
        private readonly UrunKategoriApiClient _urunKategoriApiClient;
        private readonly AdminYetkiliServisApiClient _adminYetkiliServisApiClient;
        private readonly YkcApiClient _ykcApiClient;

        public PersonelPanelController(
            ApiKullaniciOturumu kullaniciOturumu,
            SehirFirmaKodlari sehirFirmaKoduService,
            AktifSirketService aktifSirketService,
            AdminDashboardApiClient adminDashboardApiClient,
            PersonelPanelApiClient personelPanelApiClient,
            AdminYetkiBelgesiOnayApiClient yetkiBelgesiOnayApiClient,
            AdminRaporApiClient adminRaporApiClient,
            YetkiBelgesiApiClient yetkiBelgesiApiClient,
            DagitimSirketApiClient dagitimSirketApiClient,
            MarkaApiClient markaApiClient,
            UrunKategoriApiClient urunKategoriApiClient,
            AdminYetkiliServisApiClient adminYetkiliServisApiClient,
            YkcApiClient ykcApiClient)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _aktifSirketService = aktifSirketService;
            _adminDashboardApiClient = adminDashboardApiClient;
            _personelPanelApiClient = personelPanelApiClient;
            _yetkiBelgesiOnayApiClient = yetkiBelgesiOnayApiClient;
            _adminRaporApiClient = adminRaporApiClient;
            _yetkiBelgesiApiClient = yetkiBelgesiApiClient;
            _dagitimSirketApiClient = dagitimSirketApiClient;
            _markaApiClient = markaApiClient;
            _urunKategoriApiClient = urunKategoriApiClient;
            _adminYetkiliServisApiClient = adminYetkiliServisApiClient;
            _ykcApiClient = ykcApiClient;
        }

        private async Task<List<UrunKategori>> KullanilanKategorileriGetir()
        {
            return await _urunKategoriApiClient.ListeAsync() ?? new List<UrunKategori>();
        }

        private async Task<bool> KullaniciYetkiliMi(AppKullanici kullanici, string yetki)
        {
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici) || await _aktifSirketService.SirketAdminMi(kullanici))
                return true;

            var mevcutYetkiler = await GetPersonelYetkileriAsync(kullanici);
            return mevcutYetkiler.Contains(YetkiTipleri.TAM_YETKI) || mevcutYetkiler.Contains(yetki);
        }

        private async Task<List<string>> GetPersonelYetkileriAsync(AppKullanici kullanici)
        {
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var cacheKey = $"PersonelYetkileri:{kullanici.Id}:{aktifSirketId?.ToString() ?? "tum"}";
            if (HttpContext.Items.TryGetValue(cacheKey, out var cached) && cached is List<string> cachedYetkiler)
                return cachedYetkiler;

            try
            {
                var yetkiler = await _personelPanelApiClient.YetkilerimAsync(kullanici, aktifSirketId)
                    ?? new List<string>();

                if (yetkiler.Contains(YetkiTipleri.TAM_YETKI))
                    yetkiler = new List<string> { YetkiTipleri.TAM_YETKI };

                HttpContext.Items[cacheKey] = yetkiler;
                return yetkiler;
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return new List<string>();
            }
        }

        private async Task<AdminDashboardOzet?> GetPersonelDashboardOzetAsync(AppKullanici kullanici)
        {
            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var cacheKey = $"PersonelDashboard:{sirketId?.ToString() ?? "tum"}";
            if (HttpContext.Items.TryGetValue(cacheKey, out var cached))
                return cached as AdminDashboardOzet;

            try
            {
                var dashboard = await _adminDashboardApiClient.GetirAsync(kullanici, sirketId);
                if (dashboard != null)
                    HttpContext.Items[cacheKey] = dashboard;

                return dashboard;
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return null;
            }
        }

        private async Task SetPersonelYetkiViewBags(AppKullanici kullanici)
        {
            var yYetkiBelgesi = await KullaniciYetkiliMi(kullanici, YetkiTipleri.YETKI_BELGESI_ONAY);
            var yRapor = await KullaniciYetkiliMi(kullanici, YetkiTipleri.RAPOR_GOR);
            var yServis = await KullaniciYetkiliMi(kullanici, YetkiTipleri.KULLANICI_YONET);
            var yMarkaYonet = await KullaniciYetkiliMi(kullanici, YetkiTipleri.MARKA_YONET);
            var yYkcTalep = await KullaniciYetkiliMi(kullanici, YetkiTipleri.YKC_TALEP_GOR);
            var yYkcAtama = await KullaniciYetkiliMi(kullanici, YetkiTipleri.YKC_ATAMA_YAP);
            var yYkcImza = await KullaniciYetkiliMi(kullanici, YetkiTipleri.YKC_FR265_IMZA_ISLEM);
            var yYkcRapor = await KullaniciYetkiliMi(kullanici, YetkiTipleri.YKC_RAPOR_GOR);

            ViewBag.YetkiBelgesi = yYetkiBelgesi;
            ViewBag.YetkiRapor = yRapor;
            ViewBag.YetkiServis = yServis;
            ViewBag.YetkiMarka = yMarkaYonet;
            ViewBag.YetkiMarkaYonet = yMarkaYonet;
            ViewBag.YetkiYkcTalep = yYkcTalep;
            ViewBag.YetkiYkcAtama = yYkcAtama;
            ViewBag.YetkiYkcImza = yYkcImza;
            ViewBag.YetkiYkcRapor = yYkcRapor;

            List<string> yetkilerim;
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici) || await _aktifSirketService.SirketAdminMi(kullanici))
            {
                yetkilerim = new List<string>
                {
                    "Tam Yetki",
                    "Yetki Belgesi Onay",
                    "Rapor Gör",
                    "Kullanıcı Yönet",
                    "Marka Yönet",
                    "YKC Taleplerini Gör",
                    "YKC Atama ve Randevu",
                    "YKC FR265 ve İmza İşlemleri",
                    "YKC Raporlarını Gör"
                };
            }
            else
            {
                var mevcutYetkiler = await GetPersonelYetkileriAsync(kullanici);

                var yetkiAdlari = new Dictionary<string, string>
                {
                    [YetkiTipleri.YETKI_BELGESI_ONAY] = "Yetki Belgesi Onay",
                    [YetkiTipleri.RAPOR_GOR] = "Rapor Gör",
                    [YetkiTipleri.KULLANICI_YONET] = "Kullanıcı Yönet",
                    [YetkiTipleri.MARKA_YONET] = "Marka Yönet",
                    [YetkiTipleri.YKC_TALEP_GOR] = "YKC Taleplerini Gör",
                    [YetkiTipleri.YKC_ATAMA_YAP] = "YKC Atama ve Randevu",
                    [YetkiTipleri.YKC_FR265_IMZA_ISLEM] = "YKC FR265 ve İmza İşlemleri",
                    [YetkiTipleri.YKC_RAPOR_GOR] = "YKC Raporlarını Gör",
                    [YetkiTipleri.TAM_YETKI] = "Tam Yetki"
                };

                yetkilerim = mevcutYetkiler
                    .Select(x => yetkiAdlari.ContainsKey(x) ? yetkiAdlari[x] : x)
                    .Distinct()
                    .ToList();
            }

            ViewBag.Yetkilerim = yetkilerim;
        }

        private async Task SetPersonelNotifViewBags(AppKullanici kullanici)
        {
            var dashboard = await GetPersonelDashboardOzetAsync(kullanici);
            ViewBag.OnayBekleyen = dashboard?.OnayBekleyen ?? 0;
            ViewBag.SuresiBitecek = dashboard?.SuresiBitecek ?? 0;
        }

        private async Task<IActionResult?> YetkiKontrol(string yetki)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var yetkili = await KullaniciYetkiliMi(kullanici, yetki);
            if (!yetkili)
                return RedirectToAction(nameof(Index));

            return null;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index([FromServices] YkcApiClient ykcApi)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            // Panel ana sayfasi personel icin goruntulenebilir olsun
            // Yetki yoksa onay islemleri gibi aksiyonlar zaten ilgili sayfalarda kontrol edilir.

            var dashboard = await GetPersonelDashboardOzetAsync(kullanici) ?? new AdminDashboardOzet();
            ViewBag.OnayBekleyen = dashboard.OnayBekleyen;
            ViewBag.ToplamFirma = dashboard.ToplamFirma;
            ViewBag.ToplamDevreyeAlma = dashboard.ToplamDevreyeAlma;
            ViewBag.ToplamSirket = dashboard.ToplamSirket;
            ViewBag.BuAy = dashboard.BuAyDevreyeAlma;
            ViewBag.SuresiBitecek = dashboard.SuresiBitecek;
            ViewBag.SonIslemler = dashboard.SonDevreyeAlmalar;

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            if (ViewBag.YetkiBelgesi == true)
            {
                try
                {
                    var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
                    var belgeler = await _yetkiBelgesiOnayApiClient.ListeleAsync(kullanici, sirketId);
                    ViewBag.SonBekleyenler = belgeler?.Bekleyenler
                        .OrderByDescending(x => x.OlusturmaTarihi).Take(3).ToList();
                    ViewBag.OnayBekleyen = belgeler?.Bekleyenler.Count;
                    ViewBag.OnaylananBelge = belgeler?.Onaylananlar.Count;
                    ViewBag.ReddedilenBelge = belgeler?.Reddedilenler.Count;
                }
                catch (ApiIntegrationException ex)
                {
                    ViewBag.BekleyenBelgelerAlinamadi = true;
                    TempData["Hata"] = ex.Message;
                }
            }
            if (ViewBag.YetkiYkcTalep == true)
            {
                try { ViewBag.YkcOzet = await ykcApi.DashboardOzetAsync(kullanici); }
                catch (ApiIntegrationException ex) { TempData["Hata"] = ex.Message; }
            }
            if (ViewBag.YetkiServis == true)
            {
                try
                {
                    var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
                    var sonuc = await _adminYetkiliServisApiClient.ListeleAsync(kullanici, sirketId, null, null, null, null);
                    ViewBag.ServisToplam = sonuc?.Servisler.Count;
                    ViewBag.ServisAktif = sonuc?.Servisler.Count(x => x.AktifMi);
                    ViewBag.ServisPasif = sonuc?.Servisler.Count(x => !x.AktifMi);
                }
                catch (ApiIntegrationException ex) { TempData["Hata"] = ex.Message; }
            }
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Index.cshtml");
        }

        [HttpGet("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            Dag_Sirket? sirket = null;
            if (sirketId.HasValue)
            {
                try
                {
                    sirket = await _dagitimSirketApiClient.GetirAsync(kullanici, sirketId.Value);
                }
                catch (ApiIntegrationException ex)
                {
                    TempData["Hata"] = ex.Message;
                }
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Sirket = sirket;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Profil.cshtml");
        }

        [HttpPost("profil-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilGuncelle(string adSoyad, string email, string telefon)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(adSoyad) || string.IsNullOrWhiteSpace(email) ||
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim()))
            {
                TempData["Hata"] = "Ad soyad ve geçerli bir e-posta adresi girin.";
                return RedirectToAction(nameof(Profil));
            }
            kullanici.AdSoyad = adSoyad.Trim();
            kullanici.Email = email.Trim();
            kullanici.UserName = email.Trim();
            kullanici.PhoneNumber = telefon?.Trim();

            var sonuc = await _kullaniciOturumu.UpdateAsync(kullanici);
            if (sonuc.Succeeded) TempData["Basarili"] = "Profil bilgileriniz başarıyla güncellendi.";
            else TempData["Hata"] = "Güncelleme sırasında hata oluştu.";

            return RedirectToAction(nameof(Profil));
        }

        [HttpPost("sifre-degistir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SifreDegistir(string mevcutSifre, string yeniSifre, string yeniSifreTekrar)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(mevcutSifre) || string.IsNullOrWhiteSpace(yeniSifre))
            {
                TempData["SifreHata"] = "Mevcut ve yeni şifreyi girin.";
                return RedirectToAction(nameof(Profil));
            }
            if (yeniSifre != yeniSifreTekrar)
            {
                TempData["SifreHata"] = "Yeni şifreler eşleşmiyor.";
                return RedirectToAction(nameof(Profil));
            }

            var sonuc = await _kullaniciOturumu.ChangePasswordAsync(kullanici, mevcutSifre, yeniSifre);
            if (sonuc.Succeeded) TempData["SifreBasarili"] = "Şifreniz başarıyla değiştirildi.";
            else TempData["SifreHata"] = "Şifre güncellenemedi. Mevcut şifrenizi ve yeni şifrenin kurallara uygunluğunu kontrol edin.";

            return RedirectToAction(nameof(Profil));
        }

        [HttpGet("devreyealmalar")]
        public async Task<IActionResult> DevreyeAlmalar(string? tesisat, string? musteri, string? marka, string? servis, string? il, string? ilce, string? durum, DateTime? bas, DateTime? bit)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            AdminDevreyeAlmaListeSonuc sonuc;
            try
            {
                sonuc = await _adminRaporApiClient.DevreyeAlmalarAsync(
                        kullanici,
                        sirketId,
                        marka,
                        servis,
                        il,
                        durum,
                        bas,
                        bit,
                        tesisatNo: tesisat,
                        ilce: ilce)
                    ?? new AdminDevreyeAlmaListeSonuc();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sonuc = new AdminDevreyeAlmaListeSonuc();
            }

            if (!string.IsNullOrWhiteSpace(musteri))
            {
                var aranacak = musteri.Trim();
                sonuc.Islemler = sonuc.Islemler
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.MusteriAdi) && x.MusteriAdi.Contains(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.AboneNo) && x.AboneNo.Contains(aranacak, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.MusteriTelefon) && x.MusteriTelefon.Contains(aranacak, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            ViewBag.Markalar = sonuc.Markalar;
            ViewBag.FirmaIlceleri = sonuc.FirmaIlceleri;
            ViewBag.Musteri = musteri ?? "";
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/DevreyeAlmalar.cshtml", sonuc.Islemler);
        }

        [HttpGet("devreyealmalar/detay/{id}")]
        public async Task<IActionResult> DevreyeAlmaDetay(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            Ys_DevreyeAlma? kayit;
            try
            {
                kayit = await _adminRaporApiClient.DevreyeAlmaDetayAsync(kullanici, id, sirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(DevreyeAlmalar));
            }

            if (kayit == null) return RedirectToAction(nameof(DevreyeAlmalar));

            var listeUrl = Url.Action(nameof(DevreyeAlmalar), "PersonelPanel", new { tesisat = kayit.TesistatNo });
            return Redirect($"{listeUrl}#devreye-alma-detay-{id}");
        }

        [HttpGet("devreyealma-pdf/{id}")]
        public async Task<IActionResult> DevreyeAlmaPdf(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var dosya = await _adminRaporApiClient.DevreyeAlmaPdfAsync(kullanici, id, sirketId);
                if (dosya == null) return NotFound();

                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(DevreyeAlmalar));
            }
        }

        [HttpGet("devreyealma-excel/{id}")]
        public async Task<IActionResult> DevreyeAlmaExcel(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var dosya = await _adminRaporApiClient.DevreyeAlmaExcelAsync(kullanici, id, sirketId);
                if (dosya == null) return NotFound();

                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(DevreyeAlmalar));
            }
        }

        [HttpGet("devreyealmalar/pdf")]
        public async Task<IActionResult> DevreyeAlmalarPdf([FromQuery] List<int>? ids)
        {
            return await DevreyeAlmaListeDosyasi(ids, excelMi: false);
        }

        [HttpGet("devreyealmalar/excel")]
        public async Task<IActionResult> DevreyeAlmalarExcel([FromQuery] List<int>? ids)
        {
            return await DevreyeAlmaListeDosyasi(ids, excelMi: true);
        }

        private async Task<IActionResult> DevreyeAlmaListeDosyasi(List<int>? ids, bool excelMi)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var kayitIdleri = ids?.Where(x => x > 0).Distinct().ToList() ?? new List<int>();
            if (kayitIdleri.Count == 0)
            {
                TempData["Hata"] = "Dışa aktarılacak en az bir kayıt seçin.";
                return RedirectToAction(nameof(DevreyeAlmalar));
            }

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var dosya = excelMi
                    ? await _adminRaporApiClient.RaporlarExcelAsync(kullanici, sirketId, null, null, kayitIdleri)
                    : await _adminRaporApiClient.RaporlarPdfAsync(kullanici, sirketId, null, null, kayitIdleri);
                if (dosya == null) return NotFound();

                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(DevreyeAlmalar));
            }
        }

        [HttpGet("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.YETKI_BELGESI_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            AdminYetkiBelgesiOnaySonuc sonuc;
            try
            {
                sonuc = await _yetkiBelgesiOnayApiClient.ListeleAsync(kullanici, sirketId)
                    ?? new AdminYetkiBelgesiOnaySonuc();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sonuc = new AdminYetkiBelgesiOnaySonuc();
            }

            var bekleyenler = sonuc.Bekleyenler;
            var onaylananlar = sonuc.Onaylananlar;
            var reddedilenler = sonuc.Reddedilenler;

            ViewBag.OnayBekleyen = bekleyenler.Count;
            ViewBag.Kullanici = kullanici;
            ViewBag.Onaylananlar = onaylananlar;
            ViewBag.Reddedilenler = reddedilenler;
            ViewBag.SuresiDolanlar = sonuc.SuresiDolanlar;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/OnayBekleyenler.cshtml", bekleyenler);
        }

        [HttpGet("onayla")]
        public IActionResult OnaylaGet()
        {
            return Redirect("/personel-panel/onay-bekleyenler");
        }

        [HttpPost("onayla")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Onayla(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.YETKI_BELGESI_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _yetkiBelgesiApiClient.OnaylaAsync(kullanici, id);
                if (sonuc?.Basarili == true)
                    TempData["Basarili"] = "Yetki belgesi onaylandı.";
                else
                    TempData["Hata"] = sonuc?.Mesaj ?? "Yetki belgesi onaylanamadı.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return Redirect("/personel-panel/onay-bekleyenler");
        }

        [HttpPost("reddet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id, string? gerekce)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.YETKI_BELGESI_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _yetkiBelgesiApiClient.ReddetAsync(kullanici, id, gerekce);
                if (sonuc?.Basarili == true)
                    TempData["Basarili"] = "Yetki belgesi reddedildi.";
                else
                    TempData["Hata"] = sonuc?.Mesaj ?? "Yetki belgesi reddedilemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return Redirect("/personel-panel/onay-bekleyenler");
        }

        [HttpGet("onay-gecmisi")]
        public async Task<IActionResult> OnayGecmisi(DateTime? bas, DateTime? bit, string? q, string? durum)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.YETKI_BELGESI_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            List<Ys_YetkiBelgesi> belgeler;
            try
            {
                belgeler = await _yetkiBelgesiOnayApiClient.OnayGecmisiAsync(
                    kullanici, sirketId, bas, bit, q, durum) ?? new List<Ys_YetkiBelgesi>();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                belgeler = new List<Ys_YetkiBelgesi>();
            }

            ViewBag.Bas = bas;
            ViewBag.Bit = bit;
            ViewBag.Q = q ?? "";
            ViewBag.Durum = durum ?? "";
            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/OnayGecmisi.cshtml", belgeler);
        }

        [HttpGet("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            List<Ys_Marka> markalar;
            try
            {
                markalar = await _markaApiClient.TumunuGetirAsync(kullanici) ?? new List<Ys_Marka>();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                markalar = new List<Ys_Marka>();
            }

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Markalar.cshtml", markalar);
        }

        [HttpGet("markalar/ekle")]
        public async Task<IActionResult> MarkaEkle()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/MarkaEkle.cshtml");
        }

        [HttpPost("markalar/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaEkle(Ys_Marka marka)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _markaApiClient.EkleAsync(kullanici, marka);
                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Marka başarıyla eklendi."
                        : sonuc?.Mesaj ?? "Marka eklenemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(Markalar));
        }

        [HttpGet("markalar/duzenle/{id}")]
        public async Task<IActionResult> MarkaDuzenle(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            Ys_Marka? marka;
            try
            {
                marka = await _markaApiClient.GetirAsync(kullanici, id);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(Markalar));
            }

            if (marka == null) return RedirectToAction(nameof(Markalar));

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/MarkaDuzenle.cshtml", marka);
        }

        [HttpPost("markalar/duzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaDuzenle(int id, Ys_Marka model)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            model.Id = id;
            try
            {
                var sonuc = await _markaApiClient.GuncelleAsync(kullanici, model);
                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Marka başarıyla güncellendi."
                        : sonuc?.Mesaj ?? "Marka güncellenemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(Markalar));
        }

        [HttpPost("markalar/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaSil(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _markaApiClient.SilAsync(kullanici, id);
                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Marka silindi."
                        : sonuc?.Mesaj ?? "Marka silinemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(Markalar));
        }

        [HttpGet("yetkiliservisler")]
        public async Task<IActionResult> YetkiliServisler(string? q, string? il, string? durum, string? siralama)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            int? durumDegeri = int.TryParse(durum, out var parsedDurum) ? parsedDurum : null;
            AdminYetkiliServisListeSonuc? listeSonuc;
            try
            {
                listeSonuc = await _adminYetkiliServisApiClient.ListeleAsync(kullanici, sirketId, q, il, durumDegeri, siralama);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                listeSonuc = null;
            }

            var servisler = listeSonuc?.Servisler ?? new List<Ys_Firma>();
            var devreyeSayilari = listeSonuc?.DevreyeSayilari ?? new Dictionary<int, int>();

            if (listeSonuc == null)
                TempData["Hata"] = "Yetkili servis listesi API üzerinden alınamadı.";

            ViewBag.Kullanici = kullanici;
            ViewBag.Query = q ?? "";
            ViewBag.Il = il ?? "";
            ViewBag.Durum = durum ?? "";
            ViewBag.Siralama = siralama ?? "";
            ViewBag.DevreyeSayilari = devreyeSayilari;
            ViewBag.Ilceler = listeSonuc?.Ilceler ?? new Dictionary<int, string>();
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/YetkiliServisler.cshtml", servisler);
        }

        [HttpGet("yetkiliservisler/detay/{id}")]
        public async Task<IActionResult> YetkiliServisDetay(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");
            return Redirect($"/personel-panel/yetkiliservisler?servis={id}");
        }

        [HttpGet("yetkiliservisler/ekle")]
        public async Task<IActionResult> YetkiliServisEkle()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (!string.Equals(
                    Request.Headers["X-Requested-With"].ToString(),
                    "XMLHttpRequest",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/personel-panel/yetkiliservisler?panel=ekle");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.Markalar = await _markaApiClient.AktifleriGetirAsync() ?? new List<Ys_Marka>();

            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/YetkiliServisEkle.cshtml");
        }

        [HttpPost("yetkiliservisler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisEkle(string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, List<int> kategoriIds, List<int> markaIds)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(firmaAdi))
            {
                TempData["Hata"] = "Firma adı zorunludur.";
                return Redirect("/personel-panel/yetkiliservisler/ekle");
            }

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var sonuc = await _adminYetkiliServisApiClient.EkleAsync(
                    kullanici,
                    sirketId,
                    firmaAdi,
                    yetkiliKisi,
                    telefon,
                    email,
                    adres,
                    faaliyetIli,
                    vergiNo,
                    vergiDairesi,
                    kategoriIds,
                    markaIds);

                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Yetkili servis başarıyla eklendi."
                        : sonuc?.Mesaj ?? "Yetkili servis eklenemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(YetkiliServisler));
        }

        [HttpGet("yetkiliservisler/duzenle/{id}")]
        public async Task<IActionResult> YetkiliServisDuzenle(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (!string.Equals(
                    Request.Headers["X-Requested-With"].ToString(),
                    "XMLHttpRequest",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Redirect($"/personel-panel/yetkiliservisler?duzenle={id}");
            }

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            AdminYetkiliServisDetaySonuc? detay;
            try
            {
                detay = await _adminYetkiliServisApiClient.DetayAsync(kullanici, id, sirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(YetkiliServisler));
            }

            var servis = detay?.Servis;
            if (servis == null) return RedirectToAction(nameof(YetkiliServisler));

            ViewBag.Kullanici = kullanici;
            ViewBag.Servis = servis;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.Markalar = await _markaApiClient.AktifleriGetirAsync() ?? new List<Ys_Marka>();
            ViewBag.SeciliKategoriler = servis.FirmaKategoriler?.Where(x => !x.SilindiMi).Select(x => x.KategoriId).ToList() ?? new List<int>();
            ViewBag.SeciliMarkalar = servis.FirmaMarkalar?.Where(x => !x.SilindiMi).Select(x => x.MarkaId).ToList() ?? new List<int>();

            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/YetkiliServisDuzenle.cshtml", servis);
        }

        [HttpPost("yetkiliservisler/duzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisDuzenle(int id, string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, bool aktifMi, List<int> kategoriIds, List<int> markaIds)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var sonuc = await _adminYetkiliServisApiClient.GuncelleAsync(
                    kullanici,
                    id,
                    sirketId,
                    firmaAdi,
                    yetkiliKisi,
                    telefon,
                    email,
                    adres,
                    faaliyetIli,
                    vergiNo,
                    vergiDairesi,
                    aktifMi,
                    kategoriIds,
                    markaIds ?? new List<int>());

                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Yetkili servis güncellendi."
                        : sonuc?.Mesaj ?? "Yetkili servis güncellenemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(YetkiliServisler));
        }

        [HttpPost("yetkiliservisler/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisSil(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var sonuc = await _adminYetkiliServisApiClient.SilAsync(kullanici, id, sirketId);
                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Yetkili servis silindi."
                        : sonuc?.Mesaj ?? "Yetkili servis silinemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(YetkiliServisler));
        }

        [HttpGet("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit, string? tip)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var genelRaporYetkisi = await KullaniciYetkiliMi(kullanici, YetkiTipleri.RAPOR_GOR);
            var ykcRaporYetkisi = await KullaniciYetkiliMi(kullanici, YetkiTipleri.YKC_RAPOR_GOR);
            var belgeYetkisi = await KullaniciYetkiliMi(kullanici, YetkiTipleri.YETKI_BELGESI_ONAY);
            if (!genelRaporYetkisi && !ykcRaporYetkisi)
                return Redirect("/yetkisiz-erisim");

            var izinliRaporTipleri = new List<string>();
            if (ykcRaporYetkisi)
                izinliRaporTipleri.Add("ykc");
            if (genelRaporYetkisi)
                izinliRaporTipleri.Add("devreye");
            if (genelRaporYetkisi && belgeYetkisi)
                izinliRaporTipleri.AddRange(new[] { "onayli", "bekleyen", "reddedilen" });

            var istenenRaporTipi = string.IsNullOrWhiteSpace(tip) ? null : tip.Trim().ToLowerInvariant();
            var raporTipi = istenenRaporTipi != null && izinliRaporTipleri.Contains(istenenRaporTipi)
                ? istenenRaporTipi
                : izinliRaporTipleri[0];

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (raporTipi == "ykc")
            {
                var basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
                var bitTarih = bit?.Date ?? DateTime.Now.Date;
                try
                {
                    var ykcSonuc = await _ykcApiClient.RaporAsync(kullanici, new YkcTalepListeFiltre
                    {
                        SirketId = sirketId,
                        BaslangicTarihi = basTarih,
                        BitisTarihi = bitTarih,
                        Sayfa = 1,
                        SayfaBoyutu = 10
                    }) ?? new YkcRaporSonuc();
                    var durumlar = ykcSonuc.DurumOzetleri.OrderByDescending(x => x.Sayi).ToList();
                    ViewBag.ListeTipi = "ykc";
                    ViewBag.RaporTipi = "ykc";
                    ViewBag.RaporToplam = ykcSonuc.Toplam;
                    ViewBag.RaporAylar = ykcSonuc.FirmaOzetleri.Take(6).Select(x => x.Ad).ToList();
                    ViewBag.RaporAylik = ykcSonuc.FirmaOzetleri.Take(6).Select(x => x.Sayi).ToList();
                    ViewBag.RaporMarka = ykcSonuc.EkipOzetleri.Take(6).Select(x => x.Ad).ToList();
                    ViewBag.RaporMarkaSayi = ykcSonuc.EkipOzetleri.Take(6).Select(x => x.Sayi).ToList();
                    ViewBag.RaporDurum = durumlar.Select(x => x.Sayi).ToList();
                    ViewBag.RaporDurumEtiketleri = durumlar.Select(x => YkcDurumSunumu.Etiket(x.Durum)).ToList();
                    ViewBag.BasTarih = basTarih;
                    ViewBag.BitTarih = bitTarih;
                }
                catch (ApiIntegrationException ex)
                {
                    TempData["Hata"] = ex.Message;
                    ViewBag.ListeTipi = "ykc";
                    ViewBag.RaporTipi = "ykc";
                    ViewBag.RaporToplam = 0;
                    ViewBag.BasTarih = basTarih;
                    ViewBag.BitTarih = bitTarih;
                }

                ViewBag.Kullanici = kullanici;
                await SetPersonelYetkiViewBags(kullanici);
                await SetPersonelNotifViewBags(kullanici);
                return View("~/Views/PersonelPanel/Raporlar.cshtml");
            }

            AdminRaporOzetSonuc sonuc;
            try
            {
                sonuc = await _adminRaporApiClient.RaporlarOzetAsync(kullanici, sirketId, bas, bit, raporTipi)
                    ?? new AdminRaporOzetSonuc
                    {
                        BasTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30),
                        BitTarih = bit?.Date ?? DateTime.Now.Date,
                        RaporTipi = raporTipi
                    };
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sonuc = new AdminRaporOzetSonuc
                {
                    BasTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30),
                    BitTarih = bit?.Date ?? DateTime.Now.Date,
                    RaporTipi = raporTipi,
                    ListeTipi = raporTipi is "onayli" or "bekleyen" or "reddedilen" ? "yetkiBelgesi" : "devreye"
                };
            }

            ViewBag.SonIslemler = sonuc.SonIslemler;
            ViewBag.YetkiBelgesiIslemler = sonuc.YetkiBelgesiIslemler;
            ViewBag.ListeTipi = sonuc.ListeTipi;
            ViewBag.RaporToplam = sonuc.DevreyeSayisi;
            ViewBag.RaporTamam = sonuc.DevreyeTamamlanan;
            ViewBag.RaporBekleyen = sonuc.DevreyeBekleyen;
            ViewBag.RaporIptal = sonuc.DevreyeIptal;
            ViewBag.YetkiBelgesiOnayli = sonuc.YetkiBelgesiOnayli;
            ViewBag.YetkiBelgesiBekleyen = sonuc.YetkiBelgesiBekleyen;
            ViewBag.YetkiBelgesiReddedilen = sonuc.YetkiBelgesiReddedilen;
            ViewBag.RaporAylar = sonuc.ChartAylikLabels;
            ViewBag.RaporAylik = sonuc.ChartAylikData;
            ViewBag.RaporMarka = sonuc.ChartMarkaLabels;
            ViewBag.RaporMarkaSayi = sonuc.ChartMarkaData;
            ViewBag.RaporDurum = sonuc.ChartDurumData;
            ViewBag.BasTarih = sonuc.BasTarih;
            ViewBag.BitTarih = sonuc.BitTarih;
            ViewBag.RaporTipi = sonuc.RaporTipi;

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Raporlar.cshtml");
        }

        [HttpGet("raporlar/pdf")]
        public Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, string? tip)
        {
            return YetkiBelgesiRaporTipiMi(tip)
                ? YetkiBelgesiRaporDosyasi(bas, bit, tip!, excelMi: false)
                : RaporDosyasi(bas, bit, excelMi: false);
        }

        [HttpGet("raporlar/excel")]
        public Task<IActionResult> RaporlarExcel(DateTime? bas, DateTime? bit, string? tip)
        {
            return YetkiBelgesiRaporTipiMi(tip)
                ? YetkiBelgesiRaporDosyasi(bas, bit, tip!, excelMi: true)
                : RaporDosyasi(bas, bit, excelMi: true);
        }

        private static bool YetkiBelgesiRaporTipiMi(string? tip)
            => tip is "onayli" or "bekleyen" or "reddedilen";

        private async Task<IActionResult> YetkiBelgesiRaporDosyasi(
            DateTime? bas,
            DateTime? bit,
            string tip,
            bool excelMi)
        {
            var raporYetkisi = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (raporYetkisi != null) return raporYetkisi;
            var belgeYetkisi = await YetkiKontrol(YetkiTipleri.YETKI_BELGESI_ONAY);
            if (belgeYetkisi != null) return belgeYetkisi;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
            var bitTarih = bit?.Date ?? DateTime.Now.Date;
            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);

            try
            {
                List<Ys_YetkiBelgesi> belgeler;
                if (tip == "bekleyen")
                {
                    var sonuc = await _yetkiBelgesiOnayApiClient.ListeleAsync(kullanici, sirketId)
                        ?? new AdminYetkiBelgesiOnaySonuc();
                    belgeler = sonuc.Bekleyenler
                        .Where(x => x.OlusturmaTarihi.Date >= basTarih && x.OlusturmaTarihi.Date <= bitTarih)
                        .ToList();
                }
                else
                {
                    var durum = tip == "onayli"
                        ? YetkiBelgesiDurumDegerleri.Onaylandi.ToString()
                        : YetkiBelgesiDurumDegerleri.Reddedildi.ToString();
                    belgeler = await _yetkiBelgesiOnayApiClient.OnayGecmisiAsync(
                        kullanici,
                        sirketId,
                        basTarih,
                        bitTarih,
                        null,
                        durum) ?? new List<Ys_YetkiBelgesi>();
                }

                belgeler = belgeler
                    .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                    .Take(5000)
                    .ToList();
                if (belgeler.Count == 0)
                    return NotFound("Seçilen dönemde dışa aktarılacak yetki belgesi bulunamadı.");

                var baslik = tip switch
                {
                    "onayli" => "Onaylanan Yetki Belgeleri",
                    "reddedilen" => "Reddedilen Yetki Belgeleri",
                    _ => "Onay Bekleyen Yetki Belgeleri"
                };
                var tarihDamgasi = DateTime.Now.ToString("yyyyMMdd-HHmm");
                var bytes = excelMi
                    ? YetkiBelgesiRaporExcelService.Olustur(belgeler, baslik)
                    : YetkiBelgesiRaporPdfService.Olustur(belgeler, baslik);

                return this.HassasDosya(
                    bytes,
                    excelMi ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf",
                    $"yetki-belgesi-raporu-{tarihDamgasi}.{(excelMi ? "xlsx" : "pdf")}");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(Raporlar), new { bas, bit, tip });
            }
        }

        private async Task<IActionResult> RaporDosyasi(DateTime? bas, DateTime? bit, bool excelMi)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var dosya = excelMi
                    ? await _adminRaporApiClient.RaporlarExcelAsync(kullanici, sirketId, bas, bit, null)
                    : await _adminRaporApiClient.RaporlarPdfAsync(kullanici, sirketId, bas, bit, null);
                if (dosya == null) return NotFound();

                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(Raporlar), new { bas, bit, tip = "devreye" });
            }
        }
    }
}

