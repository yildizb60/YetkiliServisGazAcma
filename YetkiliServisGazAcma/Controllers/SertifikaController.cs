using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using Microsoft.EntityFrameworkCore;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ys-yetki-belgesi")]
    public class SertifikaController : Controller
    {
        private readonly SertifikaService _service;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AppDbContext _context;
        private readonly AktifSirketService _aktifSirketService;

        public SertifikaController(
            SertifikaService service,
            UserManager<AppKullanici> userManager,
            AppDbContext context,
            AktifSirketService aktifSirketService)
        {
            _service = service;
            _userManager = userManager;
            _context = context;
            _aktifSirketService = aktifSirketService;
        }

        private async Task SetBildirimler(AppKullanici kullanici)
        {
            var bildirimler = new List<string>();
            var firmaId = kullanici.FirmaId ?? 0;

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sertifikalar)
                .FirstOrDefaultAsync(x => x.Id == firmaId);

            var onayli = firma?.Sertifikalar?
                .Where(x => x.Durum == 1)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .FirstOrDefault();
            var bekleyenVar = firma?.Sertifikalar?.Any(x => x.Durum == 0) ?? false;
            if (onayli != null)
            {
                bildirimler.Add("Yetki belgeniz onaylandı. Cihaz devreye alabilirsiniz.");
                var kalan = (onayli.SertifikaBitisTarihi.Date - DateTime.Now.Date).Days;
                if (kalan <= 30)
                {
                    bildirimler.Add($"Yetki belgenizin bitmesine {kalan} gün kaldı. Lütfen yenileyin.");
                }
            }
            if (bekleyenVar)
            {
                bildirimler.Add("Yetki belgeniz onay bekliyor. Yetkili onayladıktan sonra işlem yapabilirsiniz.");
            }

            var son7Gun = DateTime.Now.AddDays(-7);
            var sonDevreye = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonDevreye > 0)
            {
                bildirimler.Add($"Son 7 günde {sonDevreye} cihaz devreye alındı.");
            }

            var sonSube = await _context.Ys_Subeler
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonSube > 0)
            {
                bildirimler.Add($"Son 7 günde {sonSube} şube kaydı eklendi.");
            }

            ViewBag.Bildirimler = bildirimler;
            ViewBag.BildirimSayisi = bildirimler.Count;
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

        [HttpGet]
        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _userManager.GetUserAsync(User);

            if (kullanici == null)
                return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            var sertifikalar = await _service.FirmaninSertifikalari(firmaId);

            ViewBag.FirmaId = firmaId;
            ViewBag.Firma = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == firmaId);
            await SetBildirimler(kullanici);
            return View("~/Views/Sertifika/Index.cshtml", sertifikalar);
        }

        [HttpPost]
        [Route("yukle")]
        public async Task<IActionResult> Yukle(
            IFormFile dosya,
            DateTime bitisTarihi,
            DateTime? baslangicTarihi)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            if (baslangicTarihi.HasValue && baslangicTarihi.Value.Date > bitisTarihi.Date)
            {
                TempData["Hata"] = "Yetki belgesi başlangıç tarihi, bitiş tarihinden büyük olamaz.";
                return Redirect("/ys-yetki-belgesi");
            }

            var firmaId = kullanici.FirmaId ?? 0;
            var (basarili, mesaj) = await _service.Yukle(
                firmaId, dosya, bitisTarihi, baslangicTarihi, kullanici.UserName);

            TempData[basarili ? "Basarili" : "Hata"] = mesaj;
            return Redirect("/ys-yetki-belgesi");
        }

        [HttpPost]
        [Route("sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            var sertifika = await _context.Ys_Sertifikalar
                .FirstOrDefaultAsync(x => x.Id == id && x.FirmaId == firmaId);

            if (sertifika == null)
            {
                TempData["Hata"] = "Yetki belgesi bulunamadı.";
                return Redirect("/ys-yetki-belgesi");
            }

            sertifika.SilindiMi = true;
            sertifika.SilinmeTarihi = DateTime.Now;
            sertifika.SilenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Yetki belgesi silindi.";
            return Redirect("/ys-yetki-belgesi");
        }

        [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
        [HttpGet]
        [Route("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (!await KullaniciYetkiliMi(kullanici, YetkiTipleri.CERTIFIKA_ONAY))
            {
                TempData["Hata"] = "Yetki belgesi onay yetkiniz yok.";
                return Redirect("/personel-panel");
            }

            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            ViewBag.OnayBekleyen = await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi
                    && x.Durum == 0
                    && x.Firma != null
                    && (sirketId == null || x.Firma.SirketId == sirketId))
                .CountAsync();

            var sertifikalar = await _service.OnayBekleyenler(sirketId);
            return View("~/Views/Sertifika/OnayBekleyenler.cshtml", sertifikalar);
        }

        [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
        [HttpPost]
        [Route("onayla")]
        public async Task<IActionResult> Onayla(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (!await KullaniciYetkiliMi(kullanici, YetkiTipleri.CERTIFIKA_ONAY))
            {
                TempData["Hata"] = "Yetki belgesi onay yetkiniz yok.";
                return Redirect("/personel-panel");
            }

            if (!await SertifikaAktifSirketteMi(id, kullanici))
            {
                TempData["Hata"] = "Bu yetki belgesi için işlem yetkiniz yok.";
                return Redirect("/ys-yetki-belgesi/onay-bekleyenler");
            }

            await _service.Onayla(id, kullanici?.UserName);
            TempData["Basarili"] = "Yetki belgesi onaylandı.";
            return Redirect("/ys-yetki-belgesi/onay-bekleyenler");
        }

        [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
        [HttpPost]
        [Route("reddet")]
        public async Task<IActionResult> Reddet(int id, string? gerekce)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            if (!await KullaniciYetkiliMi(kullanici, YetkiTipleri.CERTIFIKA_ONAY))
            {
                TempData["Hata"] = "Yetki belgesi onay yetkiniz yok.";
                return Redirect("/personel-panel");
            }

            if (!await SertifikaAktifSirketteMi(id, kullanici))
            {
                TempData["Hata"] = "Bu yetki belgesi için işlem yetkiniz yok.";
                return Redirect("/ys-yetki-belgesi/onay-bekleyenler");
            }

            await _service.Reddet(id, gerekce, kullanici?.UserName);
            TempData["Hata"] = "Yetki belgesi reddedildi.";
            return Redirect("/ys-yetki-belgesi/onay-bekleyenler");
        }

        private async Task<bool> SertifikaAktifSirketteMi(int sertifikaId, AppKullanici kullanici)
        {
            var sirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (!sirketId.HasValue && await _aktifSirketService.GenelSistemAdminMi(kullanici))
                return true;

            return await _context.Ys_Sertifikalar
                .Include(x => x.Firma)
                .AnyAsync(x => x.Id == sertifikaId
                    && !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && sirketId.HasValue
                    && x.Firma.SirketId == sirketId.Value);
        }
    }
}

