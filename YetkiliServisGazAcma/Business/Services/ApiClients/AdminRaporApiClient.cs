using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminRaporApiClient
    {
        private readonly ApiHttpClient _api;

        public AdminRaporApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminRaporApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<AdminDevreyeAlmaListeDto?> DevreyeAlmalarAsync(
            AppKullanici kullanici,
            int? sirketId,
            string? marka,
            string? servis,
            string? il,
            string? durum,
            DateTime? bas,
            DateTime? bit,
            string? tesisatNo = null,
            string? ilce = null,
            string? musteri = null)
        {
            return _api.PostAsync<AdminDevreyeAlmaListeFiltreDto, AdminDevreyeAlmaListeDto>(kullanici,
                "api/admin-panel/devreye-almalar/liste", new AdminDevreyeAlmaListeFiltreDto
            {
                SirketId = sirketId,
                Marka = marka,
                Servis = servis,
                Il = il,
                Ilce = ilce,
                TesisatNo = tesisatNo,
                Musteri = musteri,
                Durum = int.TryParse(durum, out var durumNo) ? durumNo : null,
                BaslangicTarihi = bas,
                BitisTarihi = bit
            }, "Admin devreye alma liste");
        }

        public Task<AdminDevreyeAlmaDto?> DevreyeAlmaDetayAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return _api.PostAsync<AdminDevreyeAlmaGetirFiltreDto, AdminDevreyeAlmaDto>(kullanici,
                "api/admin-panel/devreye-almalar/getir", new AdminDevreyeAlmaGetirFiltreDto
            {
                Id = id,
                SirketId = sirketId
            }, "Admin devreye alma detay");
        }

        public Task<ApiDosyaSonuc?> DevreyeAlmaPdfAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return DevreyeAlmaDosyaAsync(
                kullanici,
                id,
                sirketId,
                "api/admin-panel/devreye-almalar/pdf",
                $"DevreyeAlma_{id}.pdf",
                "Admin devreye alma PDF");
        }

        public Task<ApiDosyaSonuc?> DevreyeAlmaExcelAsync(AppKullanici kullanici, int id, int? sirketId)
        {
            return DevreyeAlmaDosyaAsync(
                kullanici,
                id,
                sirketId,
                "api/admin-panel/devreye-almalar/excel",
                $"DevreyeAlma_{id}.xlsx",
                "Admin devreye alma Excel");
        }

        public Task<ApiDosyaSonuc?> RaporlarPdfAsync(
            AppKullanici kullanici,
            int? sirketId,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids)
        {
            return RaporDosyaAsync(
                kullanici,
                "api/admin-panel/devreye-almalar/rapor/pdf",
                sirketId,
                bas,
                bit,
                ids,
                "raporlar.pdf",
                "Admin devreye alma rapor PDF");
        }

        public Task<ApiDosyaSonuc?> RaporlarExcelAsync(
            AppKullanici kullanici,
            int? sirketId,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids)
        {
            return RaporDosyaAsync(
                kullanici,
                "api/admin-panel/devreye-almalar/rapor/excel",
                sirketId,
                bas,
                bit,
                ids,
                "raporlar.xlsx",
                "Admin devreye alma rapor Excel");
        }

        private Task<ApiDosyaSonuc?> DevreyeAlmaDosyaAsync(AppKullanici kullanici, int id, int? sirketId,
            string url, string varsayilanDosyaAdi, string operasyon)
            => _api.PostFileAsync(kullanici, url, new AdminDevreyeAlmaGetirFiltreDto { Id = id, SirketId = sirketId },
                varsayilanDosyaAdi, operasyon);

        private Task<ApiDosyaSonuc?> RaporDosyaAsync(AppKullanici kullanici, string url, int? sirketId,
            DateTime? bas, DateTime? bit, List<int>? ids, string varsayilanDosyaAdi, string operasyon)
            => _api.PostFileAsync(kullanici, url, new AdminDevreyeAlmaRaporExportFiltreDto
            {
                SirketId = sirketId, BaslangicTarihi = bas, BitisTarihi = bit, Ids = ids
            }, varsayilanDosyaAdi, operasyon);

        public Task<AdminYetkiBelgesiUyariListeDto?> YetkiBelgesiUyarilariAsync(AppKullanici kullanici, int? sirketId)
        {
            return _api.PostAsync<AdminYetkiBelgesiUyariFiltreDto, AdminYetkiBelgesiUyariListeDto>(kullanici,
                "api/admin-panel/yetki-belgeleri/uyarilar", new AdminYetkiBelgesiUyariFiltreDto { SirketId = sirketId }, "Admin yetki belgesi uyarilari");
        }

        public Task<AdminRaporOzetDto?> RaporlarOzetAsync(
            AppKullanici kullanici,
            int? sirketId,
            DateTime? bas,
            DateTime? bit,
            string? tip)
        {
            return _api.PostAsync<AdminRaporOzetFiltreDto, AdminRaporOzetDto>(kullanici,
                "api/admin-panel/raporlar/ozet", new AdminRaporOzetFiltreDto
            {
                SirketId = sirketId,
                BaslangicTarihi = bas,
                BitisTarihi = bit,
                Tip = tip
            }, "Admin rapor ozet");
        }

    }

}
