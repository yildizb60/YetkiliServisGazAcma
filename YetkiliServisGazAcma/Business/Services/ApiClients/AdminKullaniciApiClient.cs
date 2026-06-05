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
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin kullanici liste");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin kullanici liste token");
                    return null;
                }

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
                    ApiClientFallback.EnsureAllowed(_options, "Admin kullanici liste");
                    return null;
                }

                var kullanicilar = await response.Content.ReadFromJsonAsync<List<AdminKullaniciListeCevap>>();
                return kullanicilar?.Select(x => x.ToEntity()).ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin kullanici liste API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin kullanici liste");
                return null;
            }
        }

        public async Task<List<Dag_Sirket>?> SirketSecenekleriAsync(AppKullanici kullanici, int? sirketId)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin kullanici sirket secenekleri");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin kullanici sirket secenekleri token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/kullanicilar/sirket-secenekleri");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminKullaniciSirketSecenekIstek { SirketId = sirketId });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin kullanici sirket secenekleri API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin kullanici sirket secenekleri");
                    return null;
                }

                var sirketler = await response.Content.ReadFromJsonAsync<List<AdminSirketSecenekCevap>>();
                return sirketler?.Select(x => new Dag_Sirket
                {
                    Id = x.Id,
                    SirketAdi = x.SirketAdi
                }).ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin kullanici sirket secenekleri API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin kullanici sirket secenekleri");
                return null;
            }
        }

        public async Task<List<Ys_Firma>?> FirmaSecenekleriAsync(AppKullanici kullanici, int? sirketId)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin kullanici firma secenekleri");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin kullanici firma secenekleri token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/kullanicilar/firma-secenekleri");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminKullaniciFirmaSecenekIstek { SirketId = sirketId });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin kullanici firma secenekleri API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin kullanici firma secenekleri");
                    return null;
                }

                var firmalar = await response.Content.ReadFromJsonAsync<List<AdminFirmaSecenekCevap>>();
                return firmalar?.Select(x => new Ys_Firma
                {
                    Id = x.Id,
                    FirmaAdi = x.FirmaAdi,
                    SirketId = x.SirketId,
                    Sirket = new Dag_Sirket
                    {
                        Id = x.SirketId,
                        SirketAdi = x.SirketAdi
                    }
                }).ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin kullanici firma secenekleri API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin kullanici firma secenekleri");
                return null;
            }
        }

        public async Task<AdminYetkiListeSonuc?> YetkiListeAsync(AppKullanici kullanici, int? sirketId)
        {
            var cevap = await PostOkuAsync<AdminYetkiListeIstek, AdminYetkiListeCevap>(
                kullanici,
                "api/admin-panel/yetkiler/liste",
                new AdminYetkiListeIstek { SirketId = sirketId },
                "Admin yetki liste");

            return cevap?.ToSonuc();
        }

        public async Task<AdminYetkiDuzenleSonuc?> YetkiDuzenleAsync(AppKullanici kullanici, string id, int? sirketId)
        {
            var cevap = await PostOkuAsync<AdminYetkiGetirIstek, AdminYetkiDuzenleCevap>(
                kullanici,
                "api/admin-panel/yetkiler/getir",
                new AdminYetkiGetirIstek { Id = id, SirketId = sirketId },
                "Admin yetki duzenle");

            return cevap?.ToSonuc();
        }

        public Task<AdminKullaniciIslemSonuc?> YetkiGuncelleAsync(
            AppKullanici kullanici,
            string id,
            int? sirketId,
            List<int> sirketIds,
            Dictionary<int, List<string>> yetkiler)
        {
            return PostIslemAsync(
                kullanici,
                "api/admin-panel/yetkiler/guncelle",
                new AdminYetkiGuncelleIstek
                {
                    Id = id,
                    SirketId = sirketId,
                    SirketIds = sirketIds,
                    Yetkiler = yetkiler
                },
                "Admin yetki guncelle");
        }

        public async Task<bool> YonetebilirMiAsync(AppKullanici kullanici, int? sirketId)
        {
            var cevap = await PostOkuAsync<AdminKullaniciYonetimYetkiIstek, AdminKullaniciYonetimYetkiCevap>(
                kullanici,
                "api/admin-panel/kullanicilar/yonetim-yetkisi",
                new AdminKullaniciYonetimYetkiIstek { SirketId = sirketId },
                "Admin kullanici yonetim yetkisi");

            return cevap?.YetkiliMi == true;
        }

        public Task<AdminKullaniciIslemSonuc?> KullaniciEkleAsync(
            AppKullanici kullanici,
            int? kapsamSirketId,
            string adSoyad,
            string email,
            string telefon,
            string sifre,
            string rol,
            int? sirketId,
            int? firmaId)
        {
            return PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/ekle",
                new AdminKullaniciKaydetIstek
                {
                    KapsamSirketId = kapsamSirketId,
                    AdSoyad = adSoyad,
                    Email = email,
                    Telefon = telefon,
                    Sifre = sifre,
                    Rol = rol,
                    SirketId = sirketId,
                    FirmaId = firmaId
                },
                "Admin kullanici ekle");
        }

        public Task<AdminKullaniciIslemSonuc?> PersonelEkleAsync(
            AppKullanici kullanici,
            int? kapsamSirketId,
            string adSoyad,
            string email,
            string telefon,
            int sirketId,
            string sifre)
        {
            return PostIslemAsync(
                kullanici,
                "api/admin-panel/personeller/ekle",
                new AdminPersonelKaydetIstek
                {
                    KapsamSirketId = kapsamSirketId,
                    AdSoyad = adSoyad,
                    Email = email,
                    Telefon = telefon,
                    SirketId = sirketId,
                    Sifre = sifre
                },
                "Admin personel ekle");
        }

        public Task<AdminKullaniciIslemSonuc?> YetkiliServisKullanicilariniSenkronizeAsync(
            AppKullanici kullanici,
            int? sirketId)
        {
            return PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/yetkili-servis-senkronize",
                new AdminKullaniciSenkronIstek
                {
                    SirketId = sirketId
                },
                "Admin yetkili servis kullanici senkronizasyonu");
        }

        public async Task<AppKullanici?> GetirAsync(AppKullanici kullanici, string id, int? sirketId)
        {
            var cevap = await PostOkuAsync<AdminKullaniciGetirIstek, AdminKullaniciListeCevap>(
                kullanici,
                "api/admin-panel/kullanicilar/getir",
                new AdminKullaniciGetirIstek
                {
                    Id = id,
                    SirketId = sirketId
                },
                "Admin kullanici getir");

            return cevap?.ToEntity();
        }

        public Task<AdminKullaniciIslemSonuc?> GuncelleAsync(
            AppKullanici kullanici,
            string id,
            int? kapsamSirketId,
            string adSoyad,
            string email,
            string telefon,
            bool aktifMi,
            int? sirketId,
            int? firmaId,
            string? yeniSifre,
            string? yeniSifreTekrar)
        {
            return PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/guncelle",
                new AdminKullaniciGuncelleIstek
                {
                    Id = id,
                    KapsamSirketId = kapsamSirketId,
                    AdSoyad = adSoyad,
                    Email = email,
                    Telefon = telefon,
                    AktifMi = aktifMi,
                    SirketId = sirketId,
                    FirmaId = firmaId,
                    YeniSifre = yeniSifre,
                    YeniSifreTekrar = yeniSifreTekrar
                },
                "Admin kullanici guncelle");
        }

        public Task<AdminKullaniciIslemSonuc?> DurumAsync(
            AppKullanici kullanici,
            string id,
            bool aktifMi,
            int? sirketId,
            bool sadecePersonel)
        {
            return PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/durum",
                new AdminKullaniciDurumIstek
                {
                    Id = id,
                    SirketId = sirketId,
                    AktifMi = aktifMi,
                    SadecePersonel = sadecePersonel
                },
                "Admin kullanici durum");
        }

        public Task<AdminKullaniciIslemSonuc?> SilAsync(
            AppKullanici kullanici,
            string id,
            int? sirketId,
            bool sadecePersonel)
        {
            return PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/sil",
                new AdminKullaniciSilIstek
                {
                    Id = id,
                    SirketId = sirketId,
                    SadecePersonel = sadecePersonel
                },
                "Admin kullanici sil");
        }

        private async Task<AdminKullaniciIslemSonuc?> PostIslemAsync<TRequest>(
            AppKullanici kullanici,
            string url,
            TRequest istek,
            string operasyon)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, $"{operasyon} token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(istek);

                using var response = await _httpClient.SendAsync(request);
                AdminKullaniciIslemCevap? cevap = null;
                try
                {
                    cevap = await response.Content.ReadFromJsonAsync<AdminKullaniciIslemCevap>();
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException)
                {
                    cevap = null;
                }

                if (cevap != null)
                    return cevap.ToSonuc();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("{Operasyon} API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}", operasyon, url, response.StatusCode);

                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        return new AdminKullaniciIslemSonuc
                        {
                            Basarili = false,
                            Mesaj = "Bu kullanici islemi icin yetkiniz yok."
                        };

                    ApiClientFallback.EnsureAllowed(_options, operasyon);
                }

                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "{Operasyon} API cagrisina ulasilamadi. Url: {Url}", operasyon, url);
                ApiClientFallback.EnsureAllowed(_options, operasyon);
                return null;
            }
        }

        private async Task<TResponse?> PostOkuAsync<TRequest, TResponse>(
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

        private class AdminKullaniciListeIstek
        {
            public int? SirketId { get; set; }
            public string? Q { get; set; }
            public string? Tip { get; set; }
            public string? Durum { get; set; }
            public string? Bagli { get; set; }
        }

        private class AdminKullaniciDurumIstek
        {
            public string Id { get; set; } = string.Empty;
            public int? SirketId { get; set; }
            public bool AktifMi { get; set; }
            public bool SadecePersonel { get; set; }
        }

        private class AdminKullaniciSirketSecenekIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminKullaniciFirmaSecenekIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminKullaniciYonetimYetkiIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminKullaniciYonetimYetkiCevap
        {
            public bool YetkiliMi { get; set; }
        }

        private class AdminKullaniciKaydetIstek
        {
            public int? KapsamSirketId { get; set; }
            public string? AdSoyad { get; set; }
            public string? Email { get; set; }
            public string? Telefon { get; set; }
            public string? Sifre { get; set; }
            public string? Rol { get; set; }
            public int? SirketId { get; set; }
            public int? FirmaId { get; set; }
        }

        private class AdminPersonelKaydetIstek
        {
            public int? KapsamSirketId { get; set; }
            public string? AdSoyad { get; set; }
            public string? Email { get; set; }
            public string? Telefon { get; set; }
            public int SirketId { get; set; }
            public string? Sifre { get; set; }
        }

        private class AdminKullaniciSenkronIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminKullaniciGetirIstek
        {
            public string Id { get; set; } = string.Empty;
            public int? SirketId { get; set; }
        }

        private class AdminKullaniciGuncelleIstek
        {
            public string Id { get; set; } = string.Empty;
            public int? KapsamSirketId { get; set; }
            public string? AdSoyad { get; set; }
            public string? Email { get; set; }
            public string? Telefon { get; set; }
            public bool AktifMi { get; set; }
            public int? SirketId { get; set; }
            public int? FirmaId { get; set; }
            public string? YeniSifre { get; set; }
            public string? YeniSifreTekrar { get; set; }
        }

        private class AdminKullaniciSilIstek
        {
            public string Id { get; set; } = string.Empty;
            public int? SirketId { get; set; }
            public bool SadecePersonel { get; set; }
        }

        private class AdminYetkiListeIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminYetkiGetirIstek
        {
            public string Id { get; set; } = string.Empty;
            public int? SirketId { get; set; }
        }

        private class AdminYetkiGuncelleIstek
        {
            public string Id { get; set; } = string.Empty;
            public int? SirketId { get; set; }
            public List<int> SirketIds { get; set; } = new();
            public Dictionary<int, List<string>> Yetkiler { get; set; } = new();
        }

        private class AdminKullaniciIslemCevap
        {
            public bool Basarili { get; set; }
            public string? Mesaj { get; set; }

            public AdminKullaniciIslemSonuc ToSonuc()
            {
                return new AdminKullaniciIslemSonuc
                {
                    Basarili = Basarili,
                    Mesaj = Mesaj
                };
            }
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

        private class AdminSirketSecenekCevap
        {
            public int Id { get; set; }
            public string? SirketAdi { get; set; }
        }

        private class AdminFirmaSecenekCevap
        {
            public int Id { get; set; }
            public string? FirmaAdi { get; set; }
            public int SirketId { get; set; }
            public string? SirketAdi { get; set; }
        }

        private class AdminYetkiListeCevap
        {
            public List<AdminKullaniciListeCevap> Personeller { get; set; } = new();
            public Dictionary<string, List<string>> YetkiMap { get; set; } = new();
            public Dictionary<string, List<string>> YetkiSirketAdlariMap { get; set; } = new();

            public AdminYetkiListeSonuc ToSonuc()
            {
                return new AdminYetkiListeSonuc
                {
                    Personeller = Personeller.Select(x => x.ToEntity()).ToList(),
                    YetkiMap = YetkiMap,
                    YetkiSirketAdlariMap = YetkiSirketAdlariMap
                };
            }
        }

        private class AdminYetkiDuzenleCevap
        {
            public AdminKullaniciListeCevap? Personel { get; set; }
            public List<AdminSirketSecenekCevap> Sirketler { get; set; } = new();
            public List<string> MevcutYetkiler { get; set; } = new();
            public Dictionary<int, List<string>> YetkiSirketMap { get; set; } = new();
            public List<int> SeciliSirketIds { get; set; } = new();

            public AdminYetkiDuzenleSonuc ToSonuc()
            {
                return new AdminYetkiDuzenleSonuc
                {
                    Personel = Personel?.ToEntity(),
                    Sirketler = Sirketler.Select(x => new Dag_Sirket
                    {
                        Id = x.Id,
                        SirketAdi = x.SirketAdi
                    }).ToList(),
                    MevcutYetkiler = MevcutYetkiler,
                    YetkiSirketMap = YetkiSirketMap,
                    SeciliSirketIds = SeciliSirketIds
                };
            }
        }
    }

    public class AdminKullaniciIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }

    public class AdminYetkiListeSonuc
    {
        public List<AppKullanici> Personeller { get; set; } = new();
        public Dictionary<string, List<string>> YetkiMap { get; set; } = new();
        public Dictionary<string, List<string>> YetkiSirketAdlariMap { get; set; } = new();
    }

    public class AdminYetkiDuzenleSonuc
    {
        public AppKullanici? Personel { get; set; }
        public List<Dag_Sirket> Sirketler { get; set; } = new();
        public List<string> MevcutYetkiler { get; set; } = new();
        public Dictionary<int, List<string>> YetkiSirketMap { get; set; } = new();
        public List<int> SeciliSirketIds { get; set; } = new();
    }
}
