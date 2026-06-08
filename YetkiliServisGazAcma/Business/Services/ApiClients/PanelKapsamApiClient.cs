using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class PanelKapsamApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<PanelKapsamApiClient> _logger;

        public PanelKapsamApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<PanelKapsamApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<List<Dag_Sirket>?> KullaniciSirketleriAsync(AppKullanici kullanici)
        {
            var cevap = await PostAsync<PanelBosIstek, List<PanelSirketCevap>>(
                kullanici,
                "api/panel-kapsam/sirketler",
                new PanelBosIstek(),
                "Panel sirket kapsam listesi");

            return cevap?.Select(x => x.ToEntity()).ToList();
        }

        public Task<PanelKimlikApiSonuc?> PanelKimlikAsync(AppKullanici kullanici, int? aktifSirketId)
        {
            return PostAsync<PanelKimlikIstek, PanelKimlikApiSonuc>(
                kullanici,
                "api/panel-kapsam/kimlik",
                new PanelKimlikIstek { AktifSirketId = aktifSirketId },
                "Panel kimlik bilgisi");
        }

        private async Task<TResponse?> PostAsync<TRequest, TResponse>(
            AppKullanici kullanici,
            string url,
            TRequest istek,
            string operasyon)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return default;
            }

            var token = await _tokenService.OlusturAsync(kullanici);
            if (string.IsNullOrWhiteSpace(token))
            {
                ApiClientFallback.EnsureAllowed(_options, $"{operasyon} token");
                return default;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(istek);

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("{Operasyon} API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}", operasyon, url, response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, operasyon);
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "{Operasyon} API cagrisina ulasilamadi. Url: {Url}", operasyon, url);
                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return default;
            }
        }

        private class PanelBosIstek
        {
        }

        private class PanelKimlikIstek
        {
            public int? AktifSirketId { get; set; }
        }

        private class PanelSirketCevap
        {
            public int Id { get; set; }
            public string? SirketAdi { get; set; }
            public string? Il { get; set; }
            public bool AktifMi { get; set; }

            public Dag_Sirket ToEntity()
            {
                return new Dag_Sirket
                {
                    Id = Id,
                    SirketAdi = SirketAdi,
                    Il = Il,
                    AktifMi = AktifMi
                };
            }
        }
    }

    public class PanelKimlikApiSonuc
    {
        public string? SirketAdi { get; set; }
        public string? Sehir { get; set; }
        public string? FirmaKodu { get; set; }
    }
}
