using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminRaporApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<AdminRaporApiClient> _logger;

        public AdminRaporApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminRaporApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<AdminDevreyeAlmaListeSonuc?> DevreyeAlmalarAsync(
            AppKullanici kullanici,
            int? sirketId,
            string? marka,
            string? servis,
            string? il,
            string? durum,
            DateTime? bas,
            DateTime? bit)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma liste");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma liste token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/devreye-almalar/liste");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminDevreyeAlmaListeIstek
                {
                    SirketId = sirketId,
                    Marka = marka,
                    Servis = servis,
                    Il = il,
                    Durum = int.TryParse(durum, out var durumNo) ? durumNo : null,
                    BaslangicTarihi = bas,
                    BitisTarihi = bit
                });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin devreye alma liste API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma liste");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<AdminDevreyeAlmaListeCevap>();
                return sonuc?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin devreye alma liste API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma liste");
                return null;
            }
        }

        public async Task<Ys_DevreyeAlma?> DevreyeAlmaDetayAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma detay");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma detay token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/devreye-almalar/getir");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminDevreyeAlmaGetirIstek
                {
                    Id = id,
                    SirketId = sirketId
                });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin devreye alma detay API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma detay");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<AdminDevreyeAlmaCevap>();
                return sonuc?.ToEntity();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin devreye alma detay API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin devreye alma detay");
                return null;
            }
        }

        public async Task<AdminYetkiBelgesiUyariSonuc?> YetkiBelgesiUyarilariAsync(AppKullanici kullanici, int? sirketId)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi uyarilari");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi uyarilari token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/yetki-belgeleri/uyarilar");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminYetkiBelgesiUyariIstek { SirketId = sirketId });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin yetki belgesi uyari API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi uyarilari");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<AdminYetkiBelgesiUyariCevap>();
                return sonuc?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin yetki belgesi uyari API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin yetki belgesi uyarilari");
                return null;
            }
        }

        public async Task<AdminRaporOzetSonuc?> RaporlarOzetAsync(
            AppKullanici kullanici,
            int? sirketId,
            DateTime? bas,
            DateTime? bit,
            string? tip)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Admin rapor ozet");
                return null;
            }

            try
            {
                var token = await _tokenService.OlusturAsync(kullanici);
                if (string.IsNullOrWhiteSpace(token))
                {
                    ApiClientFallback.EnsureAllowed(_options, "Admin rapor ozet token");
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin-panel/raporlar/ozet");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new AdminRaporOzetIstek
                {
                    SirketId = sirketId,
                    BaslangicTarihi = bas,
                    BitisTarihi = bit,
                    Tip = tip
                });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin rapor ozet API cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Admin rapor ozet");
                    return null;
                }

                var sonuc = await response.Content.ReadFromJsonAsync<AdminRaporOzetCevap>();
                return sonuc?.ToSonuc();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Admin rapor ozet API cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Admin rapor ozet");
                return null;
            }
        }

        private class AdminDevreyeAlmaListeIstek
        {
            public int? SirketId { get; set; }
            public string? Marka { get; set; }
            public string? Servis { get; set; }
            public string? Il { get; set; }
            public int? Durum { get; set; }
            public DateTime? BaslangicTarihi { get; set; }
            public DateTime? BitisTarihi { get; set; }
        }

        private class AdminDevreyeAlmaGetirIstek
        {
            public int Id { get; set; }
            public int? SirketId { get; set; }
        }

        private class AdminYetkiBelgesiUyariIstek
        {
            public int? SirketId { get; set; }
        }

        private class AdminRaporOzetIstek
        {
            public int? SirketId { get; set; }
            public DateTime? BaslangicTarihi { get; set; }
            public DateTime? BitisTarihi { get; set; }
            public string? Tip { get; set; }
        }

        private class AdminDevreyeAlmaListeCevap
        {
            public List<AdminDevreyeAlmaCevap> Islemler { get; set; } = new();
            public List<AdminMarkaCevap> Markalar { get; set; } = new();
            public Dictionary<int, string> FirmaIlceleri { get; set; } = new();

            public AdminDevreyeAlmaListeSonuc ToSonuc()
            {
                return new AdminDevreyeAlmaListeSonuc
                {
                    Islemler = Islemler.Select(x => x.ToEntity()).ToList(),
                    Markalar = Markalar.Select(x => x.ToEntity()).ToList(),
                    FirmaIlceleri = FirmaIlceleri
                };
            }
        }

        private class AdminYetkiBelgesiUyariCevap
        {
            public List<AdminSertifikaCevap> Yaklasan { get; set; } = new();
            public List<AdminSertifikaCevap> Gecmis { get; set; } = new();

            public AdminYetkiBelgesiUyariSonuc ToSonuc()
            {
                return new AdminYetkiBelgesiUyariSonuc
                {
                    Yaklasan = Yaklasan.Select(x => x.ToEntity()).ToList(),
                    Gecmis = Gecmis.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class AdminRaporOzetCevap
        {
            public DateTime BasTarih { get; set; }
            public DateTime BitTarih { get; set; }
            public string RaporTipi { get; set; } = "devreye";
            public string ListeTipi { get; set; } = "devreye";
            public int DevreyeSayisi { get; set; }
            public int DevreyeTamamlanan { get; set; }
            public int DevreyeBekleyen { get; set; }
            public int DevreyeIptal { get; set; }
            public int SertifikaOnayli { get; set; }
            public int SertifikaBekleyen { get; set; }
            public int SertifikaReddedilen { get; set; }
            public List<string> ChartSirketLabels { get; set; } = new();
            public List<int> ChartSirketData { get; set; } = new();
            public List<string> ChartAylikLabels { get; set; } = new();
            public List<int> ChartAylikData { get; set; } = new();
            public List<int> ChartDurumData { get; set; } = new();
            public List<string?> ChartMarkaLabels { get; set; } = new();
            public List<int> ChartMarkaData { get; set; } = new();
            public List<AdminDevreyeAlmaCevap> SonIslemler { get; set; } = new();
            public List<AdminSertifikaCevap> SertifikaIslemler { get; set; } = new();
            public List<AdminSirketCevap> Sirketler { get; set; } = new();

            public AdminRaporOzetSonuc ToSonuc()
            {
                return new AdminRaporOzetSonuc
                {
                    BasTarih = BasTarih,
                    BitTarih = BitTarih,
                    RaporTipi = RaporTipi,
                    ListeTipi = ListeTipi,
                    DevreyeSayisi = DevreyeSayisi,
                    DevreyeTamamlanan = DevreyeTamamlanan,
                    DevreyeBekleyen = DevreyeBekleyen,
                    DevreyeIptal = DevreyeIptal,
                    SertifikaOnayli = SertifikaOnayli,
                    SertifikaBekleyen = SertifikaBekleyen,
                    SertifikaReddedilen = SertifikaReddedilen,
                    ChartSirketLabels = ChartSirketLabels,
                    ChartSirketData = ChartSirketData,
                    ChartAylikLabels = ChartAylikLabels,
                    ChartAylikData = ChartAylikData,
                    ChartDurumData = ChartDurumData,
                    ChartMarkaLabels = ChartMarkaLabels,
                    ChartMarkaData = ChartMarkaData,
                    SonIslemler = SonIslemler.Select(x => x.ToEntity()).ToList(),
                    SertifikaIslemler = SertifikaIslemler.Select(x => x.ToEntity()).ToList(),
                    Sirketler = Sirketler.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class AdminMarkaCevap
        {
            public int Id { get; set; }
            public string? MarkaAdi { get; set; }

            public Ys_Marka ToEntity()
            {
                return new Ys_Marka
                {
                    Id = Id,
                    MarkaAdi = MarkaAdi
                };
            }
        }

        private class AdminSirketCevap
        {
            public int Id { get; set; }
            public string? SirketAdi { get; set; }

            public Dag_Sirket ToEntity()
            {
                return new Dag_Sirket
                {
                    Id = Id,
                    SirketAdi = SirketAdi
                };
            }
        }

        private class AdminDevreyeAlmaCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public int? MarkaId { get; set; }
            public string? TesistatNo { get; set; }
            public string? AboneNo { get; set; }
            public string? UygunlukBelgeNo { get; set; }
            public DateTime? UygunlukTarihi { get; set; }
            public string? MusteriAdi { get; set; }
            public string? MusteriTcNo { get; set; }
            public string? MusteriTelefon { get; set; }
            public string? Adres { get; set; }
            public string? CihazTipi { get; set; }
            public string? CihazMarka { get; set; }
            public string? CihazModeli { get; set; }
            public string? CihazKapasite { get; set; }
            public string? SeriNo { get; set; }
            public string? TeknisyenAdi { get; set; }
            public string? TeknisyenSertifikaNo { get; set; }
            public DateTime DevreyeAlmaTarihi { get; set; }
            public string? Notlar { get; set; }
            public int Durum { get; set; }
            public string? PdfYolu { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public string? FirmaAdi { get; set; }
            public string? FirmaFaaliyetIli { get; set; }
            public string? FirmaAdres { get; set; }
            public int FirmaSirketId { get; set; }
            public string? SirketAdi { get; set; }
            public string? MarkaAdi { get; set; }

            public Ys_DevreyeAlma ToEntity()
            {
                return new Ys_DevreyeAlma
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    MarkaId = MarkaId,
                    TesistatNo = TesistatNo,
                    AboneNo = AboneNo,
                    UygunlukBelgeNo = UygunlukBelgeNo,
                    UygunlukTarihi = UygunlukTarihi,
                    MusteriAdi = MusteriAdi,
                    MusteriTcNo = MusteriTcNo,
                    MusteriTelefon = MusteriTelefon,
                    Adres = Adres,
                    CihazTipi = CihazTipi,
                    CihazMarka = CihazMarka,
                    CihazModeli = CihazModeli,
                    CihazKapasite = CihazKapasite,
                    SeriNo = SeriNo,
                    TeknisyenAdi = TeknisyenAdi,
                    TeknisyenSertifikaNo = TeknisyenSertifikaNo,
                    DevreyeAlmaTarihi = DevreyeAlmaTarihi,
                    Notlar = Notlar,
                    Durum = Durum,
                    PdfYolu = PdfYolu,
                    OlusturmaTarihi = OlusturmaTarihi,
                    Firma = new Ys_Firma
                    {
                        Id = FirmaId,
                        FirmaAdi = FirmaAdi,
                        FaaliyetIli = FirmaFaaliyetIli,
                        Adres = FirmaAdres,
                        SirketId = FirmaSirketId,
                        Sirket = new Dag_Sirket
                        {
                            Id = FirmaSirketId,
                            SirketAdi = SirketAdi
                        }
                    },
                    Marka = new Ys_Marka
                    {
                        Id = MarkaId ?? 0,
                        MarkaAdi = MarkaAdi
                    }
                };
            }
        }

        private class AdminSertifikaCevap
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

    public class AdminDevreyeAlmaListeSonuc
    {
        public List<Ys_DevreyeAlma> Islemler { get; set; } = new();
        public List<Ys_Marka> Markalar { get; set; } = new();
        public Dictionary<int, string> FirmaIlceleri { get; set; } = new();
    }

    public class AdminYetkiBelgesiUyariSonuc
    {
        public List<Ys_Sertifika> Yaklasan { get; set; } = new();
        public List<Ys_Sertifika> Gecmis { get; set; } = new();
    }

    public class AdminRaporOzetSonuc
    {
        public DateTime BasTarih { get; set; }
        public DateTime BitTarih { get; set; }
        public string RaporTipi { get; set; } = "devreye";
        public string ListeTipi { get; set; } = "devreye";
        public int DevreyeSayisi { get; set; }
        public int DevreyeTamamlanan { get; set; }
        public int DevreyeBekleyen { get; set; }
        public int DevreyeIptal { get; set; }
        public int SertifikaOnayli { get; set; }
        public int SertifikaBekleyen { get; set; }
        public int SertifikaReddedilen { get; set; }
        public List<string> ChartSirketLabels { get; set; } = new();
        public List<int> ChartSirketData { get; set; } = new();
        public List<string> ChartAylikLabels { get; set; } = new();
        public List<int> ChartAylikData { get; set; } = new();
        public List<int> ChartDurumData { get; set; } = new();
        public List<string?> ChartMarkaLabels { get; set; } = new();
        public List<int> ChartMarkaData { get; set; } = new();
        public List<Ys_DevreyeAlma> SonIslemler { get; set; } = new();
        public List<Ys_Sertifika> SertifikaIslemler { get; set; } = new();
        public List<Dag_Sirket> Sirketler { get; set; } = new();
    }
}
