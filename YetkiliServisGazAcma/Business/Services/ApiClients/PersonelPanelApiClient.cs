using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class PersonelPanelApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<PersonelPanelApiClient> _logger;

        public PersonelPanelApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<PersonelPanelApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<List<string>?> YetkilerimAsync(AppKullanici kullanici, int? sirketId)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Personel yetkilerim");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Personel yetkilerim token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/personel-panel/yetkilerim");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new PersonelYetkilerimIstek { SirketId = sirketId });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Personel yetkilerim API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Personel yetkilerim");
                    return null;
                }

                var cevap = await response.Content.ReadFromJsonAsync<PersonelYetkilerimCevap>();
                return cevap?.Yetkiler ?? new List<string>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Personel yetkilerim API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Personel yetkilerim");
                return null;
            }
        }

        private class PersonelYetkilerimIstek
        {
            public int? SirketId { get; set; }
        }

        private class PersonelYetkilerimCevap
        {
            public List<string> Yetkiler { get; set; } = new();
        }
    }
}
