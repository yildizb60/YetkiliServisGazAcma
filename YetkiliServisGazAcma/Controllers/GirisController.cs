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
        private const string HariciKimlikDogrulamaReferansiKey = "HariciKimlikDogrulamaReferansi";

        private readonly SignInManager<AppKullanici> _signInManager;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AktifSirketService _aktifSirketService;
        private readonly SmsDogrulamaService _smsDogrulamaService;
        private readonly ISertifikaliFirmaKimlikProvider _sertifikaliFirmaKimlikProvider;
        private readonly SertifikaliFirmaKimlikOptions _kimlikOptions;

        public GirisController(
            SignInManager<AppKullanici> signInManager,
            UserManager<AppKullanici> userManager,
            AktifSirketService aktifSirketService,
            SmsDogrulamaService smsDogrulamaService,
            ISertifikaliFirmaKimlikProvider sertifikaliFirmaKimlikProvider,
            Microsoft.Extensions.Options.IOptions<SertifikaliFirmaKimlikOptions> kimlikOptions)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _aktifSirketService = aktifSirketService;
            _smsDogrulamaService = smsDogrulamaService;
            _sertifikaliFirmaKimlikProvider = sertifikaliFirmaKimlikProvider;
            _kimlikOptions = kimlikOptions.Value;
        }

        [HttpGet]
        [Route("giris")]
        public IActionResult Index(bool sifreUnuttum = false, bool temizle = false)
        {
            if (temizle)
            {
                HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
                HttpContext.Session.Remove(SifreSifirlaKullaniciIdKey);
                HttpContext.Session.Remove(HariciKimlikDogrulamaReferansiKey);
            }

            ViewBag.SmsBekleniyor = !string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SmsBekleyenKullaniciIdKey));
            ViewBag.SifreSifirlaKodBekleniyor = !string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SifreSifirlaKullaniciIdKey));
            ViewBag.SifreUnuttum = sifreUnuttum;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("giris")]
        public async Task<IActionResult> Index(string kullaniciAdi, string sifre)
        {
            if (string.IsNullOrEmpty(kullaniciAdi) || string.IsNullOrEmpty(sifre))
            {
                ViewBag.Hata = "Kullanıcı adı ve şifre zorunludur.";
                return View();
            }

            HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
            HttpContext.Session.Remove(HariciKimlikDogrulamaReferansiKey);
            kullaniciAdi = kullaniciAdi.Trim();
            var kullanici = await _userManager.FindByEmailAsync(kullaniciAdi)
                         ?? await _userManager.FindByNameAsync(kullaniciAdi);
            SertifikaliFirmaKimlikSonucu? hariciKimlikSonucu = null;
            var hariciKimlikKullanildi = _kimlikOptions.Enabled
                && (kullanici == null || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma);

            if (hariciKimlikKullanildi)
            {
                if (!_sertifikaliFirmaKimlikProvider.KullanilabilirMi)
                {
                    ViewBag.Hata = "Firma giriş servisi henüz bağlanmadı. Lütfen sistem yöneticisine başvurun.";
                    return View();
                }
                hariciKimlikSonucu = await _sertifikaliFirmaKimlikProvider.KimlikDogrulaAsync(
                    kullaniciAdi,
                    sifre,
                    HttpContext.RequestAborted);

                if (!hariciKimlikSonucu.Basarili)
                {
                    ViewBag.Hata = string.IsNullOrWhiteSpace(hariciKimlikSonucu.Mesaj)
                        ? "Sertifikalı firma kullanıcı bilgileri doğrulanamadı."
                        : hariciKimlikSonucu.Mesaj;
                    return View();
                }

                var yerelKullaniciAdi = string.IsNullOrWhiteSpace(hariciKimlikSonucu.YerelKullaniciAdi)
                    ? kullaniciAdi
                    : hariciKimlikSonucu.YerelKullaniciAdi.Trim();
                kullanici = await _userManager.FindByEmailAsync(yerelKullaniciAdi)
                         ?? await _userManager.FindByNameAsync(yerelKullaniciAdi);

                if (kullanici == null || kullanici.KullaniciTipi != KullaniciTipiDegerleri.SertifikaliFirma)
                {
                    ViewBag.Hata = "Kimlik servisi doğruladı ancak eşleşen yerel sertifikalı firma hesabı bulunamadı.";
                    return View();
                }
            }
            else if (kullanici == null)
            {
                ViewBag.Hata = "Kullanıcı bulunamadı.";
                return View();
            }

            if (!kullanici.AktifMi)
            {
                ViewBag.Hata = "Hesabınız aktif değil.";
                return View();
            }

            if (!hariciKimlikKullanildi)
            {
                var sonuc = await _signInManager.CheckPasswordSignInAsync(kullanici, sifre, true);
                if (!sonuc.Succeeded)
                {
                    if (sonuc.IsLockedOut)
                    {
                        ViewBag.Hata = "Çok fazla hatalı giriş denemesi yapıldı. Lütfen 15 dakika sonra tekrar deneyin.";
                        return View();
                    }

                    ViewBag.Hata = "Kullanıcı adı veya şifre hatalı.";
                    return View();
                }
            }

            await RolSenkronizeEt(kullanici);

            var smsGerekli = _smsDogrulamaService.SmsGirisAktifMi
                || hariciKimlikSonucu?.TelefonDogrulamasiGerekliMi == true;
            if (smsGerekli)
            {
                if (!_smsDogrulamaService.SmsGirisAktifMi)
                {
                    ViewBag.Hata = "Kimlik servisi telefon doğrulaması istiyor ancak SMS doğrulaması yapılandırılmamış.";
                    return View();
                }

                if (hariciKimlikKullanildi
                    && string.IsNullOrWhiteSpace(hariciKimlikSonucu?.DogrulamaReferansi))
                {
                    ViewBag.Hata = "Kimlik servisi SMS sonrası doğrulama için bir işlem referansı döndürmedi.";
                    return View();
                }

                var smsSonuc = await _smsDogrulamaService.KodGonderAsync(kullanici, "GIRIS", hariciKimlikSonucu?.Telefon);
                if (!smsSonuc.Basarili)
                {
                    ViewBag.Hata = smsSonuc.Mesaj;
                    return View();
                }

                HttpContext.Session.SetString(SmsBekleyenKullaniciIdKey, kullanici.Id);
                if (!string.IsNullOrWhiteSpace(hariciKimlikSonucu?.DogrulamaReferansi))
                {
                    HttpContext.Session.SetString(
                        HariciKimlikDogrulamaReferansiKey,
                        hariciKimlikSonucu.DogrulamaReferansi.Trim());
                }

                ViewBag.SmsBekleniyor = true;
                ViewBag.Bilgi = smsSonuc.Mesaj;
                return View();
            }

            if (!string.IsNullOrWhiteSpace(hariciKimlikSonucu?.DogrulamaReferansi))
            {
                var tamamlama = await _sertifikaliFirmaKimlikProvider.DogrulamayiTamamlaAsync(
                    hariciKimlikSonucu.DogrulamaReferansi,
                    HttpContext.RequestAborted);
                if (!tamamlama.Basarili)
                {
                    ViewBag.Hata = tamamlama.Mesaj;
                    return View();
                }
            }

            await _signInManager.SignInAsync(kullanici, false);
            return await GirisSonrasiYonlendir(kullanici);
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

            var hariciDogrulamaReferansi = HttpContext.Session.GetString(HariciKimlikDogrulamaReferansiKey);
            if (!string.IsNullOrWhiteSpace(hariciDogrulamaReferansi))
            {
                if (!_sertifikaliFirmaKimlikProvider.KullanilabilirMi)
                {
                    HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
                    HttpContext.Session.Remove(HariciKimlikDogrulamaReferansiKey);
                    ViewBag.Hata = "Sertifikalı firma kimlik servisine ulaşılamadı. Lütfen yeniden giriş yapın.";
                    return View("~/Views/Giris/Index.cshtml");
                }

                var tamamlama = await _sertifikaliFirmaKimlikProvider.DogrulamayiTamamlaAsync(
                    hariciDogrulamaReferansi,
                    HttpContext.RequestAborted);
                if (!tamamlama.Basarili)
                {
                    HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
                    HttpContext.Session.Remove(HariciKimlikDogrulamaReferansiKey);
                    ViewBag.Hata = tamamlama.Mesaj;
                    return View("~/Views/Giris/Index.cshtml");
                }
            }

            await RolSenkronizeEt(kullanici);
            await _signInManager.SignInAsync(kullanici, false);
            HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
            HttpContext.Session.Remove(HariciKimlikDogrulamaReferansiKey);
            return await GirisSonrasiYonlendir(kullanici);
        }

        [HttpGet]
        [Route("giris/sifre-unuttum")]
        public IActionResult SifreUnuttum()
        {
            HttpContext.Session.Remove(SmsBekleyenKullaniciIdKey);
            HttpContext.Session.Remove(SifreSifirlaKullaniciIdKey);
            HttpContext.Session.Remove(HariciKimlikDogrulamaReferansiKey);
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
                KullaniciTipiDegerleri.YetkiliServis => KullaniciRolAdlari.YetkiliServis,
                KullaniciTipiDegerleri.SertifikaliFirma => KullaniciRolAdlari.SertifikaliFirma,
                KullaniciTipiDegerleri.Personel => KullaniciRolAdlari.Personel,
                KullaniciTipiDegerleri.SirketAdmin => sirketAdmin ? KullaniciRolAdlari.SirketAdmin : KullaniciRolAdlari.GenelSistemAdmin,
                KullaniciTipiDegerleri.GenelSistemAdmin => KullaniciRolAdlari.GenelSistemAdmin,
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
