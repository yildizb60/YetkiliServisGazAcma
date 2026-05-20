using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminDashboardApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<AdminDashboardApiClient> _logger;

        public AdminDashboardApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminDashboardApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<AdminDashboardOzet?> GetirAsync(AppKullanici kullanici, int? sirketId)
        {
            if (!_options.Enabled)
                return null;

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                    return null;

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/dashboard");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminDashboardFiltreIstek { SirketId = sirketId });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin dashboard API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    return null;
                }

                var dashboard = await response.Content.ReadFromJsonAsync<AdminDashboardApiCevap>();
                return dashboard?.ToOzet();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin dashboard API cagrisina ulasilamadi. MVC eski servis yoluyla devam edecek.");
                return null;
            }
        }

        private class AdminDashboardFiltreIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminDashboardApiCevap
        {
            public int ToplamDevreyeAlma { get; set; }
            public int ToplamFirma { get; set; }
            public int OnayBekleyen { get; set; }
            public int SuresiBitecek { get; set; }
            public int ToplamSirket { get; set; }
            public int BuAyDevreyeAlma { get; set; }
            public List<AdminSertifikaOzetCevap> SonSertifikalar { get; set; } = new();
            public List<AdminDevreyeAlmaOzetCevap> SonDevreyeAlmalar { get; set; } = new();

            public AdminDashboardOzet ToOzet()
            {
                return new AdminDashboardOzet
                {
                    ToplamDevreyeAlma = ToplamDevreyeAlma,
                    ToplamFirma = ToplamFirma,
                    OnayBekleyen = OnayBekleyen,
                    SuresiBitecek = SuresiBitecek,
                    ToplamSirket = ToplamSirket,
                    BuAyDevreyeAlma = BuAyDevreyeAlma,
                    SonSertifikalar = SonSertifikalar.Select(x => x.ToEntity()).ToList(),
                    SonDevreyeAlmalar = SonDevreyeAlmalar.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class AdminSertifikaOzetCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public string? FirmaAdi { get; set; }
            public string? SirketAdi { get; set; }
            public int Durum { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public DateTime SertifikaBitisTarihi { get; set; }

            public Ys_Sertifika ToEntity()
            {
                return new Ys_Sertifika
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    Durum = Durum,
                    OlusturmaTarihi = OlusturmaTarihi,
                    SertifikaBitisTarihi = SertifikaBitisTarihi,
                    Firma = new Ys_Firma
                    {
                        Id = FirmaId,
                        FirmaAdi = FirmaAdi,
                        Sirket = new Dag_Sirket { SirketAdi = SirketAdi }
                    }
                };
            }
        }

        private class AdminDevreyeAlmaOzetCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public string? FirmaAdi { get; set; }
            public string? MarkaAdi { get; set; }
            public string? TesistatNo { get; set; }
            public int Durum { get; set; }
            public DateTime OlusturmaTarihi { get; set; }

            public Ys_DevreyeAlma ToEntity()
            {
                return new Ys_DevreyeAlma
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    TesistatNo = TesistatNo,
                    Durum = Durum,
                    OlusturmaTarihi = OlusturmaTarihi,
                    Firma = new Ys_Firma
                    {
                        Id = FirmaId,
                        FirmaAdi = FirmaAdi
                    },
                    Marka = new Ys_Marka
                    {
                        MarkaAdi = MarkaAdi
                    }
                };
            }
        }
    }
}
