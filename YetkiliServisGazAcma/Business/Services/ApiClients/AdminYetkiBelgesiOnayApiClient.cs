using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminYetkiBelgesiOnayApiClient
    {
        private readonly ApiHttpClient _api;

        public AdminYetkiBelgesiOnayApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminYetkiBelgesiOnayApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<AdminYetkiBelgesiOnayListeDto?> ListeleAsync(AppKullanici kullanici, int? sirketId)
        {
            return _api.PostAsync<object, AdminYetkiBelgesiOnayListeDto>(kullanici,
                "api/admin-panel/yetki-belgeleri/onay-listesi", new AdminYetkiBelgesiOnayFiltreDto { SirketId = sirketId }, "Admin yetki belgesi onay listesi");
        }

        public async Task<List<AdminYetkiBelgesiOnayDto>?> OnayGecmisiAsync(
            AppKullanici kullanici,
            int? sirketId,
            DateTime? bas,
            DateTime? bit,
            string? q,
            string? durum)
        {
            var sonuc = await _api.PostAsync<object, AdminYetkiBelgesiOnayGecmisiListeDto>(kullanici,
                "api/admin-panel/yetki-belgeleri/onay-gecmisi", new AdminYetkiBelgesiOnayGecmisiFiltreDto
            {
                SirketId = sirketId,
                BaslangicTarihi = bas,
                BitisTarihi = bit,
                Q = q,
                Durum = int.TryParse(durum, out var durumNo) ? durumNo : null
            }, "Admin yetki belgesi onay gecmisi");
            return sonuc?.Islemler ?? new List<AdminYetkiBelgesiOnayDto>();
        }

        public Task<ApiDosyaSonuc?> RaporAsync(AppKullanici kullanici, YetkiBelgesiRaporFiltre filtre, bool excelMi)
            => _api.PostFileAsync(kullanici,
                excelMi ? "api/admin-panel/yetki-belgeleri/rapor/excel" : "api/admin-panel/yetki-belgeleri/rapor/pdf",
                filtre, excelMi ? "yetki-belgesi-raporu.xlsx" : "yetki-belgesi-raporu.pdf", "Yetki belgesi raporu");

    }

}
