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
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public KayitController(
            YetkiliServisApiClient yetkiliServisApiClient,
            MarkaApiClient markaApiClient,
            UrunKategoriApiClient urunKategoriApiClient,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _yetkiliServisApiClient = yetkiliServisApiClient;
            _markaApiClient = markaApiClient;
            _urunKategoriApiClient = urunKategoriApiClient;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        private async Task BasvuruListeleriniYukle()
        {
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();

            var markalar = await _markaApiClient.TumunuGetirAsync();
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
        [Route("kayit/yetkili-servis")]
        public async Task<IActionResult> YetkiliServis(
            Ys_Firma firma,
            string sifre,
            string sifreTekrar,
            List<int> markaIdleri,
            List<int> kategoriIdleri)
        {
            // Şifre kontrolü
            if (sifre != sifreTekrar)
            {
                ViewBag.Hata = "Şifreler eşleşmiyor.";
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
                VergiNo = firma.VergiNo,
                VergiDairesi = firma.VergiDairesi,
                Sifre = sifre,
                MarkaIdleri = markaIdleri ?? new List<int>(),
                KategoriIdleri = kategoriIdleri ?? new List<int>()
            });

            var basarili = apiSonuc?.Basarili;
            var mesaj = apiSonuc?.Mesaj;

            if (apiSonuc == null)
            {
                ViewBag.Hata = "Kayıt işlemi API üzerinden gönderilemedi. Lütfen API uygulamasının çalıştığını kontrol edin.";
                await BasvuruListeleriniYukle();
                return View(firma);
            }

            if (basarili != true)
            {
                if (!string.IsNullOrEmpty(mesaj) && mesaj.ToLower().Contains("email") && mesaj.ToLower().Contains("already"))
                    mesaj = "E-posta adresi zaten kayitli.";

                ViewBag.Hata = mesaj ?? "Kayıt işlemi başarısız oldu.";
                await BasvuruListeleriniYukle();
                return View(firma);
            }

            TempData["KayitMesaj"] = "Kaydınız tamamlandı! Giriş yapabilirsiniz.";
            return Redirect("/giris");
        }
    }
}

