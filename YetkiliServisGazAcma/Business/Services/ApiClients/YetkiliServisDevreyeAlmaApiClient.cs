using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisDevreyeAlmaApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<YetkiliServisDevreyeAlmaApiClient> _logger;

        public YetkiliServisDevreyeAlmaApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YetkiliServisDevreyeAlmaApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<YsDevreyeAlmaGecmisSonuc?> GecmisAsync(
            AppKullanici kullanici,
            string? marka,
            DateTime? bas,
            DateTime? bit,
            string? musteri,
            string? durum)
        {
            var cevap = await PostAsync<YsDevreyeAlmaGecmisIstek, YsDevreyeAlmaGecmisCevap>(
                kullanici,
                "api/ys-devreyeal/gecmis",
                new YsDevreyeAlmaGecmisIstek
                {
                    Marka = marka,
                    BaslangicTarihi = bas,
                    BitisTarihi = bit,
                    Musteri = musteri,
                    Durum = durum
                },
                "Yetkili servis devreye alma gecmis");

            return cevap?.ToSonuc();
        }

        public async Task<Ys_DevreyeAlma?> DetayAsync(AppKullanici kullanici, int id)
        {
            var cevap = await PostAsync<YsDevreyeAlmaGetirIstek, YsDevreyeAlmaCevap>(
                kullanici,
                "api/ys-devreyeal/getir",
                new YsDevreyeAlmaGetirIstek { Id = id },
                "Yetkili servis devreye alma detay");

            return cevap?.ToEntity();
        }

        public async Task<YsDevreyeAlmaEkranSonuc?> EkranAsync(AppKullanici kullanici)
        {
            var cevap = await PostAsync<YsDevreyeAlmaBosIstek, YsDevreyeAlmaEkranCevap>(
                kullanici,
                "api/ys-devreyeal/ekran",
                new YsDevreyeAlmaBosIstek(),
                "Yetkili servis devreye alma ekran");

            return cevap?.ToSonuc();
        }

        public Task<YsDevreyeAlmaBildirimSonuc?> BildirimlerAsync(AppKullanici kullanici)
        {
            return PostAsync<YsDevreyeAlmaBosIstek, YsDevreyeAlmaBildirimSonuc>(
                kullanici,
                "api/ys-devreyeal/bildirimler",
                new YsDevreyeAlmaBosIstek(),
                "Yetkili servis devreye alma bildirimler");
        }

        public Task<YsTesisatSorguSonuc?> TesisatSorgulaAsync(AppKullanici kullanici, string? tesisatNo, string? sozlesmeNo)
        {
            return PostAsync<YsTesisatSorguIstek, YsTesisatSorguSonuc>(
                kullanici,
                "api/ys-devreyeal/tesisat-sorgula",
                new YsTesisatSorguIstek
                {
                    TesistatNo = tesisatNo,
                    SozlesmeNo = sozlesmeNo
                },
                "Yetkili servis tesisat sorgula");
        }

        public Task<YsMarkaKontrolSonuc?> MarkaKontrolAsync(AppKullanici kullanici, string? cihazMarka)
        {
            return PostAsync<YsMarkaKontrolIstek, YsMarkaKontrolSonuc>(
                kullanici,
                "api/ys-devreyeal/marka-kontrol",
                new YsMarkaKontrolIstek { CihazMarka = cihazMarka },
                "Yetkili servis marka kontrol");
        }

        public Task<YsDevreyeAlmaIslemSonuc?> KaydetAsync(AppKullanici kullanici, Ys_DevreyeAlma model)
        {
            return PostAsync<YsDevreyeAlmaKaydetIstek, YsDevreyeAlmaIslemSonuc>(
                kullanici,
                "api/ys-devreyeal/kaydet",
                YsDevreyeAlmaKaydetIstek.FromEntity(model),
                "Yetkili servis devreye alma kaydet");
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

        private class YsDevreyeAlmaGecmisIstek
        {
            public string? Marka { get; set; }
            public DateTime? BaslangicTarihi { get; set; }
            public DateTime? BitisTarihi { get; set; }
            public string? Musteri { get; set; }
            public string? Durum { get; set; }
        }

        private class YsDevreyeAlmaGetirIstek
        {
            public int Id { get; set; }
        }

        private class YsDevreyeAlmaBosIstek
        {
        }

        private class YsMarkaKontrolIstek
        {
            public string? CihazMarka { get; set; }
        }

        private class YsTesisatSorguIstek
        {
            public string? TesistatNo { get; set; }
            public string? SozlesmeNo { get; set; }
        }

        private class YsDevreyeAlmaKaydetIstek
        {
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
            public string? TeknisyenYetkiBelgesiNo { get; set; }
            public string? Notlar { get; set; }

            public static YsDevreyeAlmaKaydetIstek FromEntity(Ys_DevreyeAlma model)
            {
                return new YsDevreyeAlmaKaydetIstek
                {
                    TesistatNo = model.TesistatNo,
                    AboneNo = model.AboneNo,
                    UygunlukBelgeNo = model.UygunlukBelgeNo,
                    UygunlukTarihi = model.UygunlukTarihi,
                    MusteriAdi = model.MusteriAdi,
                    MusteriTcNo = model.MusteriTcNo,
                    MusteriTelefon = model.MusteriTelefon,
                    Adres = model.Adres,
                    CihazTipi = model.CihazTipi,
                    CihazMarka = model.CihazMarka,
                    CihazModeli = model.CihazModeli,
                    CihazKapasite = model.CihazKapasite,
                    SeriNo = model.SeriNo,
                    TeknisyenAdi = model.TeknisyenAdi,
                    TeknisyenYetkiBelgesiNo = model.TeknisyenYetkiBelgesiNo,
                    Notlar = model.Notlar
                };
            }
        }

        private class YsDevreyeAlmaGecmisCevap
        {
            public List<YsDevreyeAlmaCevap> Islemler { get; set; } = new();
            public YsFirmaCevap? Firma { get; set; }
            public List<string> MarkaList { get; set; } = new();

            public YsDevreyeAlmaGecmisSonuc ToSonuc()
            {
                return new YsDevreyeAlmaGecmisSonuc
                {
                    Islemler = Islemler.Select(x => x.ToEntity()).ToList(),
                    Firma = Firma?.ToEntity(),
                    MarkaList = MarkaList
                };
            }
        }

        private class YsDevreyeAlmaEkranCevap
        {
            public bool Erisilebilir { get; set; }
            public string? Hata { get; set; }
            public string? RedirectUrl { get; set; }
            public YsFirmaCevap? Firma { get; set; }
            public List<YsMarkaCevap> Markalar { get; set; } = new();

            public YsDevreyeAlmaEkranSonuc ToSonuc()
            {
                return new YsDevreyeAlmaEkranSonuc
                {
                    Erisilebilir = Erisilebilir,
                    Hata = Hata,
                    RedirectUrl = RedirectUrl,
                    Firma = Firma?.ToEntity(),
                    Markalar = Markalar.Select(x => x.ToEntity()).ToList()
                };
            }
        }

        private class YsFirmaCevap
        {
            public int Id { get; set; }
            public string? FirmaAdi { get; set; }
            public string? YetkiliKisi { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public string? FaaliyetIli { get; set; }
            public int SirketId { get; set; }
            public string? SirketAdi { get; set; }
            public string? SirketIl { get; set; }

            public Ys_Firma ToEntity()
            {
                return new Ys_Firma
                {
                    Id = Id,
                    FirmaAdi = FirmaAdi,
                    YetkiliKisi = YetkiliKisi,
                    Telefon = Telefon,
                    Email = Email,
                    Adres = Adres,
                    FaaliyetIli = FaaliyetIli,
                    SirketId = SirketId,
                    Sirket = new Dag_Sirket
                    {
                        Id = SirketId,
                        SirketAdi = SirketAdi,
                        Il = SirketIl
                    }
                };
            }
        }

        private class YsMarkaCevap
        {
            public int Id { get; set; }
            public string? MarkaAdi { get; set; }
            public string? Aciklama { get; set; }
            public bool AktifMi { get; set; }

            public Ys_Marka ToEntity()
            {
                return new Ys_Marka
                {
                    Id = Id,
                    MarkaAdi = MarkaAdi,
                    Aciklama = Aciklama,
                    AktifMi = AktifMi
                };
            }
        }

        private class YsDevreyeAlmaCevap
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
            public string? TeknisyenYetkiBelgesiNo { get; set; }
            public DateTime DevreyeAlmaTarihi { get; set; }
            public string? Notlar { get; set; }
            public int Durum { get; set; }
            public string? PdfYolu { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public string? FirmaAdi { get; set; }
            public string? FirmaYetkiliKisi { get; set; }
            public string? FirmaTelefon { get; set; }
            public string? FirmaEmail { get; set; }
            public string? FirmaAdres { get; set; }
            public string? FirmaFaaliyetIli { get; set; }
            public int FirmaSirketId { get; set; }
            public string? SirketAdi { get; set; }
            public string? SirketIl { get; set; }
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
                    TeknisyenYetkiBelgesiNo = TeknisyenYetkiBelgesiNo,
                    DevreyeAlmaTarihi = DevreyeAlmaTarihi,
                    Notlar = Notlar,
                    Durum = Durum,
                    PdfYolu = PdfYolu,
                    OlusturmaTarihi = OlusturmaTarihi,
                    Firma = new Ys_Firma
                    {
                        Id = FirmaId,
                        FirmaAdi = FirmaAdi,
                        YetkiliKisi = FirmaYetkiliKisi,
                        Telefon = FirmaTelefon,
                        Email = FirmaEmail,
                        Adres = FirmaAdres,
                        FaaliyetIli = FirmaFaaliyetIli,
                        SirketId = FirmaSirketId,
                        Sirket = new Dag_Sirket
                        {
                            Id = FirmaSirketId,
                            SirketAdi = SirketAdi,
                            Il = SirketIl
                        }
                    },
                    Marka = MarkaId.HasValue
                        ? new Ys_Marka { Id = MarkaId.Value, MarkaAdi = MarkaAdi }
                        : null
                };
            }
        }
    }

    public class YsDevreyeAlmaGecmisSonuc
    {
        public List<Ys_DevreyeAlma> Islemler { get; set; } = new();
        public Ys_Firma? Firma { get; set; }
        public List<string> MarkaList { get; set; } = new();
    }

    public class YsDevreyeAlmaEkranSonuc
    {
        public bool Erisilebilir { get; set; }
        public string? Hata { get; set; }
        public string? RedirectUrl { get; set; }
        public Ys_Firma? Firma { get; set; }
        public List<Ys_Marka> Markalar { get; set; } = new();
    }

    public class YsDevreyeAlmaBildirimSonuc
    {
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsMarkaKontrolSonuc
    {
        public bool Yetkili { get; set; }
        public string? Mesaj { get; set; }
        public int? MarkaId { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class YsTesisatSorguSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
        public string? TesistatNo { get; set; }
        public string? SozlesmeNo { get; set; }
        public string? AboneNo { get; set; }
        public string? SayacNo { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTcNo { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Adres { get; set; }
        public string? UygunlukBelgeNo { get; set; }
        public string? UygunlukTarihi { get; set; }
        public string? Durum { get; set; }
        public List<YsTesisatCihazSonuc> Cihazlar { get; set; } = new();
    }

    public class YsTesisatCihazSonuc
    {
        public string? CihazMarka { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazKapasite { get; set; }
    }

    public class YsDevreyeAlmaIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
        public int? Id { get; set; }
        public string? RedirectUrl { get; set; }
    }
}
