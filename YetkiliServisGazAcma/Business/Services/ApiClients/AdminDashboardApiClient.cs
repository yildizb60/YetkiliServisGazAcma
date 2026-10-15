using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminDashboardApiClient
    {
        private readonly ApiHttpClient _api;

        public AdminDashboardApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminDashboardApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<AdminDashboardApiDto?> GetirAsync(AppKullanici kullanici, int? sirketId)
            => _api.PostAsync<AdminDashboardFiltreDto, AdminDashboardApiDto>(kullanici,
                "api/admin-panel/dashboard", new() { SirketId = sirketId }, "Admin dashboard");

        public Task<PanelBildirimOzeti?> BildirimOzetiAsync(AppKullanici kullanici, int? sirketId)
            => _api.PostAsync<AdminDashboardFiltreDto, PanelBildirimOzeti>(kullanici,
                "api/admin-panel/bildirim-ozeti", new() { SirketId = sirketId }, "Panel bildirim ozeti");
    }
}
