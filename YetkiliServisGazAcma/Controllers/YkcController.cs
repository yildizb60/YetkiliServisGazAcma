using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel,SertifikaliFirma")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ykc")]
    public class YkcController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly YkcApiClient _ykcApiClient;

        public YkcController(UserManager<AppKullanici> userManager, YkcApiClient ykcApiClient)
        {
            _userManager = userManager;
            _ykcApiClient = ykcApiClient;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            PanelViewBag(kullanici, "YkcOzet", "Ana Sayfa", "Cihaz değişim, FR265 önizleme, randevu ve dijital imza sürecinizi izleyin");

            var sonuc = await _ykcApiClient.DashboardOzetAsync(kullanici) ?? new YkcDashboardOzetDto();
            return View("~/Views/Ykc/Index.cshtml", sonuc);
        }

        [HttpGet("talepler")]
        public async Task<IActionResult> Talepler(
            string? tesisatNo,
            string? firma,
            string? il,
            string? ilce,
            string? bolge,
            string? hedefUygulama,
            int? durum,
            DateTime? bas,
            DateTime? bit,
            int sayfa = 1)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            PanelViewBag(kullanici, "YkcTalepler", "Cihaz Değişim Talepleri", "Yakıcı cihaz değişim formu, randevu ve atama süreci");

            var filtre = new YkcTalepListeFiltre
            {
                TesisatNo = tesisatNo,
                Firma = firma,
                Il = il,
                Ilce = ilce,
                Bolge = bolge,
                HedefUygulama = hedefUygulama,
                Durum = durum,
                BaslangicTarihi = bas,
                BitisTarihi = bit,
                Sayfa = Math.Max(sayfa, 1),
                SayfaBoyutu = 50
            };

            var sonuc = await _ykcApiClient.TaleplerAsync(kullanici, filtre) ?? new YkcTalepListeSonuc();
            ViewBag.Filtre = filtre;
            return View("~/Views/Ykc/Talepler.cshtml", sonuc);
        }

        [HttpGet("raporlar")]
        public async Task<IActionResult> Raporlar(
            string? tesisatNo,
            string? firma,
            string? il,
            string? ilce,
            string? bolge,
            string? ekip,
            string? marka,
            string? hedefUygulama,
            int? durum,
            DateTime? bas,
            DateTime? bit)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            PanelViewBag(kullanici, "YkcRaporlar", "Cihaz Değişim Raporları", "Firma, ekip, tesisat ve tarih aralığına göre cihaz değişim süreci");

            var filtre = new YkcTalepListeFiltre
            {
                TesisatNo = tesisatNo,
                Firma = firma,
                Il = il,
                Ilce = ilce,
                Bolge = bolge,
                Ekip = ekip,
                Marka = marka,
                HedefUygulama = hedefUygulama,
                Durum = durum,
                BaslangicTarihi = bas,
                BitisTarihi = bit,
                Sayfa = 1,
                SayfaBoyutu = 500
            };

            var sonuc = await _ykcApiClient.RaporAsync(kullanici, filtre) ?? new YkcRaporSonuc();
            ViewBag.Filtre = filtre;
            return View("~/Views/Ykc/Raporlar.cshtml", sonuc);
        }

        [HttpGet("yeni")]
        public async Task<IActionResult> Yeni()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            PanelViewBag(kullanici, "YkcYeni", "Yeni Cihaz Değişim Talebi", "Yakıcı cihaz değişim formu oluştur");
            return View("~/Views/Ykc/Yeni.cshtml", new YkcTalepKaydetDto());
        }

        [HttpPost("yeni")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Yeni(YkcTalepKaydetDto model)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            PanelViewBag(kullanici, "YkcYeni", "Yeni Cihaz Değişim Talebi", "Yakıcı cihaz değişim formu oluştur");

            if (string.IsNullOrWhiteSpace(model.TesisatNo))
                ModelState.AddModelError(nameof(model.TesisatNo), "Tesisat no zorunludur.");

            if (string.IsNullOrWhiteSpace(model.YeniCihazTipi) && string.IsNullOrWhiteSpace(model.YeniCihazTipiKodu))
                ModelState.AddModelError(nameof(model.YeniCihazTipi), "Yeni cihaz tipi zorunludur.");

            if (string.IsNullOrWhiteSpace(model.YeniMarka) && string.IsNullOrWhiteSpace(model.YeniMarkaKodu))
                ModelState.AddModelError(nameof(model.YeniMarka), "Yeni marka zorunludur.");

            if (string.IsNullOrWhiteSpace(model.YeniBacaTipi) && string.IsNullOrWhiteSpace(model.YeniBacaTipiKodu))
                ModelState.AddModelError(nameof(model.YeniBacaTipi), "Yeni baca tipi zorunludur.");

            if (string.IsNullOrWhiteSpace(model.YeniKapasite))
                ModelState.AddModelError(nameof(model.YeniKapasite), "Yeni kapasite zorunludur.");

            if (!ModelState.IsValid)
                return View("~/Views/Ykc/Yeni.cshtml", model);

            var sonuc = await _ykcApiClient.OlusturAsync(kullanici, model);
            if (sonuc?.Basarili != true || sonuc.Id == null)
            {
                TempData["Hata"] = sonuc?.Mesaj ?? "Cihaz değişim talebi oluşturulamadı.";
                return View("~/Views/Ykc/Yeni.cshtml", model);
            }

            TempData["Basarili"] = sonuc.Mesaj ?? "Cihaz değişim talebi oluşturuldu.";
            return RedirectToAction(nameof(Detay), new { id = sonuc.Id.Value });
        }

        [HttpPost("tesisat-sorgula")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TesisatSorgula([FromForm] YkcTesisatSorguIstek model)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Unauthorized(new YkcTesisatSorguSonuc { Basarili = false, Mesaj = "Oturum bulunamadi." });

            var sonuc = await _ykcApiClient.TesisatSorgulaAsync(kullanici, model);
            return Json(sonuc ?? YkcTesisatSorguSonuc.Basarisiz("Tesisat sorgusu icin API yaniti alinamadi."));
        }

        [HttpGet("detay/{id:int}")]
        public async Task<IActionResult> Detay(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            PanelViewBag(kullanici, "YkcTalepler", "Cihaz Değişim Talebi Detayı", "Form, cihaz bilgileri ve atama süreci");

            var detay = await _ykcApiClient.DetayAsync(kullanici, id);
            if (detay == null)
            {
                TempData["Hata"] = "Cihaz değişim talebi bulunamadı.";
                return RedirectToAction(nameof(Talepler));
            }

            ViewBag.ImzaEntegrasyonu = await _ykcApiClient.ImzaEntegrasyonBilgisiAsync(kullanici)
                ?? new YkcImzaEntegrasyonDto();

            return View("~/Views/Ykc/Detay.cshtml", detay);
        }

        [HttpGet("fr265/onizle/{id:int}")]
        public async Task<IActionResult> Fr265Onizle(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            PanelViewBag(kullanici, "YkcTalepler", "FR265 Form Önizleme", "Proje tadilatı gerektirmeyen yakıcı cihaz değişim formu");

            var detay = await _ykcApiClient.DetayAsync(kullanici, id);
            if (detay == null)
            {
                TempData["Hata"] = "Cihaz değişim talebi bulunamadı.";
                return RedirectToAction(nameof(Talepler));
            }

            ViewBag.ImzaEntegrasyonu = await _ykcApiClient.ImzaEntegrasyonBilgisiAsync(kullanici)
                ?? new YkcImzaEntegrasyonDto();

            return View("~/Views/Ykc/Fr265Onizle.cshtml", detay);
        }

        [HttpPost("imzaya-gonder")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzayaGonder(int talepId)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var sonuc = await _ykcApiClient.ImzayaGonderAsync(kullanici, talepId);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj
                ?? "FR265 dijital imza uygulamasına gönderilemedi.";
            return RedirectToAction(nameof(Detay), new { id = talepId });
        }

        [HttpPost("imza-durum-sorgula")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzaDurumSorgula(int talepId)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var sonuc = await _ykcApiClient.ImzaDurumSorgulaAsync(kullanici, talepId);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj
                ?? "FR265 dijital imza durumu alınamadı.";
            return RedirectToAction(nameof(Detay), new { id = talepId });
        }

        [HttpGet("dosya/{id:int}")]
        public async Task<IActionResult> DosyaAc(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var dosya = await _ykcApiClient.DosyaIndirAsync(kullanici, id);
            if (dosya == null || dosya.Bytes.Length == 0)
            {
                TempData["Hata"] = "Cihaz değişim form dosyası açılamadı.";
                return RedirectToAction(nameof(Talepler));
            }

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("atama-yap")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> AtamaYap(YkcAtamaKaydetDto model)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var sonuc = await _ykcApiClient.AtamaYapAsync(kullanici, model);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "Cihaz değişim talebi ataması kaydedilemedi.";
            return RedirectToAction(nameof(Detay), new { id = model.TalepId });
        }

        [HttpPost("durum-guncelle")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> DurumGuncelle(YkcDurumGuncelleDto model)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var sonuc = await _ykcApiClient.DurumGuncelleAsync(kullanici, model);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "Cihaz değişim talebi durumu güncellenemedi.";
            return RedirectToAction(nameof(Detay), new { id = model.TalepId });
        }

        [HttpPost("kontroller-kaydet")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> KontrollerKaydet(YkcKontrolKaydetDto model)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var sonuc = await _ykcApiClient.KontrollerKaydetAsync(kullanici, model);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "FR265 kontrol adımları kaydedilemedi.";
            return RedirectToAction(nameof(Detay), new { id = model.TalepId });
        }

        [HttpPost("form-yukle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FormYukle(int talepId, IFormFile? formDosyasi, string? dosyaTuru)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (talepId <= 0)
            {
                TempData["Hata"] = "Form yükleme için talep bulunamadı.";
                return RedirectToAction(nameof(Talepler));
            }

            if (formDosyasi == null || formDosyasi.Length == 0)
            {
                TempData["Hata"] = "Yüklenecek form dosyası seçilmelidir.";
                return RedirectToAction(nameof(Detay), new { id = talepId });
            }

            var sonuc = await _ykcApiClient.FormYukleAsync(
                kullanici,
                talepId,
                formDosyasi,
                string.IsNullOrWhiteSpace(dosyaTuru) ? YkcFormDosyaTuruDegerleri.TeknikEk : dosyaTuru);

            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "Cihaz değişim form dosyası yüklenemedi.";
            return RedirectToAction(nameof(Detay), new { id = talepId });
        }

        private void PanelViewBag(AppKullanici kullanici, string activeMenu, string title, string subtitle)
        {
            ViewBag.Kullanici = kullanici;
            ViewData["ActiveMenu"] = activeMenu;
            ViewData["Title"] = title;
            ViewData["PanelTitle"] = title;
            ViewData["PanelSubtitle"] = subtitle;

            if (User.IsInRole("SertifikaliFirma"))
                ViewData["PanelArea"] = "SertifikaliFirma";
            else if (User.IsInRole("Personel"))
                ViewData["PanelArea"] = "Personel";
            else
                ViewData["PanelArea"] = "Admin";
        }
    }
}
