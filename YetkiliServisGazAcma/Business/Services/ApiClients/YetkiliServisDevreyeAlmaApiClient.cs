using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisDevreyeAlmaApiClient
    {
        private readonly ApiHttpClient _api;

        public YetkiliServisDevreyeAlmaApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YetkiliServisDevreyeAlmaApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<YsDevreyeAlmaGecmisDto?> GecmisAsync(
            AppKullanici kullanici,
            string? marka,
            DateTime? bas,
            DateTime? bit,
            string? musteri,
            string? durum,
            string? tesisat = null)
        {
            return _api.PostAsync<YsDevreyeAlmaGecmisFiltreDto, YsDevreyeAlmaGecmisDto>(
                kullanici,
                "api/ys-devreyeal/gecmis",
                new YsDevreyeAlmaGecmisFiltreDto
                {
                    Marka = marka,
                    BaslangicTarihi = bas,
                    BitisTarihi = bit,
                    Musteri = musteri,
                    TesisatNo = tesisat,
                    Durum = durum
                },
                "Yetkili servis devreye alma gecmis");
        }

        public Task<YsDevreyeAlmaDto?> DetayAsync(AppKullanici kullanici, int id)
        {
            return _api.PostAsync<YsDevreyeAlmaGetirDto, YsDevreyeAlmaDto>(
                kullanici,
                "api/ys-devreyeal/getir",
                new YsDevreyeAlmaGetirDto { Id = id },
                "Yetkili servis devreye alma detay");
        }

        public Task<ApiDosyaSonuc?> PdfAsync(AppKullanici kullanici, int id)
        {
            return _api.PostFileAsync(
                kullanici,
                "api/ys-devreyeal/pdf",
                new YsDevreyeAlmaGetirDto { Id = id },
                $"DevreyeAlma_{id}.pdf",
                "Yetkili servis devreye alma PDF");
        }

        public Task<ApiDosyaSonuc?> ExcelAsync(AppKullanici kullanici, int id)
        {
            return _api.PostFileAsync(
                kullanici,
                "api/ys-devreyeal/excel",
                new YsDevreyeAlmaGetirDto { Id = id },
                $"DevreyeAlma_{id}.xlsx",
                "Yetkili servis devreye alma Excel");
        }

        public Task<YsDevreyeAlmaEkranDto?> EkranAsync(AppKullanici kullanici)
        {
            return _api.PostAsync<object, YsDevreyeAlmaEkranDto>(
                kullanici,
                "api/ys-devreyeal/ekran",
                new object(),
                "Yetkili servis devreye alma ekran");
        }

        public Task<YsDevreyeAlmaBildirimDto?> BildirimlerAsync(AppKullanici kullanici)
        {
            return _api.PostAsync<object, YsDevreyeAlmaBildirimDto>(
                kullanici,
                "api/ys-devreyeal/bildirimler",
                new object(),
                "Yetkili servis devreye alma bildirimler");
        }

        public Task<YsTesisatSorguSonucDto?> TesisatSorgulaAsync(AppKullanici kullanici, string? tesisatNo, string? sozlesmeNo)
        {
            return _api.PostAsync<YsTesisatSorguDto, YsTesisatSorguSonucDto>(
                kullanici,
                "api/ys-devreyeal/tesisat-sorgula",
                new YsTesisatSorguDto
                {
                    TesistatNo = tesisatNo,
                    SozlesmeNo = sozlesmeNo
                },
                "Yetkili servis tesisat sorgula");
        }

        public Task<YsMarkaKontrolSonucDto?> MarkaKontrolAsync(AppKullanici kullanici, string? sorguReferansi)
        {
            return _api.PostAsync<YsMarkaKontrolDto, YsMarkaKontrolSonucDto>(
                kullanici,
                "api/ys-devreyeal/marka-kontrol",
                new YsMarkaKontrolDto { SorguReferansi = sorguReferansi },
                "Yetkili servis marka kontrol");
        }

        public Task<YsDevreyeAlmaIslemSonucDto?> KaydetAsync(AppKullanici kullanici, YsDevreyeAlmaKaydetDto model)
        {
            return _api.PostAsync<YsDevreyeAlmaKaydetDto, YsDevreyeAlmaIslemSonucDto>(
                kullanici,
                "api/ys-devreyeal/kaydet",
                model,
                "Yetkili servis devreye alma kaydet");
        }

    }

}
