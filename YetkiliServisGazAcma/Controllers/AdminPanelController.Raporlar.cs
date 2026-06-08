using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Controllers
{
    public partial class AdminPanelController
    {
        [HttpGet("devreyealmalar")]
        public async Task<IActionResult> DevreyeAlmalar(string? marka, string? servis, string? il, string? durum, DateTime? bas, DateTime? bit)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminRaporApiClient.DevreyeAlmalarAsync(kullanici, aktifSirketId, marka, servis, il, durum, bas, bit);
            if (sonuc == null)
            {
                TempData["Hata"] = "Devreye alma listesi API uzerinden alinamadi.";
                sonuc = new AdminDevreyeAlmaListeSonuc();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Markalar = sonuc.Markalar;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.SeciliIl = il ?? "";
            ViewBag.FirmaIlceleri = sonuc.FirmaIlceleri;
            return View("~/Views/AdminPanel/DevreyeAlmalar.cshtml", sonuc.Islemler);
        }

        [HttpGet("devreyealmalar/detay/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaDetay(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kayit = await _adminRaporApiClient.DevreyeAlmaDetayAsync(kullanici, id, aktifSirketId);
            if (kayit == null)
            {
                TempData["Hata"] = "Devreye alma detayi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/devreyealmalar");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            return View("~/Views/AdminPanel/DevreyeAlmaDetay.cshtml", kayit);
        }

        [HttpGet("devreyealmalar/pdf/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaPdf(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.DevreyeAlmaPdfAsync(kullanici, id, aktifSirketId);
            if (dosya == null) return NotFound();

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet("devreyealmalar/excel/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaExcel(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.DevreyeAlmaExcelAsync(kullanici, id, aktifSirketId);
            if (dosya == null) return NotFound();

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit, string? tip, int? sirketId)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _adminRaporApiClient.RaporlarOzetAsync(kullanici, sirketId, bas, bit, tip);
            if (sonuc == null)
            {
                TempData["Hata"] = "Rapor ozeti API uzerinden alinamadi.";
                sonuc = new AdminRaporOzetSonuc
                {
                    BasTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30),
                    BitTarih = bit?.Date ?? DateTime.Now.Date,
                    RaporTipi = string.IsNullOrWhiteSpace(tip) ? "devreye" : tip.Trim().ToLowerInvariant(),
                    ListeTipi = (tip == "onayli" || tip == "bekleyen" || tip == "reddedilen") ? "yetkiBelgesi" : "devreye"
                };
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.BasTarih = sonuc.BasTarih;
            ViewBag.BitTarih = sonuc.BitTarih;
            ViewBag.DevreyeSayisi = sonuc.DevreyeSayisi;
            ViewBag.YetkiBelgesiOnayli = sonuc.YetkiBelgesiOnayli;
            ViewBag.YetkiBelgesiBekleyen = sonuc.YetkiBelgesiBekleyen;
            ViewBag.YetkiBelgesiReddedilen = sonuc.YetkiBelgesiReddedilen;
            ViewBag.RaporTipi = sonuc.RaporTipi;
            ViewBag.ListeTipi = sonuc.ListeTipi;
            ViewBag.SonIslemler = sonuc.SonIslemler;
            ViewBag.YetkiBelgesiIslemler = sonuc.YetkiBelgesiIslemler;
            ViewBag.SeciliSirketId = sirketId;
            ViewBag.Sirketler = sonuc.Sirketler;
            ViewBag.ChartAylikLabels = sonuc.ChartAylikLabels;
            ViewBag.ChartAylikData = sonuc.ChartAylikData;
            ViewBag.ChartDurumData = sonuc.ChartDurumData;
            ViewBag.ChartSirketLabels = sonuc.ChartSirketLabels;
            ViewBag.ChartSirketData = sonuc.ChartSirketData;
            ViewBag.ChartMarkaLabels = sonuc.ChartMarkaLabels;
            ViewBag.ChartMarkaData = sonuc.ChartMarkaData;
            return View("~/Views/AdminPanel/Raporlar.cshtml");
        }

        [HttpGet("raporlar/pdf")]
        public async Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.RaporlarPdfAsync(kullanici, aktifSirketId, bas, bit, ids);
            if (dosya == null)
            {
                TempData["Hata"] = "Rapor PDF dosyasi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/raporlar");
            }

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.RaporlarExcelAsync(kullanici, aktifSirketId, bas, bit, ids);
            if (dosya == null)
            {
                TempData["Hata"] = "Rapor Excel dosyasi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/raporlar");
            }

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
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

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var onayListesi = await _adminYetkiBelgesiOnayApiClient.ListeleAsync(kullanici, aktifSirketId);
            ViewBag.AdminYetkiBelgesiOnayVeriKaynagi = "API";

            if (onayListesi == null)
            {
                TempData["Hata"] = "Yetki belgesi onay listesi API uzerinden alinamadi.";
                onayListesi = new AdminYetkiBelgesiOnaySonuc();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Bekleyenler = onayListesi.Bekleyenler;
            ViewBag.Onaylananlar = onayListesi.Onaylananlar;
            ViewBag.Reddedilenler = onayListesi.Reddedilenler;
            return View("~/Views/AdminPanel/OnayBekleyenler.cshtml");
        }

        [HttpGet("yetki-belgesi-uyarilari")]
        public async Task<IActionResult> YetkiBelgesiUyarilari()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminRaporApiClient.YetkiBelgesiUyarilariAsync(kullanici, aktifSirketId);
            if (sonuc == null)
            {
                TempData["Hata"] = "Yetki belgesi uyarilari API uzerinden alinamadi.";
                sonuc = new AdminYetkiBelgesiUyariSonuc();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Yaklasan = sonuc.Yaklasan;
            ViewBag.Gecmis = sonuc.Gecmis;
            return View("~/Views/AdminPanel/YetkiBelgesiUyarilari.cshtml");
        }
    }
}
