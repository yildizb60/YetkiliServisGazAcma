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
        private readonly AktifSirketService _aktifSirketService;
        private readonly PanelGorunumService _panelGorunum;
        private readonly PersonelPanelApiClient _personelPanelApiClient;
        private readonly AdminYetkiBelgesiOnayApiClient _yetkiBelgesiOnayApiClient;
        private readonly AdminRaporApiClient _adminRaporApiClient;
        private readonly YetkiBelgesiApiClient _yetkiBelgesiApiClient;
        private readonly DagitimSirketApiClient _dagitimSirketApiClient;
        private readonly MarkaApiClient _markaApiClient;
        private readonly AdminYetkiliServisApiClient _adminYetkiliServisApiClient;

        public PersonelPanelController(
            ApiKullaniciOturumu kullaniciOturumu,
            AktifSirketService aktifSirketService,
            PanelGorunumService panelGorunum,
            PersonelPanelApiClient personelPanelApiClient,
            AdminYetkiBelgesiOnayApiClient yetkiBelgesiOnayApiClient,
            AdminRaporApiClient adminRaporApiClient,
            YetkiBelgesiApiClient yetkiBelgesiApiClient,
            DagitimSirketApiClient dagitimSirketApiClient,
            MarkaApiClient markaApiClient,
            AdminYetkiliServisApiClient adminYetkiliServisApiClient)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _aktifSirketService = aktifSirketService;
            _panelGorunum = panelGorunum;
            _personelPanelApiClient = personelPanelApiClient;
            _yetkiBelgesiOnayApiClient = yetkiBelgesiOnayApiClient;
            _adminRaporApiClient = adminRaporApiClient;
            _yetkiBelgesiApiClient = yetkiBelgesiApiClient;
            _dagitimSirketApiClient = dagitimSirketApiClient;
            _markaApiClient = markaApiClient;
            _adminYetkiliServisApiClient = adminYetkiliServisApiClient;
        }

        private Task<bool> KullaniciYetkiliMi(AppKullanici kullanici, string yetki)
            => _panelGorunum.YetkiliMiAsync(kullanici, yetki);

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
        public async Task<IActionResult> Index()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            PersonelDashboardDto? dashboard = null;
            try
            {
                dashboard = await _personelPanelApiClient.DashboardAsync(kullanici, sirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }
            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = dashboard?.OnayBekleyen ?? 0;
            ViewBag.SuresiBitecek = dashboard?.SuresiBitecek ?? 0;
            return View("~/Views/PersonelPanel/Index.cshtml", dashboard ?? new PersonelDashboardDto { Bugun = DateTime.Today });
        }

        [HttpGet("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            DagitimSirketApiDto? sirket = null;
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
        public async Task<IActionResult> DevreyeAlmalar(string? tesisat, string? musteri, string? marka, string? servis, string? il, string? ilce, DateTime? bas, DateTime? bit)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            AdminDevreyeAlmaListeDto sonuc;
            try
            {
                sonuc = await _adminRaporApiClient.DevreyeAlmalarAsync(
                        kullanici,
                        sirketId,
                        marka,
                        servis,
                        il,
                        null,
                        bas,
                        bit,
                        tesisatNo: tesisat,
                        ilce: ilce,
                        musteri: musteri)
                    ?? new AdminDevreyeAlmaListeDto();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sonuc = new AdminDevreyeAlmaListeDto();
            }

            ViewBag.Markalar = sonuc.Markalar;
            ViewBag.FirmaIlceleri = sonuc.FirmaIlceleri;
            ViewBag.Musteri = musteri ?? "";
            ViewBag.Sehirler = sonuc.Sehirler;
            ViewBag.Kullanici = kullanici;
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
            AdminDevreyeAlmaDto? kayit;
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
            AdminYetkiBelgesiOnayListeDto sonuc;
            try
            {
                sonuc = await _yetkiBelgesiOnayApiClient.ListeleAsync(kullanici, sirketId)
                    ?? new AdminYetkiBelgesiOnayListeDto();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                sonuc = new AdminYetkiBelgesiOnayListeDto();
            }

            var bekleyenler = sonuc.Bekleyenler;
            var onaylananlar = sonuc.Onaylananlar;
            var reddedilenler = sonuc.Reddedilenler;

            ViewBag.OnayBekleyen = bekleyenler.Count;
            ViewBag.Kullanici = kullanici;
            ViewBag.Onaylananlar = onaylananlar;
            ViewBag.Reddedilenler = reddedilenler;
            ViewBag.SuresiDolanlar = sonuc.SuresiDolanlar;
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
            List<AdminYetkiBelgesiOnayDto> belgeler;
            try
            {
                belgeler = await _yetkiBelgesiOnayApiClient.OnayGecmisiAsync(
                    kullanici, sirketId, bas, bit, q, durum) ?? new List<AdminYetkiBelgesiOnayDto>();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                belgeler = new List<AdminYetkiBelgesiOnayDto>();
            }

            ViewBag.Bas = bas;
            ViewBag.Bit = bit;
            ViewBag.Q = q ?? "";
            ViewBag.Durum = durum ?? "";
            ViewBag.Kullanici = kullanici;
            return View("~/Views/PersonelPanel/OnayGecmisi.cshtml", belgeler);
        }

        [HttpGet("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            List<MarkaApiDto> markalar;
            try
            {
                markalar = await _markaApiClient.TumunuGetirAsync(kullanici) ?? new List<MarkaApiDto>();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                markalar = new List<MarkaApiDto>();
            }

            ViewBag.Kullanici = kullanici;
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
            return View("~/Views/PersonelPanel/MarkaEkle.cshtml");
        }

        [HttpPost("markalar/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaEkle(MarkaKaydetDto marka)
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

            MarkaApiDto? marka;
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
            return View("~/Views/PersonelPanel/MarkaDuzenle.cshtml", marka);
        }

        [HttpPost("markalar/duzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaDuzenle(int id, MarkaKaydetDto model)
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
            AdminYetkiliServisListeDto? listeSonuc;
            try
            {
                listeSonuc = await _adminYetkiliServisApiClient.ListeleAsync(kullanici, sirketId, q, il, durumDegeri, siralama);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                listeSonuc = null;
            }

            var servisler = listeSonuc?.Servisler ?? new List<AdminYetkiliServisDto>();
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
            ViewBag.Sehirler = listeSonuc?.Sehirler ?? new List<string>();
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

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var editor = await _adminYetkiliServisApiClient.EditorAsync(kullanici, 0, sirketId);
            if (editor == null) return RedirectToAction(nameof(YetkiliServisler));
            ViewBag.Kullanici = kullanici;
            ViewBag.Sehirler = editor.Sehirler;
            ViewBag.Kategoriler = editor.Kategoriler;
            ViewBag.Markalar = editor.Markalar;
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
            AdminYetkiliServisEditorDto? detay;
            try
            {
                detay = await _adminYetkiliServisApiClient.EditorAsync(kullanici, id, sirketId);
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
            ViewBag.Sehirler = detay!.Sehirler;
            ViewBag.Kategoriler = detay!.Kategoriler;
            ViewBag.Markalar = detay.Markalar;
            ViewBag.SeciliKategoriler = detay.SeciliKategoriIds;
            ViewBag.SeciliMarkalar = detay.SeciliMarkaIds;
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

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var rapor = await _personelPanelApiClient.RaporAsync(kullanici, new PersonelRaporFiltreDto
                {
                    SirketId = sirketId, BaslangicTarihi = bas, BitisTarihi = bit, Tip = tip
                });
                if (rapor != null)
                {
                    ViewBag.Kullanici = kullanici;
                    return View("~/Views/PersonelPanel/Raporlar.cshtml", rapor);
                }
                TempData["Hata"] = "Rapor bilgileri API üzerinden alınamadı.";
            }
            catch (ApiIntegrationException ex) when (ex.StatusCode == 403)
            {
                return Redirect("/yetkisiz-erisim");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
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

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            try
            {
                var dosya = await _yetkiBelgesiOnayApiClient.RaporAsync(kullanici, new YetkiBelgesiRaporFiltre
                {
                    SirketId = sirketId, BaslangicTarihi = bas, BitisTarihi = bit, Tip = tip
                }, excelMi);
                if (dosya == null) return NotFound();
                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
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

