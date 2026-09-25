using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

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

            return Redirect($"/AdminPanel/devreyealmalar#devreye-alma-detay-{id}");
        }

        [HttpGet("devreyealmalar/pdf/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaPdf(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.DevreyeAlmaPdfAsync(kullanici, id, aktifSirketId);
            if (dosya == null) return NotFound();

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet("devreyealmalar/excel/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaExcel(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.DevreyeAlmaExcelAsync(kullanici, id, aktifSirketId);
            if (dosya == null) return NotFound();

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit, string? tip, int? sirketId)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var kapsamSirketId = await RaporKapsamSirketIdAsync(kullanici, sirketId);
            var sonuc = await _adminRaporApiClient.RaporlarOzetAsync(kullanici, kapsamSirketId, bas, bit, tip);
            if (sonuc == null)
            {
                TempData["Hata"] = "Rapor ozeti API uzerinden alinamadi.";
                sonuc = new AdminRaporOzetSonuc
                {
                    BasTarih = bas?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                    BitTarih = bit?.Date ?? DateTime.Now.Date,
                    RaporTipi = string.IsNullOrWhiteSpace(tip) ? "devreye" : tip.Trim().ToLowerInvariant(),
                    ListeTipi = (tip == "onayli" || tip == "bekleyen" || tip == "reddedilen") ? "yetkiBelgesi" : "devreye"
                };
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            var genelSistemAdminMi = await _aktifSirketService.GenelSistemAdminMi(kullanici);
            ViewBag.GenelSistemAdminMi = genelSistemAdminMi;
            ViewBag.AktifSirketAdi = genelSistemAdminMi && !kapsamSirketId.HasValue
                ? "Tüm dağıtım şirketleri"
                : sonuc.Sirketler.FirstOrDefault(x => x.Id == kapsamSirketId)?.SirketAdi
                    ?? kullanici.Sirket?.SirketAdi
                    ?? "Aktif dağıtım şirketi";
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
            ViewBag.SeciliSirketId = kapsamSirketId;
            ViewBag.Sirketler = sonuc.Sirketler;
            ViewBag.OperasyonTalepSayisi = sonuc.OperasyonTalepSayisi;
            ViewBag.OperasyonTamamlanan = sonuc.OperasyonTamamlanan;
            ViewBag.OperasyonAktif = sonuc.OperasyonAktif;
            ViewBag.OperasyonReddedilen = sonuc.OperasyonReddedilen;
            ViewBag.OperasyonIptal = sonuc.OperasyonIptal;
            ViewBag.OrtalamaTamamlanmaSaati = sonuc.OrtalamaTamamlanmaSaati;
            ViewBag.TamamlanmaSuresiKayitSayisi = sonuc.TamamlanmaSuresiKayitSayisi;
            ViewBag.IlkKontrolUygunlukOrani = sonuc.IlkKontrolUygunlukOrani;
            ViewBag.IlkKontrolKayitSayisi = sonuc.IlkKontrolKayitSayisi;
            ViewBag.TekrarRandevuOrani = sonuc.TekrarRandevuOrani;
            ViewBag.KontrolEdilenTalepSayisi = sonuc.KontrolEdilenTalepSayisi;
            ViewBag.OperasyonAylikLabels = sonuc.OperasyonAylikLabels;
            ViewBag.OperasyonAylikData = sonuc.OperasyonAylikData;
            ViewBag.OperasyonFirmaLabels = sonuc.OperasyonFirmaLabels;
            ViewBag.OperasyonFirmaData = sonuc.OperasyonFirmaData;
            ViewBag.OperasyonLokasyonLabels = sonuc.OperasyonLokasyonLabels;
            ViewBag.OperasyonLokasyonData = sonuc.OperasyonLokasyonData;
            ViewBag.OperasyonEkipLabels = sonuc.OperasyonEkipLabels;
            ViewBag.OperasyonEkipData = sonuc.OperasyonEkipData;
            ViewBag.OperasyonRedNedeniLabels = sonuc.OperasyonRedNedeniLabels;
            ViewBag.OperasyonRedNedeniData = sonuc.OperasyonRedNedeniData;
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
        public async Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, List<int>? ids, int? sirketId)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var kapsamSirketId = await RaporKapsamSirketIdAsync(kullanici, sirketId);
            var dosya = await _adminRaporApiClient.RaporlarPdfAsync(kullanici, kapsamSirketId, bas, bit, ids);
            if (dosya == null)
            {
                TempData["Hata"] = "Rapor PDF dosyasi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/raporlar");
            }

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet("raporlar/pdf-toplu")]
        public async Task<IActionResult> RaporlarPdfToplu(int? sirketId)
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarPdf(bas, bit, null, sirketId);
        }

        [HttpGet("raporlar/excel")]
        public async Task<IActionResult> RaporlarExcel(DateTime? bas, DateTime? bit, List<int>? ids, int? sirketId)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var kapsamSirketId = await RaporKapsamSirketIdAsync(kullanici, sirketId);
            var dosya = await _adminRaporApiClient.RaporlarExcelAsync(kullanici, kapsamSirketId, bas, bit, ids);
            if (dosya == null)
            {
                TempData["Hata"] = "Rapor Excel dosyasi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/raporlar");
            }

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet("raporlar/excel-toplu")]
        public async Task<IActionResult> RaporlarExcelToplu(int? sirketId)
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarExcel(bas, bit, null, sirketId);
        }

        [HttpGet("raporlar/operasyon/pdf")]
        public Task<IActionResult> OperasyonRaporPdf(DateTime? bas, DateTime? bit, int? sirketId)
            => OperasyonRaporDosyasi(bas, bit, sirketId, excelMi: false);

        [HttpGet("raporlar/operasyon/excel")]
        public Task<IActionResult> OperasyonRaporExcel(DateTime? bas, DateTime? bit, int? sirketId)
            => OperasyonRaporDosyasi(bas, bit, sirketId, excelMi: true);

        private async Task<IActionResult> OperasyonRaporDosyasi(DateTime? bas, DateTime? bit, int? sirketId, bool excelMi)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var kapsamSirketId = await RaporKapsamSirketIdAsync(kullanici, sirketId);
            var filtre = new YkcTalepListeFiltre
            {
                SirketId = kapsamSirketId,
                BaslangicTarihi = bas?.Date,
                BitisTarihi = bit?.Date,
                Sayfa = 1,
                SayfaBoyutu = 5000
            };

            try
            {
                var dosya = excelMi
                    ? await _ykcApiClient.RaporExcelAsync(kullanici, filtre)
                    : await _ykcApiClient.RaporPdfAsync(kullanici, filtre);
                if (dosya != null)
                    return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (ApiIntegrationException)
            {
                // The user-facing message below is intentionally stable across API failure modes.
            }

            TempData["Hata"] = "Operasyon raporu dosyasi su anda olusturulamadi.";
            return RedirectToAction(nameof(Raporlar), new { bas, bit, sirketId = kapsamSirketId });
        }

        private async Task<int?> RaporKapsamSirketIdAsync(AppKullanici kullanici, int? istenenSirketId)
        {
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici))
                return istenenSirketId;

            if (await _aktifSirketService.SirketAdminMi(kullanici))
                return kullanici.SirketId;

            return await _aktifSirketService.AktifSirketIdAsync(kullanici);
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
            ViewBag.OnayBekleyen = onayListesi.Bekleyenler.Count;
            ViewBag.Bekleyenler = onayListesi.Bekleyenler;
            ViewBag.SuresiDolanlar = onayListesi.SuresiDolanlar;
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
