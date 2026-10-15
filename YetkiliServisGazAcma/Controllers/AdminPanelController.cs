using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("AdminPanel")]
    public partial class AdminPanelController : Controller
    {
        private readonly ApiKullaniciOturumu _kullaniciOturumu;
        private readonly AktifSirketService _aktifSirketService;
        private readonly AdminDashboardApiClient _adminDashboardApiClient;
        private readonly AdminKullaniciApiClient _adminKullaniciApiClient;
        private readonly AdminYetkiliServisApiClient _adminYetkiliServisApiClient;
        private readonly AdminYetkiBelgesiOnayApiClient _adminYetkiBelgesiOnayApiClient;
        private readonly AdminSubeApiClient _adminSubeApiClient;
        private readonly AdminRaporApiClient _adminRaporApiClient;
        private readonly YkcApiClient _ykcApiClient;

        public AdminPanelController(
            ApiKullaniciOturumu kullaniciOturumu,
            AktifSirketService aktifSirketService,
            AdminDashboardApiClient adminDashboardApiClient,
            AdminKullaniciApiClient adminKullaniciApiClient,
            AdminYetkiliServisApiClient adminYetkiliServisApiClient,
            AdminYetkiBelgesiOnayApiClient adminYetkiBelgesiOnayApiClient,
            AdminSubeApiClient adminSubeApiClient,
            AdminRaporApiClient adminRaporApiClient,
            YkcApiClient ykcApiClient)
        {
            _kullaniciOturumu = kullaniciOturumu;
            _aktifSirketService = aktifSirketService;
            _adminDashboardApiClient = adminDashboardApiClient;
            _adminKullaniciApiClient = adminKullaniciApiClient;
            _adminYetkiliServisApiClient = adminYetkiliServisApiClient;
            _adminYetkiBelgesiOnayApiClient = adminYetkiBelgesiOnayApiClient;
            _adminSubeApiClient = adminSubeApiClient;
            _adminRaporApiClient = adminRaporApiClient;
            _ykcApiClient = ykcApiClient;
        }

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _kullaniciOturumu.GetUserAsync(User);
        }

        private async Task<AdminDashboardApiDto?> GetAdminDashboardOzetAsync(AppKullanici kullanici, int? sirketId)
        {
            AdminDashboardApiDto? dashboard = null;
            try
            {
                dashboard = await _adminDashboardApiClient.GetirAsync(kullanici, sirketId);
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return dashboard;
        }

    }
}
