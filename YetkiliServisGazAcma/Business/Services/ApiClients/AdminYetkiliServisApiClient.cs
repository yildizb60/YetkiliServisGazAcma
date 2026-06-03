using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminYetkiliServisApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<AdminYetkiliServisApiClient> _logger;

        public AdminYetkiliServisApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminYetkiliServisApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<AdminYetkiliServisListeSonuc?> ListeleAsync(
            AppKullanici kullanici,
            int? sirketId,
            string? q,
            string? il,
            int? durum,
            string? devreyeSiralama)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis liste");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis liste token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/yetkili-servisler/liste");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminYetkiliServisListeIstek
                {
                    SirketId = sirketId,
                    Q = q,
                    Il = il,
                    Durum = durum,
                    DevreyeSiralama = devreyeSiralama
                });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin yetkili servis liste API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis liste");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<AdminYetkiliServisListeCevap>();
                return sonuc?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin yetkili servis liste API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis liste");
                return null;
            }
        }

        public async Task<AdminYetkiliServisDetaySonuc?> DetayAsync(
            AppKullanici kullanici,
            int id,
            int? sirketId)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis detay");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis detay token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/yetkili-servisler/getir");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminYetkiliServisGetirIstek
                {
                    Id = id,
                    SirketId = sirketId
                });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin yetkili servis detay API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis detay");
                    return null;
                }

                var detay = await response.Content.ReadFromJsonAsync<AdminYetkiliServisDetayCevap>();
                return detay?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin yetkili servis detay API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin yetkili servis detay");
                return null;
            }
        }

        public async Task<AdminYetkiliServisIslemSonuc?> GuncelleAsync(
            AppKullanici kullanici,
            int id,
            int? sirketId,
            string? firmaAdi,
            string? yetkiliKisi,
            string? telefon,
            string? email,
            string? adres,
            string? faaliyetIli,
            string? vergiNo,
            string? vergiDairesi,
            bool aktifMi,
            List<int>? kategoriIds,
            List<int>? markaIds = null)
        {
            var cevap = await PostAsync<AdminYetkiliServisKaydetIstek, AdminYetkiliServisIslemCevap>(
                kullanici,
                "api/admin-panel/yetkili-servisler/guncelle",
                new AdminYetkiliServisKaydetIstek
                {
                    Id = id,
                    SirketId = sirketId,
                    FirmaAdi = firmaAdi,
                    YetkiliKisi = yetkiliKisi,
                    Telefon = telefon,
                    Email = email,
                    Adres = adres,
                    FaaliyetIli = faaliyetIli,
                    VergiNo = vergiNo,
                    VergiDairesi = vergiDairesi,
                    AktifMi = aktifMi,
                    KategoriIds = kategoriIds?.Distinct().ToList(),
                    MarkaIds = markaIds?.Distinct().ToList()
                },
                "Admin yetkili servis guncelle");

            return cevap?.ToSonuc();
        }

        public async Task<AdminYetkiliServisIslemSonuc?> EkleAsync(
            AppKullanici kullanici,
            int? sirketId,
            string? firmaAdi,
            string? yetkiliKisi,
            string? telefon,
            string? email,
            string? adres,
            string? faaliyetIli,
            string? vergiNo,
            string? vergiDairesi,
            List<int>? kategoriIds,
            List<int>? markaIds)
        {
            var cevap = await PostAsync<AdminYetkiliServisKaydetIstek, AdminYetkiliServisIslemCevap>(
                kullanici,
                "api/admin-panel/yetkili-servisler/ekle",
                new AdminYetkiliServisKaydetIstek
                {
                    SirketId = sirketId,
                    FirmaAdi = firmaAdi,
                    YetkiliKisi = yetkiliKisi,
                    Telefon = telefon,
                    Email = email,
                    Adres = adres,
                    FaaliyetIli = faaliyetIli,
                    VergiNo = vergiNo,
                    VergiDairesi = vergiDairesi,
                    AktifMi = true,
                    KategoriIds = kategoriIds?.Distinct().ToList(),
                    MarkaIds = markaIds?.Distinct().ToList()
                },
                "Admin yetkili servis ekle");

            return cevap?.ToSonuc();
        }

        public async Task<AdminYetkiliServisIslemSonuc?> SilAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            var cevap = await PostAsync<AdminYetkiliServisIdIstek, AdminYetkiliServisIslemCevap>(
                kullanici,
                "api/admin-panel/yetkili-servisler/sil",
                new AdminYetkiliServisIdIstek { Id = id, SirketId = sirketId },
                "Admin yetkili servis sil");

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

                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        return (TResponse?)(object)new AdminYetkiliServisIslemCevap
                        {
                            Basarili = false,
                            Mesaj = "Bu yetkili servis islemi icin yetkiniz yok."
                        };

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

        private class AdminYetkiliServisListeIstek
        {
            public int? SirketId { get; set; }
            public string? Q { get; set; }
            public string? Il { get; set; }
            public int? Durum { get; set; }
            public string? DevreyeSiralama { get; set; }
        }

        private class AdminYetkiliServisGetirIstek
        {
            public int Id { get; set; }
            public int? SirketId { get; set; }
        }

        private class AdminYetkiliServisIdIstek
        {
            public int Id { get; set; }
            public int? SirketId { get; set; }
        }

        private class AdminYetkiliServisKaydetIstek
        {
            public int Id { get; set; }
            public int? SirketId { get; set; }
            public string? FirmaAdi { get; set; }
            public string? YetkiliKisi { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public string? FaaliyetIli { get; set; }
            public string? VergiNo { get; set; }
            public string? VergiDairesi { get; set; }
            public bool AktifMi { get; set; }
            public List<int>? KategoriIds { get; set; }
            public List<int>? MarkaIds { get; set; }
        }

        private class AdminYetkiliServisIslemCevap
        {
            public bool Basarili { get; set; }
            public string? Mesaj { get; set; }

            public AdminYetkiliServisIslemSonuc ToSonuc()
            {
                return new AdminYetkiliServisIslemSonuc
                {
                    Basarili = Basarili,
                    Mesaj = Mesaj
                };
            }
        }

        private class AdminYetkiliServisListeCevap
        {
            public List<AdminYetkiliServisCevap> Servisler { get; set; } = new();
            public Dictionary<int, int> DevreyeSayilari { get; set; } = new();

            public AdminYetkiliServisListeSonuc ToSonuc()
            {
                return new AdminYetkiliServisListeSonuc
                {
                    Servisler = Servisler.Select(x => x.ToEntity()).ToList(),
                    DevreyeSayilari = DevreyeSayilari
                };
            }
        }

        private class AdminYetkiliServisCevap
        {
            public int Id { get; set; }
            public string? FirmaAdi { get; set; }
            public string? YetkiliKisi { get; set; }
            public string? VergiNo { get; set; }
            public string? VergiDairesi { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public string? FaaliyetIli { get; set; }
            public bool AktifMi { get; set; }
            public int SirketId { get; set; }
            public string? SirketAdi { get; set; }
            public List<AdminKategoriCevap> Kategoriler { get; set; } = new();

            public Ys_Firma ToEntity()
            {
                return new Ys_Firma
                {
                    Id = Id,
                    FirmaAdi = FirmaAdi,
                    YetkiliKisi = YetkiliKisi,
                    VergiNo = VergiNo,
                    VergiDairesi = VergiDairesi,
                    Telefon = Telefon,
                    Email = Email,
                    Adres = Adres,
                    FaaliyetIli = FaaliyetIli,
                    AktifMi = AktifMi,
                    SirketId = SirketId,
                    Sirket = new Dag_Sirket
                    {
                        Id = SirketId,
                        SirketAdi = SirketAdi
                    },
                    FirmaKategoriler = Kategoriler
                        .Select(x => new Ys_FirmaKategori
                        {
                            FirmaId = Id,
                            KategoriId = x.Id,
                            Kategori = new UrunKategori
                            {
                                Id = x.Id,
                                Ad = x.Ad,
                                IconUrl = x.IconUrl,
                                AktifMi = true
                            },
                            SilindiMi = false
                        })
                        .ToList()
                };
            }
        }

        private class AdminYetkiliServisDetayCevap
        {
            public AdminYetkiliServisCevap? Servis { get; set; }
            public List<AdminYetkiBelgesiCevap> YetkiBelgeleri { get; set; } = new();
            public List<AdminSubeCevap> Subeler { get; set; } = new();
            public List<AdminDevreyeAlmaCevap> Devreye { get; set; } = new();

            public AdminYetkiliServisDetaySonuc ToSonuc()
            {
                return new AdminYetkiliServisDetaySonuc
                {
                    Servis = Servis?.ToEntity(),
                    Sertifikalar = YetkiBelgeleri.Select(x => x.ToEntity()).ToList(),
                    Subeler = Subeler.Select(x => x.ToEntity()).ToList(),
                    Devreye = Devreye.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class AdminKategoriCevap
        {
            public int Id { get; set; }
            public string? Ad { get; set; }
            public string? IconUrl { get; set; }
        }

        private class AdminYetkiBelgesiCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public int Durum { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
            public DateTime YetkiBelgesiBitisTarihi { get; set; }

            public Ys_Sertifika ToEntity()
            {
                return new Ys_Sertifika
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    Durum = Durum,
                    OlusturmaTarihi = OlusturmaTarihi,
                    SertifikaBaslangicTarihi = YetkiBelgesiBaslangicTarihi,
                    SertifikaBitisTarihi = YetkiBelgesiBitisTarihi
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

            public Ys_Sube ToEntity()
            {
                return new Ys_Sube
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    SubeAdi = SubeAdi,
                    Il = Il,
                    Ilce = Ilce,
                    Telefon = Telefon
                };
            }
        }

        private class AdminDevreyeAlmaCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public string? TesistatNo { get; set; }
            public int Durum { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public string? MarkaAdi { get; set; }

            public Ys_DevreyeAlma ToEntity()
            {
                return new Ys_DevreyeAlma
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    TesistatNo = TesistatNo,
                    Durum = Durum,
                    OlusturmaTarihi = OlusturmaTarihi,
                    Marka = new Ys_Marka { MarkaAdi = MarkaAdi }
                };
            }
        }
    }

    public class AdminYetkiliServisIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }
}
