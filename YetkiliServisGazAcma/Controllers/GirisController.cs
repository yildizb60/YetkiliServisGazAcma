using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class GirisController(AuthApiClient api, ApiKullaniciOturumu oturum, AktifSirketService aktifSirketService) : Controller
    {
        private const string SmsKey = "API.LoginChallenge";
        private const string ResetKey = "API.ResetChallenge";
        private readonly AktifSirketService _aktifSirketService = aktifSirketService;

        [HttpGet, Route("giris")]
        public IActionResult Index(bool sifreUnuttum = false, bool temizle = false)
        {
            if (temizle) ClearChallenges();
            ViewBag.SmsBekleniyor = HttpContext.Session.GetString(SmsKey) != null;
            ViewBag.SifreSifirlaKodBekleniyor = HttpContext.Session.GetString(ResetKey) != null;
            ViewBag.SifreUnuttum = sifreUnuttum;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, Route("giris")]
        public async Task<IActionResult> Index(string kullaniciAdi, string sifre)
        {
            ClearChallenges();
            if (string.IsNullOrWhiteSpace(kullaniciAdi) || string.IsNullOrEmpty(sifre))
                return LoginView("Kullanıcı adı ve şifre zorunludur.");
            var result = await api.GirisAsync(kullaniciAdi, sifre);
            if (!result.Basarili) return LoginView(result.Mesaj);
            if (!string.IsNullOrWhiteSpace(result.Dogrulama))
            {
                HttpContext.Session.SetString(SmsKey, result.Dogrulama);
                ViewBag.SmsBekleniyor = true;
                ViewBag.Bilgi = result.Mesaj;
                return LoginView();
            }
            var user = await oturum.BaslatAsync(result);
            return await GirisSonrasiYonlendir(user);
        }

        [HttpGet, Route("giris/sms-dogrula")]
        public IActionResult SmsDogrula() => Redirect("/giris");

        [HttpPost, ValidateAntiForgeryToken, Route("giris/sms-dogrula")]
        public async Task<IActionResult> SmsDogrula(string kod)
        {
            var challenge = HttpContext.Session.GetString(SmsKey);
            if (challenge == null) return Redirect("/giris");
            var result = await api.SmsAsync(challenge, kod ?? "");
            if (!result.Basarili)
            {
                ViewBag.SmsBekleniyor = true;
                return LoginView(result.Mesaj);
            }
            ClearChallenges();
            return await GirisSonrasiYonlendir(await oturum.BaslatAsync(result));
        }

        [HttpGet, Route("giris/sifre-unuttum")]
        public IActionResult SifreUnuttum()
        {
            ClearChallenges();
            ViewBag.SifreUnuttum = true;
            return LoginView();
        }

        [HttpPost, ValidateAntiForgeryToken, Route("giris/sifre-unuttum")]
        public async Task<IActionResult> SifreUnuttum(string kullaniciAdi)
        {
            ClearChallenges();
            ViewBag.SifreUnuttum = true;
            if (string.IsNullOrWhiteSpace(kullaniciAdi)) return LoginView("E-posta veya VKN zorunludur.");
            var result = await api.SifreUnuttumAsync(kullaniciAdi);
            if (!result.Basarili || string.IsNullOrWhiteSpace(result.Dogrulama)) return LoginView(result.Mesaj);
            HttpContext.Session.SetString(ResetKey, result.Dogrulama);
            ViewBag.SifreUnuttum = false;
            ViewBag.SifreSifirlaKodBekleniyor = true;
            ViewBag.Bilgi = result.Mesaj;
            return LoginView();
        }

        [HttpPost, ValidateAntiForgeryToken, Route("giris/sifre-yenile")]
        public async Task<IActionResult> SifreYenile(string kod, string yeniSifre, string yeniSifreTekrar)
        {
            var challenge = HttpContext.Session.GetString(ResetKey);
            if (challenge == null) return Redirect("/giris");
            ViewBag.SifreSifirlaKodBekleniyor = true;
            if (yeniSifre != yeniSifreTekrar) return LoginView("Yeni şifreler eşleşmiyor.");
            var result = await api.SifreYenileAsync(challenge, kod ?? "", yeniSifre ?? "");
            if (!result.Basarili) return LoginView(result.Mesaj);
            ClearChallenges();
            ViewBag.SifreSifirlaKodBekleniyor = false;
            ViewBag.Bilgi = result.Mesaj;
            return LoginView();
        }

        [HttpGet, HttpPost, Route("cikis")]
        public async Task<IActionResult> Cikis()
        {
            await oturum.BitirAsync();
            return Redirect("/giris");
        }

        private void ClearChallenges()
        {
            HttpContext.Session.Remove(SmsKey);
            HttpContext.Session.Remove(ResetKey);
        }

        private ViewResult LoginView(string? error = null)
        {
            ViewBag.Hata = error;
            return View("~/Views/Giris/Index.cshtml");
        }

        private async Task<IActionResult> GirisSonrasiYonlendir(AppKullanici kullanici)
        {
            if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.Personel || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin)
            {
                var sirketler = await _aktifSirketService.KullaniciSirketleriAsync(kullanici);
                if (sirketler.Count > 1)
                    return Redirect("/panel/sirket-sec");

                await _aktifSirketService.AktifSirketIdAsync(kullanici);
            }

            return kullanici.KullaniciTipi switch
            {
                KullaniciTipiDegerleri.YetkiliServis => Redirect("/ys-panel"),
                KullaniciTipiDegerleri.SertifikaliFirma => Redirect("/ykc"),
                KullaniciTipiDegerleri.Personel => Redirect("/personel-panel"),
                KullaniciTipiDegerleri.SirketAdmin => Redirect("/AdminPanel"),
                KullaniciTipiDegerleri.GenelSistemAdmin => Redirect("/AdminPanel"),
                _ => Redirect("/giris")
            };
        }
    }
}
