using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class DagitimSirketApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ILogger<DagitimSirketApiClient> _logger;

        public DagitimSirketApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<DagitimSirketApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<List<Dag_Sirket>?> TumunuGetirAsync()
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Dagitim sirket liste");
                return null;
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/dagitim-sirket/liste",
                    new DagitimSirketListeIstek { TumunuGetir = true });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Dagitim sirket API liste cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Dagitim sirket liste");
                    return null;
                }

                var sirketler = await response.Content.ReadFromJsonAsync<List<DagitimSirketApiDto>>();
                return sirketler?
                    .Select(x => new Dag_Sirket
                    {
                        Id = x.Id,
                        SirketAdi = x.SirketAdi,
                        Il = x.Il,
                        Telefon = x.Telefon,
                        Email = x.Email,
                        Adres = x.Adres,
                        AktifMi = x.AktifMi
                    })
                    .OrderBy(x => x.SirketAdi)
                    .ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Dagitim sirket API liste cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Dagitim sirket liste");
                return null;
            }
        }

        private class DagitimSirketListeIstek
        {
            public bool TumunuGetir { get; set; }
        }

        private class DagitimSirketApiDto
        {
            public int Id { get; set; }
            public string? SirketAdi { get; set; }
            public string? Il { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public bool AktifMi { get; set; }
        }
    }
}
