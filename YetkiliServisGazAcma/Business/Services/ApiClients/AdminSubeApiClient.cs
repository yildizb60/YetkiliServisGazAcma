using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminSubeApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<AdminSubeApiClient> _logger;

        public AdminSubeApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminSubeApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<AdminSubeListeSonuc?> ListeleAsync(AppKullanici kullanici, int? sirketId, string? q, int firmaId)
        {
            var cevap = await PostAsync<AdminSubeListeIstek, AdminSubeListeCevap>(
                kullanici,
                "api/admin-panel/subeler/liste",
                new AdminSubeListeIstek
                {
                    SirketId = sirketId,
                    Q = q,
                    FirmaId = firmaId
                });

            return cevap?.ToSonuc();
        }

        public async Task<AdminSubeDetaySonuc?> DetayAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            var cevap = await PostAsync<AdminSubeGetirIstek, AdminSubeDetayCevap>(
                kullanici,
                "api/admin-panel/subeler/getir",
                new AdminSubeGetirIstek { Id = id, SirketId = sirketId });

            return cevap?.ToSonuc();
        }

        public Task<AdminSubeIslemSonuc?> EkleAsync(
            AppKullanici kullanici,
            int? sirketId,
            int firmaId,
            string subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            return SubeKaydetAsync(kullanici, "api/admin-panel/subeler/ekle", new AdminSubeKaydetIstek
            {
                SirketId = sirketId,
                FirmaId = firmaId,
                SubeAdi = subeAdi,
                Il = il,
                Ilce = ilce,
                Telefon = telefon,
                Adres = adres,
                AktifMi = aktifMi
            });
        }

        public Task<AdminSubeIslemSonuc?> GuncelleAsync(
            AppKullanici kullanici,
            int id,
            int? sirketId,
            int firmaId,
            string subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            return SubeKaydetAsync(kullanici, "api/admin-panel/subeler/guncelle", new AdminSubeKaydetIstek
            {
                Id = id,
                SirketId = sirketId,
                FirmaId = firmaId,
                SubeAdi = subeAdi,
                Il = il,
                Ilce = ilce,
                Telefon = telefon,
                Adres = adres,
                AktifMi = aktifMi
            });
        }

        public Task<AdminSubeIslemSonuc?> DurumAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return PostIslemAsync(kullanici, "api/admin-panel/subeler/durum", new AdminSubeGetirIstek { Id = id, SirketId = sirketId });
        }

        public Task<AdminSubeIslemSonuc?> SilAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return PostIslemAsync(kullanici, "api/admin-panel/subeler/sil", new AdminSubeGetirIstek { Id = id, SirketId = sirketId });
        }

        private Task<AdminSubeIslemSonuc?> SubeKaydetAsync(AppKullanici kullanici, string url, AdminSubeKaydetIstek istek)
        {
            return PostIslemAsync(kullanici, url, istek);
        }

        private async Task<AdminSubeIslemSonuc?> PostIslemAsync<TRequest>(AppKullanici kullanici, string url, TRequest istek)
        {
            var cevap = await PostAsync<TRequest, AdminSubeIslemCevap>(kullanici, url, istek);
            return cevap?.ToSonuc();
        }

        private async Task<TResponse?> PostAsync<TRequest, TResponse>(AppKullanici kullanici, string url, TRequest istek)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin sube");
                return default;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin sube token");
                    return default;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(istek);

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin sube API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}", url, response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin sube");
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin sube API cagrisina ulasilamadi. Url: {Url}", url);
                ApiClientFallback.EnsureAllowed(_options, "Admin sube");
                return default;
            }
        }

        private class AdminSubeListeIstek
        {
            public int? SirketId { get; set; }
            public int FirmaId { get; set; }
            public string? Q { get; set; }
        }

        private class AdminSubeGetirIstek
        {
            public int Id { get; set; }
            public int? SirketId { get; set; }
        }

        private class AdminSubeKaydetIstek
        {
            public int Id { get; set; }
            public int? SirketId { get; set; }
            public int FirmaId { get; set; }
            public string? SubeAdi { get; set; }
            public string? Il { get; set; }
            public string? Ilce { get; set; }
            public string? Telefon { get; set; }
            public string? Adres { get; set; }
            public bool AktifMi { get; set; }
        }

        private class AdminSubeListeCevap
        {
            public List<AdminSubeCevap> Subeler { get; set; } = new();
            public List<AdminSubeFirmaCevap> Firmalar { get; set; } = new();

            public AdminSubeListeSonuc ToSonuc()
            {
                return new AdminSubeListeSonuc
                {
                    Subeler = Subeler.Select(x => x.ToEntity()).ToList(),
                    Firmalar = Firmalar.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class AdminSubeDetayCevap : AdminSubeIslemCevap
        {
            public AdminSubeCevap? Sube { get; set; }
            public List<AdminSubeFirmaCevap> Firmalar { get; set; } = new();

            public new AdminSubeDetaySonuc ToSonuc()
            {
                return new AdminSubeDetaySonuc
                {
                    Basarili = Basarili,
                    Mesaj = Mesaj,
                    Sube = Sube?.ToEntity(),
                    Firmalar = Firmalar.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class AdminSubeIslemCevap
        {
            public bool Basarili { get; set; }
            public string? Mesaj { get; set; }

            public AdminSubeIslemSonuc ToSonuc()
            {
                return new AdminSubeIslemSonuc
                {
                    Basarili = Basarili,
                    Mesaj = Mesaj
                };
            }
        }

        private class AdminSubeCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public string? SubeAdi { get; set; }
            public string? Il { get; set; }
            public string? Ilce { get; set; }
            public string? Telefon { get; set; }
            public string? Adres { get; set; }
            public bool AktifMi { get; set; }
            public string? FirmaAdi { get; set; }
            public string? FirmaEmail { get; set; }
            public string? FirmaTelefon { get; set; }
            public int? FirmaSirketId { get; set; }

            public Ys_Sube ToEntity()
            {
                return new Ys_Sube
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    SubeAdi = SubeAdi,
                    Il = Il,
                    Ilce = Ilce,
                    Telefon = Telefon,
                    Adres = Adres,
                    AktifMi = AktifMi,
                    Firma = new Ys_Firma
                    {
                        Id = FirmaId,
                        FirmaAdi = FirmaAdi,
                        Email = FirmaEmail,
                        Telefon = FirmaTelefon,
                        SirketId = FirmaSirketId ?? 0
                    }
                };
            }
        }

        private class AdminSubeFirmaCevap
        {
            public int Id { get; set; }
            public string? FirmaAdi { get; set; }
            public string? Email { get; set; }
            public string? Telefon { get; set; }
            public int SirketId { get; set; }

            public Ys_Firma ToEntity()
            {
                return new Ys_Firma
                {
                    Id = Id,
                    FirmaAdi = FirmaAdi,
                    Email = Email,
                    Telefon = Telefon,
                    SirketId = SirketId,
                    AktifMi = true,
                    SilindiMi = false
                };
            }
        }
    }

    public class AdminSubeListeSonuc
    {
        public List<Ys_Sube> Subeler { get; set; } = new();
        public List<Ys_Firma> Firmalar { get; set; } = new();
    }

    public class AdminSubeDetaySonuc : AdminSubeIslemSonuc
    {
        public Ys_Sube? Sube { get; set; }
        public List<Ys_Firma> Firmalar { get; set; } = new();
    }

    public class AdminSubeIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }
}
