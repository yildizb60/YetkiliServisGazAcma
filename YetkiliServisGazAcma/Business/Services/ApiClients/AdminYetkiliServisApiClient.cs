using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminYetkiliServisApiClient
    {
        private readonly ApiHttpClient _api;

        public AdminYetkiliServisApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminYetkiliServisApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<AdminYetkiliServisListeDto?> ListeleAsync(
            AppKullanici kullanici,
            int? sirketId,
            string? q,
            string? il,
            int? durum,
            string? devreyeSiralama)
        {
            return _api.PostAsync<object, AdminYetkiliServisListeDto>(kullanici,
                "api/admin-panel/yetkili-servisler/liste", new AdminYetkiliServisListeFiltreDto
            {
                SirketId = sirketId,
                Q = q,
                Il = il,
                Durum = durum,
                DevreyeSiralama = devreyeSiralama
            }, "Admin yetkili servis liste");
        }

        public Task<AdminYetkiliServisEditorDto?> EditorAsync(AppKullanici kullanici, int id, int? sirketId)
            => _api.PostAsync<AdminYetkiliServisGetirFiltreDto, AdminYetkiliServisEditorDto>(kullanici,
                "api/admin-panel/yetkili-servisler/editor", new() { Id = id, SirketId = sirketId }, "Yetkili servis düzenleme");

        public Task<AdminYetkiliServisDetayDto?> DetayAsync(
            AppKullanici kullanici,
            int id,
            int? sirketId)
        {
            return _api.PostAsync<object, AdminYetkiliServisDetayDto>(kullanici,
                "api/admin-panel/yetkili-servisler/getir", new AdminYetkiliServisGetirFiltreDto
            {
                Id = id,
                SirketId = sirketId
            }, "Admin yetkili servis detay");
        }

        public Task<ApiIslemSonuc?> GuncelleAsync(
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
            return _api.PostAsync<AdminYetkiliServisKaydetDto, ApiIslemSonuc>(
                kullanici,
                "api/admin-panel/yetkili-servisler/guncelle",
                new AdminYetkiliServisKaydetDto
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
                    KategoriIds = kategoriIds,
                    MarkaIds = markaIds
                },
                "Admin yetkili servis guncelle");
        }

        public Task<ApiIslemSonuc?> EkleAsync(
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
            return _api.PostAsync<AdminYetkiliServisKaydetDto, ApiIslemSonuc>(
                kullanici,
                "api/admin-panel/yetkili-servisler/ekle",
                new AdminYetkiliServisKaydetDto
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
                    KategoriIds = kategoriIds,
                    MarkaIds = markaIds
                },
                "Admin yetkili servis ekle");
        }

        public Task<ApiIslemSonuc?> SilAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return _api.PostAsync<AdminYetkiliServisDurumDto, ApiIslemSonuc>(
                kullanici,
                "api/admin-panel/yetkili-servisler/sil",
                new AdminYetkiliServisDurumDto { Id = id, SirketId = sirketId },
                "Admin yetkili servis sil");
        }

        public Task<ApiDosyaSonuc?> DosyaAsync(AppKullanici kullanici, int id, int? sirketId, bool pdf)
            => _api.PostFileAsync(kullanici, $"api/admin-panel/yetkili-servisler/{(pdf ? "pdf" : "excel")}",
                new AdminYetkiliServisGetirFiltreDto { Id = id, SirketId = sirketId },
                $"Yetkili_Servis_{id}.{(pdf ? "pdf" : "xlsx")}", "Yetkili servis kayit dosyasi");

    }

}
