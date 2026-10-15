using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class KayitController : Controller
    {
        private readonly YetkiliServisApiClient _yetkiliServisApiClient;

        public KayitController(YetkiliServisApiClient yetkiliServisApiClient)
        {
            _yetkiliServisApiClient = yetkiliServisApiClient;
        }

        private async Task BasvuruListeleriniYukle()
        {
            YetkiliServisBasvuruSecenekleriDto? secenekler = null;
            try
            {
                secenekler = await _yetkiliServisApiClient.BasvuruSecenekleriAsync();
            }
            catch (ApiIntegrationException ex)
            {
                ViewBag.ApiUyari = ex.Message;
            }

            if (secenekler == null || secenekler.Kategoriler.Count == 0)
                ViewBag.ApiUyari ??= "Başvuru listeleri API üzerinden alınamadı. Lütfen API uygulamasının çalıştığını kontrol edin.";

            secenekler ??= new();
            ViewBag.Sehirler = secenekler.Sehirler;
            ViewBag.SehirFirmaKodlari = secenekler.SehirFirmaKodlari;
            ViewBag.Markalar = secenekler.Markalar;
            ViewBag.Kategoriler = secenekler.Kategoriler;
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
        public async Task<IActionResult> YetkiliServis(YetkiliServisBasvuruDto firma, string sifreTekrar)
        {
            ViewBag.SeciliIlce = firma.Ilce;
            ViewBag.SeciliMarkaIdleri = firma.MarkaIdleri;
            ViewBag.SeciliKategoriIdleri = firma.KategoriIdleri;

            if (firma.Sifre != sifreTekrar)
            {
                ViewBag.Hata = "Şifreler eşleşmiyor.";
                await BasvuruListeleriniYukle();
                return View(firma);
            }

            YetkiliServisKayitSonuc? apiSonuc = null;
            try
            {
                firma.Ilce = firma.Ilce?.Trim();
                apiSonuc = await _yetkiliServisApiClient.KayitAsync(firma);
            }
            catch (ApiIntegrationException ex)
            {
                ViewBag.Hata = ex.Message;
            }

            if (apiSonuc?.Basarili != true)
            {
                var mesaj = apiSonuc?.Mesaj;
                if (!string.IsNullOrEmpty(mesaj) && mesaj.ToLower().Contains("email") && mesaj.ToLower().Contains("already"))
                    mesaj = "E-posta adresi zaten kayitli.";

                ViewBag.Hata ??= apiSonuc == null
                    ? "Kayıt işlemi API üzerinden gönderilemedi. Lütfen API uygulamasının çalıştığını kontrol edin."
                    : mesaj ?? "Kayıt işlemi başarısız oldu.";
                await BasvuruListeleriniYukle();
                return View(firma);
            }

            TempData["KayitMesaj"] = "Kaydınız tamamlandı! Giriş yapabilirsiniz.";
            return Redirect("/giris");
        }
    }
}

