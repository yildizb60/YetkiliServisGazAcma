using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminSubeApiClient
    {
        private readonly ApiHttpClient _api;

        public AdminSubeApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminSubeApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<AdminSubeListeDto?> ListeleAsync(AppKullanici kullanici, int? sirketId, string? q, int firmaId)
        {
            return _api.PostAsync<AdminSubeListeFiltreDto, AdminSubeListeDto>(
                kullanici,
                "api/admin-panel/subeler/liste",
                new AdminSubeListeFiltreDto
                {
                    SirketId = sirketId,
                    Q = q,
                    FirmaId = firmaId
                }, "Şube yönetimi");
        }

        public Task<AdminSubeDetayDto?> DetayAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return _api.PostAsync<AdminSubeGetirFiltreDto, AdminSubeDetayDto>(
                kullanici,
                "api/admin-panel/subeler/getir",
                new AdminSubeGetirFiltreDto { Id = id, SirketId = sirketId }, "Şube yönetimi");
        }

        public Task<ApiIslemSonuc?> EkleAsync(
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
            return _api.PostIslemAsync(kullanici, "api/admin-panel/subeler/ekle", new AdminSubeKaydetDto
            {
                SirketId = sirketId,
                FirmaId = firmaId,
                SubeAdi = subeAdi,
                Il = il,
                Ilce = ilce,
                Telefon = telefon,
                Adres = adres,
                AktifMi = aktifMi
            }, "Şube yönetimi");
        }

        public Task<ApiIslemSonuc?> GuncelleAsync(
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
            return _api.PostIslemAsync(kullanici, "api/admin-panel/subeler/guncelle", new AdminSubeKaydetDto
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
            }, "Şube yönetimi");
        }

        public Task<ApiIslemSonuc?> DurumAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return _api.PostIslemAsync(kullanici, "api/admin-panel/subeler/durum", new AdminSubeGetirFiltreDto { Id = id, SirketId = sirketId }, "Şube yönetimi");
        }

        public Task<ApiIslemSonuc?> SilAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return _api.PostIslemAsync(kullanici, "api/admin-panel/subeler/sil", new AdminSubeGetirFiltreDto { Id = id, SirketId = sirketId }, "Şube yönetimi");
        }
    }
}
