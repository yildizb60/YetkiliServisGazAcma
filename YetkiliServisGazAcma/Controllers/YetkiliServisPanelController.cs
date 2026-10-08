using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ys-panel")]
    public class YetkiliServisPanelController : Controller
    {
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly YetkiliServisPanelApiClient _yetkiliServisPanelApiClient;

        public YetkiliServisPanelController(
            ApiKullaniciOturumu kullaniciOturumu,
            YetkiliServisPanelApiClient yetkiliServisPanelApiClient)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _yetkiliServisPanelApiClient = yetkiliServisPanelApiClient;
        }


        private async Task<AppKullanici?> GetYetkiliServisKullanici()
        {
            var kullanici = await _kullaniciOturumu.GetUserAsync(User);
            if (kullanici == null) return null;
            if (kullanici.KullaniciTipi != KullaniciTipiDegerleri.YetkiliServis) return null;
            return kullanici;
        }



        [HttpGet]
        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index(DateTime? takvimTarih, string? takvimGorunum)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var dashboard = await _yetkiliServisPanelApiClient.DashboardAsync(kullanici, takvimTarih, takvimGorunum)
                ?? new YsPanelDashboardDto();

            ViewBag.Firma = dashboard.Firma;
            ViewBag.BuAy = dashboard.BuAy;
            ViewBag.GecerliBelgeSayisi = dashboard.GecerliBelgeSayisi;
            ViewBag.BekleyenBelgeSayisi = dashboard.BekleyenBelgeSayisi;
            ViewBag.AktifMarkaSayisi = dashboard.AktifMarkaSayisi;
            ViewBag.AktifSubeSayisi = dashboard.AktifSubeSayisi;
            ViewBag.Toplam = dashboard.Toplam;
            ViewBag.BekleyenIslem = dashboard.Bekleyen;
            ViewBag.TamamlananIslem = dashboard.Tamamlanan;
            ViewBag.IptalIslem = dashboard.Iptal;
            ViewBag.SonIslemler = dashboard.SonIslemler;
            ViewBag.Kullanici = kullanici;
            ViewBag.IlkKurulumZorunlu = dashboard.IlkKurulumZorunlu;
            ViewBag.IlkKurulumTamamlandi = dashboard.IlkKurulumTamamlandi;
            ViewBag.IlkKurulumEksikler = dashboard.IlkKurulumEksikler;
            ViewBag.Bildirimler = dashboard.Bildirimler;
            ViewBag.BildirimSayisi = dashboard.BildirimSayisi;
            ViewBag.YetkiBelgesiUyariGun = dashboard.YetkiBelgesiUyariGun;
            ViewBag.TakvimTarih = dashboard.TakvimTarih;
            ViewBag.TakvimGorunum = dashboard.TakvimGorunum;
            ViewBag.TakvimIslemleri = dashboard.TakvimIslemleri;
            ViewBag.TakvimVerisiTam = dashboard.TakvimVerisiTam;

            return View("~/Views/YetkiliServisPanel/Index.cshtml");
        }

        [HttpGet]
        [Route("ilk-kurulum")]
        public async Task<IActionResult> IlkKurulum()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var kurulum = await _yetkiliServisPanelApiClient.IlkKurulumAsync(kullanici);
            if (kurulum == null)
            {
                TempData["Hata"] = "Ilk kurulum bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            if (!string.IsNullOrWhiteSpace(kurulum.HataMesaji))
            {
                TempData["Hata"] = kurulum.HataMesaji;
                return Redirect("/ys-panel");
            }

            if (!kurulum.ZorunluMu) return Redirect("/ys-panel");
            if (kurulum.TamamlandiMi)
            {
                TempData["Basarili"] = "Ilk kurulum zaten tamamlanmis.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = kurulum.Firma;
            ViewBag.TumMarkalar = kurulum.TumMarkalar;
            ViewBag.TumKategoriler = kurulum.TumKategoriler;
            ViewBag.SeciliMarkaIds = kurulum.SeciliMarkaIds;
            ViewBag.SeciliKategoriIds = kurulum.SeciliKategoriIds;
            ViewBag.AktifSubeSayisi = kurulum.AktifSubeSayisi;
            ViewBag.YetkiBelgesiVar = kurulum.YetkiBelgesiVar;
            ViewBag.OnayliYetkiBelgesiVar = kurulum.OnayliYetkiBelgesiVar;
            ViewBag.IlkKurulumEksikler = kurulum.Eksikler;
            return View("~/Views/YetkiliServisPanel/IlkKurulum.cshtml");
        }

        [HttpGet]
        [Route("subeler")]
        public async Task<IActionResult> Subeler()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var subeler = await _yetkiliServisPanelApiClient.SubelerAsync(kullanici);
            if (subeler == null)
            {
                TempData["Hata"] = "Sube bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Subeler = subeler;

            return View("~/Views/YetkiliServisPanel/Subeler.cshtml");
        }

        [HttpGet]
        [Route("subeler/duzenle/{id:int}")]
        public async Task<IActionResult> SubeDuzenle(int id)
        {
            if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
                return RedirectToAction(nameof(Subeler), new { duzenle = id });

            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            AdminSubeDto? sube;
            try
            {
                sube = await _yetkiliServisPanelApiClient.SubeGetirAsync(kullanici, id);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return Redirect("/ys-panel/subeler");
            }
            if (sube == null)
            {
                TempData["Hata"] = "Sube kaydi bulunamadi.";
                return Redirect("/ys-panel/subeler");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Sube = sube;

            return View("~/Views/YetkiliServisPanel/SubeDuzenle.cshtml");
        }

        [HttpGet]
        [Route("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkalarAsync(kullanici);
            if (sonuc == null)
            {
                TempData["Hata"] = "Marka bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = sonuc.Firma;
            ViewBag.TumMarkalar = sonuc.TumMarkalar;
            ViewBag.FirmaMarkalar = sonuc.FirmaMarkalar;
            ViewBag.SeciliMarkaIds = sonuc.SeciliMarkaIds;

            return View("~/Views/YetkiliServisPanel/Markalar.cshtml");
        }

        [HttpGet]
        [Route("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firma = await _yetkiliServisPanelApiClient.ProfilAsync(kullanici);
            if (firma == null)
            {
                TempData["Hata"] = "Profil bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = firma;

            return View("~/Views/YetkiliServisPanel/Profil.cshtml");
        }

        [HttpPost]
        [Route("subeler/duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDuzenleKaydet(
            int id,
            string? subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.SubeKaydetAsync(
                kullanici,
                id,
                subeAdi,
                il,
                ilce,
                telefon,
                adres,
                aktifMi);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube guncellendi.";
            else if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { basarili = false, mesaj = sonuc?.Mesaj ?? "Şube kaydedilemedi." });
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube API uzerinden guncellenemedi.";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { basarili = true });
            return Redirect("/ys-panel/subeler");
        }
        [HttpPost]
        [Route("subeler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeEkle(
            string? subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.SubeKaydetAsync(
                kullanici,
                0,
                subeAdi,
                il,
                ilce,
                telefon,
                adres,
                aktifMi);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube kaydi eklendi.";
            else if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { basarili = false, mesaj = sonuc?.Mesaj ?? "Şube kaydedilemedi." });
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube API uzerinden eklenemedi.";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { basarili = true });
            return Redirect("/ys-panel/subeler");
        }
        [HttpPost]
        [Route("subeler/durum")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDurum(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.SubeDurumAsync(kullanici, id);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube durumu guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube durumu API uzerinden guncellenemedi.";

            return Redirect("/ys-panel/subeler");
        }
        [HttpPost]
        [Route("subeler/sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeSil(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.SubeSilAsync(kullanici, id);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube kaydi silindi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube API uzerinden silinemedi.";

            return Redirect("/ys-panel/subeler");
        }
        [HttpPost]
        [Route("marka-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaGuncelle(List<int> markaIds)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaGuncelleAsync(kullanici, markaIds ?? new List<int>());

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka yetkileri guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka yetkileri API uzerinden guncellenemedi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaEkle(string? markaAdi, string? aciklama)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaEkleAsync(kullanici, markaAdi, aciklama);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka eklendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka API uzerinden eklenemedi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-duzenle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaDuzenle(int id, string? markaAdi, string? aciklama)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaDuzenleAsync(kullanici, id, markaAdi, aciklama);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka API uzerinden guncellenemedi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaSil(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaSilAsync(kullanici, id);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka yetkisi kaldirildi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka yetkisi API uzerinden kaldirilamadi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("profil-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilGuncelle(
            string? adSoyad, string? telefon, string? email)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.ProfilGuncelleAsync(kullanici, adSoyad, telefon, email);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Profil bilgileri guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Guncelleme sirasinda hata olustu.";

            return Redirect("/ys-panel/profil");
        }

        [HttpPost]
        [Route("sifre-degistir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SifreDegistir(
            string mevcutSifre, string yeniSifre, string yeniSifreTekrar)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            if (yeniSifre != yeniSifreTekrar)
            {
                TempData["SifreHata"] = "Yeni \u015fifreler e\u015fle\u015fmiyor.";
                return Redirect("/ys-panel/profil");
            }

            if (yeniSifre.Length < 6)
            {
                TempData["SifreHata"] = "\u015eifre en az 6 karakter olmal\u0131d\u0131r.";
                return Redirect("/ys-panel/profil");
            }

            var sonuc = await _kullaniciOturumu.ChangePasswordAsync(
                kullanici, mevcutSifre, yeniSifre);

            if (sonuc.Succeeded)
                TempData["SifreBasarili"] = "\u015eifreniz ba\u015far\u0131yla de\u011fi\u015ftirildi.";
            else
                TempData["SifreHata"] = "Mevcut \u015fifre hatal\u0131.";

            return Redirect("/ys-panel/profil");
        }

        [HttpGet]
        [Route("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var rapor = await _yetkiliServisPanelApiClient.RaporlarAsync(kullanici, bas, bit, limit: 10);
            if (rapor == null)
            {
                TempData["Hata"] = "Rapor bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.BasTarih = rapor.BasTarih;
            ViewBag.BitTarih = rapor.BitTarih;
            ViewBag.DevreyeSayisi = rapor.DevreyeSayisi;
            ViewBag.Bekleyen = rapor.Bekleyen;
            ViewBag.YetkiBelgesiOnayli = rapor.YetkiBelgesiOnayli;
            ViewBag.YetkiBelgesiBekleyen = rapor.YetkiBelgesiBekleyen;
            ViewBag.YetkiBelgesiReddedilen = rapor.YetkiBelgesiReddedilen;
            ViewBag.ChartAylikLabels = rapor.ChartAylikLabels;
            ViewBag.ChartAylikData = rapor.ChartAylikData;
            ViewBag.ChartDurumData = rapor.ChartDurumData;
            ViewBag.ChartMarkaLabels = rapor.ChartMarkaLabels;
            ViewBag.ChartMarkaData = rapor.ChartMarkaData;
            ViewBag.Firma = rapor.Firma;
            ViewBag.Kullanici = kullanici;
            return View("~/Views/YetkiliServisPanel/Raporlar.cshtml");
        }

        [HttpGet]
        [Route("raporlar/pdf")]
        public Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, List<int>? ids)
            => RaporDosyasi(bas, bit, ids, excelMi: false);

        [HttpGet]
        [Route("raporlar/pdf-toplu")]
        public async Task<IActionResult> RaporlarPdfToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarPdf(bas, bit, null);
        }

        [HttpGet]
        [Route("raporlar/excel")]
        public Task<IActionResult> RaporlarExcel(DateTime? bas, DateTime? bit, List<int>? ids)
            => RaporDosyasi(bas, bit, ids, excelMi: true);

        private async Task<IActionResult> RaporDosyasi(DateTime? bas, DateTime? bit, List<int>? ids, bool excelMi)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            try
            {
                var dosya = excelMi
                    ? await _yetkiliServisPanelApiClient.RaporlarExcelAsync(kullanici, bas, bit, ids)
                    : await _yetkiliServisPanelApiClient.RaporlarPdfAsync(kullanici, bas, bit, ids);
                if (dosya != null)
                    return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
                TempData["Hata"] = "Rapor dosyası şu anda oluşturulamadı.";
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }
            return RedirectToAction(nameof(Raporlar), new { bas, bit });
        }

        [HttpGet]
        [Route("raporlar/excel-toplu")]
        public async Task<IActionResult> RaporlarExcelToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarExcel(bas, bit, null);
        }
    }
}
