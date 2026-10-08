using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class DagitimSirketApiClient
    {
        private readonly ApiHttpClient _api;

        public DagitimSirketApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<DagitimSirketApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<List<DagitimSirketApiDto>?> TumunuGetirAsync()
            => _api.PostAsync<DagitimSirketListeFiltreDto, List<DagitimSirketApiDto>>(
                null, "api/dagitim-sirket/liste", new() { TumunuGetir = true }, "Dagitim sirket liste");

        public Task<DagitimSirketApiDto?> GetirAsync(AppKullanici kullanici, int id)
            => _api.PostAsync<object, DagitimSirketApiDto>(
                kullanici,
                "api/dagitim-sirket/getir",
                new { Id = id },
                "Dagitim sirket getir");
    }
}
