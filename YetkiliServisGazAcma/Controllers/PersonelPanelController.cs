using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("personel-panel")]
    public class PersonelPanelController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AppDbContext _context;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
        private readonly AktifSirketService _aktifSirketService;

        public PersonelPanelController(
            UserManager<AppKullanici> userManager,
            AppDbContext context,
            SehirFirmaKoduService sehirFirmaKoduService,
            AktifSirketService aktifSirketService)
        {
            _userManager = userManager;
            _context = context;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _aktifSirketService = aktifSirketService;
        }

        private static bool KullanilanKategoriMi(string? ad)
        {
            var key = NormalizeKategori(ad);

            return key == "kombi"
                || key.Contains("merkezikazan")
                || key.Contains("sofben")
                || key.Contains("sohben");
        }

        private static string NormalizeKategori(string? ad)
        {
            if (string.IsNullOrWhiteSpace(ad))
                return string.Empty;

            var normalized = ad.Trim().ToLower(new CultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
            var chars = normalized
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
                .ToArray();

            return new string(chars)
                .Replace("ı", "i")
                .Replace("ş", "s")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c");
        }

        private async Task<List<UrunKategori>> KullanilanKategorileriGetir()
        {
            return (await _context.UrunKategoriler
                    .Where(x => !x.SilindiMi)
                    .OrderBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .ToListAsync())
                .Where(x => KullanilanKategoriMi(x.Ad))
                .GroupBy(x => NormalizeKategori(x.Ad))
                .Select(g => g
                    .OrderByDescending(x => x.AktifMi)
                    .ThenBy(x => string.IsNullOrWhiteSpace(x.IconUrl) ? 1 : 0)
                    .ThenBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .First())
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .ToList();
        }

        private async Task<bool> KullaniciYetkiliMi(AppKullanici kullanici, string yetki)
        {
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici) || await _aktifSirketService.SirketAdminMi(kullanici))
                return true;

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                (aktifSirketId == null || x.SirketId == aktifSirketId) &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == yetki));
        }

        private async Task SetPersonelYetkiViewBags(AppKullanici kullanici)
        {
            var ySertifika = await KullaniciYetkiliMi(kullanici, YetkiTipleri.CERTIFIKA_ONAY);
            var yRapor = await KullaniciYetkiliMi(kullanici, YetkiTipleri.RAPOR_GOR);
            var yServis = await KullaniciYetkiliMi(kullanici, YetkiTipleri.KULLANICI_YONET);
            var ySirketYonet = await KullaniciYetkiliMi(kullanici, YetkiTipleri.DAGITIM_SIRKET_YONET);
            var yMarkaYonet = await KullaniciYetkiliMi(kullanici, YetkiTipleri.MARKA_YONET);

            ViewBag.YetkiSertifika = ySertifika;
            ViewBag.YetkiRapor = yRapor;
            ViewBag.YetkiServis = yServis;
            ViewBag.YetkiSirket = ySirketYonet;
            ViewBag.YetkiMarka = yMarkaYonet;
            ViewBag.YetkiSirketYonet = ySirketYonet;
            ViewBag.YetkiMarkaYonet = yMarkaYonet;

            List<string> yetkilerim;
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici) || await _aktifSirketService.SirketAdminMi(kullanici))
            {
                yetkilerim = new List<string>
                {
                    "Tam Yetki",
                    "Yetki Belgesi Onay",
                    "Rapor Gör",
                    "Kullanıcı Yönet",
                    "Marka Yönet"
                };
            }
            else
            {
                var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
                var mevcutYetkiler = await _context.Dag_PersonelYetkiler
                    .Where(x => x.KullaniciId == kullanici.Id
                        && !x.SilindiMi
                        && (aktifSirketId == null || x.SirketId == aktifSirketId))
                    .Select(x => x.YetkiTipi)
                    .Distinct()
                    .ToListAsync();

                if (mevcutYetkiler.Contains(YetkiTipleri.TAM_YETKI))
                    mevcutYetkiler = new List<string> { YetkiTipleri.TAM_YETKI };

                var yetkiAdlari = new Dictionary<string, string>
                {
                    [YetkiTipleri.CERTIFIKA_ONAY] = "Yetki Belgesi Onay",
                    [YetkiTipleri.RAPOR_GOR] = "Rapor Gör",
                    [YetkiTipleri.KULLANICI_YONET] = "Kullanıcı Yönet",
                    [YetkiTipleri.MARKA_YONET] = "Marka Yönet",
                    [YetkiTipleri.TAM_YETKI] = "Tam Yetki"
                };

                yetkilerim = mevcutYetkiler
                    .Select(x => yetkiAdlari.ContainsKey(x) ? yetkiAdlari[x] : x)
                    .Distinct()
                    .ToList();
            }

            ViewBag.Yetkilerim = yetkilerim;
        }

        private async Task SetPersonelNotifViewBags(AppKullanici kullanici)
        {
            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);

            ViewBag.OnayBekleyen = await _context.Ys_Sertifikalar.Include(x => x.Firma)
                .Where(x => !x.SilindiMi && x.Durum == 0
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .CountAsync();

            ViewBag.SuresiBitecek = await _context.Ys_Sertifikalar.Include(x => x.Firma)
                .Where(x => !x.SilindiMi && x.Durum == 1 && x.SertifikaBitisTarihi <= DateTime.Now.AddDays(30) && x.SertifikaBitisTarihi >= DateTime.Now
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .CountAsync();
        }

        private async Task<IActionResult?> YetkiKontrol(string yetki)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var yetkili = await KullaniciYetkiliMi(kullanici, yetki);
            if (!yetkili)
                return RedirectToAction(nameof(Index));

            return null;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            // Panel ana sayfasi personel icin goruntulenebilir olsun
            // Yetki yoksa onay islemleri gibi aksiyonlar zaten ilgili sayfalarda kontrol edilir.

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);

            ViewBag.OnayBekleyen = await _context.Ys_Sertifikalar.Include(x => x.Firma)
                .Where(x => !x.SilindiMi
                    && x.Durum == 0
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .CountAsync();

            ViewBag.ToplamFirma = await _context.Ys_Firmalar
                .Where(x => !x.SilindiMi && (sirketId == null || x.SirketId == sirketId))
                .CountAsync();

            ViewBag.ToplamDevreyeAlma = await _context.Ys_DevreyeAlmalar.Include(x => x.Firma)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .CountAsync();

            ViewBag.ToplamMarka = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .CountAsync();

            ViewBag.ToplamSirket = await _context.Set<Dag_Sirket>()
                .Where(x => !x.SilindiMi)
                .CountAsync();

            ViewBag.BuAy = await _context.Ys_DevreyeAlmalar.Include(x => x.Firma)
                .Where(x => !x.SilindiMi && x.OlusturmaTarihi.Month == DateTime.Now.Month && x.OlusturmaTarihi.Year == DateTime.Now.Year
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .CountAsync();

            ViewBag.SuresiBitecek = await _context.Ys_Sertifikalar.Include(x => x.Firma)
                .Where(x => !x.SilindiMi && x.Durum == 1 && x.SertifikaBitisTarihi <= DateTime.Now.AddDays(30) && x.SertifikaBitisTarihi >= DateTime.Now
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .CountAsync();

            ViewBag.SonBekleyenler = await _context.Ys_Sertifikalar.Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum == 0
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .OrderByDescending(x => x.OlusturmaTarihi).Take(5).ToListAsync();

            ViewBag.SonIslemler = await _context.Ys_DevreyeAlmalar.Include(x => x.Firma).Include(x => x.Marka)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .OrderByDescending(x => x.OlusturmaTarihi).Take(8).ToListAsync();

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Index.cshtml");
        }

        [HttpGet("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sirket = await _context.Set<Dag_Sirket>().FirstOrDefaultAsync(x => x.Id == sirketId);

            ViewBag.Kullanici = kullanici;
            ViewBag.Sirket = sirket;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Profil.cshtml");
        }

        [HttpPost("profil-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilGuncelle(string adSoyad, string email, string telefon)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            kullanici.AdSoyad = adSoyad;
            kullanici.Email = email;
            kullanici.UserName = email;
            kullanici.PhoneNumber = telefon;

            var sonuc = await _userManager.UpdateAsync(kullanici);
            if (sonuc.Succeeded) TempData["Basarili"] = "Profil bilgileriniz başarıyla güncellendi.";
            else TempData["Hata"] = "Güncelleme sırasında hata oluştu.";

            return RedirectToAction(nameof(Profil));
        }

        [HttpPost("sifre-degistir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SifreDegistir(string mevcutSifre, string yeniSifre, string yeniSifreTekrar)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (yeniSifre != yeniSifreTekrar)
            {
                TempData["SifreHata"] = "Yeni şifreler eşleşmiyor.";
                return RedirectToAction(nameof(Profil));
            }

            var sonuc = await _userManager.ChangePasswordAsync(kullanici, mevcutSifre, yeniSifre);
            if (sonuc.Succeeded) TempData["SifreBasarili"] = "Şifreniz başarıyla değiştirildi.";
            else TempData["SifreHata"] = "Mevcut şifreniz yanlış.";

            return RedirectToAction(nameof(Profil));
        }

        [HttpGet("devreyealmalar")]
        public async Task<IActionResult> DevreyeAlmalar(string? marka, string? servis, string? durum, DateTime? bas, DateTime? bit)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            // Devreye almalar raporu personel icin goruntulenebilir olsun.

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var query = _context.Ys_DevreyeAlmalar.Include(x => x.Firma).Include(x => x.Marka)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .AsQueryable();

            if (!string.IsNullOrEmpty(marka)) query = query.Where(x => x.Marka != null && x.Marka.MarkaAdi != null && x.Marka.MarkaAdi.Contains(marka));
            if (!string.IsNullOrEmpty(servis)) query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(servis));
            if (int.TryParse(durum, out int d)) query = query.Where(x => x.Durum == d);
            if (bas.HasValue) query = query.Where(x => x.OlusturmaTarihi >= bas.Value);
            if (bit.HasValue) query = query.Where(x => x.OlusturmaTarihi <= bit.Value.AddDays(1));

            ViewBag.Markalar = await _context.Ys_Markalar.Where(x => !x.SilindiMi).ToListAsync();
            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/DevreyeAlmalar.cshtml", await query.OrderByDescending(x => x.OlusturmaTarihi).ToListAsync());
        }

        [HttpGet("devreyealmalar/detay/{id}")]
        public async Task<IActionResult> DevreyeAlmaDetay(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kayit = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .Include(x => x.Marka)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));

            if (kayit == null) return RedirectToAction(nameof(DevreyeAlmalar));

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/DevreyeAlmaDetay.cshtml", kayit);
        }

        [HttpGet("devreyealma-pdf/{id}")]
        public async Task<IActionResult> DevreyeAlmaPdf(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kayit = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));

            if (kayit == null) return NotFound();

            var pdf = DevreyeAlmaPdfService.Olustur(kayit);
            return File(pdf, "application/pdf",
                $"DevreyeAlma_{kayit.TesistatNo ?? id.ToString()}_{id}.pdf");
        }

        [HttpGet("devreyealma-excel/{id}")]
        public async Task<IActionResult> DevreyeAlmaExcel(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kayit = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .FirstOrDefaultAsync(x => x.Id == id
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));

            if (kayit == null) return NotFound();

            var bytes = DevreyeAlmaExcelService.Olustur(new[] { kayit });
            return File(bytes, "text/csv; charset=windows-1254",
                $"DevreyeAlma_{kayit.TesistatNo ?? id.ToString()}_{id}.csv");
        }

        [HttpGet("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.CERTIFIKA_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var bekleyenler = await _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum == 0
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            var onaylananlar = await _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum == 1
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            var reddedilenler = await _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum == 2
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .Take(100)
                .ToListAsync();

            ViewBag.OnayBekleyen = bekleyenler.Count;
            ViewBag.Kullanici = kullanici;
            ViewBag.Onaylananlar = onaylananlar;
            ViewBag.Reddedilenler = reddedilenler;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/OnayBekleyenler.cshtml", bekleyenler);
        }

        [HttpGet("onayla")]
        public IActionResult OnaylaGet()
        {
            return Redirect("/personel-panel/onay-bekleyenler");
        }

        [HttpPost("onayla")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Onayla(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.CERTIFIKA_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sertifika = await _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);

            if (sertifika == null)
            {
                TempData["Hata"] = "Yetki belgesi bulunamadı.";
                return Redirect("/personel-panel/onay-bekleyenler");
            }

            if (sertifika.Firma == null || sertifika.Firma.SilindiMi)
            {
                TempData["Hata"] = "Silinmiş firmaya ait yetki belgesi için işlem yapılamaz.";
                return Redirect("/personel-panel/onay-bekleyenler");
            }

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (aktifSirketId.HasValue && sertifika.Firma?.SirketId != aktifSirketId)
            {
                TempData["Hata"] = "Bu yetki belgesi için işlem yetkiniz yok.";
                return Redirect("/personel-panel/onay-bekleyenler");
            }

            sertifika.Durum = 1;
            sertifika.RedGerekce = null;
            sertifika.OnayTarihi = DateTime.Now;
            sertifika.OnaylayanKullanici = kullanici.UserName ?? "sistem";
            sertifika.GuncellemeTarihi = DateTime.Now;
            sertifika.GuncelleyenKullanici = kullanici.UserName ?? "sistem";

            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Yetki belgesi onaylandı.";
            return Redirect("/personel-panel/onay-bekleyenler");
        }

        [HttpPost("reddet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id, string? gerekce)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.CERTIFIKA_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sertifika = await _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);

            if (sertifika == null)
            {
                TempData["Hata"] = "Yetki belgesi bulunamadı.";
                return Redirect("/personel-panel/onay-bekleyenler");
            }

            if (sertifika.Firma == null || sertifika.Firma.SilindiMi)
            {
                TempData["Hata"] = "Silinmiş firmaya ait yetki belgesi için işlem yapılamaz.";
                return Redirect("/personel-panel/onay-bekleyenler");
            }

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (aktifSirketId.HasValue && sertifika.Firma?.SirketId != aktifSirketId)
            {
                TempData["Hata"] = "Bu yetki belgesi için işlem yetkiniz yok.";
                return Redirect("/personel-panel/onay-bekleyenler");
            }

            sertifika.Durum = 2;
            sertifika.RedGerekce = string.IsNullOrWhiteSpace(gerekce) ? "Belirtilmedi." : gerekce.Trim();
            sertifika.OnayTarihi = DateTime.Now;
            sertifika.OnaylayanKullanici = kullanici.UserName ?? "sistem";
            sertifika.GuncellemeTarihi = DateTime.Now;
            sertifika.GuncelleyenKullanici = kullanici.UserName ?? "sistem";

            await _context.SaveChangesAsync();

            TempData["Hata"] = "Yetki belgesi reddedildi.";
            return Redirect("/personel-panel/onay-bekleyenler");
        }

        [HttpGet("onay-gecmisi")]
        public async Task<IActionResult> OnayGecmisi(DateTime? bas, DateTime? bit, string? q, string? durum)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.CERTIFIKA_ONAY);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var query = _context.Ys_Sertifikalar
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Durum != 0
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .AsQueryable();

            if (bas.HasValue)
            {
                var baslangic = bas.Value.Date;
                query = query.Where(x => x.OnayTarihi.HasValue && x.OnayTarihi.Value >= baslangic);
            }

            if (bit.HasValue)
            {
                var bitis = bit.Value.Date.AddDays(1);
                query = query.Where(x => x.OnayTarihi.HasValue && x.OnayTarihi.Value < bitis);
            }

            if (!string.IsNullOrWhiteSpace(durum) && int.TryParse(durum, out var durumNo) && (durumNo == 1 || durumNo == 2))
            {
                query = query.Where(x => x.Durum == durumNo);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var aranacak = q.Trim();
                query = query.Where(x =>
                    (x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(aranacak)) ||
                    (x.Firma != null && x.Firma.Sirket != null && x.Firma.Sirket.SirketAdi != null && x.Firma.Sirket.SirketAdi.Contains(aranacak)) ||
                    (x.OnaylayanKullanici != null && x.OnaylayanKullanici.Contains(aranacak)));
            }

            var onaylar = await query
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            ViewBag.Bas = bas;
            ViewBag.Bit = bit;
            ViewBag.Q = q ?? "";
            ViewBag.Durum = durum ?? "";
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/OnayGecmisi.cshtml", onaylar);
        }

        [HttpGet("sirketler")]
        public async Task<IActionResult> Sirketler()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketler = await _context.Set<Dag_Sirket>()
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.SirketAdi)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Sirketler.cshtml", sirketler);
        }

        [HttpGet("sirketler/ekle")]
        public async Task<IActionResult> SirketEkle()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici!);
            await SetPersonelNotifViewBags(kullanici!);
            return View("~/Views/PersonelPanel/SirketEkle.cshtml");
        }

        [HttpPost("sirketler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SirketEkle(Dag_Sirket sirket)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            sirket.OlusturmaTarihi = DateTime.Now;
            sirket.OlusturanKullanici = kullanici?.UserName ?? "sistem";

            _context.Set<Dag_Sirket>().Add(sirket);
            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Şirket başarıyla eklendi.";
            return RedirectToAction(nameof(Sirketler));
        }

        [HttpGet("sirketler/duzenle/{id}")]
        public async Task<IActionResult> SirketDuzenle(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var sirket = await _context.Set<Dag_Sirket>().FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (sirket == null) return RedirectToAction(nameof(Sirketler));

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici!);
            await SetPersonelNotifViewBags(kullanici!);
            return View("~/Views/PersonelPanel/SirketDuzenle.cshtml", sirket);
        }

        [HttpPost("sirketler/duzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SirketDuzenle(int id, Dag_Sirket model)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var sirket = await _context.Set<Dag_Sirket>().FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (sirket == null) return RedirectToAction(nameof(Sirketler));

            sirket.SirketAdi = model.SirketAdi;
            sirket.Il = model.Il;
            sirket.Telefon = model.Telefon;
            sirket.Email = model.Email;
            sirket.Adres = model.Adres;
            sirket.AktifMi = model.AktifMi;
            sirket.GuncellemeTarihi = DateTime.Now;
            sirket.GuncelleyenKullanici = kullanici?.UserName ?? "sistem";

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Şirket başarıyla güncellendi.";
            return RedirectToAction(nameof(Sirketler));
        }

        [HttpPost("sirketler/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SirketSil(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.DAGITIM_SIRKET_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var sirket = await _context.Set<Dag_Sirket>().FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (sirket == null) return RedirectToAction(nameof(Sirketler));

            sirket.SilindiMi = true;
            sirket.SilinmeTarihi = DateTime.Now;
            sirket.SilenKullanici = kullanici?.UserName ?? "sistem";
            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Şirket silindi.";
            return RedirectToAction(nameof(Sirketler));
        }

        [HttpGet("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Markalar.cshtml", markalar);
        }

        [HttpGet("markalar/ekle")]
        public async Task<IActionResult> MarkaEkle()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici!);
            await SetPersonelNotifViewBags(kullanici!);
            return View("~/Views/PersonelPanel/MarkaEkle.cshtml");
        }

        [HttpPost("markalar/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaEkle(Ys_Marka marka)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            marka.OlusturmaTarihi = DateTime.Now;
            marka.OlusturanKullanici = kullanici?.UserName ?? "sistem";

            _context.Ys_Markalar.Add(marka);
            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Marka başarıyla eklendi.";
            return RedirectToAction(nameof(Markalar));
        }

        [HttpGet("markalar/duzenle/{id}")]
        public async Task<IActionResult> MarkaDuzenle(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var marka = await _context.Ys_Markalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (marka == null) return RedirectToAction(nameof(Markalar));

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici!);
            await SetPersonelNotifViewBags(kullanici!);
            return View("~/Views/PersonelPanel/MarkaDuzenle.cshtml", marka);
        }

        [HttpPost("markalar/duzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaDuzenle(int id, Ys_Marka model)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var marka = await _context.Ys_Markalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (marka == null) return RedirectToAction(nameof(Markalar));

            marka.MarkaAdi = model.MarkaAdi;
            marka.Aciklama = model.Aciklama;
            marka.AktifMi = model.AktifMi;
            marka.GuncellemeTarihi = DateTime.Now;
            marka.GuncelleyenKullanici = kullanici?.UserName ?? "sistem";

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Marka başarıyla güncellendi.";
            return RedirectToAction(nameof(Markalar));
        }

        [HttpPost("markalar/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaSil(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.MARKA_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var marka = await _context.Ys_Markalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
            if (marka == null) return RedirectToAction(nameof(Markalar));

            var kullanimVar = await _context.Ys_DevreyeAlmalar.AnyAsync(x => !x.SilindiMi && x.MarkaId == id)
                || await _context.Ys_FirmaMarkalar.AnyAsync(x => !x.SilindiMi && x.MarkaId == id);

            if (kullanimVar)
            {
                TempData["Hata"] = "Bu marka üzerinde devreye alma veya yetkili servis kaydı olduğu için silinemez.";
                return RedirectToAction(nameof(Markalar));
            }

            marka.SilindiMi = true;
            marka.SilinmeTarihi = DateTime.Now;
            marka.SilenKullanici = kullanici?.UserName ?? "sistem";
            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Marka silindi.";
            return RedirectToAction(nameof(Markalar));
        }

        [HttpGet("yetkiliservisler")]
        public async Task<IActionResult> YetkiliServisler(string? q)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var query = _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (sirketId.HasValue)
                query = query.Where(x => x.SirketId == sirketId.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    (x.FirmaAdi != null && x.FirmaAdi.Contains(q)) ||
                    (x.VergiNo != null && x.VergiNo.Contains(q)) ||
                    (x.Sirket != null && x.Sirket.SirketAdi != null && x.Sirket.SirketAdi.Contains(q)));
            }

            var servisler = await query.OrderBy(x => x.FirmaAdi).ToListAsync();
            var devreyeSayilari = await _context.Ys_DevreyeAlmalar
                .Where(x => !x.SilindiMi)
                .GroupBy(x => x.FirmaId)
                .Select(x => new { FirmaId = x.Key, Sayisi = x.Count() })
                .ToDictionaryAsync(x => x.FirmaId, x => x.Sayisi);

            ViewBag.Kullanici = kullanici;
            ViewBag.Query = q ?? "";
            ViewBag.DevreyeSayilari = devreyeSayilari;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/YetkiliServisler.cshtml", servisler);
        }

        [HttpGet("yetkiliservisler/detay/{id}")]
        public async Task<IActionResult> YetkiliServisDetay(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.Subeler)
                .Include(x => x.FirmaMarkalar!).ThenInclude(x => x.Marka)
                .Include(x => x.FirmaKategoriler!).ThenInclude(x => x.Kategori)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi
                    && (!sirketId.HasValue || x.SirketId == sirketId.Value));

            if (firma == null) return RedirectToAction(nameof(YetkiliServisler));

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/YetkiliServisDetay.cshtml", firma);
        }

        [HttpGet("yetkiliservisler/ekle")]
        public async Task<IActionResult> YetkiliServisEkle()
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            ViewBag.Kullanici = kullanici;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.Markalar = await _context.Ys_Markalar.Where(x => !x.SilindiMi).OrderBy(x => x.MarkaAdi).ToListAsync();

            await SetPersonelYetkiViewBags(kullanici!);
            await SetPersonelNotifViewBags(kullanici!);
            return View("~/Views/PersonelPanel/YetkiliServisEkle.cshtml");
        }

        [HttpPost("yetkiliservisler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisEkle(string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, List<int> kategoriIds, List<int> markaIds)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);

            if (string.IsNullOrWhiteSpace(firmaAdi))
            {
                TempData["Hata"] = "Firma adı zorunludur.";
                return Redirect("/personel-panel/yetkiliservisler/ekle");
            }

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici)
                ?? await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    faaliyetIli,
                    kullanici?.UserName ?? "sistem");
            var kullanilanKategoriIds = (await KullanilanKategorileriGetir())
                .Select(x => x.Id)
                .ToHashSet();
            kategoriIds = kategoriIds?
                .Where(kullanilanKategoriIds.Contains)
                .Distinct()
                .ToList() ?? new List<int>();

            var yeni = new Ys_Firma
            {
                FirmaAdi = firmaAdi,
                YetkiliKisi = yetkiliKisi,
                Telefon = telefon,
                Email = email,
                Adres = adres,
                FaaliyetIli = faaliyetIli,
                VergiNo = vergiNo,
                VergiDairesi = vergiDairesi,
                SirketId = sirketId,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici?.UserName ?? "sistem"
            };

            _context.Ys_Firmalar.Add(yeni);
            await _context.SaveChangesAsync();

            if (kategoriIds != null && kategoriIds.Count > 0)
            {
                foreach (var kid in kategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = yeni.Id,
                        KategoriId = kid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici?.UserName ?? "sistem"
                    });
                }
            }

            if (markaIds != null && markaIds.Count > 0)
            {
                foreach (var mid in markaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = yeni.Id,
                        MarkaId = mid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici?.UserName ?? "sistem"
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Yetkili servis başarıyla eklendi.";
            return RedirectToAction(nameof(YetkiliServisler));
        }

        [HttpGet("yetkiliservisler/duzenle/{id}")]
        public async Task<IActionResult> YetkiliServisDuzenle(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var servis = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi
                    && (!sirketId.HasValue || x.SirketId == sirketId.Value));
            if (servis == null) return RedirectToAction(nameof(YetkiliServisler));

            ViewBag.Kullanici = kullanici;
            ViewBag.Servis = servis;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.Kategoriler = await KullanilanKategorileriGetir();
            ViewBag.Markalar = await _context.Ys_Markalar.Where(x => !x.SilindiMi).OrderBy(x => x.MarkaAdi).ToListAsync();
            ViewBag.SeciliKategoriler = await _context.Ys_FirmaKategoriler.Where(x => x.FirmaId == servis.Id).Select(x => x.KategoriId).ToListAsync();
            ViewBag.SeciliMarkalar = await _context.Ys_FirmaMarkalar.Where(x => x.FirmaId == servis.Id).Select(x => x.MarkaId).ToListAsync();

            await SetPersonelYetkiViewBags(kullanici!);
            await SetPersonelNotifViewBags(kullanici!);
            return View("~/Views/PersonelPanel/YetkiliServisDuzenle.cshtml", servis);
        }

        [HttpPost("yetkiliservisler/duzenle/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisDuzenle(int id, string firmaAdi, string yetkiliKisi, string telefon, string email, string adres, string faaliyetIli, string vergiNo, string vergiDairesi, bool aktifMi, List<int> kategoriIds, List<int> markaIds)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var servis = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi
                && (!sirketId.HasValue || x.SirketId == sirketId.Value));
            if (servis == null) return RedirectToAction(nameof(YetkiliServisler));

            var hedefSirketId = sirketId
                ?? await _sehirFirmaKoduService.SirketIdBulVeyaOlustur(
                    faaliyetIli,
                    kullanici?.UserName ?? "sistem");
            var kullanilanKategoriIds = (await KullanilanKategorileriGetir())
                .Select(x => x.Id)
                .ToHashSet();
            kategoriIds = kategoriIds?
                .Where(kullanilanKategoriIds.Contains)
                .Distinct()
                .ToList() ?? new List<int>();

            servis.FirmaAdi = firmaAdi;
            servis.YetkiliKisi = yetkiliKisi;
            servis.Telefon = telefon;
            servis.Email = email;
            servis.Adres = adres;
            servis.FaaliyetIli = faaliyetIli;
            servis.VergiNo = vergiNo;
            servis.VergiDairesi = vergiDairesi;
            servis.SirketId = hedefSirketId;
            servis.AktifMi = aktifMi;
            servis.GuncellemeTarihi = DateTime.Now;
            servis.GuncelleyenKullanici = kullanici?.UserName ?? "sistem";

            var mevcutKat = await _context.Ys_FirmaKategoriler.Where(x => x.FirmaId == servis.Id).ToListAsync();
            _context.Ys_FirmaKategoriler.RemoveRange(mevcutKat);

            if (kategoriIds != null && kategoriIds.Count > 0)
            {
                foreach (var kid in kategoriIds.Distinct())
                {
                    _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = servis.Id,
                        KategoriId = kid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici?.UserName ?? "sistem"
                    });
                }
            }

            var mevcutMarka = await _context.Ys_FirmaMarkalar.Where(x => x.FirmaId == servis.Id).ToListAsync();
            _context.Ys_FirmaMarkalar.RemoveRange(mevcutMarka);

            if (markaIds != null && markaIds.Count > 0)
            {
                foreach (var mid in markaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = servis.Id,
                        MarkaId = mid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici?.UserName ?? "sistem"
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Yetkili servis güncellendi.";
            return RedirectToAction(nameof(YetkiliServisler));
        }

        [HttpPost("yetkiliservisler/sil/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YetkiliServisSil(int id)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.KULLANICI_YONET);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var servis = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi
                && (!sirketId.HasValue || x.SirketId == sirketId.Value));
            if (servis == null) return RedirectToAction(nameof(YetkiliServisler));

            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.FirmaId == id);

            if (devreyeAlmaVar)
            {
                TempData.Remove("Basarili");
                TempData["Hata"] = "Bu yetkili servis üzerinde devreye alma işlemi olduğu için silinemez.";
                return RedirectToAction(nameof(YetkiliServisler));
            }

            servis.SilindiMi = true;
            servis.SilinmeTarihi = DateTime.Now;
            servis.SilenKullanici = kullanici?.UserName ?? "sistem";
            await _context.SaveChangesAsync();
            TempData.Remove("Hata");
            TempData["Basarili"] = "Yetkili servis silindi.";
            return RedirectToAction(nameof(YetkiliServisler));
        }

        [HttpGet("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit, string? tip)
        {
            var yetkiResult = await YetkiKontrol(YetkiTipleri.RAPOR_GOR);
            if (yetkiResult != null) return yetkiResult;

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var devreyeBaseQuery = _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));

            DateTime basTarih;
            DateTime bitTarih;
            if (!bas.HasValue && !bit.HasValue)
            {
                var mevcutAralik = await devreyeBaseQuery
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Bas = g.Min(x => x.DevreyeAlmaTarihi),
                        Bit = g.Max(x => x.DevreyeAlmaTarihi)
                    })
                    .FirstOrDefaultAsync();

                basTarih = mevcutAralik?.Bas.Date ?? DateTime.Now.Date.AddDays(-30);
                bitTarih = mevcutAralik?.Bit.Date ?? DateTime.Now.Date;
            }
            else
            {
                bitTarih = bit?.Date ?? DateTime.Now.Date;
                basTarih = bas?.Date ?? bitTarih.AddDays(-30);
            }

            var bitSonrasi = bitTarih.AddDays(1);
            var raporTipi = string.IsNullOrWhiteSpace(tip) ? "devreye" : tip.Trim().ToLowerInvariant();
            var tr = new System.Globalization.CultureInfo("tr-TR");

            var temelQuery = devreyeBaseQuery
                .Include(x => x.Marka)
                .Where(x => x.DevreyeAlmaTarihi >= basTarih
                    && x.DevreyeAlmaTarihi < bitSonrasi);

            var sertifikaQuery = _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && x.OlusturmaTarihi >= basTarih
                    && x.OlusturmaTarihi < bitSonrasi
                    && (sirketId == null || x.Firma.SirketId == sirketId));

            var toplam = await temelQuery.CountAsync();
            var tamam = await temelQuery.CountAsync(x => x.Durum == 1);
            var bekleyen = await temelQuery.CountAsync(x => x.Durum == 0);
            var iptal = await temelQuery.CountAsync(x => x.Durum == 2);

            var aylar = new List<DateTime>();
            var ayBaslangic = new DateTime(basTarih.Year, basTarih.Month, 1);
            var ayBitis = new DateTime(bitTarih.Year, bitTarih.Month, 1);
            for (var ay = ayBaslangic; ay <= ayBitis && aylar.Count < 24; ay = ay.AddMonths(1))
                aylar.Add(ay);

            var aylik = aylar.Select(a => new
            {
                Ay = a.ToString("MMM", tr),
                Sayi = temelQuery.Count(x => x.DevreyeAlmaTarihi.Month == a.Month && x.DevreyeAlmaTarihi.Year == a.Year)
            }).ToList();

            var markaTop = await temelQuery
                .Where(x => x.MarkaId != null)
                .GroupBy(x => x.Marka!.MarkaAdi)
                .Select(g => new { Marka = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(5)
                .ToListAsync();

            var sertifikaOnayli = await sertifikaQuery.Where(x => x.Durum == 1).CountAsync();
            var sertifikaBekleyen = await sertifikaQuery.Where(x => x.Durum == 0).CountAsync();
            var sertifikaReddedilen = await sertifikaQuery.Where(x => x.Durum == 2).CountAsync();

            if (raporTipi == "onayli" || raporTipi == "bekleyen" || raporTipi == "reddedilen")
            {
                var durum = raporTipi == "onayli" ? 1 : (raporTipi == "bekleyen" ? 0 : 2);
                ViewBag.SertifikaIslemler = await sertifikaQuery
                    .Where(x => x.Durum == durum)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();
                ViewBag.ListeTipi = "sertifika";
            }
            else
            {
                ViewBag.SonIslemler = await temelQuery
                    .OrderByDescending(x => x.DevreyeAlmaTarihi)
                    .Take(12)
                    .ToListAsync();
                ViewBag.ListeTipi = "devreye";
            }

            ViewBag.RaporToplam = toplam;
            ViewBag.RaporTamam = tamam;
            ViewBag.RaporBekleyen = bekleyen;
            ViewBag.RaporIptal = iptal;
            ViewBag.SertifikaOnayli = sertifikaOnayli;
            ViewBag.SertifikaBekleyen = sertifikaBekleyen;
            ViewBag.SertifikaReddedilen = sertifikaReddedilen;
            ViewBag.RaporAylar = aylik.Select(x => x.Ay).ToList();
            ViewBag.RaporAylik = aylik.Select(x => x.Sayi).ToList();
            ViewBag.RaporMarka = markaTop.Select(x => x.Marka).ToList();
            ViewBag.RaporMarkaSayi = markaTop.Select(x => x.Sayi).ToList();
            ViewBag.BasTarih = basTarih;
            ViewBag.BitTarih = bitTarih;
            ViewBag.RaporTipi = raporTipi;

            ViewBag.Kullanici = kullanici;
            await SetPersonelYetkiViewBags(kullanici);
            await SetPersonelNotifViewBags(kullanici);
            return View("~/Views/PersonelPanel/Raporlar.cshtml");
        }
    }
}

