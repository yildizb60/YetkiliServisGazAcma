using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminYetkiBelgesiOnayApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<AdminYetkiBelgesiOnayApiClient> _logger;

        public AdminYetkiBelgesiOnayApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminYetkiBelgesiOnayApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<AdminYetkiBelgesiOnaySonuc?> ListeleAsync(AppKullanici kullanici, int? sirketId)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi onay listesi");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi onay listesi token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/yetki-belgeleri/onay-listesi");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminYetkiBelgesiOnayFiltreIstek { SirketId = sirketId });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin yetki belgesi onay listesi API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi onay listesi");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<AdminYetkiBelgesiOnayListeCevap>();
                return sonuc?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin yetki belgesi onay listesi API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi onay listesi");
                return null;
            }
        }

        private class AdminYetkiBelgesiOnayFiltreIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminYetkiBelgesiOnayListeCevap
        {
            public List<AdminYetkiBelgesiOnayCevap> Bekleyenler { get; set; } = new();
            public List<AdminYetkiBelgesiOnayCevap> Onaylananlar { get; set; } = new();
            public List<AdminYetkiBelgesiOnayCevap> Reddedilenler { get; set; } = new();

            public AdminYetkiBelgesiOnaySonuc ToSonuc()
            {
                return new AdminYetkiBelgesiOnaySonuc
                {
                    Bekleyenler = Bekleyenler.Select(x => x.ToEntity()).ToList(),
                    Onaylananlar = Onaylananlar.Select(x => x.ToEntity()).ToList(),
                    Reddedilenler = Reddedilenler.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class AdminYetkiBelgesiOnayCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public string? FirmaAdi { get; set; }
            public string? VergiNo { get; set; }
            public string? SirketAdi { get; set; }
            public int Durum { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public DateTime? SertifikaBaslangicTarihi { get; set; }
            public DateTime SertifikaBitisTarihi { get; set; }
            public string? DosyaYolu { get; set; }
            public string? OnaylayanKullanici { get; set; }
            public DateTime? OnayTarihi { get; set; }
            public string? RedGerekce { get; set; }

            public Ys_Sertifika ToEntity()
            {
                return new Ys_Sertifika
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    Durum = Durum,
                    OlusturmaTarihi = OlusturmaTarihi,
                    SertifikaBaslangicTarihi = SertifikaBaslangicTarihi,
                    SertifikaBitisTarihi = SertifikaBitisTarihi,
                    DosyaYolu = DosyaYolu,
                    OnaylayanKullanici = OnaylayanKullanici,
                    OnayTarihi = OnayTarihi,
                    RedGerekce = RedGerekce,
                    Firma = new Ys_Firma
                    {
                        Id = FirmaId,
                        FirmaAdi = FirmaAdi,
                        VergiNo = VergiNo,
                        Sirket = new Dag_Sirket { SirketAdi = SirketAdi }
                    }
                };
            }
        }
    }

    public class AdminYetkiBelgesiOnaySonuc
    {
        public List<Ys_Sertifika> Bekleyenler { get; set; } = new();
        public List<Ys_Sertifika> Onaylananlar { get; set; } = new();
        public List<Ys_Sertifika> Reddedilenler { get; set; } = new();
    }
}
