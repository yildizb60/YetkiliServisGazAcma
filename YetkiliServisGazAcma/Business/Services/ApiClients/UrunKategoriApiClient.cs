using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class UrunKategoriApiClient
    {
        private readonly ApiHttpClient _api;

        public UrunKategoriApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<UrunKategoriApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, null, logger);
        }

        public Task<List<UrunKategoriApiDto>?> ListeAsync()
            => _api.PostAsync<object, List<UrunKategoriApiDto>>(
                null, "api/urun-kategorileri/liste", new { }, "Urun kategori liste");
    }
}
