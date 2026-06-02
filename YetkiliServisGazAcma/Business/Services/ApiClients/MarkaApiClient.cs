using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class MarkaApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ILogger<MarkaApiClient> _logger;

        public MarkaApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<MarkaApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<List<Ys_Marka>?> TumunuGetirAsync()
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Marka liste");
                return null;
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/marka/liste",
                    new MarkaListeIstek { TumunuGetir = true });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Marka API liste cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Marka liste");
                    return null;
                }

                var markalar = await response.Content.ReadFromJsonAsync<List<MarkaApiDto>>();
                return markalar?
                    .Select(x => new Ys_Marka
                    {
                        Id = x.Id,
                        MarkaAdi = x.MarkaAdi,
                        Aciklama = x.Aciklama,
                        AktifMi = x.AktifMi
                    })
                    .OrderBy(x => x.MarkaAdi)
                    .ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Marka API liste cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Marka liste");
                return null;
            }
        }

        private class MarkaListeIstek
        {
            public bool TumunuGetir { get; set; }
        }

        private class MarkaApiDto
        {
            public int Id { get; set; }
            public string? MarkaAdi { get; set; }
            public string? Aciklama { get; set; }
            public bool AktifMi { get; set; }
        }
    }
}
