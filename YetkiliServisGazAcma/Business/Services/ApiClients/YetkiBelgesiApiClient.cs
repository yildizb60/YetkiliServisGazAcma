using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiBelgesiApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<YetkiBelgesiApiClient> _logger;

        public YetkiBelgesiApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YetkiBelgesiApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public Task<YetkiBelgesiIslemSonuc?> OnaylaAsync(AppKullanici kullanici, int id)
        {
            return PostAsync<IdIstek>("api/sertifika/onayla", kullanici, new IdIstek { Id = id });
        }

        public Task<YetkiBelgesiIslemSonuc?> ReddetAsync(AppKullanici kullanici, int id, string? gerekce)
        {
            return PostAsync<YetkiBelgesiRedIstek>(
                "api/sertifika/reddet",
                kullanici,
                new YetkiBelgesiRedIstek
                {
                    Id = id,
                    Gerekce = string.IsNullOrWhiteSpace(gerekce) ? "Belirtilmedi." : gerekce.Trim()
                });
        }

        private async Task<YetkiBelgesiIslemSonuc?> PostAsync<TRequest>(string url, AppKullanici kullanici, TRequest istek)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi islem");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi islem token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(istek);

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Yetki belgesi API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}", url, response.StatusCode);

                    YetkiBelgesiIslemCevap? hata = null;
                    try
                    {
                        hata = await response.Content.ReadFromJsonAsync<YetkiBelgesiIslemCevap>();
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or JsonException)
                    {
                        hata = null;
                    }

                    if (hata != null)
                        return hata.ToSonuc();

                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        return new YetkiBelgesiIslemSonuc { Basarili = false, Mesaj = "Bu yetki belgesi icin islem yetkiniz yok." };

                    ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi islem");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<YetkiBelgesiIslemCevap>();
                return sonuc?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Yetki belgesi API cagrisina ulasilamadi. Url: {Url}", url);
                ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi islem");
                return null;
            }
        }

        private class IdIstek
        {
            public int Id { get; set; }
        }

        private class YetkiBelgesiRedIstek
        {
            public int Id { get; set; }
            public string? Gerekce { get; set; }
        }

        private class YetkiBelgesiIslemCevap
        {
            public bool Basarili { get; set; }
            public string? Mesaj { get; set; }

            public YetkiBelgesiIslemSonuc ToSonuc()
            {
                return new YetkiBelgesiIslemSonuc
                {
                    Basarili = Basarili,
                    Mesaj = Mesaj
                };
            }
        }
    }

    public class YetkiBelgesiIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }
}
