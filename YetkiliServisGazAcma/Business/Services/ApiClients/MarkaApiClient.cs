using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class MarkaApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<MarkaApiClient> _logger;

        public MarkaApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<MarkaApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
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
                    .Select(x => x.ToEntity())
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

        public async Task<Ys_Marka?> GetirAsync(AppKullanici kullanici, int id)
        {
            var cevap = await PostAsync<IdIstek, MarkaApiDto>(
                kullanici,
                "api/marka/getir",
                new IdIstek { Id = id },
                "Marka getir");

            return cevap?.ToEntity();
        }

        public Task<MarkaIslemSonuc?> EkleAsync(AppKullanici kullanici, Ys_Marka marka)
        {
            return PostIslemAsync(
                kullanici,
                "api/marka/ekle",
                MarkaKaydetIstek.FromEntity(marka),
                "Marka ekle");
        }

        public Task<MarkaIslemSonuc?> GuncelleAsync(AppKullanici kullanici, Ys_Marka marka)
        {
            return PostIslemAsync(
                kullanici,
                "api/marka/guncelle",
                MarkaKaydetIstek.FromEntity(marka),
                "Marka guncelle");
        }

        public Task<MarkaIslemSonuc?> SilAsync(AppKullanici kullanici, int id)
        {
            return PostIslemAsync(
                kullanici,
                "api/marka/sil",
                new IdIstek { Id = id },
                "Marka sil");
        }

        private async Task<MarkaIslemSonuc?> PostIslemAsync<TRequest>(
            AppKullanici kullanici,
            string url,
            TRequest istek,
            string operasyon)
        {
            var cevap = await PostAsync<TRequest, MarkaIslemCevap>(kullanici, url, istek, operasyon);
            return cevap?.ToSonuc();
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

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, $"{operasyon} token");
                    return default;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(istek);

                using var response = await _httpClient.SendAsync(request);
                TResponse? cevap = default;
                try
                {
                    cevap = await response.Content.ReadFromJsonAsync<TResponse>();
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException)
                {
                    cevap = default;
                }

                if (cevap != null)
                    return cevap;

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("{Operasyon} API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}", operasyon, url, response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, operasyon);
                }

                return default;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "{Operasyon} API cagrisina ulasilamadi. Url: {Url}", operasyon, url);
                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return default;
            }
        }

        private class MarkaListeIstek
        {
            public bool TumunuGetir { get; set; }
        }

        private class IdIstek
        {
            public int Id { get; set; }
        }

        private class MarkaKaydetIstek
        {
            public int? Id { get; set; }
            public string? MarkaAdi { get; set; }
            public string? Aciklama { get; set; }
            public bool AktifMi { get; set; } = true;

            public static MarkaKaydetIstek FromEntity(Ys_Marka marka)
            {
                return new MarkaKaydetIstek
                {
                    Id = marka.Id > 0 ? marka.Id : null,
                    MarkaAdi = marka.MarkaAdi,
                    Aciklama = marka.Aciklama,
                    AktifMi = marka.AktifMi
                };
            }
        }

        private class MarkaApiDto
        {
            public int Id { get; set; }
            public string? MarkaAdi { get; set; }
            public string? Aciklama { get; set; }
            public bool AktifMi { get; set; }

            public Ys_Marka ToEntity()
            {
                return new Ys_Marka
                {
                    Id = Id,
                    MarkaAdi = MarkaAdi,
                    Aciklama = Aciklama,
                    AktifMi = AktifMi
                };
            }
        }

        private class MarkaIslemCevap
        {
            public bool Basarili { get; set; }
            public string? Mesaj { get; set; }

            public MarkaIslemSonuc ToSonuc()
            {
                return new MarkaIslemSonuc
                {
                    Basarili = Basarili,
                    Mesaj = Mesaj
                };
            }
        }
    }

    public class MarkaIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }
}
