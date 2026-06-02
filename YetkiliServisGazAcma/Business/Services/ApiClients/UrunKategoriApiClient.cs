using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class UrunKategoriApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ILogger<UrunKategoriApiClient> _logger;

        public UrunKategoriApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<UrunKategoriApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<List<UrunKategori>?> ListeAsync()
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Urun kategori liste");
                return null;
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/urun-kategorileri/liste", new { });
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Urun kategori API liste cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Urun kategori liste");
                    return null;
                }

                var kategoriler = await response.Content.ReadFromJsonAsync<List<UrunKategoriApiDto>>();
                return kategoriler?
                    .Select(x => new UrunKategori
                    {
                        Id = x.Id,
                        Ad = x.Ad,
                        IconUrl = x.IconUrl,
                        SiraNo = x.SiraNo,
                        AktifMi = x.AktifMi
                    })
                    .OrderBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Urun kategori API liste cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Urun kategori liste");
                return null;
            }
        }

        private class UrunKategoriApiDto
        {
            public int Id { get; set; }
            public string? Ad { get; set; }
            public string? IconUrl { get; set; }
            public int SiraNo { get; set; }
            public bool AktifMi { get; set; }
        }
    }
}
