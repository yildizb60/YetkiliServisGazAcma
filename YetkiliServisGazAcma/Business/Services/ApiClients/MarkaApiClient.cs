using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public class MarkaApiClient
{
    private readonly ApiHttpClient _api;

    public MarkaApiClient(HttpClient httpClient, IOptions<ApiIntegrationOptions> options,
        ApiJwtTokenService tokenService, ILogger<MarkaApiClient> logger)
        => _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);

    public Task<List<MarkaApiDto>?> AktifleriGetirAsync()
        => _api.PostAsync<MarkaListeFiltreDto, List<MarkaApiDto>>(null,
            "api/marka/liste", new(), "Aktif marka listesi");

    public Task<List<MarkaApiDto>?> TumunuGetirAsync(AppKullanici kullanici, string? q = null, bool? aktifMi = null)
        => _api.PostAsync<MarkaListeFiltreDto, List<MarkaApiDto>>(kullanici,
            "api/marka/liste", new() { TumunuGetir = true, Q = q, AktifMi = aktifMi },
            "Marka yönetim listesi");

    public Task<MarkaApiDto?> GetirAsync(AppKullanici kullanici, int id)
        => _api.PostAsync<object, MarkaApiDto>(kullanici,
            "api/marka/getir", new { Id = id }, "Marka getir");

    public Task<ApiIslemSonuc?> EkleAsync(AppKullanici kullanici, MarkaKaydetDto marka)
        => _api.PostAsync<MarkaKaydetDto, ApiIslemSonuc>(kullanici,
            "api/marka/ekle", marka, "Marka ekle");

    public Task<ApiIslemSonuc?> GuncelleAsync(AppKullanici kullanici, MarkaKaydetDto marka)
        => _api.PostAsync<MarkaKaydetDto, ApiIslemSonuc>(kullanici,
            "api/marka/guncelle", marka, "Marka güncelle");

    public Task<ApiIslemSonuc?> SilAsync(AppKullanici kullanici, int id)
        => _api.PostAsync<object, ApiIslemSonuc>(kullanici,
            "api/marka/sil", new { Id = id }, "Marka sil");
}
