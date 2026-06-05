using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class GirisController : Controller
    {
        private const string SmsBekleyenKullaniciIdKey = "SmsBekleyenKullaniciId";
        private const string SifreSifirlaKullaniciIdKey = "SifreSifirlaKullaniciId";

        private readonly SignInManager<AppKullanici> _signInManager;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AktifSirketService _aktifSirketService;
        private readonly SmsDogrulamaService _smsDogrulamaService;

        public GirisController(
            SignInManager<AppKullanici> signInManager,
            UserManager<AppKullanici> userManager,
            AktifSirketService aktifSirketService,
            SmsDogrulamaService smsDogrulamaService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _aktifSirketService = aktifSirketService;
            _smsDogrulamaService = smsDogrulamaService;
        }

        [HttpGet]
        [Route("giris")]
        public IActionResult Index(bool sifreUnuttum = false, bool temizle = false)
        {
            if (temizle)
            {
                HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
                HttpContext.Session.Remove(SifreSifirlaKullaniciIdKey);
            }

            ViewBag.SmsBekleniyor = !string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SmsBekleyenKullaniciIdKey));
            ViewBag.SifreSifirlaKodBekleniyor = !string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SifreSifirlaKullaniciIdKey));
            ViewBag.SifreUnuttum = sifreUnuttum;
            return View();
        }

        [HttpPost]
        [Route("giris")]
        public async Task<IActionResult> Index(string kullaniciAdi, string sifre)
        {
            if (string.IsNullOrEmpty(kullaniciAdi) || string.IsNullOrEmpty(sifre))
            {
                ViewBag.Hata = "Kullanıcı adı ve şifre zorunludur.";
                return View();
            }

            var kullanici = await _userManager.FindByEmailAsync(kullaniciAdi)
                         ?? await _userManager.FindByNameAsync(kullaniciAdi);

            if (kullanici == null)
            {
                ViewBag.Hata = "Kullanıcı bulunamadı.";
                return View();
            }

            if (!kullanici.AktifMi)
            {
                ViewBag.Hata = "Hesabınız aktif değil.";
                return View();
            }

            var sonuc = await _signInManager.CheckPasswordSignInAsync(kullanici, sifre, true);

            if (sonuc.Succeeded)
            {
                await RolSenkronizeEt(kullanici);

                if (_smsDogrulamaService.SmsGirisAktifMi)
                {
                    var smsSonuc = await _smsDogrulamaService.KodGonderAsync(kullanici, "GIRIS");
                    if (!smsSonuc.Basarili)
                    {
                        ViewBag.Hata = smsSonuc.Mesaj;
                        return View();
                    }

                    HttpContext.Session.SetString(SmsBekleyenKullaniciIdKey, kullanici.Id);
                    ViewBag.SmsBekleniyor = true;
                    ViewBag.Bilgi = smsSonuc.Mesaj;
                    return View();
                }

                await _signInManager.SignInAsync(kullanici, false);
                return await GirisSonrasiYonlendir(kullanici);
            }

            if (sonuc.IsLockedOut)
            {
                ViewBag.Hata = "Cok fazla hatali giris denemesi yapildi. Lutfen 15 dakika sonra tekrar deneyin.";
                return View();
            }

            ViewBag.Hata = "Kullanıcı adı veya şifre hatalı.";
            return View();
        }

        [HttpGet]
        [Route("giris/sms-dogrula")]
        public IActionResult SmsDogrula()
        {
            return Redirect("/giris");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("giris/sms-dogrula")]
        public async Task<IActionResult> SmsDogrula(string kod)
        {
            var bekleyenKullaniciId = HttpContext.Session.GetString(SmsBekleyenKullaniciIdKey);
            if (string.IsNullOrWhiteSpace(bekleyenKullaniciId))
                return Redirect("/giris");

            var kullanici = await _userManager.FindByIdAsync(bekleyenKullaniciId);
            if (kullanici == null || !kullanici.AktifMi)
                return Redirect("/giris");

            var sonuc = await _smsDogrulamaService.KodDogrulaAsync(kullanici.Id, kod, "GIRIS");
            if (!sonuc.Basarili)
            {
                ViewBag.SmsBekleniyor = true;
                ViewBag.Hata = sonuc.Mesaj;
                return View("~/Views/Giris/Index.cshtml");
            }

            await RolSenkronizeEt(kullanici);
            await _signInManager.SignInAsync(kullanici, false);
            HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
            return await GirisSonrasiYonlendir(kullanici);
        }

        [HttpGet]
        [Route("giris/sifre-unuttum")]
        public IActionResult SifreUnuttum()
        {
            HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
            HttpContext.Session.Remove(SifreSifirlaKullaniciIdKey);
            ViewBag.SifreUnuttum = true;
            return View("~/Views/Giris/Index.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("giris/sifre-unuttum")]
        public async Task<IActionResult> SifreUnuttum(string kullaniciAdi)
        {
            if (string.IsNullOrWhiteSpace(kullaniciAdi))
            {
                ViewBag.SifreUnuttum = true;
                ViewBag.Hata = "E-posta veya VKN zorunludur.";
                return View("~/Views/Giris/Index.cshtml");
            }

            var kullanici = await _userManager.FindByEmailAsync(kullaniciAdi)
                         ?? await _userManager.FindByNameAsync(kullaniciAdi);

            if (kullanici == null || !kullanici.AktifMi)
            {
                ViewBag.SifreUnuttum = true;
                ViewBag.Hata = "Kullanıcı bulunamadı veya aktif değil.";
                return View("~/Views/Giris/Index.cshtml");
            }

            var smsSonuc = await _smsDogrulamaService.KodGonderAsync(kullanici, "SIFRE_SIFIRLA");
            if (!smsSonuc.Basarili)
            {
                ViewBag.SifreUnuttum = true;
                ViewBag.Hata = smsSonuc.Mesaj;
                return View("~/Views/Giris/Index.cshtml");
            }

            HttpContext.Session.SetString(SifreSifirlaKullaniciIdKey, kullanici.Id);
            ViewBag.SifreSifirlaKodBekleniyor = true;
            ViewBag.Bilgi = smsSonuc.Mesaj;
            return View("~/Views/Giris/Index.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("giris/sifre-yenile")]
        public async Task<IActionResult> SifreYenile(string kod, string yeniSifre, string yeniSifreTekrar)
        {
            var kullaniciId = HttpContext.Session.GetString(SifreSifirlaKullaniciIdKey);
            if (string.IsNullOrWhiteSpace(kullaniciId))
                return Redirect("/giris");

            if (yeniSifre != yeniSifreTekrar)
            {
                ViewBag.SifreSifirlaKodBekleniyor = true;
                ViewBag.Hata = "Yeni şifreler eşleşmiyor.";
                return View("~/Views/Giris/Index.cshtml");
            }

            var kullanici = await _userManager.FindByIdAsync(kullaniciId);
            if (kullanici == null || !kullanici.AktifMi)
                return Redirect("/giris");

            var smsSonuc = await _smsDogrulamaService.KodDogrulaAsync(kullanici.Id, kod, "SIFRE_SIFIRLA");
            if (!smsSonuc.Basarili)
            {
                ViewBag.SifreSifirlaKodBekleniyor = true;
                ViewBag.Hata = smsSonuc.Mesaj;
                return View("~/Views/Giris/Index.cshtml");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(kullanici);
            var resetSonuc = await _userManager.ResetPasswordAsync(kullanici, token, yeniSifre);
            if (!resetSonuc.Succeeded)
            {
                ViewBag.SifreSifirlaKodBekleniyor = true;
                ViewBag.Hata = string.Join(" ", resetSonuc.Errors.Select(x => x.Description));
                return View("~/Views/Giris/Index.cshtml");
            }

            HttpContext.Session.Remove(SifreSifirlaKullaniciIdKey);
            ViewBag.Bilgi = "Şifreniz değiştirildi. Yeni şifrenizle giriş yapabilirsiniz.";
            return View("~/Views/Giris/Index.cshtml");
        }

        [HttpPost]
        [Route("cikis")]
        [HttpGet]
        public async Task<IActionResult> Cikis()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return Redirect("/giris");
        }

        private async Task RolSenkronizeEt(AppKullanici kullanici)
        {
            var genelSistemAdmin = AktifSirketService.GenelSistemAdminTipi(kullanici);
            var sirketAdmin = AktifSirketService.SirketAdminTipi(kullanici);

            var hedefRol = kullanici.KullaniciTipi switch
            {
                1 => KullaniciRolAdlari.YetkiliServis,
                2 => KullaniciRolAdlari.Personel,
                3 => sirketAdmin ? KullaniciRolAdlari.SirketAdmin : KullaniciRolAdlari.GenelSistemAdmin,
                4 => KullaniciRolAdlari.GenelSistemAdmin,
                _ => null
            };

            if (!string.IsNullOrEmpty(hedefRol) && !await _userManager.IsInRoleAsync(kullanici, hedefRol))
                await _userManager.AddToRoleAsync(kullanici, hedefRol);

            if (genelSistemAdmin && !await _userManager.IsInRoleAsync(kullanici, KullaniciRolAdlari.EskiSuperAdmin))
                await _userManager.AddToRoleAsync(kullanici, KullaniciRolAdlari.EskiSuperAdmin);

            if (sirketAdmin && await _userManager.IsInRoleAsync(kullanici, KullaniciRolAdlari.EskiSuperAdmin))
                await _userManager.RemoveFromRoleAsync(kullanici, KullaniciRolAdlari.EskiSuperAdmin);
        }

        private async Task<IActionResult> GirisSonrasiYonlendir(AppKullanici kullanici)
        {
            if (kullanici.KullaniciTipi == 2 || kullanici.KullaniciTipi == 3)
            {
                var sirketler = await _aktifSirketService.KullaniciSirketleriAsync(kullanici);
                if (sirketler.Count > 1)
                    return Redirect("/panel/sirket-sec");

                await _aktifSirketService.AktifSirketIdAsync(kullanici);
            }

            return kullanici.KullaniciTipi switch
            {
                1 => Redirect("/ys-panel"),
                2 => Redirect("/personel-panel"),
                3 => Redirect("/AdminPanel"),
                4 => Redirect("/AdminPanel"),
                _ => Redirect("/giris")
            };
        }
    }
}
