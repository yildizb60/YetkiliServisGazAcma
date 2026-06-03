using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using Microsoft.AspNetCore.Identity;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    public class DagitimSirketController : Controller
    {
        private readonly DagitimSirketApiClient _dagitimSirketApiClient;
        private readonly AdminDashboardApiClient _adminDashboardApiClient;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AktifSirketService _aktifSirketService;

        public DagitimSirketController(
            DagitimSirketApiClient dagitimSirketApiClient,
            AdminDashboardApiClient adminDashboardApiClient,
            UserManager<AppKullanici> userManager,
            AktifSirketService aktifSirketService)
        {
            _dagitimSirketApiClient = dagitimSirketApiClient;
            _adminDashboardApiClient = adminDashboardApiClient;
            _userManager = userManager;
            _aktifSirketService = aktifSirketService;
        }

        private async Task<int> GetOnayBekleyenCount()
        {
            var dashboard = await GetDashboardOzetAsync();
            return dashboard?.OnayBekleyen ?? 0;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var dashboard = await GetDashboardOzetAsync();
            ViewBag.OnayBekleyen = dashboard?.OnayBekleyen ?? 0;
            ViewBag.SuresiBitecek = dashboard?.SuresiBitecek ?? 0;
            await next();
        }

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<AdminDashboardOzet?> GetDashboardOzetAsync()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return null;

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var cacheKey = $"DagitimSirketDashboard:{aktifSirketId?.ToString() ?? "tum"}";
            if (HttpContext.Items.TryGetValue(cacheKey, out var cached))
                return cached as AdminDashboardOzet;

            var dashboard = await _adminDashboardApiClient.GetirAsync(kullanici, aktifSirketId);
            if (dashboard != null)
                HttpContext.Items[cacheKey] = dashboard;

            return dashboard;
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
            var dashboard = await GetDashboardOzetAsync();
            ViewBag.OnayBekleyen = dashboard?.OnayBekleyen ?? 0;
            ViewBag.SuresiBitecek = dashboard?.SuresiBitecek ?? 0;
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


