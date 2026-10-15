using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiBelgesiApiClient
    {
        private readonly ApiHttpClient _api;

        public YetkiBelgesiApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YetkiBelgesiApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<ApiIslemSonuc?> OnaylaAsync(AppKullanici kullanici, int id)
        {
            return _api.PostIslemAsync(kullanici, "api/yetki-belgesi/onayla",
                new YetkiBelgesiIdDto { Id = id }, "Yetki belgesi işlemi");
        }

        public Task<YetkiBelgesiFirmaEkraniDto?> FirmaEkraniAsync(AppKullanici kullanici, int firmaId)
        {
            return _api.PostAsync<YetkiBelgesiIdDto, YetkiBelgesiFirmaEkraniDto>(
                kullanici,
                "api/yetki-belgesi/firma-ekrani",
                new YetkiBelgesiIdDto { Id = firmaId },
                "Yetki belgesi firma ekrani");
        }

        public Task<YetkiBelgesiOnayEkraniDto?> OnayEkraniAsync(AppKullanici kullanici, int? sirketId, YetkiBelgesiOnayFiltreDto? filtre = null)
        {
            filtre ??= new();
            filtre.SirketId = sirketId;
            return _api.PostAsync<YetkiBelgesiOnayFiltreDto, YetkiBelgesiOnayEkraniDto>(
                kullanici,
                "api/yetki-belgesi/onay-ekrani",
                filtre,
                "Yetki belgesi onay ekrani");
        }

        public Task<ApiIslemSonuc?> SilAsync(AppKullanici kullanici, int id)
        {
            return _api.PostIslemAsync(kullanici, "api/yetki-belgesi/sil",
                new YetkiBelgesiIdDto { Id = id }, "Yetki belgesi işlemi");
        }

        public Task<ApiDosyaSonuc?> DosyaIndirAsync(AppKullanici kullanici, int id)
        {
            return PostFileAsync(
                kullanici,
                "api/yetki-belgesi/dosya-indir",
                new YetkiBelgesiIdDto { Id = id },
                $"Yetki_Belgesi_{id}",
                "Yetki belgesi dosya indir");
        }

        public Task<ApiIslemSonuc?> YukleAsync(AppKullanici kullanici, int firmaId, IFormFile dosya,
            DateTime bitisTarihi, DateTime? baslangicTarihi)
            => _api.PostFormAsync<ApiIslemSonuc>(kullanici, "api/yetki-belgesi/yukle", () =>
            {
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(firmaId.ToString(CultureInfo.InvariantCulture)), "FirmaId");
                form.Add(new StringContent(bitisTarihi.ToString("O", CultureInfo.InvariantCulture)), "BitisTarihi");
                if (baslangicTarihi.HasValue)
                    form.Add(new StringContent(baslangicTarihi.Value.ToString("O", CultureInfo.InvariantCulture)), "BaslangicTarihi");
                form.Add(new StreamContent(dosya.OpenReadStream())
                {
                    Headers = { ContentType = MediaTypeHeaderValue.TryParse(dosya.ContentType, out var contentType)
                        ? contentType : new MediaTypeHeaderValue("application/octet-stream") }
                }, "Dosya", dosya.FileName);
                return form;
            }, "Yetki belgesi yükleme");

        public Task<ApiIslemSonuc?> ReddetAsync(AppKullanici kullanici, int id, string? gerekce)
        {
            return _api.PostIslemAsync(
                kullanici,
                "api/yetki-belgesi/reddet",
                new YetkiBelgesiRedDto
                {
                    Id = id,
                    Gerekce = gerekce
                }, "Yetki belgesi işlemi");
        }

        private async Task<ApiDosyaSonuc?> PostFileAsync<TRequest>(AppKullanici kullanici, string url,
            TRequest istek, string varsayilanDosyaAdi, string operasyon)
        {
            try { return await _api.PostFileAsync(kullanici, url, istek, varsayilanDosyaAdi, operasyon); }
            catch (ApiIntegrationException ex) when (ex.StatusCode == 404) { return null; }
        }

    }

}
