using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    public class DagitimSirketController : Controller
    {
        private readonly DagitimSirketApiClient _dagitimSirketApiClient;
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AktifSirketService _aktifSirketService;

        public DagitimSirketController(
            DagitimSirketApiClient dagitimSirketApiClient,
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            AktifSirketService aktifSirketService)
        {
            _dagitimSirketApiClient = dagitimSirketApiClient;
            _context = context;
            _userManager = userManager;
            _aktifSirketService = aktifSirketService;
        }

        private async Task<int> GetOnayBekleyenCount()
        {
            return await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi && x.Durum == 0)
                .CountAsync();
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.SuresiBitecek = await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi && x.Durum == 1 && x.SertifikaBitisTarihi <= DateTime.Now.AddDays(30) && x.SertifikaBitisTarihi >= DateTime.Now)
                .CountAsync();
            await next();
        }

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _userManager.GetUserAsync(User);
        }
        private async Task<bool> KullaniciYetkiliMi(AppKullanici? kullanici, string yetki)
        {
            if (kullanici == null) return false;
            if (await _aktifSirketService.GenelSistemAdminMi(kullanici) || await _aktifSirketService.SirketAdminMi(kullanici))
                return true;
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                (aktifSirketId == null || x.SirketId == aktifSirketId) &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == yetki));
        }

        private async Task<IActionResult?> YetkiKontrol(string yetki, string redirectPath)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var yetkili = await KullaniciYetkiliMi(kullanici, yetki);
            if (!yetkili && await _userManager.IsInRoleAsync(kullanici, "Personel"))
                return Redirect(redirectPath);

            return null;
        }

        // Liste sayfası
        public async Task<IActionResult> Index(string? q, string? durum)
        {
            var kullanici = await GetCurrentUser();
            var sirketler = await _dagitimSirketApiClient.TumunuGetirAsync();
            ViewBag.DagitimSirketVeriKaynagi = "API";

            if (sirketler == null)
            {
                TempData["Hata"] = "Sirket listesi API uzerinden alinamadi.";
                sirketler = new List<Dag_Sirket>();
            }

            var aktifSirketId = kullanici == null ? null : await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (aktifSirketId.HasValue || (kullanici != null && !await _aktifSirketService.GenelSistemAdminMi(kullanici)))
                sirketler = sirketler.Where(x => aktifSirketId.HasValue && x.Id == aktifSirketId.Value).ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var aranacak = q.Trim();
                sirketler = sirketler
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.SirketAdi) && x.SirketAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.Il) && x.Il.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.Telefon) && x.Telefon.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(durum) && durum != "tumu")
            {
                var aktifMi = durum == "aktif";
                sirketler = sirketler.Where(x => x.AktifMi == aktifMi).ToList();
            }

            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliDurum = string.IsNullOrWhiteSpace(durum) ? "tumu" : durum;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.SuresiBitecek = await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi && x.Durum == 1 && x.SertifikaBitisTarihi <= DateTime.Now.AddDays(30) && x.SertifikaBitisTarihi >= DateTime.Now)
                .CountAsync();
            ViewBag.Kullanici = kullanici;
            return View(sirketler);
        }

        // Yeni ekle sayfası
        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Kullanici = kullanici;
            return View();
        }

        // Yeni ekle kaydet
        [HttpPost]
        public async Task<IActionResult> Ekle(Dag_Sirket sirket)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            var sonuc = await _dagitimSirketApiClient.EkleAsync(kullanici, sirket);
            SetDagitimSirketIslemMesaji(sonuc, "Sirket basariyla eklendi.");
            return RedirectToAction("Index");
        }

        // Düzenle sayfası
        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sirket = await _dagitimSirketApiClient.GetirAsync(kullanici, id);
            if (sirket == null)
            {
                TempData["Hata"] = "Sirket detayi API uzerinden alinamadi.";
                return RedirectToAction(nameof(Index));
            }

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici) && (!aktifSirketId.HasValue || sirket.Id != aktifSirketId.Value))
                return RedirectToAction(nameof(Index));

            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Kullanici = kullanici;
            return View(sirket);
        }

        // Düzenle kaydet
        [HttpPost]
        public async Task<IActionResult> Duzenle(Dag_Sirket sirket)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici) && (!aktifSirketId.HasValue || sirket.Id != aktifSirketId.Value))
                return RedirectToAction(nameof(Index));

            var sonuc = await _dagitimSirketApiClient.GuncelleAsync(kullanici, sirket);
            SetDagitimSirketIslemMesaji(sonuc, "Sirket basariyla guncellendi.");
            return RedirectToAction("Index");
        }

        // Sil
        public async Task<IActionResult> Sil(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");
            if (!await _aktifSirketService.GenelSistemAdminMi(kullanici)) return RedirectToAction(nameof(Index));

            var sonuc = await _dagitimSirketApiClient.SilAsync(kullanici, id);
            SetDagitimSirketIslemMesaji(sonuc, "Sirket basariyla silindi.");
            return RedirectToAction("Index");
        }

        private void SetDagitimSirketIslemMesaji(DagitimSirketIslemSonuc? sonuc, string varsayilanBasari)
        {
            if (sonuc?.Basarili == true)
            {
                TempData["Mesaj"] = sonuc.Mesaj ?? varsayilanBasari;
                return;
            }

            TempData["Hata"] = sonuc?.Mesaj ?? "Sirket islemi API uzerinden tamamlanamadi.";
        }
    }
}


