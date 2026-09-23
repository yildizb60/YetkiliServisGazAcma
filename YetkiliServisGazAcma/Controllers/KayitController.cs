using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class KayitController : Controller
    {
        private readonly YetkiliServisApiClient _yetkiliServisApiClient;
        private readonly MarkaApiClient _markaApiClient;
        private readonly UrunKategoriApiClient _urunKategoriApiClient;
        private readonly SehirFirmaKodlari _sehirFirmaKoduService;

        public KayitController(
            YetkiliServisApiClient yetkiliServisApiClient,
            MarkaApiClient markaApiClient,
            UrunKategoriApiClient urunKategoriApiClient,
            SehirFirmaKodlari sehirFirmaKoduService)
        {
            _yetkiliServisApiClient = yetkiliServisApiClient;
            _markaApiClient = markaApiClient;
            _urunKategoriApiClient = urunKategoriApiClient;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        private async Task BasvuruListeleriniYukle()
        {
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.SehirFirmaKodlari = _sehirFirmaKoduService.TumKodlar();

            var markalar = await _markaApiClient.AktifleriGetirAsync();
            ViewBag.Markalar = markalar == null
                ? new List<Ys_Marka>()
                : markalar
                    .Where(x => x.AktifMi)
                    .OrderBy(x => x.MarkaAdi)
                    .ToList();

            ViewBag.Kategoriler = await _urunKategoriApiClient.ListeAsync()
                ?? new List<UrunKategori>();

            if (markalar == null || !((List<UrunKategori>)ViewBag.Kategoriler).Any())
            {
                ViewBag.ApiUyari = "Başvuru listeleri API üzerinden alınamadı. Lütfen API uygulamasının çalıştığını kontrol edin.";
            }
        }

        [HttpGet]
        [Route("kayit/yetkili-servis")]
        public async Task<IActionResult> YetkiliServis()
        {
            await BasvuruListeleriniYukle();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("kayit/yetkili-servis")]
        public async Task<IActionResult> YetkiliServis(
            Ys_Firma firma,
            string? ilce,
            string sifre,
            string sifreTekrar,
            List<int> markaIdleri,
            List<int> kategoriIdleri)
        {
            // Şifre kontrolü
            if (sifre != sifreTekrar)
            {
                ViewBag.Hata = "Şifreler eşleşmiyor.";
                ViewBag.SeciliIlce = ilce;
                ViewBag.SeciliMarkaIdleri = markaIdleri;
                ViewBag.SeciliKategoriIdleri = kategoriIdleri;
                await BasvuruListeleriniYukle();
                return View(firma);
            }

            var apiSonuc = await _yetkiliServisApiClient.KayitAsync(new YetkiliServisApiClient.YetkiliServisKayitIstek
            {
                FirmaAdi = firma.FirmaAdi,
                YetkiliKisi = firma.YetkiliKisi,
                Telefon = firma.Telefon,
                Email = firma.Email,
                Adres = firma.Adres,
                FaaliyetIli = firma.FaaliyetIli,
                Ilce = ilce?.Trim(),
                VergiNo = firma.VergiNo,
                TcKimlikNo = firma.TcKimlikNo,
                Sifre = sifre,
                MarkaIdleri = markaIdleri ?? new List<int>(),
                KategoriIdleri = kategoriIdleri ?? new List<int>()
            });

            var basarili = apiSonuc?.Basarili;
            var mesaj = apiSonuc?.Mesaj;

            if (apiSonuc == null)
            {
                ViewBag.Hata = "Kayıt işlemi API üzerinden gönderilemedi. Lütfen API uygulamasının çalıştığını kontrol edin.";
                ViewBag.SeciliIlce = ilce;
                ViewBag.SeciliMarkaIdleri = markaIdleri;
                ViewBag.SeciliKategoriIdleri = kategoriIdleri;
                await BasvuruListeleriniYukle();
                return View(firma);
            }

            if (basarili != true)
            {
                if (!string.IsNullOrEmpty(mesaj) && mesaj.ToLower().Contains("email") && mesaj.ToLower().Contains("already"))
                    mesaj = "E-posta adresi zaten kayitli.";

                ViewBag.Hata = mesaj ?? "Kayıt işlemi başarısız oldu.";
                ViewBag.SeciliIlce = ilce;
                ViewBag.SeciliMarkaIdleri = markaIdleri;
                ViewBag.SeciliKategoriIdleri = kategoriIdleri;
                await BasvuruListeleriniYukle();
                return View(firma);
            }

            TempData["KayitMesaj"] = "Kaydınız tamamlandı! Giriş yapabilirsiniz.";
            return Redirect("/giris");
        }
    }
}

