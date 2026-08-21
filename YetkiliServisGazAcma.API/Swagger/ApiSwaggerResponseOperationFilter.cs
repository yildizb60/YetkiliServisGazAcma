using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Swagger
{
    public class ApiSwaggerResponseOperationFilter : IOperationFilter
    {
        private static readonly Type GenericErrorType = typeof(ApiErrorResponseDto);
        private static readonly Type GenericOperationType = typeof(ApiOperationResponseDto);
        private static readonly Type ValidationErrorType = typeof(ValidationProblemDetails);
        private static readonly Type ProblemType = typeof(ProblemDetails);

        private static readonly Dictionary<string, ResponseSpec> RouteResponses = new(StringComparer.OrdinalIgnoreCase)
        {
            ["api/auth/token"] = Json<AuthTokenResponseDto>("Token üretildi."),

            ["api/health"] = Json<SystemHealthApiController.SistemSaglikCevap>("Sistem sağlık bilgisi.", serviceUnavailableType: typeof(SystemHealthApiController.SistemSaglikCevap)),
            ["api/home/ozet"] = Json<HomeOzetDto>("Ana sayfa özet değerleri."),

            ["api/panel-kapsam/sirketler"] = Json<List<PanelSirketDto>>("Kullanıcının erişebildiği şirketler."),
            ["api/panel-kapsam/kimlik"] = Json<PanelKimlikDto>("Aktif panel kimliği."),
            ["api/personel-panel/yetkilerim"] = Json<PersonelYetkilerimCevap>("Personel yetki listesi."),
            ["api/ic-tesisat/devreye-almalar/liste"] = Json<IcTesisatDevreyeAlmaListeDto>("İç tesisat devreye alma listesi."),

            ["api/dagitim-sirket/liste"] = Json<List<DagitimSirketResponseDto>>("Dağıtım şirketi listesi."),
            ["api/dagitim-sirket/getir"] = Json<DagitimSirketResponseDto>("Dağıtım şirketi detayı."),
            ["api/dagitim-sirket/ekle"] = Json<ApiOperationResponseDto>("Dağıtım şirketi eklendi."),
            ["api/dagitim-sirket/guncelle"] = Json<ApiOperationResponseDto>("Dağıtım şirketi güncellendi."),
            ["api/dagitim-sirket/sil"] = Json<ApiOperationResponseDto>("Dağıtım şirketi silindi."),

            ["api/marka/liste"] = Json<List<MarkaResponseDto>>("Marka listesi."),
            ["api/marka/getir"] = Json<MarkaResponseDto>("Marka detayı."),
            ["api/marka/ekle"] = Json<ApiOperationResponseDto>("Marka eklendi."),
            ["api/marka/guncelle"] = Json<ApiOperationResponseDto>("Marka güncellendi."),
            ["api/marka/sil"] = Json<ApiOperationResponseDto>("Marka silindi."),

            ["api/urun-kategorileri/liste"] = Json<List<UrunKategoriResponseDto>>("Ürün kategori listesi."),

            ["api/yetkili-servisler/liste"] = Json<YetkiliServisSayfaliDto>("Yetkili servis sayfalı liste."),
            ["api/yetkili-servisler"] = Json<YetkiliServisSayfaliDto>("Yetkili servis sayfalı liste."),
            ["api/yetkili-servisler/filtre-secenekleri"] = Json<YetkiliServisFiltreSecenekleriDto>("Yetkili servis filtre seçenekleri."),
            ["api/yetkili-servisler/kayit"] = Json<ApiOperationResponseDto>("Yetkili servis başvuru sonucu."),
            ["api/yetkili-servisler/getir"] = Json<YetkiliServisDetayDto>("Yetkili servis detayı."),
            ["api/yetkili-servisler/guncelle"] = Json<ApiOperationResponseDto>("Yetkili servis güncelleme sonucu."),
            ["api/yetkili-servisler/sil"] = Json<ApiOperationResponseDto>("Yetkili servis silme sonucu."),

            ["api/yetki-belgesi/firma-liste"] = Json<List<YetkiBelgesiDto>>("Firmaya ait yetki belgeleri."),
            ["api/yetki-belgesi/firma-ekrani"] = Json<YetkiBelgesiFirmaEkraniDto>("Firma yetki belgesi ekranı."),
            ["api/yetki-belgesi/yukle"] = Json<ApiOperationResponseDto>("Yetki belgesi yükleme sonucu."),
            ["api/yetki-belgesi/onay-bekleyenler"] = Json<List<YetkiBelgesiDto>>("Onay bekleyen yetki belgeleri."),
            ["api/yetki-belgesi/onay-ekrani"] = Json<YetkiBelgesiOnayEkraniDto>("Yetki belgesi onay ekranı."),
            ["api/yetki-belgesi/sil"] = Json<ApiOperationResponseDto>("Yetki belgesi silme sonucu."),
            ["api/yetki-belgesi/onayla"] = Json<ApiOperationResponseDto>("Yetki belgesi onay sonucu."),
            ["api/yetki-belgesi/reddet"] = Json<ApiOperationResponseDto>("Yetki belgesi red sonucu."),

            ["api/ys-devreyeal/gecmis"] = Json<YsDevreyeAlmaGecmisDto>("Yetkili servis devreye alma geçmişi."),
            ["api/ys-devreyeal/getir"] = Json<YsDevreyeAlmaDto>("Devreye alma detayı."),
            ["api/ys-devreyeal/pdf"] = File("Devreye alma PDF dosyası."),
            ["api/ys-devreyeal/excel"] = File("Devreye alma Excel dosyası."),
            ["api/ys-devreyeal/ekran"] = Json<YsDevreyeAlmaEkranDto>("Devreye alma ekran bilgisi."),
            ["api/ys-devreyeal/tesisat-sorgula"] = Json<YsTesisatSorguSonucDto>("Tesisat sorgu sonucu."),
            ["api/ys-devreyeal/bildirimler"] = Json<YsDevreyeAlmaBildirimDto>("Yetkili servis bildirimleri."),
            ["api/ys-devreyeal/marka-kontrol"] = Json<YsMarkaKontrolSonucDto>("Marka yetki kontrol sonucu."),
            ["api/ys-devreyeal/kaydet"] = Json<YsDevreyeAlmaIslemSonucDto>("Devreye alma kayıt sonucu."),

            ["api/ys-panel/dashboard"] = Json<YsPanelDashboardDto>("Yetkili servis panel dashboard bilgisi."),
            ["api/ys-panel/bildirimler"] = Json<YsPanelBildirimDto>("Yetkili servis panel bildirimleri."),
            ["api/ys-panel/profil"] = Json<YsPanelFirmaDto>("Yetkili servis firma profili."),
            ["api/ys-panel/profil/guncelle"] = Json<YsPanelIslemSonucDto>("Profil güncelleme sonucu."),
            ["api/ys-panel/ilk-kurulum"] = Json<YsPanelIlkKurulumDto>("İlk kurulum ekran bilgisi."),
            ["api/ys-panel/markalar"] = Json<YsPanelMarkalarDto>("Yetkili servis marka bilgileri."),
            ["api/ys-panel/raporlar"] = Json<YsPanelRaporSonucDto>("Yetkili servis rapor sonucu."),
            ["api/ys-panel/raporlar/pdf"] = File("Yetkili servis rapor PDF dosyası."),
            ["api/ys-panel/raporlar/excel"] = File("Yetkili servis rapor Excel dosyası."),
            ["api/ys-panel/subeler/kaydet"] = Json<YsPanelIslemSonucDto>("Şube kayıt sonucu."),
            ["api/ys-panel/subeler/durum"] = Json<YsPanelIslemSonucDto>("Şube durum sonucu."),
            ["api/ys-panel/subeler/sil"] = Json<YsPanelIslemSonucDto>("Şube silme sonucu."),
            ["api/ys-panel/markalar/guncelle"] = Json<YsPanelIslemSonucDto>("Marka güncelleme sonucu."),
            ["api/ys-panel/markalar/ekle"] = Json<YsPanelIslemSonucDto>("Marka ekleme sonucu."),
            ["api/ys-panel/markalar/duzenle"] = Json<YsPanelIslemSonucDto>("Marka düzenleme sonucu."),
            ["api/ys-panel/markalar/sil"] = Json<YsPanelIslemSonucDto>("Marka silme sonucu."),

            ["api/admin-panel/dashboard"] = Json<AdminDashboardApiDto>("Admin dashboard bilgisi."),
            ["api/admin-panel/yetkili-servisler/liste"] = Json<AdminYetkiliServisListeDto>("Admin yetkili servis listesi."),
            ["api/admin-panel/yetkili-servisler/getir"] = Json<AdminYetkiliServisDetayDto>("Admin yetkili servis detayı."),
            ["api/admin-panel/yetkili-servisler/ekle"] = Json<AdminIslemSonucDto>("Yetkili servis ekleme sonucu."),
            ["api/admin-panel/yetkili-servisler/guncelle"] = Json<AdminIslemSonucDto>("Yetkili servis güncelleme sonucu."),
            ["api/admin-panel/yetkili-servisler/sil"] = Json<AdminIslemSonucDto>("Yetkili servis silme sonucu."),
            ["api/admin-panel/yetki-belgeleri/onay-listesi"] = Json<AdminYetkiBelgesiOnayListeDto>("Yetki belgesi onay listesi."),
            ["api/admin-panel/yetki-belgeleri/onay-gecmisi"] = Json<AdminYetkiBelgesiOnayGecmisiListeDto>("Yetki belgesi onay geçmişi."),
            ["api/admin-panel/subeler/liste"] = Json<AdminSubeListeDto>("Admin şube listesi."),
            ["api/admin-panel/subeler/getir"] = Json<AdminSubeDetayDto>("Admin şube detayı."),
            ["api/admin-panel/subeler/ekle"] = Json<AdminIslemSonucDto>("Şube ekleme sonucu."),
            ["api/admin-panel/subeler/guncelle"] = Json<AdminIslemSonucDto>("Şube güncelleme sonucu."),
            ["api/admin-panel/subeler/durum"] = Json<AdminIslemSonucDto>("Şube durum değiştirme sonucu."),
            ["api/admin-panel/subeler/sil"] = Json<AdminIslemSonucDto>("Şube silme sonucu."),
            ["api/admin-panel/devreye-almalar/liste"] = Json<AdminDevreyeAlmaListeDto>("Admin devreye alma listesi."),
            ["api/admin-panel/devreye-almalar/getir"] = Json<AdminDevreyeAlmaDto>("Admin devreye alma detayı."),
            ["api/admin-panel/devreye-almalar/pdf"] = File("Admin devreye alma PDF dosyası."),
            ["api/admin-panel/devreye-almalar/excel"] = File("Admin devreye alma Excel dosyası."),
            ["api/admin-panel/devreye-almalar/rapor/pdf"] = File("Admin devreye alma rapor PDF dosyası."),
            ["api/admin-panel/devreye-almalar/rapor/excel"] = File("Admin devreye alma rapor Excel dosyası."),
            ["api/admin-panel/yetki-belgeleri/uyarilar"] = Json<AdminYetkiBelgesiUyariListeDto>("Yaklaşan/geçmiş yetki belgesi uyarıları."),
            ["api/admin-panel/raporlar/ozet"] = Json<AdminRaporOzetDto>("Admin rapor özeti."),
            ["api/admin-panel/kullanicilar/liste"] = Json<List<AdminKullaniciListeDto>>("Admin kullanıcı listesi."),
            ["api/admin-panel/kullanicilar/sirket-secenekleri"] = Json<List<AdminSirketSecenekDto>>("Kullanıcı şirket seçenekleri."),
            ["api/admin-panel/kullanicilar/firma-secenekleri"] = Json<List<AdminFirmaSecenekDto>>("Kullanıcı firma seçenekleri."),
            ["api/admin-panel/kullanicilar/yetkili-servis-senkronize"] = Json<AdminIslemSonucDto>("Yetkili servis kullanıcı senkronizasyon sonucu."),
            ["api/admin-panel/kullanicilar/yonetim-yetkisi"] = Json<AdminKullaniciYonetimYetkiSonucDto>("Kullanıcı yönetim yetkisi sonucu."),
            ["api/admin-panel/kullanicilar/getir"] = Json<AdminKullaniciListeDto>("Admin kullanıcı detayı."),
            ["api/admin-panel/kullanicilar/guncelle"] = Json<AdminIslemSonucDto>("Kullanıcı güncelleme sonucu."),
            ["api/admin-panel/kullanicilar/ekle"] = Json<AdminIslemSonucDto>("Kullanıcı ekleme sonucu."),
            ["api/admin-panel/kullanicilar/durum"] = Json<AdminIslemSonucDto>("Kullanıcı durum değiştirme sonucu."),
            ["api/admin-panel/kullanicilar/sil"] = Json<AdminIslemSonucDto>("Kullanıcı silme sonucu."),
            ["api/admin-panel/personeller/ekle"] = Json<AdminIslemSonucDto>("Personel ekleme sonucu."),
            ["api/admin-panel/yetkiler/liste"] = Json<AdminYetkiListeDto>("Personel yetki listesi."),
            ["api/admin-panel/yetkiler/getir"] = Json<AdminYetkiDuzenleDto>("Personel yetki düzenleme bilgisi."),
            ["api/admin-panel/yetkiler/guncelle"] = Json<AdminIslemSonucDto>("Personel yetki güncelleme sonucu."),

            ["api/ykc/tesisat-sorgula"] = Json<YkcTesisatSorguSonuc>("YKC tesisat sorgu sonucu."),
            ["api/ykc/talepler/liste"] = Json<YkcTalepListeSonuc>("YKC talep listesi."),
            ["api/ykc/dashboard/ozet"] = Json<YkcDashboardOzetDto>("YKC dashboard özeti."),
            ["api/ykc/talepler/rapor"] = Json<YkcRaporSonuc>("YKC rapor sonucu."),
            ["api/ykc/imza/entegrasyon"] = Json<YkcImzaEntegrasyonDto>("YKC imza entegrasyon bilgisi."),
            ["api/ykc/talepler/imzaya-gonder"] = Json<YkcIslemSonuc>("YKC imzaya gönderme sonucu.", badRequestType: typeof(YkcIslemSonuc), serviceUnavailableType: typeof(YkcIslemSonuc)),
            ["api/ykc/talepler/imza-durum-sorgula"] = Json<YkcIslemSonuc>("YKC imza durum sorgulama sonucu.", badRequestType: typeof(YkcIslemSonuc), serviceUnavailableType: typeof(YkcIslemSonuc)),
            ["api/ykc/talepler/dosya-indir"] = File("YKC teknik ek veya imzalı nihai belge dosyası."),
            ["api/ykc/dogalgaz-mobile/talepler/liste"] = Json<YkcTalepListeSonuc>("Doğalgaz Mobile hedefli YKC talep listesi."),
            ["api/ykc/crm187/talepler/liste"] = Json<YkcTalepListeSonuc>("CRM187 hedefli YKC talep listesi."),
            ["api/ykc/talepler/getir"] = Json<YkcTalepDetayDto>("YKC talep detayı."),
            ["api/ykc/talepler/olustur"] = Json<YkcIslemSonuc>("YKC talep oluşturma sonucu.", badRequestType: typeof(YkcIslemSonuc)),
            ["api/ykc/talepler/atama-yap"] = Json<YkcIslemSonuc>("YKC atama sonucu.", badRequestType: typeof(YkcIslemSonuc)),
            ["api/ykc/talepler/durum-guncelle"] = Json<YkcIslemSonuc>("YKC durum güncelleme sonucu.", badRequestType: typeof(YkcIslemSonuc)),
            ["api/ykc/talepler/kontroller-kaydet"] = Json<YkcIslemSonuc>("YKC FR265 kontrol kayıt sonucu.", badRequestType: typeof(YkcIslemSonuc)),
            ["api/ykc/talepler/dosya-kaydet"] = Json<YkcIslemSonuc>("YKC dosya kayıt sonucu.", badRequestType: typeof(YkcIslemSonuc)),
            ["api/ykc/talepler/form-yukle"] = Json<YkcIslemSonuc>("YKC teknik ek dosya yükleme sonucu.", badRequestType: typeof(YkcIslemSonuc)),
        };

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var route = NormalizeRoute(context.ApiDescription.RelativePath);
            if (route == null)
                return;

            if (RouteResponses.TryGetValue(route, out var spec))
            {
                if (spec.FileResponse)
                    SetFileResponse(operation, "200", spec.SuccessDescription);
                else if (spec.SuccessType != null)
                    SetJsonResponse(operation, context, "200", spec.SuccessType, spec.SuccessDescription);

                if (spec.BadRequestType != null)
                    SetJsonResponse(operation, context, "400", spec.BadRequestType, "İstek doğrulanamadı veya iş kuralı sağlanamadı.");

                if (spec.ServiceUnavailableType != null)
                    SetJsonResponse(operation, context, "503", spec.ServiceUnavailableType, "Bağımlı servis veya entegrasyon hazır değil.");
            }

            EnsureBadRequestResponse(operation, context, route, spec);
            EnsureAuthResponses(operation, context);
            EnsureNotFoundResponse(operation, context, route);
            EnsureRateLimitResponse(operation, context);
            EnsureServerErrorResponse(operation, context);
        }

        private static ResponseSpec Json<T>(string description, Type? badRequestType = null, Type? serviceUnavailableType = null)
        {
            return new ResponseSpec(typeof(T), description, badRequestType, serviceUnavailableType, FileResponse: false);
        }

        private static ResponseSpec File(string description)
        {
            return new ResponseSpec(null, description, null, null, FileResponse: true);
        }

        private static void EnsureBadRequestResponse(OpenApiOperation operation, OperationFilterContext context, string route, ResponseSpec? spec)
        {
            if (operation.Responses.ContainsKey("400"))
                return;

            var badRequestType = spec?.BadRequestType
                ?? (route.StartsWith("api/ykc/talepler/", StringComparison.OrdinalIgnoreCase) ? typeof(YkcIslemSonuc) : null)
                ?? (route.Contains("/ekle", StringComparison.OrdinalIgnoreCase)
                    || route.Contains("/guncelle", StringComparison.OrdinalIgnoreCase)
                    || route.Contains("/sil", StringComparison.OrdinalIgnoreCase)
                    || route.Contains("/kaydet", StringComparison.OrdinalIgnoreCase)
                    || route.Contains("/kayit", StringComparison.OrdinalIgnoreCase)
                    || route.Contains("/yukle", StringComparison.OrdinalIgnoreCase)
                    || route.Contains("/getir", StringComparison.OrdinalIgnoreCase)
                        ? GenericErrorType
                        : ValidationErrorType);

            SetJsonResponse(operation, context, "400", badRequestType, "Geçersiz istek veya doğrulama hatası.");
        }

        private static void EnsureAuthResponses(OpenApiOperation operation, OperationFilterContext context)
        {
            if (!RequiresAuthorization(context))
                return;

            if (!operation.Responses.ContainsKey("401"))
                SetJsonResponse(operation, context, "401", GenericErrorType, "Kimlik doğrulama gerekli veya oturum geçersiz.");

            if (!operation.Responses.ContainsKey("403"))
                SetJsonResponse(operation, context, "403", GenericErrorType, "Bu işlem için yetki yok.");
        }

        private static void EnsureNotFoundResponse(OpenApiOperation operation, OperationFilterContext context, string route)
        {
            if (operation.Responses.ContainsKey("404"))
                return;

            var canReturnNotFound = route.Contains("/getir", StringComparison.OrdinalIgnoreCase)
                || route.Contains("/sil", StringComparison.OrdinalIgnoreCase)
                || route.Contains("/pdf", StringComparison.OrdinalIgnoreCase)
                || route.Contains("/excel", StringComparison.OrdinalIgnoreCase)
                || route.Contains("/dosya-indir", StringComparison.OrdinalIgnoreCase)
                || route.Contains("/firma-ekrani", StringComparison.OrdinalIgnoreCase)
                || route.EndsWith("/profil", StringComparison.OrdinalIgnoreCase);

            if (canReturnNotFound)
                SetJsonResponse(operation, context, "404", GenericErrorType, "Kayıt veya dosya bulunamadı.");
        }

        private static void EnsureRateLimitResponse(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Responses.ContainsKey("429") || !HasMetadata<EnableRateLimitingAttribute>(context))
                return;

            SetJsonResponse(operation, context, "429", GenericErrorType, "Çok fazla istek gönderildi.");
        }

        private static void EnsureServerErrorResponse(OpenApiOperation operation, OperationFilterContext context)
        {
            if (!operation.Responses.ContainsKey("500"))
                SetJsonResponse(operation, context, "500", ProblemType, "Beklenmeyen sunucu hatası.");
        }

        private static void SetJsonResponse(OpenApiOperation operation, OperationFilterContext context, string statusCode, Type responseType, string description)
        {
            operation.Responses[statusCode] = new OpenApiResponse
            {
                Description = description,
                Content =
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(responseType, context.SchemaRepository)
                    }
                }
            };
        }

        private static void SetFileResponse(OpenApiOperation operation, string statusCode, string description)
        {
            var response = new OpenApiResponse
            {
                Description = description
            };

            foreach (var contentType in new[]
            {
                "application/octet-stream",
                "application/pdf",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "image/jpeg",
                "image/png"
            })
            {
                response.Content[contentType] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    }
                };
            }

            operation.Responses[statusCode] = response;
        }

        private static bool RequiresAuthorization(OperationFilterContext context)
        {
            if (HasMetadata<IAllowAnonymous>(context))
                return false;

            return HasMetadata<IAuthorizeData>(context);
        }

        private static bool HasMetadata<T>(OperationFilterContext context)
        {
            if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<T>().Any())
                return true;

            if (context.MethodInfo.GetCustomAttributes(inherit: true).OfType<T>().Any())
                return true;

            return context.ApiDescription.ActionDescriptor is ControllerActionDescriptor controllerAction
                && controllerAction.ControllerTypeInfo.GetCustomAttributes(inherit: true).OfType<T>().Any();
        }

        private static string? NormalizeRoute(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            var route = relativePath.Split('?')[0].Trim().Trim('/');
            return string.IsNullOrWhiteSpace(route)
                ? null
                : route.ToLowerInvariant();
        }

        private sealed record ResponseSpec(
            Type? SuccessType,
            string SuccessDescription,
            Type? BadRequestType,
            Type? ServiceUnavailableType,
            bool FileResponse);
    }

    public class ApiErrorResponseDto
    {
        public bool? Basarili { get; set; }
        public string? Mesaj { get; set; }
        public IDictionary<string, string[]>? Errors { get; set; }
    }

    public class ApiOperationResponseDto
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
        public int? Id { get; set; }
        public int? FirmaId { get; set; }
    }

    public class AuthTokenResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? AdSoyad { get; set; }
        public int Tip { get; set; }
        public IList<string> Roller { get; set; } = new List<string>();
    }

    public class DagitimSirketResponseDto
    {
        public int Id { get; set; }
        public string? SirketAdi { get; set; }
        public string? Il { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; }
    }

    public class MarkaResponseDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
        public string? Aciklama { get; set; }
        public bool AktifMi { get; set; }
    }

    public class UrunKategoriResponseDto
    {
        public int Id { get; set; }
        public string? Ad { get; set; }
        public string? IconUrl { get; set; }
        public int SiraNo { get; set; }
        public bool AktifMi { get; set; }
    }
}
