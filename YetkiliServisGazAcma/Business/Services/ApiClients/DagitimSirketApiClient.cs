using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class DagitimSirketApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<DagitimSirketApiClient> _logger;

        public DagitimSirketApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<DagitimSirketApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
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
                    .Select(x => x.ToEntity())
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

        public async Task<Dag_Sirket?> GetirAsync(AppKullanici kullanici, int id)
        {
            var cevap = await PostAsync<IdIstek, DagitimSirketApiDto>(
                kullanici,
                "api/dagitim-sirket/getir",
                new IdIstek { Id = id },
                "Dagitim sirket getir");

            return cevap?.ToEntity();
        }

        public Task<DagitimSirketIslemSonuc?> EkleAsync(AppKullanici kullanici, Dag_Sirket sirket)
        {
            return PostIslemAsync(
                kullanici,
                "api/dagitim-sirket/ekle",
                DagitimSirketKaydetIstek.FromEntity(sirket),
                "Dagitim sirket ekle");
        }

        public Task<DagitimSirketIslemSonuc?> GuncelleAsync(AppKullanici kullanici, Dag_Sirket sirket)
        {
            return PostIslemAsync(
                kullanici,
                "api/dagitim-sirket/guncelle",
                DagitimSirketKaydetIstek.FromEntity(sirket),
                "Dagitim sirket guncelle");
        }

        public Task<DagitimSirketIslemSonuc?> SilAsync(AppKullanici kullanici, int id)
        {
            return PostIslemAsync(
                kullanici,
                "api/dagitim-sirket/sil",
                new IdIstek { Id = id },
                "Dagitim sirket sil");
        }

        private async Task<DagitimSirketIslemSonuc?> PostIslemAsync<TRequest>(
            AppKullanici kullanici,
            string url,
            TRequest istek,
            string operasyon)
        {
            var cevap = await PostAsync<TRequest, DagitimSirketIslemCevap>(kullanici, url, istek, operasyon);
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

        private class DagitimSirketListeIstek
        {
            public bool TumunuGetir { get; set; }
        }

        private class IdIstek
        {
            public int Id { get; set; }
        }

        private class DagitimSirketKaydetIstek
        {
            public int? Id { get; set; }
            public string? SirketAdi { get; set; }
            public string? Il { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public bool AktifMi { get; set; } = true;

            public static DagitimSirketKaydetIstek FromEntity(Dag_Sirket sirket)
            {
                return new DagitimSirketKaydetIstek
                {
                    Id = sirket.Id > 0 ? sirket.Id : null,
                    SirketAdi = sirket.SirketAdi,
                    Il = sirket.Il,
                    Telefon = sirket.Telefon,
                    Email = sirket.Email,
                    Adres = sirket.Adres,
                    AktifMi = sirket.AktifMi
                };
            }
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

            public Dag_Sirket ToEntity()
            {
                return new Dag_Sirket
                {
                    Id = Id,
                    SirketAdi = SirketAdi,
                    Il = Il,
                    Telefon = Telefon,
                    Email = Email,
                    Adres = Adres,
                    AktifMi = AktifMi
                };
            }
        }

        private class DagitimSirketIslemCevap
        {
            public bool Basarili { get; set; }
            public string? Mesaj { get; set; }

            public DagitimSirketIslemSonuc ToSonuc()
            {
                return new DagitimSirketIslemSonuc
                {
                    Basarili = Basarili,
                    Mesaj = Mesaj
                };
            }
        }
    }

    public class DagitimSirketIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }
}
