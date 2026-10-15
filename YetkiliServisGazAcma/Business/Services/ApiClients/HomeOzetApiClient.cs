using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class HomeOzetApiClient
    {
        private readonly ApiHttpClient _api;

        public HomeOzetApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<HomeOzetApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, null, logger);
        }

        public Task<HomeOzetCevap?> GetirAsync()
            => _api.PostAsync<object, HomeOzetCevap>(null, "api/home/ozet", new { }, "Ana sayfa ozet");
    }

}
