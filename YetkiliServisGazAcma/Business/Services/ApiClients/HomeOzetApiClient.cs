using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class HomeOzetApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ILogger<HomeOzetApiClient> _logger;

        public HomeOzetApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<HomeOzetApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<HomeOzetCevap?> GetirAsync()
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Ana sayfa ozet");
                return null;
            }

            try
            {
                using var response = await _httpClient.PostAsync("api/home/ozet", JsonContent.Create(new { }));
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ana sayfa ozet API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Ana sayfa ozet");
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<HomeOzetCevap>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Ana sayfa ozet API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Ana sayfa ozet");
                return null;
            }
        }
    }

    public class HomeOzetCevap
    {
        public int ServisCount { get; set; }
        public int DevreyeCount { get; set; }
        public int YetkiBelgesiCount { get; set; }
        public double ZamanindaOran { get; set; }
    }
}
