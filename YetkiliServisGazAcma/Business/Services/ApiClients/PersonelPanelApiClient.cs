using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class PersonelPanelApiClient
    {
        private readonly ApiHttpClient _api;

        public PersonelPanelApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<PersonelPanelApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<PersonelDashboardDto?> DashboardAsync(AppKullanici kullanici, int? sirketId) =>
            _api.PostAsync<PersonelYetkilerimIstek, PersonelDashboardDto>(kullanici,
                "api/admin-panel/personel-dashboard", new PersonelYetkilerimIstek { SirketId = sirketId }, "Personel ana sayfa");

        public Task<PersonelRaporDto?> RaporAsync(AppKullanici kullanici, PersonelRaporFiltreDto filtre) =>
            _api.PostAsync<PersonelRaporFiltreDto, PersonelRaporDto>(kullanici,
                "api/admin-panel/personel-rapor", filtre, "Personel rapor ekrani", retryTransient: true);

        public async Task<List<string>?> YetkilerimAsync(AppKullanici kullanici, int? sirketId)
        {
            var cevap = await _api.PostAsync<PersonelYetkilerimIstek, PersonelYetkilerimCevap>(kullanici,
                "api/personel-panel/yetkilerim", new PersonelYetkilerimIstek { SirketId = sirketId }, "Personel yetkilerim");
            return cevap?.Yetkiler ?? new List<string>();
        }
    }
}
