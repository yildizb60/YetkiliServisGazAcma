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
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly YkcApiClient _ykcApiClient;
        private readonly ILogger<YkcController> _logger;

        private static string? GecerliKaynak(string? kaynak) => kaynak?.ToLowerInvariant() switch
        {
            "rapor" => "rapor",
            "takvim" => "takvim",
            _ => null
        };

        private IActionResult KaynakListesineDon(string? kaynak) => GecerliKaynak(kaynak) switch
        {
            "rapor" => RedirectToAction(nameof(Raporlar)),
            "takvim" => RedirectToAction(nameof(Takvim)),
            _ => RedirectToAction(nameof(Talepler))
        };

        public YkcController(
            ApiKullaniciOturumu kullaniciOturumu,
            YkcApiClient ykcApiClient,
            ILogger<YkcController> logger)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _ykcApiClient = ykcApiClient;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!User.IsInRole(KullaniciRolAdlari.SertifikaliFirma))
                return Redirect(User.IsInRole(KullaniciRolAdlari.Personel) ? "/personel-panel" : "/AdminPanel");

            if (!YkcYetkileri().TalepleriGorebilir)
                return Redirect("/yetkisiz-erisim");

            PanelViewBag(kullanici, "YkcOzet", "Ana Sayfa", "Cihaz değişim, form, randevu ve dijital imza sürecinizi izleyin");

            var sonuc = await DashboardOzetGuvenliAsync(kullanici);
            ViewBag.ImzaEntegrasyonu = await ImzaEntegrasyonGuvenliAsync(kullanici);
            return View("~/Views/Ykc/Index.cshtml", sonuc);
        }

        [Authorize(Roles = KullaniciRolAdlari.SertifikaliFirma)]
        [HttpGet("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            PanelViewBag(kullanici, "Profil", "Profilim", string.Empty);
            return View("~/Views/Ykc/Profil.cshtml");
        }

        [Authorize(Roles = KullaniciRolAdlari.SertifikaliFirma)]
        [HttpPost("profil-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilGuncelle(string adSoyad, string email, string? telefon)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(adSoyad) || string.IsNullOrWhiteSpace(email)
                || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim()))
            {
                TempData["Hata"] = "Ad soyad ve geçerli bir e-posta adresi girin.";
                return RedirectToAction(nameof(Profil));
            }

            kullanici.AdSoyad = adSoyad.Trim();
            kullanici.Email = email.Trim();
            kullanici.PhoneNumber = telefon?.Trim();
            var sonuc = await _kullaniciOturumu.UpdateAsync(kullanici);
            TempData[sonuc.Succeeded ? "Basarili" : "Hata"] = sonuc.Succeeded
                ? "Profil bilgileriniz güncellendi."
                : sonuc.Errors.FirstOrDefault()?.Description ?? "Profil güncellenemedi.";
            return RedirectToAction(nameof(Profil));
        }

        [Authorize(Roles = KullaniciRolAdlari.SertifikaliFirma)]
        [HttpPost("sifre-degistir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SifreDegistir(string mevcutSifre, string yeniSifre, string yeniSifreTekrar)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(mevcutSifre) || string.IsNullOrWhiteSpace(yeniSifre))
                TempData["SifreHata"] = "Mevcut ve yeni şifreyi girin.";
            else if (yeniSifre != yeniSifreTekrar)
                TempData["SifreHata"] = "Yeni şifreler eşleşmiyor.";
            else
            {
                var sonuc = await _kullaniciOturumu.ChangePasswordAsync(kullanici, mevcutSifre, yeniSifre);
                TempData[sonuc.Succeeded ? "SifreBasarili" : "SifreHata"] = sonuc.Succeeded
                    ? "Şifreniz değiştirildi."
                    : sonuc.Errors.FirstOrDefault()?.Description ?? "Şifre değiştirilemedi.";
            }

            return RedirectToAction(nameof(Profil));
        }

        [HttpGet("talepler")]
        public async Task<IActionResult> Talepler(
            string? tesisatNo,
            string? musteriAdi,
            string? sozlesmeNo,
            string? aboneNo,
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
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().TalepleriGorebilir)
                return Redirect("/yetkisiz-erisim");

            PanelViewBag(kullanici, "YkcTalepler", "Cihaz Değişim Talepleri", "Cihaz değişim formu, randevu ve atama süreci");

            var filtre = new YkcTalepListeFiltre
            {
                TesisatNo = tesisatNo,
                MusteriAdi = musteriAdi,
                SozlesmeNo = sozlesmeNo,
                AboneNo = aboneNo,
                Firma = firma,
                Il = il,
                Ilce = ilce,
                Bolge = bolge,
                HedefUygulama = hedefUygulama,
                Durum = durum,
                BaslangicTarihi = bas,
                BitisTarihi = bit,
                Sayfa = Math.Max(sayfa, 1),
                SayfaBoyutu = 10
            };

            YkcTalepListeSonuc sonuc;
            try
            {
                sonuc = await _ykcApiClient.TaleplerAsync(kullanici, filtre) ?? new YkcTalepListeSonuc();
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "YKC talep listesi alınamadı.");
                TempData["Hata"] = "Talep listesi şu anda alınamadı. Veri bağlantısı yeniden kurulurken kısa bir süre sonra tekrar deneyin.";
                sonuc = new YkcTalepListeSonuc
                {
                    Sayfa = filtre.Sayfa,
                    SayfaBoyutu = filtre.SayfaBoyutu
                };
            }
            ViewBag.Filtre = filtre;
            ViewBag.Ozet = await DashboardOzetGuvenliAsync(kullanici);
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
            DateTime? bit,
            int sayfa = 1,
            int sayfaBoyutu = 10)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().RaporlariGorebilir)
                return Redirect("/yetkisiz-erisim");

            PanelViewBag(kullanici, "YkcRaporlar", "Cihaz Değişim Raporları", "Firma, ekip, tesisat ve tarih aralığına göre cihaz değişim süreci");

            var filtre = RaporFiltresi(
                tesisatNo, firma, il, ilce, bolge, ekip, marka, hedefUygulama,
                durum, bas, bit, sayfa, sayfaBoyutu);

            YkcRaporSonuc sonuc;
            try
            {
                sonuc = await _ykcApiClient.RaporAsync(kullanici, filtre) ?? new YkcRaporSonuc();
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "YKC raporu alınamadı.");
                TempData["Hata"] = "Rapor verileri şu anda alınamadı. Veri bağlantısı yeniden kurulurken kısa bir süre sonra tekrar deneyin.";
                sonuc = new YkcRaporSonuc();
            }
            ViewBag.Filtre = filtre;
            return View("~/Views/Ykc/Raporlar.cshtml", sonuc);
        }

        [HttpGet("raporlar/pdf")]
        public async Task<IActionResult> RaporPdf(
            string? tesisatNo, string? firma, string? il, string? ilce, string? bolge,
            string? ekip, string? marka, string? hedefUygulama, int? durum,
            DateTime? bas, DateTime? bit)
        {
            return await RaporDosyasi(
                RaporFiltresi(tesisatNo, firma, il, ilce, bolge, ekip, marka, hedefUygulama, durum, bas, bit),
                excelMi: false);
        }

        [HttpGet("raporlar/excel")]
        public async Task<IActionResult> RaporExcel(
            string? tesisatNo, string? firma, string? il, string? ilce, string? bolge,
            string? ekip, string? marka, string? hedefUygulama, int? durum,
            DateTime? bas, DateTime? bit)
        {
            return await RaporDosyasi(
                RaporFiltresi(tesisatNo, firma, il, ilce, bolge, ekip, marka, hedefUygulama, durum, bas, bit),
                excelMi: true);
        }

        private async Task<IActionResult> RaporDosyasi(YkcTalepListeFiltre filtre, bool excelMi)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().RaporlariGorebilir)
                return Redirect("/yetkisiz-erisim");

            try
            {
                var dosya = excelMi
                    ? await _ykcApiClient.RaporExcelAsync(kullanici, filtre)
                    : await _ykcApiClient.RaporPdfAsync(kullanici, filtre);

                if (dosya != null)
                    return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);

                TempData["Hata"] = "Rapor dosyası şu anda oluşturulamadı.";
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "YKC {RaporTuru} raporu oluşturulamadı.", excelMi ? "Excel" : "PDF");
                TempData["Hata"] = ex.Message;
            }

            return RedirectToAction(nameof(Raporlar));
        }

        [HttpGet("yeni")]
        public async Task<IActionResult> Yeni()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().TalepOlusturabilir)
                return Redirect("/yetkisiz-erisim");

            PanelViewBag(kullanici, "YkcYeni", "Talep Oluştur", string.Empty);
            return View("~/Views/Ykc/Yeni.cshtml", new YkcTalepKaydetDto());
        }

        [HttpPost("yeni")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Yeni(YkcTalepKaydetDto model, string? secilenCihazAdi, string? secilenCihazTipi)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().TalepOlusturabilir)
                return Redirect("/yetkisiz-erisim");

            PanelViewBag(kullanici, "YkcYeni", "Talep Oluştur", string.Empty);

            // Presentation only; persisted source values are resolved by the API snapshot.
            ViewData["SecilenCihazAdi"] = secilenCihazAdi?.Trim() is { Length: > 0 and <= 120 } etiket ? etiket : null;
            ViewData["SecilenCihazTipi"] = secilenCihazTipi?.Trim() is { Length: > 0 and <= 100 } tip ? tip : null;

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
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Unauthorized(new YkcTesisatSorguSonuc { Basarili = false, Mesaj = "Oturum bulunamadi." });

            if (!YkcYetkileri().TalepOlusturabilir)
                return StatusCode(StatusCodes.Status403Forbidden, YkcTesisatSorguSonuc.Basarisiz("Tesisat sorgulama yetkiniz bulunmuyor."));

            try
            {
                var sonuc = await _ykcApiClient.TesisatSorgulaAsync(kullanici, model);
                return Json(sonuc ?? YkcTesisatSorguSonuc.Basarisiz("Tesisat bilgisi alınamadı. Lütfen yeniden deneyin."));
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "YKC tesisat sorgusu alınamadı.");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    YkcTesisatSorguSonuc.Basarisiz("Tesisat servisine şu anda ulaşılamıyor. Lütfen kısa bir süre sonra yeniden deneyin."));
            }
        }

        [HttpGet("detay/{id:int}")]
        public async Task<IActionResult> Detay(int id, string? kaynak = null)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().TalepleriGorebilir)
                return Redirect("/yetkisiz-erisim");

            var aktifMenu = GecerliKaynak(kaynak) switch { "rapor" => "YkcRaporlar", "takvim" => "YkcTakvim", _ => "YkcTalepler" };
            PanelViewBag(kullanici, aktifMenu, "Cihaz Değişim Talebi Detayı", "Form, cihaz bilgileri ve atama süreci");

            YkcTalepDetayDto? detay;
            try
            {
                detay = await _ykcApiClient.DetayAsync(kullanici, id);
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "YKC talep detayı alınamadı. TalepId: {TalepId}", id);
                TempData["Hata"] = "Talep detayı şu anda alınamadı. Yerel API bağlantısı yeniden kuruluyor; lütfen kısa bir süre sonra tekrar deneyin.";
                return KaynakListesineDon(kaynak);
            }

            if (detay == null)
            {
                TempData["Hata"] = "Cihaz değişim talebi bulunamadı.";
                return KaynakListesineDon(kaynak);
            }

            ViewBag.ImzaEntegrasyonu = await ImzaEntegrasyonGuvenliAsync(kullanici);

            ViewBag.Ekipler = YkcYetkileri().AtamaYapabilir
                ? await EkipleriGuvenliGetirAsync(kullanici, id)
                : new List<YkcEkipSecenegi>();
            return View("~/Views/Ykc/Detay.cshtml", detay);
        }

        [HttpGet("fr265/onizle/{id:int}")]
        public async Task<IActionResult> Fr265Onizle(int id)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().TalepleriGorebilir)
                return Redirect("/yetkisiz-erisim");

            PanelViewBag(kullanici, "YkcTalepler", "Form Önizleme", "Cihaz değişim formu");

            YkcTalepDetayDto? detay;
            try
            {
                detay = await _ykcApiClient.DetayAsync(kullanici, id, formVerisi: true);
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "YKC form önizleme verisi alınamadı. TalepId: {TalepId}", id);
                TempData["Hata"] = "Form önizlemesi şu anda açılamadı. Veri bağlantısı yeniden kurulurken kısa bir süre sonra tekrar deneyin.";
                return RedirectToAction(nameof(Talepler));
            }
            if (detay == null)
            {
                TempData["Hata"] = "Cihaz değişim talebi bulunamadı.";
                return RedirectToAction(nameof(Talepler));
            }

            ViewBag.ImzaEntegrasyonu = await ImzaEntegrasyonGuvenliAsync(kullanici);

            return View("~/Views/Ykc/Fr265Onizle.cshtml", detay);
        }

        [HttpGet("fr265/pdf/{id:int}")]
        public async Task<IActionResult> FormPdf(int id, bool indir = false)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Unauthorized();
            if (!YkcYetkileri().TalepleriGorebilir) return Forbid();
            ApiDosyaSonuc? pdf;
            try { pdf = await _ykcApiClient.FormPdfAsync(kullanici, id); }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "Form PDF alınamadı. TalepId: {TalepId}", id);
                return StatusCode(503, "Form şu anda alınamadı. Lütfen yeniden deneyin.");
            }
            if (pdf == null || pdf.Bytes.Length == 0) return NotFound();
            if (pdf.ContentType != "application/pdf")
                return StatusCode(409, "Bu eski kayıt PDF biçiminde değil. Belge dönüştürülmeden önizlenemez.");
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            return indir ? File(pdf.Bytes, pdf.ContentType, pdf.DosyaAdi) : File(pdf.Bytes, pdf.ContentType);
        }

        [HttpGet("takvim")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Takvim([FromQuery] YkcTakvimFiltre filtre)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");
            if (!YkcYetkileri().TalepleriGorebilir) return Redirect("/yetkisiz-erisim");
            PanelViewBag(kullanici, "YkcTakvim", "Cihaz Değişim Randevuları", "");
            try
            {
                var sonuc = await _ykcApiClient.TakvimAsync(kullanici, filtre);
                if (sonuc == null) return StatusCode(503);
                return View("~/Views/Ykc/Takvim.cshtml", sonuc);
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "Randevu takvimi alınamadı.");
                TempData["Hata"] = "Takvim şu anda alınamadı. Lütfen yeniden deneyin.";
                return RedirectToAction(nameof(Talepler));
            }
        }

        [HttpPost("imzaya-gonder")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzayaGonder(int talepId, string? kaynak = null)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().Fr265ImzaIslemiYapabilir)
                return Redirect("/yetkisiz-erisim");

            var sonuc = await _ykcApiClient.ImzayaGonderAsync(kullanici, talepId);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj
                ?? "Form dijital imza uygulamasına gönderilemedi.";
            return RedirectToAction(nameof(Detay), new { id = talepId, kaynak = GecerliKaynak(kaynak) });
        }

        [HttpPost("imza-durum-sorgula")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzaDurumSorgula(int talepId, string? kaynak = null)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().Fr265ImzaIslemiYapabilir)
                return Redirect("/yetkisiz-erisim");

            var sonuc = await _ykcApiClient.ImzaDurumSorgulaAsync(kullanici, talepId);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj
                ?? "Formun dijital imza durumu alınamadı.";
            return RedirectToAction(nameof(Detay), new { id = talepId, kaynak = GecerliKaynak(kaynak) });
        }

        [HttpGet("dosya/{id:int}")]
        public async Task<IActionResult> DosyaAc(int id)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().TalepleriGorebilir)
                return Redirect("/yetkisiz-erisim");

            var dosya = await _ykcApiClient.DosyaIndirAsync(kullanici, id);
            if (dosya == null || dosya.Bytes.Length == 0)
            {
                TempData["Hata"] = "Cihaz değişim form dosyası açılamadı.";
                return RedirectToAction(nameof(Talepler));
            }

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("atama-yap")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> AtamaYap(YkcAtamaKaydetDto model, string? kaynak = null)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().AtamaYapabilir)
                return Redirect("/yetkisiz-erisim");

            var sonuc = await _ykcApiClient.AtamaYapAsync(kullanici, model);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "Cihaz değişim talebi ataması kaydedilemedi.";
            return RedirectToAction(nameof(Detay), new { id = model.TalepId, kaynak = GecerliKaynak(kaynak) });
        }

        [HttpPost("durum-guncelle")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> DurumGuncelle(YkcDurumGuncelleDto model, string? kaynak = null)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var ykcYetkileri = YkcYetkileri();
            var durumYetkili = model.Durum == YkcDurumDegerleri.Tamamlandi
                ? ykcYetkileri.Fr265ImzaIslemiYapabilir
                : model.Durum == YkcDurumDegerleri.SahaIsleminde
                    ? ykcYetkileri.AtamaYapabilir || ykcYetkileri.Fr265ImzaIslemiYapabilir
                    : ykcYetkileri.AtamaYapabilir;
            if (!durumYetkili)
                return Redirect("/yetkisiz-erisim");

            var sonuc = await _ykcApiClient.DurumGuncelleAsync(kullanici, model);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "Cihaz değişim talebi durumu güncellenemedi.";
            return RedirectToAction(nameof(Detay), new { id = model.TalepId, kaynak = GecerliKaynak(kaynak) });
        }

        [HttpPost("kontroller-kaydet")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> KontrollerKaydet(YkcKontrolKaydetDto model, string? kaynak = null)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().Fr265ImzaIslemiYapabilir)
                return Redirect("/yetkisiz-erisim");

            var sonuc = await _ykcApiClient.KontrollerKaydetAsync(kullanici, model);
            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "Kontrol sonucu kaydedilemedi.";
            return RedirectToAction(nameof(Detay), new { id = model.TalepId, kaynak = GecerliKaynak(kaynak) });
        }

        [HttpPost("form-yukle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FormYukle(int talepId, IFormFile? formDosyasi, string? dosyaTuru, string? kaynak = null)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (!YkcYetkileri().Fr265ImzaIslemiYapabilir)
                return Redirect("/yetkisiz-erisim");

            if (talepId <= 0)
            {
                TempData["Hata"] = "Form yükleme için talep bulunamadı.";
                return RedirectToAction(nameof(Talepler));
            }

            if (formDosyasi == null || formDosyasi.Length == 0)
            {
                TempData["Hata"] = "Yüklenecek form dosyası seçilmelidir.";
                return RedirectToAction(nameof(Detay), new { id = talepId, kaynak = GecerliKaynak(kaynak) });
            }

            var sonuc = await _ykcApiClient.FormYukleAsync(
                kullanici,
                talepId,
                formDosyasi,
                string.IsNullOrWhiteSpace(dosyaTuru) ? YkcFormDosyaTuruDegerleri.TeknikEk : dosyaTuru);

            TempData[sonuc?.Basarili == true ? "Basarili" : "Hata"] = sonuc?.Mesaj ?? "Cihaz değişim form dosyası yüklenemedi.";
            return RedirectToAction(nameof(Detay), new { id = talepId, kaynak = GecerliKaynak(kaynak) });
        }

        private static YkcTalepListeFiltre RaporFiltresi(
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
            DateTime? bit,
            int sayfa = 1,
            int sayfaBoyutu = 10)
        {
            return new YkcTalepListeFiltre
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
                Sayfa = Math.Max(sayfa, 1),
                SayfaBoyutu = Math.Clamp(sayfaBoyutu, 10, 100)
            };
        }

        private YkcYetkiOzeti YkcYetkileri()
        {
            return ViewBag.YkcYetkileri as YkcYetkiOzeti ?? new YkcYetkiOzeti();
        }

        private async Task<YkcDashboardOzetDto> DashboardOzetGuvenliAsync(AppKullanici kullanici)
        {
            try
            {
                return await _ykcApiClient.DashboardOzetAsync(kullanici) ?? new YkcDashboardOzetDto();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] ??= "Cihaz değişim özet bilgisi şu an alınamadı; sayfa boş özetle açıldı.";
                _logger.LogWarning(ex, "YKC dashboard özeti alınamadı.");
                return new YkcDashboardOzetDto();
            }
        }

        private async Task<YkcImzaEntegrasyonDto> ImzaEntegrasyonGuvenliAsync(AppKullanici kullanici)
        {
            try
            {
                return await _ykcApiClient.ImzaEntegrasyonBilgisiAsync(kullanici) ?? new YkcImzaEntegrasyonDto();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] ??= "Dijital imza entegrasyon bilgisi şu an alınamadı; ekran mevcut kayıtlarla açıldı.";
                _logger.LogWarning(ex, "YKC imza entegrasyon bilgisi alınamadı.");
                return new YkcImzaEntegrasyonDto();
            }
        }

        private async Task<List<YkcEkipSecenegi>> EkipleriGuvenliGetirAsync(AppKullanici kullanici, int talepId)
        {
            try
            {
                return await _ykcApiClient.EkiplerAsync(kullanici, talepId) ?? new List<YkcEkipSecenegi>();
            }
            catch (ApiIntegrationException ex)
            {
                _logger.LogWarning(ex, "YKC ekip listesi alınamadı. TalepId: {TalepId}", talepId);
                TempData["Hata"] ??= "Bölge ekipleri şu anda alınamadı. Randevu kaydetmeden önce kısa bir süre sonra yeniden deneyin.";
                return new List<YkcEkipSecenegi>();
            }
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
