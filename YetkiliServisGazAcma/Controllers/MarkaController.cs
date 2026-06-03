using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using Microsoft.AspNetCore.Identity;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MarkaController : Controller
    {
        private readonly MarkaApiClient _markaApiClient;
        private readonly AdminDashboardApiClient _adminDashboardApiClient;
        private readonly UserManager<AppKullanici> _userManager;

        public MarkaController(
            MarkaApiClient markaApiClient,
            AdminDashboardApiClient adminDashboardApiClient,
            UserManager<AppKullanici> userManager)
        {
            _markaApiClient = markaApiClient;
            _adminDashboardApiClient = adminDashboardApiClient;
            _userManager = userManager;
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

            var cacheKey = "MarkaDashboard:tum";
            if (HttpContext.Items.TryGetValue(cacheKey, out var cached))
                return cached as AdminDashboardOzet;

            var dashboard = await _adminDashboardApiClient.GetirAsync(kullanici, null);
            if (dashboard != null)
                HttpContext.Items[cacheKey] = dashboard;

            return dashboard;
        }

        public async Task<IActionResult> Index(string? q, string? durum)
        {
            var markalar = await _markaApiClient.TumunuGetirAsync();
            ViewBag.MarkaVeriKaynagi = "API";

            if (markalar == null)
            {
                TempData["Hata"] = "Marka listesi API uzerinden alinamadi.";
                markalar = new List<Ys_Marka>();
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var aranacak = q.Trim();
                markalar = markalar
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.MarkaAdi) &&
                        x.MarkaAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(durum) && durum != "tumu")
            {
                var aktifMi = durum == "aktif";
                markalar = markalar.Where(x => x.AktifMi == aktifMi).ToList();
            }

            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliDurum = string.IsNullOrWhiteSpace(durum) ? "tumu" : durum;
            var dashboard = await GetDashboardOzetAsync();
            ViewBag.OnayBekleyen = dashboard?.OnayBekleyen ?? 0;
            ViewBag.SuresiBitecek = dashboard?.SuresiBitecek ?? 0;
            ViewBag.Kullanici = await GetCurrentUser();
            return View(markalar);
        }

        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Kullanici = await GetCurrentUser();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Ekle(Ys_Marka marka)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _markaApiClient.EkleAsync(kullanici, marka);
            SetMarkaIslemMesaji(sonuc, "Marka basariyla eklendi.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var marka = await _markaApiClient.GetirAsync(kullanici, id);
            if (marka == null)
            {
                TempData["Hata"] = "Marka detayi API uzerinden alinamadi.";
                return RedirectToAction("Index");
            }
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Kullanici = kullanici;
            return View(marka);
        }

        [HttpPost]
        public async Task<IActionResult> Duzenle(Ys_Marka marka)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _markaApiClient.GuncelleAsync(kullanici, marka);
            SetMarkaIslemMesaji(sonuc, "Marka basariyla guncellendi.");
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Sil(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _markaApiClient.SilAsync(kullanici, id);
            SetMarkaIslemMesaji(
                sonuc,
                "Marka basariyla silindi.",
                "Bu marka uzerinde devreye alma veya yetkili servis kaydi oldugu icin silinemez.");
            return RedirectToAction("Index");
        }

        private void SetMarkaIslemMesaji(MarkaIslemSonuc? sonuc, string varsayilanBasari, string? varsayilanHata = null)
        {
            if (sonuc?.Basarili == true)
            {
                TempData["Mesaj"] = sonuc.Mesaj ?? varsayilanBasari;
                return;
            }

            TempData["Hata"] = sonuc?.Mesaj ?? varsayilanHata ?? "Marka islemi API uzerinden tamamlanamadi.";
        }
    }
}

