using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        private readonly AktifSirketService _aktifSirket;

        public YkcApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YkcApiClient> logger,
            AktifSirketService aktifSirket)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
            _aktifSirket = aktifSirket;
        }

        public Task<YkcTalepListeSonuc?> TaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/talepler/liste",
                filtre,
                "Cihaz değişim talep listesi",
                retryTransient: true);
        }

        public Task<YkcRaporSonuc?> RaporAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return PostAsync<YkcTalepListeFiltre, YkcRaporSonuc>(
                kullanici,
                "api/ykc/talepler/rapor",
                filtre,
                "Cihaz değişim raporu",
                retryTransient: true);
        }

        public Task<ApiDosyaSonuc?> RaporPdfAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
            => PostFileAsync(
                kullanici,
                "api/ykc/talepler/rapor/pdf",
                filtre,
                "Cihaz_Degisim_Raporu.pdf",
                "Cihaz değişim raporu PDF");

        public Task<ApiDosyaSonuc?> RaporExcelAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
            => PostFileAsync(
                kullanici,
                "api/ykc/talepler/rapor/excel",
                filtre,
                "Cihaz_Degisim_Raporu.xlsx",
                "Cihaz değişim raporu Excel");

        public async Task<YkcDashboardOzetDto?> DashboardOzetAsync(AppKullanici kullanici)
        {
            return await PostAsync<object, YkcDashboardOzetDto>(
                kullanici,
                "api/ykc/dashboard/ozet",
                new { AktifSirketId = await _aktifSirket.AktifSirketIdAsync(kullanici) },
                "Cihaz değişim dashboard özeti",
                retryTransient: true);
        }

        public Task<YkcImzaEntegrasyonDto?> ImzaEntegrasyonBilgisiAsync(AppKullanici kullanici)
        {
            return PostAsync<object, YkcImzaEntegrasyonDto>(
                kullanici,
                "api/ykc/imza/entegrasyon",
                new { },
                "YKC dijital imza entegrasyon bilgisi",
                retryTransient: true);
        }

        public Task<YkcTalepListeSonuc?> DogalgazMobileTaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/dogalgaz-mobile/talepler/liste",
                filtre,
                "Cihaz değişim doğalgaz mobile talep listesi",
                retryTransient: true);
        }

        public Task<YkcTalepListeSonuc?> Crm187TaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/crm187/talepler/liste",
                filtre,
                "Cihaz değişim CRM187 talep listesi",
                retryTransient: true);
        }

        public Task<YkcTalepDetayDto?> DetayAsync(AppKullanici kullanici, int id, bool formVerisi = false)
        {
            return PostAsync<YkcTalepGetirIstek, YkcTalepDetayDto>(
                kullanici,
                formVerisi ? "api/ykc/talepler/form-verisi" : "api/ykc/talepler/getir",
                new YkcTalepGetirIstek { Id = id },
                "Cihaz değişim talep detay",
                retryTransient: true);
        }

        public Task<YkcTesisatSorguSonuc?> TesisatSorgulaAsync(AppKullanici kullanici, YkcTesisatSorguIstek istek)
        {
            return PostAsync<YkcTesisatSorguIstek, YkcTesisatSorguSonuc>(
                kullanici,
                "api/ykc/tesisat-sorgula",
                istek,
                "Cihaz degisim tesisat sorgula",
                retryTransient: true);
        }

        public async Task<YkcTakvimSonuc?> TakvimAsync(AppKullanici kullanici, YkcTakvimFiltre filtre)
        {
            filtre.AktifSirketId = await _aktifSirket.AktifSirketIdAsync(kullanici);
            return await PostAsync<YkcTakvimFiltre, YkcTakvimSonuc>(kullanici, "api/ykc/takvim", filtre, "Randevu takvimi", retryTransient: true);
        }

        public Task<List<YkcEkipSecenegi>?> EkiplerAsync(AppKullanici kullanici, int id)
            => PostAsync<object, List<YkcEkipSecenegi>>(
                kullanici,
                "api/ykc/talepler/ekipler",
                new { Id = id },
                "Bölge ekipleri",
                retryTransient: true);

        public Task<ApiDosyaSonuc?> FormPdfAsync(AppKullanici kullanici, int talepId)
            => PostFileAsync(kullanici, "api/ykc/talepler/form-pdf",
                new YkcTalepGetirIstek { Id = talepId }, $"Cihaz_Degisim_Formu_{talepId}.pdf", "Cihaz değişim formu PDF");

        public Task<ApiDosyaSonuc?> DosyaIndirAsync(AppKullanici kullanici, int dosyaId)
        {
            return PostFileAsync(
                kullanici,
                "api/ykc/talepler/dosya-indir",
                new YkcDosyaGetirIstek { Id = dosyaId },
                $"YKC_Form_Dosyasi_{dosyaId}",
                "Cihaz degisim form dosyasi indir");
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

        public Task<YkcIslemSonuc?> KontrollerKaydetAsync(AppKullanici kullanici, YkcKontrolKaydetDto dto)
        {
            return PostAsync<YkcKontrolKaydetDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/kontroller-kaydet",
                dto,
                "Cihaz değişim FR265 kontrol kaydet");
        }

        public Task<YkcIslemSonuc?> ImzayaGonderAsync(AppKullanici kullanici, int talepId)
        {
            return PostAsync<YkcTalepGetirIstek, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/imzaya-gonder",
                new YkcTalepGetirIstek { Id = talepId },
                "YKC FR265 imzaya gönder");
        }

        public Task<YkcIslemSonuc?> ImzaDurumSorgulaAsync(AppKullanici kullanici, int talepId)
        {
            return PostAsync<YkcTalepGetirIstek, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/imza-durum-sorgula",
                new YkcTalepGetirIstek { Id = talepId },
                "YKC FR265 imza durumu sorgula");
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
            string operasyon,
            bool retryTransient = false)
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

                var denemeSayisi = retryTransient ? 10 : 1;
                for (var deneme = 1; deneme <= denemeSayisi; deneme++)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        request.Content = JsonContent.Create(istek);

                        using var response = await _httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                            return await response.Content.ReadFromJsonAsync<TResponse>();

                        if (retryTransient && IsTransientStatusCode(response.StatusCode) && deneme < denemeSayisi)
                        {
                            _logger.LogWarning(
                                "{Operasyon} API cagrisinda gecici hata alindi. Url: {Url}, StatusCode: {StatusCode}, Deneme: {Deneme}/{ToplamDeneme}",
                                operasyon,
                                url,
                                response.StatusCode,
                                deneme,
                                denemeSayisi);
                            await Task.Delay(GeciciHataBeklemeSuresi(deneme));
                            continue;
                        }

                        var hataCevabi = await TryReadResponseAsync<TResponse>(response);
                        if (hataCevabi != null)
                            return hataCevabi;

                        var hataMetni = await SafeReadBodyAsync(response);
                        _logger.LogWarning("{Operasyon} API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}, Body: {Body}", operasyon, url, response.StatusCode, hataMetni);
                        ApiClientFallback.EnsureAllowed(_options, operasyon);
                        return default;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
                    {
                        if (retryTransient && deneme < denemeSayisi)
                        {
                            _logger.LogWarning(
                                ex,
                                "{Operasyon} API cagrisina gecici olarak ulasilamadi. Url: {Url}, Deneme: {Deneme}/{ToplamDeneme}",
                                operasyon,
                                url,
                                deneme,
                                denemeSayisi);
                            await Task.Delay(GeciciHataBeklemeSuresi(deneme));
                            continue;
                        }

                        _logger.LogWarning(ex, "{Operasyon} API cagrisina ulasilamadi. Url: {Url}", operasyon, url);
                        ApiClientFallback.EnsureAllowed(_options, operasyon);
                        return default;
                    }
                }

                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return default;
            }
            catch (ApiIntegrationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "{Operasyon} API cagrisina ulasilamadi. Url: {Url}", operasyon, url);
                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return default;
            }
        }

        private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
        {
            var kod = (int)statusCode;
            return statusCode is System.Net.HttpStatusCode.RequestTimeout
                or System.Net.HttpStatusCode.TooManyRequests
                || kod >= 500;
        }

        private static TimeSpan GeciciHataBeklemeSuresi(int deneme)
            => TimeSpan.FromMilliseconds(Math.Min(500 * deneme, 2000));

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

        private static async Task<TResponse?> TryReadResponseAsync<TResponse>(HttpResponseMessage response)
        {
            try
            {
                if (response.Content.Headers.ContentLength == 0)
                    return default;

                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch (JsonException)
            {
                return default;
            }
            catch (NotSupportedException)
            {
                return default;
            }
        }

        private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response)
        {
            try
            {
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return null;
            }
        }
    }

    public class YkcTalepGetirIstek
    {
        public int Id { get; set; }
    }

    public class YkcDosyaGetirIstek
    {
        public int Id { get; set; }
    }
}
