using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("personel-panel")]
    public class PersonelPanelController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
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

        public PersonelPanelController(
            UserManager<AppKullanici> userManager,
            SehirFirmaKoduService sehirFirmaKoduService,
            AktifSirketService aktifSirketService,
            AdminDashboardApiClient adminDashboardApiClient,
            PersonelPanelApiClient personelPanelApiClient,
            AdminYetkiBelgesiOnayApiClient yetkiBelgesiOnayApiClient,
            AdminRaporApiClient adminRaporApiClient,
            YetkiBelgesiApiClient yetkiBelgesiApiClient,
            DagitimSirketApiClient dagitimSirketApiClient,
            MarkaApiClient markaApiClient,
            UrunKategoriApiClient urunKategoriApiClient,
            AdminYetkiliServisApiClient adminYetkiliServisApiClient)
        {
            _userManager = userManager;
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
            var ySirketYonet = await KullaniciYetkiliMi(kullanici, YetkiTipleri.DAGITIM_SIRKET_YONET);
            var yMarkaYonet = await KullaniciYetkiliMi(kullanici, YetkiTipleri.MARKA_YONET);

            ViewBag.YetkiBelgesi = yYetkiBelgesi;
            ViewBag.YetkiRapor = yRapor;
            ViewBag.YetkiServis = yServis;
            ViewBag.YetkiSirket = ySirketYonet;
            ViewBag.YetkiMarka = yMarkaYonet;
            ViewBag.YetkiSirketYonet = ySirketYonet;
            ViewBag.YetkiMarkaYonet = yMarkaYonet;

            List<string> yetkilerim;
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici) || await _aktifSirketService.SirketAdminMi(kullanici))
            {
                yetkilerim = new List<string>
                {
                    "Tam Yetki",
                    "Yetki Belgesi Onay",
                    "Rapor Gör",
                    "Kullanıcı Yönet",
                    "Marka Yönet"
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
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var yetkili = await KullaniciYetkiliMi(kullanici, yetki);
            if (!yetkili)
                return RedirectToAction(nameof(Index));

            return null;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            // Panel ana sayfasi personel icin goruntulenebilir olsun
            // Yetki yoksa onay islemleri gibi aksiyonlar zaten ilgili sayfalarda kontrol edilir.

            var dashboard = await GetPersonelDashboardOzetAsync(kullanici) ?? new AdminDashboardOzet();
            var markalar = await _markaApiClient.TumunuGetirAsync() ?? new List<Ys_Marka>();

            ViewBag.OnayBekleyen = dashboard.OnayBekleyen;
            ViewBag.ToplamFirma = dashboard.ToplamFirma;
            ViewBag.ToplamDevreyeAlma = dashboard.ToplamDevreyeAlma;
            ViewBag.ToplamMarka = markalar.Count;
            ViewBag.ToplamSirket = dashboard.ToplamSirket;
            ViewBag.BuAy = dashboard.BuAyDevreyeAlma;
            ViewBag.SuresiBitecek = dashboard.SuresiBitecek;
            ViewBag.SonBekleyenler = dashboard.SonYetkiBelgeleri;
            ViewBag.SonIslemler = dashboard.SonDevreyeAlmalar;

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Index.cshtml");
        }

        [HttpGet("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await _userManager.GetUserAsync(User);
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
            var kullanici = await _userManager.GetUserAsync(User);
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
            var kullanici = await _userManager.GetUserAsync(User);
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

        [HttpGet("devreyealmalar")]
        public async Task<IActionResult> DevreyeAlmalar(string? tesisat, string? marka, string? servis, string? il, string? ilce, string? durum, DateTime? bas, DateTime? bit)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            // Devreye almalar raporu personel icin goruntulenebilir olsun.

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

            ViewBag.Markalar = sonuc.Markalar;
            ViewBag.FirmaIlceleri = sonuc.FirmaIlceleri;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/DevreyeAlmalar.cshtml", sonuc.Islemler);
        }

        [HttpGet("devreyealmalar/detay/{id}")]
        public async Task<IActionResult> DevreyeAlmaDetay(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
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

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/DevreyeAlmaDetay.cshtml", kayit);
        }

        [HttpGet("devreyealma-pdf/{id}")]
        public async Task<IActionResult> DevreyeAlmaPdf(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
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

            if (kayit == null) return NotFound();

            var pdf = DevreyeAlmaPdfService.Olustur(kayit);
            return File(pdf, "application/pdf",
                $"DevreyeAlma_{kayit.TesistatNo ?? id.ToString()}_{id}.pdf");
        }

        [HttpGet("devreyealma-excel/{id}")]
        public async Task<IActionResult> DevreyeAlmaExcel(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
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

            if (kayit == null) return NotFound();

            var bytes = DevreyeAlmaExcelService.Olustur(new[] { kayit });
            return File(bytes, "text/csv; charset=windows-1254",
                $"DevreyeAlma_{kayit.TesistatNo ?? id.ToString()}_{id}.csv");
        }

        [HttpGet("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.YETKI_BELGESI_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
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

            var kullanici = await _userManager.GetUserAsync(User);
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

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _yetkiBelgesiApiClient.ReddetAsync(kullanici, id, gerekce);
                if (sonuc?.Basarili == true)
                    TempData["Hata"] = "Yetki belgesi reddedildi.";
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

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            List<Ys_YetkiBelgesi> onaylar;
            try
            {
                onaylar = await _yetkiBelgesiOnayApiClient.OnayGecmisiAsync(kullanici, sirketId, bas, bit, q, durum)
                    ?? new List<Ys_YetkiBelgesi>();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                onaylar = new List<Ys_YetkiBelgesi>();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Bas = bas;
            ViewBag.Bit = bit;
            ViewBag.Q = q ?? "";
            ViewBag.Durum = durum ?? "";
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/OnayGecmisi.cshtml", onaylar);
        }

        [HttpGet("sirketler")]
        public async Task<IActionResult> Sirketler()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            List<Dag_Sirket> sirketler;
            try
            {
                sirketler = await _dagitimSirketApiClient.TumunuGetirAsync() ?? new List<Dag_Sirket>();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sirketler = new List<Dag_Sirket>();
            }

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Sirketler.cshtml", sirketler);
        }

        [HttpGet("sirketler/ekle")]
        public async Task<IActionResult> SirketEkle()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/SirketEkle.cshtml");
        }

        [HttpPost("sirketler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SirketEkle(Dag_Sirket sirket)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _dagitimSirketApiClient.EkleAsync(kullanici, sirket);
                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Şirket başarıyla eklendi."
                        : sonuc?.Mesaj ?? "Şirket eklenemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(Sirketler));
        }

        [HttpGet("sirketler/duzenle/{id}")]
        public async Task<IActionResult> SirketDuzenle(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            Dag_Sirket? sirket;
            try
            {
                sirket = await _dagitimSirketApiClient.GetirAsync(kullanici, id);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return RedirectToAction(nameof(Sirketler));
            }

            if (sirket == null) return RedirectToAction(nameof(Sirketler));

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/SirketDuzenle.cshtml", sirket);
        }

        [HttpPost("sirketler/duzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SirketDuzenle(int id, Dag_Sirket model)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            model.Id = id;
            try
            {
                var sonuc = await _dagitimSirketApiClient.GuncelleAsync(kullanici, model);
                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Şirket başarıyla güncellendi."
                        : sonuc?.Mesaj ?? "Şirket güncellenemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(Sirketler));
        }

        [HttpPost("sirketler/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SirketSil(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _dagitimSirketApiClient.SilAsync(kullanici, id);
                TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] =
                    sonuc?.Basarili == true
                        ? "Şirket silindi."
                        : sonuc?.Mesaj ?? "Şirket silinemedi.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(Sirketler));
        }

        [HttpGet("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            List<Ys_Marka> markalar;
            try
            {
                markalar = await _markaApiClient.TumunuGetirAsync() ?? new List<Ys_Marka>();
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

            var kullanici = await _userManager.GetUserAsync(User);
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

            var kullanici = await _userManager.GetUserAsync(User);
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

            var kullanici = await _userManager.GetUserAsync(User);
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

            var kullanici = await _userManager.GetUserAsync(User);
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

            var kullanici = await _userManager.GetUserAsync(User);
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
        public async Task<IActionResult> YetkiliServisler(string? q)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            AdminYetkiliServisListeSonuc? listeSonuc;
            try
            {
                listeSonuc = await _adminYetkiliServisApiClient.ListeleAsync(kullanici, sirketId, q, null, null, null);
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
            ViewBag.DevreyeSayilari = devreyeSayilari;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/YetkiliServisler.cshtml", servisler);
        }

        [HttpGet("yetkiliservisler/detay/{id}")]
        public async Task<IActionResult> YetkiliServisDetay(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

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

            var firma = detay?.Servis;
            if (firma == null) return RedirectToAction(nameof(YetkiliServisler));
            firma.Subeler = detay!.Subeler;

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/YetkiliServisDetay.cshtml", firma);
        }

        [HttpGet("yetkiliservisler/ekle")]
        public async Task<IActionResult> YetkiliServisEkle()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            ViewBag.Kullanici = kullanici;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.Markalar = await _markaApiClient.TumunuGetirAsync() ?? new List<Ys_Marka>();

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

            var kullanici = await _userManager.GetUserAsync(User);
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

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

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
            ViewBag.Markalar = await _markaApiClient.TumunuGetirAsync() ?? new List<Ys_Marka>();
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

            var kullanici = await _userManager.GetUserAsync(User);
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
                    markaIds?.Count > 0 ? markaIds : null);

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

            var kullanici = await _userManager.GetUserAsync(User);
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
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            AdminRaporOzetSonuc sonuc;
            try
            {
                sonuc = await _adminRaporApiClient.RaporlarOzetAsync(kullanici, sirketId, bas, bit, tip)
                    ?? new AdminRaporOzetSonuc
                    {
                        BasTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30),
                        BitTarih = bit?.Date ?? DateTime.Now.Date,
                        RaporTipi = string.IsNullOrWhiteSpace(tip) ? "devreye" : tip.Trim().ToLowerInvariant()
                    };
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sonuc = new AdminRaporOzetSonuc
                {
                    BasTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30),
                    BitTarih = bit?.Date ?? DateTime.Now.Date,
                    RaporTipi = string.IsNullOrWhiteSpace(tip) ? "devreye" : tip.Trim().ToLowerInvariant(),
                    ListeTipi = (tip == "onayli" || tip == "bekleyen" || tip == "reddedilen") ? "yetkiBelgesi" : "devreye"
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
            ViewBag.BasTarih = sonuc.BasTarih;
            ViewBag.BitTarih = sonuc.BitTarih;
            ViewBag.RaporTipi = sonuc.RaporTipi;

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Raporlar.cshtml");
        }
    }
}

