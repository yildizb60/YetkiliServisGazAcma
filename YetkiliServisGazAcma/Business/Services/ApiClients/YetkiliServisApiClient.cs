using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisApiClient
    {
        private readonly ApiHttpClient _api;

        public YetkiliServisApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<YetkiliServisApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, null, logger);
        }

        public Task<YetkiliServisSayfaliDto?> ListeSayfaliAsync(YetkiliServisFiltreDto istek)
            => _api.PostAsync<YetkiliServisFiltreDto, YetkiliServisSayfaliDto>(
                null, "api/yetkili-servisler/liste", istek, "Yetkili servis liste");

        public Task<YetkiliServisFiltreSecenekleriDto?> FiltreSecenekleriAsync(string? il)
            => _api.PostAsync<YetkiliServisFiltreSecenekleriIstek, YetkiliServisFiltreSecenekleriDto>(
                null, "api/yetkili-servisler/filtre-secenekleri", new() { Il = il }, "Yetkili servis filtre secenekleri");

        public Task<YetkiliServisRehberEkranDto?> RehberEkraniAsync(YetkiliServisFiltreDto istek)
            => _api.PostAsync<YetkiliServisFiltreDto, YetkiliServisRehberEkranDto>(
                null, "api/yetkili-servisler/rehber-ekrani", istek, "Yetkili servis rehber ekrani");

        public Task<YetkiliServisBasvuruSecenekleriDto?> BasvuruSecenekleriAsync()
            => _api.PostAsync<object, YetkiliServisBasvuruSecenekleriDto>(
                null, "api/yetkili-servisler/basvuru-secenekleri", new { }, "Yetkili servis basvuru secenekleri");

        public Task<YetkiliServisKayitSonuc?> KayitAsync(YetkiliServisBasvuruDto istek)
            => _api.PostAsync<YetkiliServisBasvuruDto, YetkiliServisKayitSonuc>(
                null, "api/yetkili-servisler/kayit", istek, "Yetkili servis kayit");
    }
}
