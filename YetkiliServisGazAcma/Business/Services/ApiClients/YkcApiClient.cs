using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YkcApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<YkcApiClient> _logger;

        public YkcApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YkcApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public Task<YkcTalepListeSonuc?> TaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/talepler/liste",
                filtre,
                "Cihaz değişim talep listesi");
        }

        public Task<YkcTalepListeSonuc?> DogalgazMobileTaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/dogalgaz-mobile/talepler/liste",
                filtre,
                "Cihaz değişim doğalgaz mobile talep listesi");
        }

        public Task<YkcTalepListeSonuc?> Crm187TaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/crm187/talepler/liste",
                filtre,
                "Cihaz değişim CRM187 talep listesi");
        }

        public Task<YkcTalepDetayDto?> DetayAsync(AppKullanici kullanici, int id)
        {
            return PostAsync<YkcTalepGetirIstek, YkcTalepDetayDto>(
                kullanici,
                "api/ykc/talepler/getir",
                new YkcTalepGetirIstek { Id = id },
                "Cihaz değişim talep detay");
        }

        public Task<YkcTesisatSorguSonuc?> TesisatSorgulaAsync(AppKullanici kullanici, YkcTesisatSorguIstek istek)
        {
            return PostAsync<YkcTesisatSorguIstek, YkcTesisatSorguSonuc>(
                kullanici,
                "api/ykc/tesisat-sorgula",
                istek,
                "Cihaz degisim tesisat sorgula");
        }

        public Task<ApiDosyaSonuc?> Fr265WordAsync(AppKullanici kullanici, int id)
        {
            return PostFileAsync(
                kullanici,
                "api/ykc/talepler/fr265-word",
                new YkcTalepGetirIstek { Id = id },
                $"FR265_Cihaz_Degisim_Talebi_{id}.docx",
                "FR265 cihaz degisim formu Word");
        }

        public Task<YkcIslemSonuc?> OlusturAsync(AppKullanici kullanici, YkcTalepKaydetDto dto)
        {
            return PostAsync<YkcTalepKaydetDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/olustur",
                dto,
                "Cihaz değişim talebi oluştur");
        }

        public Task<YkcIslemSonuc?> AtamaYapAsync(AppKullanici kullanici, YkcAtamaKaydetDto dto)
        {
            return PostAsync<YkcAtamaKaydetDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/atama-yap",
                dto,
                "Cihaz değişim atama yap");
        }

        public Task<YkcIslemSonuc?> DurumGuncelleAsync(AppKullanici kullanici, YkcDurumGuncelleDto dto)
        {
            return PostAsync<YkcDurumGuncelleDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/durum-guncelle",
                dto,
                "Cihaz değişim durum güncelle");
        }

        public async Task<YkcIslemSonuc?> FormYukleAsync(
            AppKullanici kullanici,
            int talepId,
            IFormFile dosya,
            string? dosyaTuru)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Cihaz değişim form yükle");
                return default;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Cihaz değişim form yükle token");
                    return default;
                }

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(talepId.ToString()), "TalepId");
                if (!string.IsNullOrWhiteSpace(dosyaTuru))
                    form.Add(new StringContent(dosyaTuru), "DosyaTuru");

                var streamContent = new StreamContent(dosya.OpenReadStream());
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(dosya.ContentType) ? "application/octet-stream" : dosya.ContentType);
                form.Add(streamContent, "Dosya", dosya.FileName);

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/ykc/talepler/form-yukle");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = form;

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Cihaz değişim form yükle API çağrısında başarısız yanıt döndü. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Cihaz değişim form yükle");
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<YkcIslemSonuc>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Cihaz değişim form yükle API çağrısına ulaşılamadı.");
                ApiClientFallback.EnsureAllowed(_options, "Cihaz değişim form yükle");
                return default;
            }
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

        private async Task<ApiDosyaSonuc?> PostFileAsync<TRequest>(
            AppKullanici kullanici,
            string url,
            TRequest istek,
            string varsayilanDosyaAdi,
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
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("{Operasyon} API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}", operasyon, url, response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, operasyon);
                    return default;
                }

                return await ApiDosyaSonuc.FromResponseAsync(response, varsayilanDosyaAdi);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "{Operasyon} API cagrisina ulasilamadi. Url: {Url}", operasyon, url);
                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return default;
            }
        }
    }

    public class YkcTalepGetirIstek
    {
        public int Id { get; set; }
    }
}
