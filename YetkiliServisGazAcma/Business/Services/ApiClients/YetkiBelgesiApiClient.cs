using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
            return PostAsync<IdIstek>("api/yetki-belgesi/onayla", kullanici, new IdIstek { Id = id });
        }

        public Task<YetkiBelgesiFirmaEkraniSonuc?> FirmaEkraniAsync(AppKullanici kullanici, int firmaId)
        {
            return PostForResponseAsync<IdIstek, YetkiBelgesiFirmaEkraniCevap>(
                "api/yetki-belgesi/firma-ekrani",
                kullanici,
                new IdIstek { Id = firmaId },
                "Yetki belgesi firma ekrani")
                .ContinueWith(t => t.Result?.ToSonuc());
        }

        public Task<YetkiBelgesiOnayEkraniSonuc?> OnayEkraniAsync(AppKullanici kullanici, int? sirketId)
        {
            return PostForResponseAsync<YetkiBelgesiFiltreIstek, YetkiBelgesiOnayEkraniCevap>(
                "api/yetki-belgesi/onay-ekrani",
                kullanici,
                new YetkiBelgesiFiltreIstek { SirketId = sirketId },
                "Yetki belgesi onay ekrani")
                .ContinueWith(t => t.Result?.ToSonuc());
        }

        public Task<YetkiBelgesiIslemSonuc?> SilAsync(AppKullanici kullanici, int id)
        {
            return PostAsync<IdIstek>("api/yetki-belgesi/sil", kullanici, new IdIstek { Id = id });
        }

        public async Task<YetkiBelgesiIslemSonuc?> YukleAsync(
            AppKullanici kullanici,
            int firmaId,
            IFormFile dosya,
            DateTime bitisTarihi,
            DateTime? baslangicTarihi)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi yukleme");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi yukleme token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/yetki-belgesi/yukle");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(firmaId.ToString(CultureInfo.InvariantCulture)), "FirmaId");
                form.Add(new StringContent(bitisTarihi.ToString("O", CultureInfo.InvariantCulture)), "BitisTarihi");
                if (baslangicTarihi.HasValue)
                {
                    form.Add(
                        new StringContent(baslangicTarihi.Value.ToString("O", CultureInfo.InvariantCulture)),
                        "BaslangicTarihi");
                }

                var fileContent = new StreamContent(dosya.OpenReadStream());
                if (!string.IsNullOrWhiteSpace(dosya.ContentType))
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(dosya.ContentType);

                form.Add(fileContent, "Dosya", dosya.FileName);
                request.Content = form;

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Yetki belgesi yukleme API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);

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

                    ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi yukleme");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<YetkiBelgesiIslemCevap>();
                return sonuc?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Yetki belgesi yukleme API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Yetki belgesi yukleme");
                return null;
            }
        }

        public Task<YetkiBelgesiIslemSonuc?> ReddetAsync(AppKullanici kullanici, int id, string? gerekce)
        {
            return PostAsync<YetkiBelgesiRedIstek>(
                "api/yetki-belgesi/reddet",
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

        private async Task<TResponse?> PostForResponseAsync<TRequest, TResponse>(
            string url,
            AppKullanici kullanici,
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

                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        return default;

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

        private class IdIstek
        {
            public int Id { get; set; }
        }

        private class YetkiBelgesiFiltreIstek
        {
            public int? SirketId { get; set; }
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

        private class YetkiBelgesiFirmaEkraniCevap
        {
            public YetkiBelgesiFirmaCevap? Firma { get; set; }
            public List<YetkiBelgesiCevap> Belgeler { get; set; } = new();
            public List<string> Bildirimler { get; set; } = new();

            public YetkiBelgesiFirmaEkraniSonuc ToSonuc()
            {
                return new YetkiBelgesiFirmaEkraniSonuc
                {
                    Firma = Firma?.ToEntity(),
                    Belgeler = Belgeler.Select(x => x.ToEntity()).ToList(),
                    Bildirimler = Bildirimler
                };
            }
        }

        private class YetkiBelgesiOnayEkraniCevap
        {
            public List<YetkiBelgesiCevap> Bekleyenler { get; set; } = new();
            public List<YetkiBelgesiCevap> Onaylananlar { get; set; } = new();
            public List<YetkiBelgesiCevap> Reddedilenler { get; set; } = new();

            public YetkiBelgesiOnayEkraniSonuc ToSonuc()
            {
                return new YetkiBelgesiOnayEkraniSonuc
                {
                    Bekleyenler = Bekleyenler.Select(x => x.ToEntity()).ToList(),
                    Onaylananlar = Onaylananlar.Select(x => x.ToEntity()).ToList(),
                    Reddedilenler = Reddedilenler.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class YetkiBelgesiFirmaCevap
        {
            public int Id { get; set; }
            public string? FirmaAdi { get; set; }
            public string? YetkiliKisi { get; set; }
            public string? VergiNo { get; set; }
            public string? FaaliyetIli { get; set; }

            public Ys_Firma ToEntity()
            {
                return new Ys_Firma
                {
                    Id = Id,
                    FirmaAdi = FirmaAdi,
                    YetkiliKisi = YetkiliKisi,
                    VergiNo = VergiNo,
                    FaaliyetIli = FaaliyetIli
                };
            }
        }

        private class YetkiBelgesiCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public string? FirmaAdi { get; set; }
            public int? SirketId { get; set; }
            public string? SirketAdi { get; set; }
            public string? DosyaYolu { get; set; }
            public int Durum { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
            public DateTime? YetkiBelgesiBitisTarihi { get; set; }
            public DateTime? OnayTarihi { get; set; }
            public string? OnaylayanKullanici { get; set; }
            public string? RedGerekce { get; set; }

            public Ys_YetkiBelgesi ToEntity()
            {
                return new Ys_YetkiBelgesi
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    Firma = new Ys_Firma
                    {
                        Id = FirmaId,
                        FirmaAdi = FirmaAdi,
                        SirketId = SirketId ?? 0,
                        Sirket = string.IsNullOrWhiteSpace(SirketAdi)
                            ? null
                            : new Dag_Sirket { Id = SirketId ?? 0, SirketAdi = SirketAdi }
                    },
                    DosyaYolu = DosyaYolu,
                    Durum = Durum,
                    OlusturmaTarihi = OlusturmaTarihi,
                    YetkiBelgesiBaslangicTarihi = YetkiBelgesiBaslangicTarihi,
                    YetkiBelgesiBitisTarihi = YetkiBelgesiBitisTarihi ?? DateTime.MinValue,
                    OnayTarihi = OnayTarihi,
                    OnaylayanKullanici = OnaylayanKullanici,
                    RedGerekce = RedGerekce
                };
            }
        }
    }

    public class YetkiBelgesiIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }

    public class YetkiBelgesiFirmaEkraniSonuc
    {
        public Ys_Firma? Firma { get; set; }
        public List<Ys_YetkiBelgesi> Belgeler { get; set; } = new();
        public List<string> Bildirimler { get; set; } = new();
    }

    public class YetkiBelgesiOnayEkraniSonuc
    {
        public List<Ys_YetkiBelgesi> Bekleyenler { get; set; } = new();
        public List<Ys_YetkiBelgesi> Onaylananlar { get; set; } = new();
        public List<Ys_YetkiBelgesi> Reddedilenler { get; set; } = new();
    }
}
