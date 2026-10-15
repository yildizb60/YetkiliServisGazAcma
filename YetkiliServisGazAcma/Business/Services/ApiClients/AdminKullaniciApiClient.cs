using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminKullaniciApiClient
    {
        private readonly ApiHttpClient _api;

        public AdminKullaniciApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ApiJwtTokenService tokenService,
            ILogger<AdminKullaniciApiClient> logger)
        {
            _api = new ApiHttpClient(httpClient, options.Value, tokenService, logger);
        }

        public Task<List<AdminKullaniciListeDto>?> ListeleAsync(
            AppKullanici kullanici,
            int? sirketId,
            string? q,
            string? tip,
            string? durum,
            string? bagli)
        {
            return _api.PostAsync<AdminKullaniciListeFiltreDto, List<AdminKullaniciListeDto>>(kullanici,
                "api/admin-panel/kullanicilar/liste", new AdminKullaniciListeFiltreDto
            {
                SirketId = sirketId,
                Q = q,
                Tip = tip,
                Durum = durum,
                Bagli = bagli
            }, "Admin kullanici liste");
        }

        public Task<List<AdminSirketSecenekDto>?> SirketSecenekleriAsync(AppKullanici kullanici, int? sirketId)
        {
            return _api.PostAsync<AdminKullaniciSirketSecenekFiltreDto, List<AdminSirketSecenekDto>>(kullanici,
                "api/admin-panel/kullanicilar/sirket-secenekleri", new AdminKullaniciSirketSecenekFiltreDto { SirketId = sirketId }, "Admin kullanici sirket secenekleri");
        }

        public Task<List<AdminFirmaSecenekDto>?> FirmaSecenekleriAsync(AppKullanici kullanici, int? sirketId)
        {
            return _api.PostAsync<AdminKullaniciFirmaSecenekFiltreDto, List<AdminFirmaSecenekDto>>(kullanici,
                "api/admin-panel/kullanicilar/firma-secenekleri", new AdminKullaniciFirmaSecenekFiltreDto { SirketId = sirketId }, "Admin kullanici firma secenekleri");
        }

        public Task<AdminYetkiListeDto?> YetkiListeAsync(AppKullanici kullanici, int? sirketId, string? q = null, int sayfa = 1)
        {
            return _api.PostAsync<AdminYetkiListeFiltreDto, AdminYetkiListeDto>(
                kullanici,
                "api/admin-panel/yetkiler/liste",
                new AdminYetkiListeFiltreDto { SirketId = sirketId, Q = q, Sayfa = sayfa },
                "Admin yetki liste");
        }

        public Task<AdminYetkiDuzenleDto?> YetkiDuzenleAsync(AppKullanici kullanici, string id, int? sirketId)
        {
            return _api.PostAsync<AdminYetkiGetirDto, AdminYetkiDuzenleDto>(
                kullanici,
                "api/admin-panel/yetkiler/getir",
                new AdminYetkiGetirDto { Id = id, SirketId = sirketId },
                "Admin yetki duzenle");
        }

        public Task<ApiIslemSonuc?> YetkiGuncelleAsync(
            AppKullanici kullanici,
            string id,
            int? sirketId,
            List<int> sirketIds,
            Dictionary<int, List<string>> yetkiler)
        {
            return _api.PostIslemAsync(
                kullanici,
                "api/admin-panel/yetkiler/guncelle",
                new AdminYetkiGuncelleDto
                {
                    Id = id,
                    SirketId = sirketId,
                    SirketIds = sirketIds,
                    Yetkiler = yetkiler
                },
                "Admin yetki guncelle");
        }

        public async Task<bool> YonetebilirMiAsync(AppKullanici kullanici, int? sirketId)
        {
            var cevap = await _api.PostAsync<AdminKullaniciYonetimYetkiDto, AdminKullaniciYonetimYetkiSonucDto>(
                kullanici,
                "api/admin-panel/kullanicilar/yonetim-yetkisi",
                new AdminKullaniciYonetimYetkiDto { SirketId = sirketId },
                "Admin kullanici yonetim yetkisi");

            return cevap?.YetkiliMi == true;
        }

        public Task<ApiIslemSonuc?> KullaniciEkleAsync(
            AppKullanici kullanici,
            int? kapsamSirketId,
            string adSoyad,
            string email,
            string telefon,
            string sifre,
            string rol,
            int? sirketId,
            int? firmaId)
        {
            return _api.PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/ekle",
                new AdminKullaniciKaydetDto
                {
                    KapsamSirketId = kapsamSirketId,
                    AdSoyad = adSoyad,
                    Email = email,
                    Telefon = telefon,
                    Sifre = sifre,
                    Rol = rol,
                    SirketId = sirketId,
                    FirmaId = firmaId
                },
                "Admin kullanici ekle");
        }

        public Task<ApiIslemSonuc?> PersonelEkleAsync(
            AppKullanici kullanici,
            int? kapsamSirketId,
            string adSoyad,
            string email,
            string telefon,
            int sirketId,
            string sifre)
        {
            return _api.PostIslemAsync(
                kullanici,
                "api/admin-panel/personeller/ekle",
                new AdminPersonelKaydetDto
                {
                    KapsamSirketId = kapsamSirketId,
                    AdSoyad = adSoyad,
                    Email = email,
                    Telefon = telefon,
                    SirketId = sirketId,
                    Sifre = sifre
                },
                "Admin personel ekle");
        }

        public Task<AdminKullaniciListeDto?> GetirAsync(AppKullanici kullanici, string id, int? sirketId)
        {
            return _api.PostAsync<AdminKullaniciGetirDto, AdminKullaniciListeDto>(
                kullanici,
                "api/admin-panel/kullanicilar/getir",
                new AdminKullaniciGetirDto
                {
                    Id = id,
                    SirketId = sirketId
                },
                "Admin kullanici getir");
        }

        public Task<ApiIslemSonuc?> GuncelleAsync(
            AppKullanici kullanici,
            string id,
            int? kapsamSirketId,
            string adSoyad,
            string email,
            string telefon,
            bool aktifMi,
            int? sirketId,
            int? firmaId,
            string? yeniSifre,
            string? yeniSifreTekrar)
        {
            return _api.PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/guncelle",
                new AdminKullaniciGuncelleDto
                {
                    Id = id,
                    KapsamSirketId = kapsamSirketId,
                    AdSoyad = adSoyad,
                    Email = email,
                    Telefon = telefon,
                    AktifMi = aktifMi,
                    SirketId = sirketId,
                    FirmaId = firmaId,
                    YeniSifre = yeniSifre,
                    YeniSifreTekrar = yeniSifreTekrar
                },
                "Admin kullanici guncelle");
        }

        public Task<ApiIslemSonuc?> DurumAsync(
            AppKullanici kullanici,
            string id,
            bool aktifMi,
            int? sirketId,
            bool sadecePersonel)
        {
            return _api.PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/durum",
                new AdminKullaniciDurumDto
                {
                    Id = id,
                    SirketId = sirketId,
                    AktifMi = aktifMi,
                    SadecePersonel = sadecePersonel
                },
                "Admin kullanici durum");
        }

        public Task<ApiIslemSonuc?> SilAsync(
            AppKullanici kullanici,
            string id,
            int? sirketId,
            bool sadecePersonel)
        {
            return _api.PostIslemAsync(
                kullanici,
                "api/admin-panel/kullanicilar/sil",
                new AdminKullaniciSilDto
                {
                    Id = id,
                    SirketId = sirketId,
                    SadecePersonel = sadecePersonel
                },
                "Admin kullanici sil");
        }

    }

}
