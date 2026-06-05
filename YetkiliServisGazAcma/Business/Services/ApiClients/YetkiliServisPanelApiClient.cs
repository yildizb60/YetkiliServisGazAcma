using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisPanelApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ApiJwtTokenService _tokenService;
        private readonly ILogger<YetkiliServisPanelApiClient> _logger;

        public YetkiliServisPanelApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YetkiliServisPanelApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<YsPanelDashboardSonuc?> DashboardAsync(AppKullanici kullanici)
        {
            var cevap = await PostAsync<YsPanelBosIstek, YsPanelDashboardCevap>(
                kullanici,
                "api/ys-panel/dashboard",
                new YsPanelBosIstek(),
                "Yetkili servis panel dashboard");

            return cevap?.ToSonuc();
        }

        public Task<YsPanelBildirimSonuc?> BildirimlerAsync(AppKullanici kullanici)
        {
            return PostAsync<YsPanelBosIstek, YsPanelBildirimSonuc>(
                kullanici,
                "api/ys-panel/bildirimler",
                new YsPanelBosIstek(),
                "Yetkili servis panel bildirimler");
        }

        public async Task<Ys_Firma?> ProfilAsync(AppKullanici kullanici)
        {
            var cevap = await PostAsync<YsPanelBosIstek, YsPanelFirmaCevap>(
                kullanici,
                "api/ys-panel/profil",
                new YsPanelBosIstek(),
                "Yetkili servis panel profil");

            return cevap?.ToEntity();
        }

        public Task<YsPanelIslemSonuc?> ProfilGuncelleAsync(
            AppKullanici kullanici,
            string? adSoyad,
            string? telefon,
            string? email)
        {
            return PostAsync<YsPanelProfilGuncelleIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/profil/guncelle",
                new YsPanelProfilGuncelleIstek
                {
                    AdSoyad = adSoyad,
                    Telefon = telefon,
                    Email = email
                },
                "Yetkili servis panel profil guncelle");
        }

        public async Task<YsPanelIlkKurulumSonuc?> IlkKurulumAsync(AppKullanici kullanici)
        {
            var cevap = await PostAsync<YsPanelBosIstek, YsPanelIlkKurulumCevap>(
                kullanici,
                "api/ys-panel/ilk-kurulum",
                new YsPanelBosIstek(),
                "Yetkili servis panel ilk kurulum");

            return cevap?.ToSonuc();
        }

        public async Task<YsPanelMarkalarSonuc?> MarkalarAsync(AppKullanici kullanici)
        {
            var cevap = await PostAsync<YsPanelBosIstek, YsPanelMarkalarCevap>(
                kullanici,
                "api/ys-panel/markalar",
                new YsPanelBosIstek(),
                "Yetkili servis panel markalar");

            return cevap?.ToSonuc();
        }

        public Task<YsPanelIslemSonuc?> SubeKaydetAsync(
            AppKullanici kullanici,
            int id,
            string? subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            return PostAsync<YsPanelSubeKaydetIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/subeler/kaydet",
                new YsPanelSubeKaydetIstek
                {
                    Id = id,
                    SubeAdi = subeAdi,
                    Il = il,
                    Ilce = ilce,
                    Telefon = telefon,
                    Adres = adres,
                    AktifMi = aktifMi
                },
                "Yetkili servis panel sube kaydet");
        }

        public Task<YsPanelIslemSonuc?> SubeDurumAsync(AppKullanici kullanici, int id)
        {
            return PostAsync<YsPanelIdIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/subeler/durum",
                new YsPanelIdIstek { Id = id },
                "Yetkili servis panel sube durum");
        }

        public Task<YsPanelIslemSonuc?> SubeSilAsync(AppKullanici kullanici, int id)
        {
            return PostAsync<YsPanelIdIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/subeler/sil",
                new YsPanelIdIstek { Id = id },
                "Yetkili servis panel sube sil");
        }

        public Task<YsPanelIslemSonuc?> MarkaGuncelleAsync(AppKullanici kullanici, List<int> markaIds)
        {
            return PostAsync<YsPanelMarkaGuncelleIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/guncelle",
                new YsPanelMarkaGuncelleIstek { MarkaIds = markaIds ?? new List<int>() },
                "Yetkili servis panel marka guncelle");
        }

        public Task<YsPanelIslemSonuc?> MarkaEkleAsync(AppKullanici kullanici, string? markaAdi, string? aciklama)
        {
            return PostAsync<YsPanelMarkaKaydetIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/ekle",
                new YsPanelMarkaKaydetIstek
                {
                    MarkaAdi = markaAdi,
                    Aciklama = aciklama
                },
                "Yetkili servis panel marka ekle");
        }

        public Task<YsPanelIslemSonuc?> MarkaDuzenleAsync(AppKullanici kullanici, int id, string? markaAdi, string? aciklama)
        {
            return PostAsync<YsPanelMarkaKaydetIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/duzenle",
                new YsPanelMarkaKaydetIstek
                {
                    Id = id,
                    MarkaAdi = markaAdi,
                    Aciklama = aciklama
                },
                "Yetkili servis panel marka duzenle");
        }

        public Task<YsPanelIslemSonuc?> MarkaSilAsync(AppKullanici kullanici, int id)
        {
            return PostAsync<YsPanelIdIstek, YsPanelIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/sil",
                new YsPanelIdIstek { Id = id },
                "Yetkili servis panel marka sil");
        }

        public async Task<YsPanelRaporSonuc?> RaporlarAsync(
            AppKullanici kullanici,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids = null,
            int? limit = null)
        {
            var cevap = await PostAsync<YsPanelRaporFiltreIstek, YsPanelRaporSonucCevap>(
                kullanici,
                "api/ys-panel/raporlar",
                new YsPanelRaporFiltreIstek
                {
                    Bas = bas,
                    Bit = bit,
                    Ids = ids,
                    Limit = limit
                },
                "Yetkili servis panel raporlar");

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

            var token = await _tokenService.OlusturAsync(kullanici);
            if (string.IsNullOrWhiteSpace(token))
            {
                ApiClientFallback.EnsureAllowed(_options, $"{operasyon} token");
                return default;
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = JsonContent.Create(istek);

                    using var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        if ((int)response.StatusCode >= 500 && attempt < 3)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
                            continue;
                        }

                        _logger.LogWarning("{Operasyon} API cagrisinda basarisiz yanit dondu. Url: {Url}, StatusCode: {StatusCode}", operasyon, url, response.StatusCode);
                        ApiClientFallback.EnsureAllowed(_options, operasyon);
                        return default;
                    }

                    return await response.Content.ReadFromJsonAsync<TResponse>();
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
                {
                    if (attempt < 3)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
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

        private class YsPanelBosIstek
        {
        }

        private class YsPanelIdIstek
        {
            public int Id { get; set; }
        }

        private class YsPanelSubeKaydetIstek
        {
            public int Id { get; set; }
            public string? SubeAdi { get; set; }
            public string? Il { get; set; }
            public string? Ilce { get; set; }
            public string? Telefon { get; set; }
            public string? Adres { get; set; }
            public bool AktifMi { get; set; }
        }

        private class YsPanelMarkaGuncelleIstek
        {
            public List<int> MarkaIds { get; set; } = new();
        }

        private class YsPanelMarkaKaydetIstek
        {
            public int Id { get; set; }
            public string? MarkaAdi { get; set; }
            public string? Aciklama { get; set; }
        }

        private class YsPanelProfilGuncelleIstek
        {
            public string? AdSoyad { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
        }

        private class YsPanelRaporFiltreIstek
        {
            public DateTime? Bas { get; set; }
            public DateTime? Bit { get; set; }
            public List<int>? Ids { get; set; }
            public int? Limit { get; set; }
        }

        private class YsPanelDashboardCevap
        {
            public YsPanelFirmaCevap? Firma { get; set; }
            public int BuAy { get; set; }
            public int Toplam { get; set; }
            public List<YsPanelDevreyeAlmaCevap> SonIslemler { get; set; } = new();
            public bool IlkKurulumZorunlu { get; set; }
            public bool IlkKurulumTamamlandi { get; set; }
            public List<string> IlkKurulumEksikler { get; set; } = new();
            public int? YetkiBelgesiUyariGun { get; set; }
            public List<string> Bildirimler { get; set; } = new();
            public int BildirimSayisi { get; set; }

            public YsPanelDashboardSonuc ToSonuc()
            {
                return new YsPanelDashboardSonuc
                {
                    Firma = Firma?.ToEntity(),
                    BuAy = BuAy,
                    Toplam = Toplam,
                    SonIslemler = SonIslemler.Select(x => x.ToEntity()).ToList(),
                    IlkKurulumZorunlu = IlkKurulumZorunlu,
                    IlkKurulumTamamlandi = IlkKurulumTamamlandi,
                    IlkKurulumEksikler = IlkKurulumEksikler,
                    YetkiBelgesiUyariGun = YetkiBelgesiUyariGun,
                    Bildirimler = Bildirimler,
                    BildirimSayisi = BildirimSayisi
                };
            }
        }

        private class YsPanelRaporSonucCevap
        {
            public YsPanelFirmaCevap? Firma { get; set; }
            public DateTime BasTarih { get; set; }
            public DateTime BitTarih { get; set; }
            public int DevreyeSayisi { get; set; }
            public int Tamamlanan { get; set; }
            public int Bekleyen { get; set; }
            public int YetkiBelgesiOnayli { get; set; }
            public int YetkiBelgesiBekleyen { get; set; }
            public int YetkiBelgesiReddedilen { get; set; }
            public List<YsPanelDevreyeAlmaCevap> SonIslemler { get; set; } = new();
            public List<string> ChartAylikLabels { get; set; } = new();
            public List<int> ChartAylikData { get; set; } = new();
            public List<int> ChartDurumData { get; set; } = new();
            public List<string> ChartMarkaLabels { get; set; } = new();
            public List<int> ChartMarkaData { get; set; } = new();

            public YsPanelRaporSonuc ToSonuc()
            {
                return new YsPanelRaporSonuc
                {
                    Firma = Firma?.ToEntity(),
                    BasTarih = BasTarih,
                    BitTarih = BitTarih,
                    DevreyeSayisi = DevreyeSayisi,
                    Tamamlanan = Tamamlanan,
                    Bekleyen = Bekleyen,
                    YetkiBelgesiOnayli = YetkiBelgesiOnayli,
                    YetkiBelgesiBekleyen = YetkiBelgesiBekleyen,
                    YetkiBelgesiReddedilen = YetkiBelgesiReddedilen,
                    SonIslemler = SonIslemler.Select(x => x.ToEntity()).ToList(),
                    ChartAylikLabels = ChartAylikLabels,
                    ChartAylikData = ChartAylikData,
                    ChartDurumData = ChartDurumData,
                    ChartMarkaLabels = ChartMarkaLabels,
                    ChartMarkaData = ChartMarkaData
                };
            }
        }

        private class YsPanelMarkalarCevap
        {
            public YsPanelFirmaCevap? Firma { get; set; }
            public List<YsPanelMarkaCevap> TumMarkalar { get; set; } = new();
            public List<YsPanelFirmaMarkaCevap> FirmaMarkalar { get; set; } = new();
            public List<int> SeciliMarkaIds { get; set; } = new();

            public YsPanelMarkalarSonuc ToSonuc()
            {
                return new YsPanelMarkalarSonuc
                {
                    Firma = Firma?.ToEntity(),
                    TumMarkalar = TumMarkalar.Select(x => x.ToEntity()).ToList(),
                    FirmaMarkalar = FirmaMarkalar.Select(x => x.ToEntity()).ToList(),
                    SeciliMarkaIds = SeciliMarkaIds
                };
            }
        }

        private class YsPanelIlkKurulumCevap
        {
            public YsPanelFirmaCevap? Firma { get; set; }
            public List<YsPanelMarkaCevap> TumMarkalar { get; set; } = new();
            public List<YsPanelUrunKategoriCevap> TumKategoriler { get; set; } = new();
            public List<int> SeciliMarkaIds { get; set; } = new();
            public List<int> SeciliKategoriIds { get; set; } = new();
            public int AktifSubeSayisi { get; set; }
            public bool YetkiBelgesiVar { get; set; }
            public bool OnayliYetkiBelgesiVar { get; set; }
            public bool ZorunluMu { get; set; }
            public bool TamamlandiMi { get; set; }
            public List<string> Eksikler { get; set; } = new();
            public string? HataMesaji { get; set; }

            public YsPanelIlkKurulumSonuc ToSonuc()
            {
                return new YsPanelIlkKurulumSonuc
                {
                    Firma = Firma?.ToEntity(),
                    TumMarkalar = TumMarkalar.Select(x => x.ToEntity()).ToList(),
                    TumKategoriler = TumKategoriler.Select(x => x.ToEntity()).ToList(),
                    SeciliMarkaIds = SeciliMarkaIds,
                    SeciliKategoriIds = SeciliKategoriIds,
                    AktifSubeSayisi = AktifSubeSayisi,
                    YetkiBelgesiVar = YetkiBelgesiVar,
                    OnayliYetkiBelgesiVar = OnayliYetkiBelgesiVar,
                    ZorunluMu = ZorunluMu,
                    TamamlandiMi = TamamlandiMi,
                    Eksikler = Eksikler,
                    HataMesaji = HataMesaji
                };
            }
        }

        private class YsPanelFirmaCevap
        {
            public int Id { get; set; }
            public string? FirmaAdi { get; set; }
            public string? YetkiliKisi { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public string? VergiNo { get; set; }
            public string? FaaliyetIli { get; set; }
            public int SirketId { get; set; }
            public YsPanelSirketCevap? Sirket { get; set; }
            public List<YsPanelYetkiBelgesiCevap> YetkiBelgeleri { get; set; } = new();
            public List<YsPanelFirmaMarkaCevap> FirmaMarkalar { get; set; } = new();
            public List<YsPanelFirmaKategoriCevap> FirmaKategoriler { get; set; } = new();
            public List<YsPanelSubeCevap> Subeler { get; set; } = new();

            public Ys_Firma ToEntity()
            {
                var firma = new Ys_Firma
                {
                    Id = Id,
                    FirmaAdi = FirmaAdi,
                    YetkiliKisi = YetkiliKisi,
                    Telefon = Telefon,
                    Email = Email,
                    Adres = Adres,
                    VergiNo = VergiNo,
                    FaaliyetIli = FaaliyetIli,
                    SirketId = SirketId,
                    Sirket = Sirket?.ToEntity()
                };

                firma.YetkiBelgeleri = YetkiBelgeleri.Select(x => x.ToEntity()).ToList();
                firma.FirmaMarkalar = FirmaMarkalar.Select(x => x.ToEntity()).ToList();
                firma.FirmaKategoriler = FirmaKategoriler.Select(x => x.ToEntity()).ToList();
                firma.Subeler = Subeler.Select(x => x.ToEntity()).ToList();
                return firma;
            }
        }

        private class YsPanelSirketCevap
        {
            public int Id { get; set; }
            public string? SirketAdi { get; set; }
            public string? Il { get; set; }

            public Dag_Sirket ToEntity()
            {
                return new Dag_Sirket
                {
                    Id = Id,
                    SirketAdi = SirketAdi,
                    Il = Il
                };
            }
        }

        private class YsPanelYetkiBelgesiCevap
        {
            public int Id { get; set; }
            public int Durum { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
            public DateTime YetkiBelgesiBitisTarihi { get; set; }
            public bool SilindiMi { get; set; }

            public Ys_YetkiBelgesi ToEntity()
            {
                return new Ys_YetkiBelgesi
                {
                    Id = Id,
                    Durum = Durum,
                    OlusturmaTarihi = OlusturmaTarihi,
                    YetkiBelgesiBaslangicTarihi = YetkiBelgesiBaslangicTarihi,
                    YetkiBelgesiBitisTarihi = YetkiBelgesiBitisTarihi,
                    SilindiMi = SilindiMi
                };
            }
        }

        private class YsPanelFirmaMarkaCevap
        {
            public int Id { get; set; }
            public int MarkaId { get; set; }
            public bool SilindiMi { get; set; }
            public YsPanelMarkaCevap? Marka { get; set; }

            public Ys_FirmaMarka ToEntity()
            {
                return new Ys_FirmaMarka
                {
                    Id = Id,
                    MarkaId = MarkaId,
                    SilindiMi = SilindiMi,
                    Marka = Marka?.ToEntity()
                };
            }
        }

        private class YsPanelMarkaCevap
        {
            public int Id { get; set; }
            public string? MarkaAdi { get; set; }
            public bool AktifMi { get; set; }

            public Ys_Marka ToEntity()
            {
                return new Ys_Marka
                {
                    Id = Id,
                    MarkaAdi = MarkaAdi,
                    AktifMi = AktifMi
                };
            }
        }

        private class YsPanelFirmaKategoriCevap
        {
            public int Id { get; set; }
            public int KategoriId { get; set; }
            public bool SilindiMi { get; set; }
            public YsPanelUrunKategoriCevap? Kategori { get; set; }

            public Ys_FirmaKategori ToEntity()
            {
                return new Ys_FirmaKategori
                {
                    Id = Id,
                    KategoriId = KategoriId,
                    SilindiMi = SilindiMi,
                    Kategori = Kategori?.ToEntity()
                };
            }
        }

        private class YsPanelUrunKategoriCevap
        {
            public int Id { get; set; }
            public string? Ad { get; set; }
            public bool AktifMi { get; set; }

            public UrunKategori ToEntity()
            {
                return new UrunKategori
                {
                    Id = Id,
                    Ad = Ad,
                    AktifMi = AktifMi
                };
            }
        }

        private class YsPanelSubeCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public string? SubeAdi { get; set; }
            public string? Il { get; set; }
            public string? Ilce { get; set; }
            public string? Telefon { get; set; }
            public string? Adres { get; set; }
            public bool AktifMi { get; set; }
            public bool SilindiMi { get; set; }
            public DateTime OlusturmaTarihi { get; set; }

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
                    SilindiMi = SilindiMi,
                    OlusturmaTarihi = OlusturmaTarihi
                };
            }
        }

        private class YsPanelDevreyeAlmaCevap
        {
            public int Id { get; set; }
            public int FirmaId { get; set; }
            public int? MarkaId { get; set; }
            public string? TesistatNo { get; set; }
            public string? MusteriAdi { get; set; }
            public string? MusteriTelefon { get; set; }
            public string? MusteriTcNo { get; set; }
            public string? Adres { get; set; }
            public string? CihazTipi { get; set; }
            public string? CihazMarka { get; set; }
            public string? CihazModeli { get; set; }
            public string? SeriNo { get; set; }
            public string? CihazKapasite { get; set; }
            public string? TeknisyenAdi { get; set; }
            public string? TeknisyenYetkiBelgesiNo { get; set; }
            public int Durum { get; set; }
            public DateTime DevreyeAlmaTarihi { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public string? Notlar { get; set; }
            public YsPanelFirmaCevap? Firma { get; set; }
            public YsPanelMarkaCevap? Marka { get; set; }

            public Ys_DevreyeAlma ToEntity()
            {
                return new Ys_DevreyeAlma
                {
                    Id = Id,
                    FirmaId = FirmaId,
                    MarkaId = MarkaId,
                    TesistatNo = TesistatNo,
                    MusteriAdi = MusteriAdi,
                    MusteriTelefon = MusteriTelefon,
                    MusteriTcNo = MusteriTcNo,
                    Adres = Adres,
                    CihazTipi = CihazTipi,
                    CihazMarka = CihazMarka,
                    CihazModeli = CihazModeli,
                    SeriNo = SeriNo,
                    CihazKapasite = CihazKapasite,
                    TeknisyenAdi = TeknisyenAdi,
                    TeknisyenYetkiBelgesiNo = TeknisyenYetkiBelgesiNo,
                    Durum = Durum,
                    DevreyeAlmaTarihi = DevreyeAlmaTarihi,
                    OlusturmaTarihi = OlusturmaTarihi,
                    Notlar = Notlar,
                    Firma = Firma?.ToEntity(),
                    Marka = Marka?.ToEntity()
                };
            }
        }
    }

    public class YsPanelDashboardSonuc
    {
        public Ys_Firma? Firma { get; set; }
        public int BuAy { get; set; }
        public int Toplam { get; set; }
        public List<Ys_DevreyeAlma> SonIslemler { get; set; } = new();
        public bool IlkKurulumZorunlu { get; set; }
        public bool IlkKurulumTamamlandi { get; set; }
        public List<string> IlkKurulumEksikler { get; set; } = new();
        public int? YetkiBelgesiUyariGun { get; set; }
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsPanelBildirimSonuc
    {
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsPanelMarkalarSonuc
    {
        public Ys_Firma? Firma { get; set; }
        public List<Ys_Marka> TumMarkalar { get; set; } = new();
        public List<Ys_FirmaMarka> FirmaMarkalar { get; set; } = new();
        public List<int> SeciliMarkaIds { get; set; } = new();
    }

    public class YsPanelIlkKurulumSonuc
    {
        public Ys_Firma? Firma { get; set; }
        public List<Ys_Marka> TumMarkalar { get; set; } = new();
        public List<UrunKategori> TumKategoriler { get; set; } = new();
        public List<int> SeciliMarkaIds { get; set; } = new();
        public List<int> SeciliKategoriIds { get; set; } = new();
        public int AktifSubeSayisi { get; set; }
        public bool YetkiBelgesiVar { get; set; }
        public bool OnayliYetkiBelgesiVar { get; set; }
        public bool ZorunluMu { get; set; }
        public bool TamamlandiMi { get; set; }
        public List<string> Eksikler { get; set; } = new();
        public string? HataMesaji { get; set; }
    }

    public class YsPanelRaporSonuc
    {
        public Ys_Firma? Firma { get; set; }
        public DateTime BasTarih { get; set; }
        public DateTime BitTarih { get; set; }
        public int DevreyeSayisi { get; set; }
        public int Tamamlanan { get; set; }
        public int Bekleyen { get; set; }
        public int YetkiBelgesiOnayli { get; set; }
        public int YetkiBelgesiBekleyen { get; set; }
        public int YetkiBelgesiReddedilen { get; set; }
        public List<Ys_DevreyeAlma> SonIslemler { get; set; } = new();
        public List<string> ChartAylikLabels { get; set; } = new();
        public List<int> ChartAylikData { get; set; } = new();
        public List<int> ChartDurumData { get; set; } = new();
        public List<string> ChartMarkaLabels { get; set; } = new();
        public List<int> ChartMarkaData { get; set; } = new();
    }

    public class YsPanelIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
    }
}
