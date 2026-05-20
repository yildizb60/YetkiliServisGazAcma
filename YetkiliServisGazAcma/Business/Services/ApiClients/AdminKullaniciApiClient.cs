using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminKullaniciApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<AdminKullaniciApiClient> _logger;

        public AdminKullaniciApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminKullaniciApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<List<AppKullanici>?> ListeleAsync(
            AppKullanici kullanici,
            int? sirketId,
            string? q,
            string? tip,
            string? durum,
            string? bagli)
        {
            if (!_options.Enabled)
                return null;

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                    return null;

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/kullanicilar/liste");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminKullaniciListeIstek
                {
                    SirketId = sirketId,
                    Q = q,
                    Tip = tip,
                    Durum = durum,
                    Bagli = bagli
                });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin kullanici liste API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    return null;
                }

                var kullanicilar = await response.Content.ReadFromJsonAsync<List<AdminKullaniciListeCevap>>();
                return kullanicilar?.Select(x => x.ToEntity()).ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin kullanici liste API cagrisina ulasilamadi. MVC eski servis yoluyla devam edecek.");
                return null;
            }
        }

        private class AdminKullaniciListeIstek
        {
            public int? SirketId { get; set; }
            public string? Q { get; set; }
            public string? Tip { get; set; }
            public string? Durum { get; set; }
            public string? Bagli { get; set; }
        }

        private class AdminKullaniciListeCevap
        {
            public string Id { get; set; } = string.Empty;
            public string? AdSoyad { get; set; }
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public int KullaniciTipi { get; set; }
            public bool AktifMi { get; set; }
            public int? SirketId { get; set; }
            public string? SirketAdi { get; set; }
            public int? FirmaId { get; set; }
            public string? FirmaAdi { get; set; }
            public string? FirmaYetkiliKisi { get; set; }
            public string? FirmaEmail { get; set; }
            public string? FirmaTelefon { get; set; }

            public AppKullanici ToEntity()
            {
                return new AppKullanici
                {
                    Id = Id,
                    AdSoyad = AdSoyad,
                    Email = Email,
                    UserName = Email,
                    PhoneNumber = PhoneNumber,
                    KullaniciTipi = KullaniciTipi,
                    AktifMi = AktifMi,
                    SirketId = SirketId,
                    Sirket = SirketId.HasValue
                        ? new Dag_Sirket { Id = SirketId.Value, SirketAdi = SirketAdi }
                        : null,
                    FirmaId = FirmaId,
                    Firma = FirmaId.HasValue
                        ? new Ys_Firma
                        {
                            Id = FirmaId.Value,
                            FirmaAdi = FirmaAdi,
                            YetkiliKisi = FirmaYetkiliKisi,
                            Email = FirmaEmail,
                            Telefon = FirmaTelefon
                        }
                        : null
                };
            }
        }
    }
}
