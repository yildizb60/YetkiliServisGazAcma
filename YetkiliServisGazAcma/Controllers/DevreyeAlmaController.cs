using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "YetkiliServis")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ys-devreyeal")]
    public class DevreyeAlmaController : Controller
    {
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly YetkiliServisDevreyeAlmaApiClient _devreyeAlmaApiClient;

        public DevreyeAlmaController(
            ApiKullaniciOturumu kullaniciOturumu,
            YetkiliServisDevreyeAlmaApiClient devreyeAlmaApiClient)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _devreyeAlmaApiClient = devreyeAlmaApiClient;
        }

        private async Task SetBildirimler(AppKullanici kullanici)
        {
            try
            {
                var sonuc = await _devreyeAlmaApiClient.BildirimlerAsync(kullanici)
                    ?? new YsDevreyeAlmaBildirimSonuc();

                ViewBag.Bildirimler = sonuc.Bildirimler;
                ViewBag.BildirimSayisi = sonuc.BildirimSayisi;
            }
            catch (ApiIntegrationException)
            {
                ViewBag.Bildirimler = new List<string>();
                ViewBag.BildirimSayisi = 0;
            }
        }

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var ekran = await _devreyeAlmaApiClient.EkranAsync(kullanici);
            if (ekran == null)
            {
                TempData["Hata"] = "Cihaz devreye alma ekrani API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            if (!ekran.Erisilebilir)
            {
                TempData["Hata"] = ekran.Hata ?? "Cihaz devreye alma ekrani acilamadi.";
                return Redirect(ekran.RedirectUrl ?? "/ys-panel");
            }

            ViewBag.Markalar = ekran.Markalar;
            ViewBag.Firma = ekran.Firma;
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);

            return View("~/Views/DevreyeAlma/Index.cshtml");
        }

        [HttpPost]
        [Route("tesisat-sorgula")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TesistatSorgula([FromBody] TesistatSorguDto dto)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Json(new { basarili = false, mesaj = "Oturum suresi dolmus." });

            try
            {
                var sonuc = await _devreyeAlmaApiClient.TesisatSorgulaAsync(kullanici, dto.TesistatNo, dto.SozlesmeNo);
                return Json(sonuc ?? new YsTesisatSorguSonuc
                {
                    Basarili = false,
                    Mesaj = "Tesisat sorgulama API uzerinden tamamlanamadi."
                });
            }
            catch (ApiIntegrationException ex)
            {
                return Json(new { basarili = false, mesaj = ex.Message });
            }
        }

        [HttpPost]
        [Route("marka-kontrol")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaKontrol([FromBody] MarkaKontrolDto dto)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null)
                return Json(new { yetkili = false, mesaj = "Oturum süresi dolmuş." });

            try
            {
                var sonuc = await _devreyeAlmaApiClient.MarkaKontrolAsync(kullanici, dto.CihazMarka);
                return Json(new
                {
                    yetkili = sonuc?.Yetkili == true,
                    mesaj = sonuc?.Mesaj,
                    markaId = sonuc?.MarkaId,
                    markaAdi = sonuc?.MarkaAdi
                });
            }
            catch (ApiIntegrationException ex)
            {
                return Json(new { yetkili = false, mesaj = ex.Message });
            }
        }

        [HttpGet]
        [Route("detay/{id}")]
        public IActionResult Detay(int id)
        {
            var gecmisUrl = Url.Action(nameof(Gecmis), "DevreyeAlma") ?? "/ys-devreyeal/gecmis";
            return Redirect(id > 0 ? $"{gecmisUrl}#devreye-alma-detay-{id}" : gecmisUrl);
        }

        [HttpGet]
        [Route("pdf/{id}")]
        public async Task<IActionResult> PdfIndir(int id)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var dosya = await _devreyeAlmaApiClient.PdfAsync(kullanici, id);
            if (dosya == null) return NotFound();

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet]
        [Route("excel/{id}")]
        public async Task<IActionResult> ExcelIndir(int id)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var dosya = await _devreyeAlmaApiClient.ExcelAsync(kullanici, id);
            if (dosya == null) return NotFound();

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost]
        [Route("kaydet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kaydet(Ys_DevreyeAlma model)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var sonuc = await _devreyeAlmaApiClient.KaydetAsync(kullanici, model);
                if (sonuc?.Basarili == true)
                {
                    TempData["Basarili"] = sonuc.Mesaj ?? "Cihaz devreye alma islemi tamamlandi!";
                    return Redirect(sonuc.RedirectUrl ?? "/ys-devreyeal/gecmis");
                }

                TempData["Hata"] = sonuc?.Mesaj ?? "Cihaz devreye alma islemi API uzerinden tamamlanamadi.";
                return Redirect(sonuc?.RedirectUrl ?? "/ys-devreyeal");
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return Redirect("/ys-devreyeal");
            }
        }

        [HttpPost]
        [Route("kaydet-json")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KaydetJson([FromBody] Ys_DevreyeAlma model)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Unauthorized();

            try
            {
                var sonuc = await _devreyeAlmaApiClient.KaydetAsync(kullanici, model);
                return Json(new
                {
                    basarili = sonuc?.Basarili == true,
                    mesaj = sonuc?.Mesaj ?? "Cihaz devreye alma işlemi tamamlanamadı.",
                    id = sonuc?.Id
                });
            }
            catch (ApiIntegrationException ex)
            {
                return Json(new { basarili = false, mesaj = ex.Message });
            }
        }

        [HttpGet]
        [Route("gecmis")]
        public async Task<IActionResult> Gecmis(string? marka, DateTime? bas, DateTime? bit, string? musteri, string? durum, string? tesisat)
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _devreyeAlmaApiClient.GecmisAsync(kullanici, marka, bas, bit, musteri, durum, tesisat)
                ?? new YsDevreyeAlmaGecmisSonuc();

            var islemler = sonuc.Islemler;

            ViewBag.Firma = sonuc.Firma;
            ViewBag.MarkaList = sonuc.MarkaList;
            ViewBag.Toplam = sonuc.Toplam;
            ViewBag.Tamamlanan = sonuc.Tamamlanan;
            ViewBag.Bekleyen = sonuc.Bekleyen;
            ViewBag.Iptal = sonuc.Iptal;
            ViewBag.SeciliMarka = marka;
            ViewBag.SeciliBas = bas?.ToString("yyyy-MM-dd");
            ViewBag.SeciliBit = bit?.ToString("yyyy-MM-dd");
            ViewBag.SeciliMusteri = musteri;
            ViewBag.SeciliTesisat = tesisat;
            ViewBag.SeciliDurum = durum;
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);
            return View("~/Views/DevreyeAlma/Gecmis.cshtml", islemler);
        }
    }

    public class TesistatSorguDto
    {
        public string? TesistatNo { get; set; }
        public string? SozlesmeNo { get; set; }
    }

    public class MarkaKontrolDto
    {
        public string? CihazMarka { get; set; }
    }
}


