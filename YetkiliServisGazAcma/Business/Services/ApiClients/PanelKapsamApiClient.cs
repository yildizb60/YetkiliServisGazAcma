using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class PanelKapsamApiClient
    {
        private readonly ApiHttpClient _api;

        public PanelKapsamApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<PanelKapsamApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<List<PanelSirketDto>?> KullaniciSirketleriAsync(AppKullanici kullanici)
            => _api.PostAsync<object, List<PanelSirketDto>>(
                kullanici,
                "api/panel-kapsam/sirketler",
                new { },
                "Panel sirket kapsam listesi");

        public Task<PanelKimlikApiSonuc?> PanelKimlikAsync(AppKullanici kullanici, int? aktifSirketId)
        {
            return _api.PostAsync<PanelKimlikIstekDto, PanelKimlikApiSonuc>(
                kullanici,
                "api/panel-kapsam/kimlik",
                new PanelKimlikIstekDto { AktifSirketId = aktifSirketId },
                "Panel kimlik bilgisi");
        }

        public async Task<YkcYetkiOzeti> YkcYetkileriAsync(AppKullanici kullanici, int? aktifSirketId = null)
        {
            return await _api.PostAsync<PanelKimlikIstekDto, YkcYetkiOzeti>(
                kullanici,
                "api/panel-kapsam/ykc-yetkileri",
                new PanelKimlikIstekDto { AktifSirketId = aktifSirketId },
                "Cihaz degisim yetkileri") ?? new YkcYetkiOzeti();
        }

    }

}
