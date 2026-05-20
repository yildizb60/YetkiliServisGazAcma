using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class KayitController : Controller
    {
        private readonly YetkiliServisService _ysService;
        private readonly YetkiliServisApiClient _yetkiliServisApiClient;
        private readonly MarkaApiClient _markaApiClient;
        private readonly UrunKategoriApiClient _urunKategoriApiClient;
        private readonly AppDbContext _context;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public KayitController(
            YetkiliServisService ysService,
            YetkiliServisApiClient yetkiliServisApiClient,
            MarkaApiClient markaApiClient,
            UrunKategoriApiClient urunKategoriApiClient,
            AppDbContext context,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _ysService = ysService;
            _yetkiliServisApiClient = yetkiliServisApiClient;
            _markaApiClient = markaApiClient;
            _urunKategoriApiClient = urunKategoriApiClient;
            _context = context;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        private async Task BasvuruListeleriniYukle()
        {
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();

            var markalar = await _markaApiClient.TumunuGetirAsync();
            ViewBag.Markalar = markalar?
                .Where(x => x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .ToList()
                ?? await _context.Ys_Markalar
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .OrderBy(x => x.MarkaAdi)
                    .ToListAsync();

            ViewBag.Kategoriler = await _urunKategoriApiClient.ListeAsync()
                ?? await _context.UrunKategoriler
                    .Where(x => !x.SilindiMi)
                    .OrderBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .ToListAsync();
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
                firma.SirketId = await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    firma.FaaliyetIli,
                    firma.Email ?? firma.VergiNo ?? "kayit");

                var servisSonuc = await _ysService.Kayit(
                    firma, sifre, markaIdleri ?? new List<int>(), kategoriIdleri ?? new List<int>());

                basarili = servisSonuc.basarili;
                mesaj = servisSonuc.mesaj;
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

