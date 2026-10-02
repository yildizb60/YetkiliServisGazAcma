using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Linq;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("AdminPanel")]
    public partial class AdminPanelController : Controller
    {
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly SehirFirmaKodlari _sehirFirmaKoduService;
        private readonly AktifSirketService _aktifSirketService;
        private readonly AdminDashboardApiClient _adminDashboardApiClient;
        private readonly AdminKullaniciApiClient _adminKullaniciApiClient;
        private readonly AdminYetkiliServisApiClient _adminYetkiliServisApiClient;
        private readonly AdminYetkiBelgesiOnayApiClient _adminYetkiBelgesiOnayApiClient;
        private readonly AdminSubeApiClient _adminSubeApiClient;
        private readonly AdminRaporApiClient _adminRaporApiClient;
        private readonly YkcApiClient _ykcApiClient;
        private readonly MarkaApiClient _markaApiClient;
        private readonly UrunKategoriApiClient _urunKategoriApiClient;

        public AdminPanelController(
            ApiKullaniciOturumu kullaniciOturumu,
            SehirFirmaKodlari sehirFirmaKoduService,
            AktifSirketService aktifSirketService,
            AdminDashboardApiClient adminDashboardApiClient,
            AdminKullaniciApiClient adminKullaniciApiClient,
            AdminYetkiliServisApiClient adminYetkiliServisApiClient,
            AdminYetkiBelgesiOnayApiClient adminYetkiBelgesiOnayApiClient,
            AdminSubeApiClient adminSubeApiClient,
            AdminRaporApiClient adminRaporApiClient,
            YkcApiClient ykcApiClient,
            MarkaApiClient markaApiClient,
            UrunKategoriApiClient urunKategoriApiClient)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _aktifSirketService = aktifSirketService;
            _adminDashboardApiClient = adminDashboardApiClient;
            _adminKullaniciApiClient = adminKullaniciApiClient;
            _adminYetkiliServisApiClient = adminYetkiliServisApiClient;
            _adminYetkiBelgesiOnayApiClient = adminYetkiBelgesiOnayApiClient;
            _adminSubeApiClient = adminSubeApiClient;
            _adminRaporApiClient = adminRaporApiClient;
            _ykcApiClient = ykcApiClient;
            _markaApiClient = markaApiClient;
            _urunKategoriApiClient = urunKategoriApiClient;
        }

        private async Task<List<UrunKategori>> KullanilanKategorileriGetir()
        {
            return (await _urunKategoriApiClient.ListeAsync() ?? new List<UrunKategori>())
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .ToList();
        }

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _kullaniciOturumu.GetUserAsync(User);
        }

        private async Task<int> GetOnayBekleyenCount()
        {
            var kullanici = await GetCurrentUser();
            var sirketId = kullanici == null ? null : await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dashboard = kullanici == null ? null : await GetAdminDashboardOzetAsync(kullanici, sirketId);

            return dashboard?.OnayBekleyen ?? 0;
        }

        private async Task<AdminDashboardOzet?> GetAdminDashboardOzetAsync(AppKullanici kullanici, int? sirketId)
        {
            var cacheKey = $"AdminDashboardOzet:{sirketId?.ToString(CultureInfo.InvariantCulture) ?? "tum"}";
            if (HttpContext.Items.TryGetValue(cacheKey, out var cached))
                return cached as AdminDashboardOzet;

            AdminDashboardOzet? dashboard;
            try
            {
                dashboard = await _adminDashboardApiClient.GetirAsync(kullanici, sirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                return null;
            }

            if (dashboard != null)
                HttpContext.Items[cacheKey] = dashboard;

            return dashboard;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var kullanici = await GetCurrentUser();
            var sirketId = kullanici == null ? null : await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dashboard = kullanici == null ? null : await GetAdminDashboardOzetAsync(kullanici, sirketId);

            ViewBag.OnayBekleyen = dashboard?.OnayBekleyen ?? 0;
            ViewBag.SuresiBitecek = dashboard?.SuresiBitecek ?? 0;
            ViewBag.GenelSistemAdminMi = kullanici != null && await _aktifSirketService.GenelSistemAdminMi(kullanici);
            await next();
        }


    }
}


