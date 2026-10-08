using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YkcApiClient
    {
        private readonly AktifSirketService _aktifSirket;
        private readonly ApiHttpClient _api;

        public YkcApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<YkcApiClient> logger,
            AktifSirketService aktifSirket)
        {
            _aktifSirket = aktifSirket;
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        private async Task<YkcTalepListeFiltre> SirketKapsamiEkleAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre, bool rapor = false)
        {
            if (!await _aktifSirket.GenelSistemAdminMi(kullanici))
                filtre.SirketId = await _aktifSirket.AktifSirketIdAsync(kullanici);
            else if (!rapor)
                filtre.SirketId ??= await _aktifSirket.AktifSirketIdAsync(kullanici);
            return filtre;
        }

        public async Task<YkcTalepListeSonuc?> TaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return await _api.PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/talepler/liste",
                await SirketKapsamiEkleAsync(kullanici, filtre),
                "Cihaz değişim talep listesi",
                retryTransient: true);
        }

        public async Task<YkcRaporSonuc?> RaporAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return await _api.PostAsync<YkcTalepListeFiltre, YkcRaporSonuc>(
                kullanici,
                "api/ykc/talepler/rapor",
                await SirketKapsamiEkleAsync(kullanici, filtre, rapor: true),
                "Cihaz değişim raporu",
                retryTransient: true);
        }

        public async Task<ApiDosyaSonuc?> RaporPdfAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
            => await _api.PostFileAsync(
                kullanici,
                "api/ykc/talepler/rapor/pdf",
                await SirketKapsamiEkleAsync(kullanici, filtre, rapor: true),
                "Cihaz_Degisim_Raporu.pdf",
                "Cihaz değişim raporu PDF");

        public async Task<ApiDosyaSonuc?> RaporExcelAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
            => await _api.PostFileAsync(
                kullanici,
                "api/ykc/talepler/rapor/excel",
                await SirketKapsamiEkleAsync(kullanici, filtre, rapor: true),
                "Cihaz_Degisim_Raporu.xlsx",
                "Cihaz değişim raporu Excel");

        public async Task<YkcDashboardOzetDto?> DashboardOzetAsync(AppKullanici kullanici)
        {
            return await _api.PostAsync<object, YkcDashboardOzetDto>(
                kullanici,
                "api/ykc/dashboard/ozet",
                new { AktifSirketId = await _aktifSirket.AktifSirketIdAsync(kullanici) },
                "Cihaz değişim dashboard özeti",
                retryTransient: true);
        }

        public Task<YkcImzaEntegrasyonDto?> ImzaEntegrasyonBilgisiAsync(AppKullanici kullanici)
        {
            return _api.PostAsync<object, YkcImzaEntegrasyonDto>(
                kullanici,
                "api/ykc/imza/entegrasyon",
                new { },
                "YKC dijital imza entegrasyon bilgisi",
                retryTransient: true);
        }

        public async Task<YkcTalepListeSonuc?> DogalgazMobileTaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return await _api.PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/dogalgaz-mobile/talepler/liste",
                await SirketKapsamiEkleAsync(kullanici, filtre),
                "Cihaz değişim doğalgaz mobile talep listesi",
                retryTransient: true);
        }

        public async Task<YkcTalepListeSonuc?> Crm187TaleplerAsync(AppKullanici kullanici, YkcTalepListeFiltre filtre)
        {
            return await _api.PostAsync<YkcTalepListeFiltre, YkcTalepListeSonuc>(
                kullanici,
                "api/ykc/crm187/talepler/liste",
                await SirketKapsamiEkleAsync(kullanici, filtre),
                "Cihaz değişim CRM187 talep listesi",
                retryTransient: true);
        }

        public Task<YkcTalepDetayDto?> DetayAsync(AppKullanici kullanici, int id, bool formVerisi = false)
        {
            return _api.PostAsync<YkcTalepGetirIstek, YkcTalepDetayDto>(
                kullanici,
                formVerisi ? "api/ykc/talepler/form-verisi" : "api/ykc/talepler/getir",
                new YkcTalepGetirIstek { Id = id },
                "Cihaz değişim talep detay",
                retryTransient: true);
        }

        public Task<YkcTesisatSorguSonuc?> TesisatSorgulaAsync(AppKullanici kullanici, YkcTesisatSorguIstek istek)
        {
            return _api.PostAsync<YkcTesisatSorguIstek, YkcTesisatSorguSonuc>(
                kullanici,
                "api/ykc/tesisat-sorgula",
                istek,
                "Cihaz degisim tesisat sorgula",
                retryTransient: true);
        }

        public Task<YkcCihazKarsilastirmaSonuc?> CihazKarsilastirAsync(AppKullanici kullanici, YkcCihazKarsilastirmaIstek istek)
            => _api.PostAsync<YkcCihazKarsilastirmaIstek, YkcCihazKarsilastirmaSonuc>(
                kullanici, "api/ykc/cihaz-karsilastir", istek, "Yeni cihaz bilgilerini karşılaştır");

        public async Task<YkcTakvimSonuc?> TakvimAsync(AppKullanici kullanici, YkcTakvimFiltre filtre)
        {
            filtre.AktifSirketId = await _aktifSirket.AktifSirketIdAsync(kullanici);
            return await _api.PostAsync<YkcTakvimFiltre, YkcTakvimSonuc>(kullanici, "api/ykc/takvim", filtre, "Randevu takvimi", retryTransient: true);
        }

        public Task<List<YkcEkipSecenegi>?> EkiplerAsync(AppKullanici kullanici, int id)
            => _api.PostAsync<object, List<YkcEkipSecenegi>>(
                kullanici,
                "api/ykc/talepler/ekipler",
                new { Id = id },
                "Bölge ekipleri",
                retryTransient: true);

        public Task<ApiDosyaSonuc?> FormPdfAsync(AppKullanici kullanici, int talepId)
            => _api.PostFileAsync(kullanici, "api/ykc/talepler/form-pdf",
                new YkcTalepGetirIstek { Id = talepId }, $"Cihaz_Degisim_Formu_{talepId}.pdf", "Cihaz değişim formu PDF");

        public Task<ApiDosyaSonuc?> DosyaIndirAsync(AppKullanici kullanici, int dosyaId)
        {
            return _api.PostFileAsync(
                kullanici,
                "api/ykc/talepler/dosya-indir",
                new YkcDosyaGetirIstek { Id = dosyaId },
                $"YKC_Form_Dosyasi_{dosyaId}",
                "Cihaz degisim form dosyasi indir");
        }

        public Task<YkcIslemSonuc?> OlusturAsync(AppKullanici kullanici, YkcTalepKaydetDto dto)
        {
            return _api.PostAsync<YkcTalepKaydetDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/olustur",
                dto,
                "Cihaz değişim talebi oluştur");
        }

        public Task<YkcIslemSonuc?> AtamaYapAsync(AppKullanici kullanici, YkcAtamaKaydetDto dto)
        {
            return _api.PostAsync<YkcAtamaKaydetDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/atama-yap",
                dto,
                "Cihaz değişim atama yap");
        }

        public Task<YkcIslemSonuc?> DurumGuncelleAsync(AppKullanici kullanici, YkcDurumGuncelleDto dto)
        {
            return _api.PostAsync<YkcDurumGuncelleDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/durum-guncelle",
                dto,
                "Cihaz değişim durum güncelle");
        }

        public Task<YkcIslemSonuc?> KontrollerKaydetAsync(AppKullanici kullanici, YkcKontrolKaydetDto dto)
        {
            return _api.PostAsync<YkcKontrolKaydetDto, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/kontroller-kaydet",
                dto,
                "Cihaz değişim FR265 kontrol kaydet");
        }

        public Task<YkcIslemSonuc?> ImzayaGonderAsync(AppKullanici kullanici, int talepId)
        {
            return _api.PostAsync<YkcTalepGetirIstek, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/imzaya-gonder",
                new YkcTalepGetirIstek { Id = talepId },
                "YKC FR265 imzaya gönder");
        }

        public Task<YkcIslemSonuc?> ImzaDurumSorgulaAsync(AppKullanici kullanici, int talepId)
        {
            return _api.PostAsync<YkcTalepGetirIstek, YkcIslemSonuc>(
                kullanici,
                "api/ykc/talepler/imza-durum-sorgula",
                new YkcTalepGetirIstek { Id = talepId },
                "YKC FR265 imza durumu sorgula");
        }

        public Task<YkcIslemSonuc?> FormYukleAsync(AppKullanici kullanici, int talepId, IFormFile dosya, string? dosyaTuru)
            => _api.PostFormAsync<YkcIslemSonuc>(kullanici, "api/ykc/talepler/form-yukle", () =>
            {
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(talepId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "TalepId");
                if (!string.IsNullOrWhiteSpace(dosyaTuru))
                    form.Add(new StringContent(dosyaTuru), "DosyaTuru");
                form.Add(new StreamContent(dosya.OpenReadStream())
                {
                    Headers = { ContentType = MediaTypeHeaderValue.TryParse(dosya.ContentType, out var contentType)
                        ? contentType : new MediaTypeHeaderValue("application/octet-stream") }
                }, "Dosya", dosya.FileName);
                return form;
            }, "Cihaz değişim form yükle");

    }

}
