using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisPanelApiClient
    {
        private readonly ApiHttpClient _api;

        public YetkiliServisPanelApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YetkiliServisPanelApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<YsPanelDashboardDto?> DashboardAsync(AppKullanici kullanici, DateTime? takvimTarih = null, string? takvimGorunum = null)
        {
            return _api.PostAsync<YsPanelDashboardFiltreDto, YsPanelDashboardDto>(
                kullanici,
                "api/ys-panel/dashboard",
                new YsPanelDashboardFiltreDto { TakvimTarih = takvimTarih, TakvimGorunum = takvimGorunum },
                "Yetkili servis panel dashboard", retryTransient: true);
        }

        public Task<YsPanelBildirimDto?> BildirimlerAsync(AppKullanici kullanici)
        {
            return _api.PostAsync<object, YsPanelBildirimDto>(
                kullanici,
                "api/ys-panel/bildirimler",
                new object(),
                "Yetkili servis panel bildirimler", retryTransient: true);
        }

        public Task<List<AdminSubeDto>?> SubelerAsync(AppKullanici kullanici)
            => _api.PostAsync<object, List<AdminSubeDto>>(kullanici, "api/ys-panel/subeler/liste",
                new(), "Yetkili servis şube listesi");

        public Task<AdminSubeDto?> SubeGetirAsync(AppKullanici kullanici, int id)
            => _api.PostAsync<SubeGetirDto, AdminSubeDto>(kullanici, "api/ys-panel/subeler/getir",
                new SubeGetirDto { Id = id }, "Yetkili servis şube detayı");

        public Task<YsPanelFirmaDto?> ProfilAsync(AppKullanici kullanici)
        {
            return _api.PostAsync<object, YsPanelFirmaDto>(
                kullanici,
                "api/ys-panel/profil",
                new object(),
                "Yetkili servis panel profil", retryTransient: true);
        }

        public Task<ApiIslemSonuc?> ProfilGuncelleAsync(
            AppKullanici kullanici,
            string? adSoyad,
            string? telefon,
            string? email)
        {
            return _api.PostAsync<YsPanelProfilGuncelleDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/profil/guncelle",
                new YsPanelProfilGuncelleDto
                {
                    AdSoyad = adSoyad,
                    Telefon = telefon,
                    Email = email
                },
                "Yetkili servis panel profil guncelle");
        }

        public Task<YsPanelIlkKurulumDto?> IlkKurulumAsync(AppKullanici kullanici)
        {
            return _api.PostAsync<object, YsPanelIlkKurulumDto>(
                kullanici,
                "api/ys-panel/ilk-kurulum",
                new object(),
                "Yetkili servis panel ilk kurulum", retryTransient: true);
        }

        public Task<YsPanelMarkalarDto?> MarkalarAsync(AppKullanici kullanici)
        {
            return _api.PostAsync<object, YsPanelMarkalarDto>(
                kullanici,
                "api/ys-panel/markalar",
                new object(),
                "Yetkili servis panel markalar", retryTransient: true);
        }

        public Task<ApiIslemSonuc?> SubeKaydetAsync(
            AppKullanici kullanici,
            int id,
            string? subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            return _api.PostAsync<YsPanelSubeKaydetDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/subeler/kaydet",
                new YsPanelSubeKaydetDto
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

        public Task<ApiIslemSonuc?> SubeDurumAsync(AppKullanici kullanici, int id)
        {
            return _api.PostAsync<YsPanelIdDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/subeler/durum",
                new YsPanelIdDto { Id = id },
                "Yetkili servis panel sube durum");
        }

        public Task<ApiIslemSonuc?> SubeSilAsync(AppKullanici kullanici, int id)
        {
            return _api.PostAsync<YsPanelIdDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/subeler/sil",
                new YsPanelIdDto { Id = id },
                "Yetkili servis panel sube sil");
        }

        public Task<ApiIslemSonuc?> MarkaGuncelleAsync(AppKullanici kullanici, List<int> markaIds)
        {
            return _api.PostAsync<YsPanelMarkaGuncelleDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/guncelle",
                new YsPanelMarkaGuncelleDto { MarkaIds = markaIds ?? new List<int>() },
                "Yetkili servis panel marka guncelle");
        }

        public Task<ApiIslemSonuc?> MarkaEkleAsync(AppKullanici kullanici, string? markaAdi, string? aciklama)
        {
            return _api.PostAsync<YsPanelMarkaKaydetDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/ekle",
                new YsPanelMarkaKaydetDto
                {
                    MarkaAdi = markaAdi,
                    Aciklama = aciklama
                },
                "Yetkili servis panel marka ekle");
        }

        public Task<ApiIslemSonuc?> MarkaDuzenleAsync(AppKullanici kullanici, int id, string? markaAdi, string? aciklama)
        {
            return _api.PostAsync<YsPanelMarkaKaydetDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/duzenle",
                new YsPanelMarkaKaydetDto
                {
                    Id = id,
                    MarkaAdi = markaAdi,
                    Aciklama = aciklama
                },
                "Yetkili servis panel marka duzenle");
        }

        public Task<ApiIslemSonuc?> MarkaSilAsync(AppKullanici kullanici, int id)
        {
            return _api.PostAsync<YsPanelIdDto, ApiIslemSonuc>(
                kullanici,
                "api/ys-panel/markalar/sil",
                new YsPanelIdDto { Id = id },
                "Yetkili servis panel marka sil");
        }

        public Task<YsPanelRaporSonucDto?> RaporlarAsync(
            AppKullanici kullanici,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids = null,
            int? limit = null)
        {
            return _api.PostAsync<YsPanelRaporFiltreDto, YsPanelRaporSonucDto>(
                kullanici,
                "api/ys-panel/raporlar",
                new YsPanelRaporFiltreDto
                {
                    Bas = bas,
                    Bit = bit,
                    Ids = ids,
                    Limit = limit
                },
                "Yetkili servis panel raporlar", retryTransient: true);
        }

        public Task<ApiDosyaSonuc?> RaporlarPdfAsync(
            AppKullanici kullanici,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids = null)
        {
            return _api.PostFileAsync(
                kullanici,
                "api/ys-panel/raporlar/pdf",
                new YsPanelRaporFiltreDto
                {
                    Bas = bas,
                    Bit = bit,
                    Ids = ids
                },
                "raporlar.pdf",
                "Yetkili servis panel rapor PDF");
        }

        public Task<ApiDosyaSonuc?> RaporlarExcelAsync(
            AppKullanici kullanici,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids = null)
        {
            return _api.PostFileAsync(
                kullanici,
                "api/ys-panel/raporlar/excel",
                new YsPanelRaporFiltreDto
                {
                    Bas = bas,
                    Bit = bit,
                    Ids = ids
                },
                "raporlar.xlsx",
                "Yetkili servis panel rapor Excel");
        }

    }

}
