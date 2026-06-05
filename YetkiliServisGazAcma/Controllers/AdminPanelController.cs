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
using System.Text;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("AdminPanel")]
    public partial class AdminPanelController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
        private readonly AktifSirketService _aktifSirketService;
        private readonly AdminDashboardApiClient _adminDashboardApiClient;
        private readonly AdminKullaniciApiClient _adminKullaniciApiClient;
        private readonly AdminYetkiliServisApiClient _adminYetkiliServisApiClient;
        private readonly AdminYetkiBelgesiOnayApiClient _adminYetkiBelgesiOnayApiClient;
        private readonly AdminSubeApiClient _adminSubeApiClient;
        private readonly AdminRaporApiClient _adminRaporApiClient;
        private readonly MarkaApiClient _markaApiClient;
        private readonly UrunKategoriApiClient _urunKategoriApiClient;

        public AdminPanelController(
            UserManager<AppKullanici> userManager,
            SehirFirmaKoduService sehirFirmaKoduService,
            AktifSirketService aktifSirketService,
            AdminDashboardApiClient adminDashboardApiClient,
            AdminKullaniciApiClient adminKullaniciApiClient,
            AdminYetkiliServisApiClient adminYetkiliServisApiClient,
            AdminYetkiBelgesiOnayApiClient adminYetkiBelgesiOnayApiClient,
            AdminSubeApiClient adminSubeApiClient,
            AdminRaporApiClient adminRaporApiClient,
            MarkaApiClient markaApiClient,
            UrunKategoriApiClient urunKategoriApiClient)
        {
            _userManager = userManager;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _aktifSirketService = aktifSirketService;
            _adminDashboardApiClient = adminDashboardApiClient;
            _adminKullaniciApiClient = adminKullaniciApiClient;
            _adminYetkiliServisApiClient = adminYetkiliServisApiClient;
            _adminYetkiBelgesiOnayApiClient = adminYetkiBelgesiOnayApiClient;
            _adminSubeApiClient = adminSubeApiClient;
            _adminRaporApiClient = adminRaporApiClient;
            _markaApiClient = markaApiClient;
            _urunKategoriApiClient = urunKategoriApiClient;
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
            return (await _urunKategoriApiClient.ListeAsync() ?? new List<UrunKategori>())
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

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _userManager.GetUserAsync(User);
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
            await next();
        }


    }
}


