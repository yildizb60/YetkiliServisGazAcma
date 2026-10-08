using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class MvcPolicyRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        // Compile/render MVC views without the application host, a server or a database.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(YkcController).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(YkcController).Assembly);
        builder.Services.AddSingleton<IUrlHelperFactory, FixtureUrlHelperFactory>();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();

        foreach (var role in new[] { "Personel", "SertifikaliFirma" })
        {
            using var fixture = new PolicyFixture(role);
            var preview = (ViewResult)await fixture.Ykc.Fr265Onizle(42);
            var model = (YkcTalepDetayDto)preview.Model!;
            Check(fixture.Handler.Paths.Where(x => x.StartsWith("/api/ykc/", StringComparison.Ordinal))
                .SequenceEqual(["/api/ykc/talepler/getir"]),
                role + ": preview only requests normal detail, not form data or separate signature metadata");
            Check(model.Ekran.ImzaEntegrasyonu.ProviderAdi == "API fixture"
                && !model.Ekran.ImzayaGonderebilir,
                role + ": controller retains the API screen state");
            if (role == "SertifikaliFirma")
                Check(model.ProjeNo == null && model.EskiCihaz == null && model.EskiMarka == null,
                    "Firm preview retains the normal API response's source-field redaction");

            var html = await RenderAsync(scope.ServiceProvider, fixture, model);
            Check(!html.Contains("action=\"/ykc/imzaya-gonder\"")
                && !html.Contains("action=\"/ykc/imza-durum-sorgula\""),
                role + ": API denial overrides apparently suitable raw control data and stale ViewBag permissions");
            Check(!html.Contains("PRIVATE-SOURCE-") && html.Contains("data-pdf-url=\"/ykc/fr265/pdf/42\""),
                role + ": HTML contains only the PDF URL, never source fields");

            fixture.Handler.Paths.Clear();
            var pdf = (FileContentResult)await fixture.Ykc.FormPdf(42);
            Check(pdf.FileContents.SequenceEqual(PolicyHandler.PdfBytes) && pdf.ContentType == "application/pdf"
                && fixture.Handler.Paths.SequenceEqual(["/api/ykc/talepler/form-pdf"])
                && fixture.Http.Response.Headers.CacheControl == "private, no-store"
                && fixture.Http.Response.Headers["X-Frame-Options"] == "SAMEORIGIN",
                role + ": PDF remains an authenticated, non-cacheable API byte proxy without source-data retrieval");
        }

        using (var fixture = new PolicyFixture("Personel"))
        {
            var model = new YkcTalepDetayDto
            {
                Id = 42,
                Ekran = new() { ImzayaGonderebilir = true, ImzaEntegrasyonu = new() { KullanilabilirMi = true } }
            };
            var html = await RenderAsync(scope.ServiceProvider, fixture, model);
            Check(html.Contains("action=\"/ykc/imzaya-gonder\"") && html.Contains("name=\"__RequestVerificationToken\""),
                "Preview uses API send eligibility without rebuilding appointment/control rules and retains antiforgery");

            model.Ekran.ImzayaGonderebilir = false;
            model.Ekran.ImzaDurumuSorgulanabilir = true;
            model.ImzaSureci = new() { Durum = YkcImzaDurumDegerleri.Tamamlandi, ProviderDocumentId = "provider-42" };
            html = await RenderAsync(scope.ServiceProvider, fixture, model);
            Check(html.Contains("action=\"/ykc/imza-durum-sorgula\"") && !html.Contains("action=\"/ykc/imzaya-gonder\""),
                "Completed signature status does not hide API-authorized missing-document recovery");

            model.Ekran.ImzaDurumuSorgulanabilir = false;
            model.Ekran.ImzaliBelgeHazir = true;
            model.Ekran.ImzaliBelge = new() { Id = 91, DosyaAdi = "api-final.pdf" };
            html = await RenderAsync(scope.ServiceProvider, fixture, model);
            Check(html.Contains("href=\"/ykc/dosya/91\"") && !html.Contains("action=\"/ykc/imza-durum-sorgula\""),
                "Signed document download uses the API-selected file even without a raw document-list match");
            model.Ekran.DemoPdfGuncellenebilir = true;
            html = await RenderAsync(scope.ServiceProvider, fixture, model);
            Check(html.Contains("action=\"/ykc/imza-durum-sorgula\""),
                "Preview consumes API demo-document refresh eligibility without provider or version checks");
        }

        foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin" })
        {
            using var fixture = new PolicyFixture(role);
            string[] selected = [YetkiTipleri.TAM_YETKI, YetkiTipleri.RAPOR_GOR,
                YetkiTipleri.DAGITIM_SIRKET_YONET, "API_ONLY_PERMISSION"];
            var result = await fixture.Admin.YetkiDuzenle("target-user", [7],
                new FormCollection(new Dictionary<string, StringValues> { ["yetkiler_7"] = selected }), "filter", 2);
            var payload = fixture.Handler.Permissions;
            Check(payload?.Id == "target-user" && payload.SirketId == 7 && payload.SirketIds.SequenceEqual([7]),
                role + ": permission update preserves the target and selected company scope");
            Check(payload!.Yetkiler[7].SequenceEqual(selected),
                role + ": MVC forwards full-access, excluded and unknown codes to canonical API normalization");
            Check(result is RedirectToActionResult redirect && (string?)redirect.RouteValues!["q"] == "filter"
                && (int?)redirect.RouteValues["sayfa"] == 2,
                role + ": permission save retains filtered-list navigation");
        }
        using (var fixture = new PolicyFixture("Personel"))
        {
            Check(await fixture.Admin.YetkiDuzenle("target-user", [7], new FormCollection(new())) is ForbidResult
                && fixture.Handler.Permissions == null,
                "Removing payload normalization does not remove the personnel management guard");
        }
        Console.WriteLine($"{passed} MVC policy checks passed. No server or application data used.");
    }

    private static async Task<string> RenderAsync(IServiceProvider services, PolicyFixture fixture, YkcTalepDetayDto model)
    {
        fixture.Http.RequestServices = services;
        var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, "/Views/Ykc/Fr265Onizle.cshtml", false);
        if (!view.Success) throw new InvalidOperationException("Compiled FR265 preview is missing.");
        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        data["YkcYetkileri"] = new YkcYetkiOzeti { Fr265ImzaIslemiYapabilir = true };
        data["ImzaEntegrasyonu"] = new YkcImzaEntegrasyonDto { KullanilabilirMi = true };
        using var writer = new StringWriter();
        await view.View.RenderAsync(new ViewContext(new ActionContext(fixture.Http, new RouteData(), new ActionDescriptor()),
            view.View, data, new TempDataDictionary(fixture.Http, new MemoryTempData()), writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    private sealed class FixtureUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context) => new FixtureUrlHelper(context);
    }

    private sealed class FixtureUrlHelper(ActionContext context) : IUrlHelper
    {
        public ActionContext ActionContext => context;
        public string? Action(UrlActionContext action) => $"/{action.Controller}/{action.Action}";
        public string? Content(string? path) => path?.Replace("~/", "/", StringComparison.Ordinal);
        public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true && !url.StartsWith("//", StringComparison.Ordinal);
        public string? Link(string? name, object? values) => throw new NotSupportedException();
        public string? RouteUrl(UrlRouteContext context) => throw new NotSupportedException();
    }

    private sealed class PolicyFixture : IDisposable
    {
        private readonly HttpClient client;
        public PolicyHandler Handler { get; }
        public DefaultHttpContext Http { get; }
        public YkcController Ykc { get; }
        public AdminPanelController Admin { get; }

        public PolicyFixture(string role)
        {
            Handler = new PolicyHandler(role);
            client = new HttpClient(Handler) { BaseAddress = new Uri("https://fixture.invalid/") };
            Http = new DefaultHttpContext
            {
                Session = new MemorySession(),
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "policy-user"), new Claim(ClaimTypes.Role, role)], "Fixture"))
            };
            Http.Session.SetString("API.UserId", "policy-user");
            Http.Session.SetString("API.AccessToken", "policy-token");
            Http.Session.SetString("API.Expires", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
            Http.Session.SetInt32("AktifSirketId:policy-user", 7);
            var accessor = new HttpContextAccessor { HttpContext = Http };
            var session = new ApiKullaniciOturumu(new AuthApiClient(client), accessor);
            var options = Options.Create(new ApiIntegrationOptions { Enabled = true });
            var tokens = new ApiJwtTokenService(accessor);
            var companies = new PanelKapsamApiClient(client, options, tokens, NullLogger<PanelKapsamApiClient>.Instance);
            var activeCompany = new AktifSirketService(accessor, session, companies);
            var api = new YkcApiClient(client, options, tokens, NullLogger<YkcApiClient>.Instance, activeCompany);
            Ykc = new YkcController(session, api, NullLogger<YkcController>.Instance, null!)
            {
                ControllerContext = new ControllerContext { HttpContext = Http },
                TempData = new TempDataDictionary(Http, new MemoryTempData())
            };
            Ykc.ViewData["YkcYetkileri"] = new YkcYetkiOzeti { TalepleriGorebilir = true };
            Admin = new AdminPanelController(session, activeCompany, null!,
                new AdminKullaniciApiClient(client, options, tokens, NullLogger<AdminKullaniciApiClient>.Instance),
                null!, null!, null!, null!, api)
            {
                ControllerContext = new ControllerContext { HttpContext = Http },
                TempData = new TempDataDictionary(Http, new MemoryTempData())
            };
        }

        public void Dispose() => client.Dispose();
    }

    private sealed class PolicyHandler(string role) : HttpMessageHandler
    {
        public static readonly byte[] PdfBytes = [37, 80, 68, 70, 45, 49, 46, 55];
        public List<string> Paths { get; } = [];
        public AdminYetkiGuncelleDto? Permissions { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (request.Headers.Authorization?.Parameter != "policy-token")
                throw new InvalidOperationException("Fixture requests must retain the session bearer.");
            object response;
            if (path == "/api/auth/me")
                response = new OturumSonucu { Basarili = true, Kullanici = new OturumKullaniciDto(
                    "policy-user", "policy", null, "Policy Fixture", null, role switch
                    {
                        "GenelSistemAdmin" => KullaniciTipiDegerleri.GenelSistemAdmin,
                        "SirketAdmin" => KullaniciTipiDegerleri.SirketAdmin,
                        "SertifikaliFirma" => KullaniciTipiDegerleri.SertifikaliFirma,
                        _ => KullaniciTipiDegerleri.Personel
                    }, null, 7, [role]) };
            else if (path == "/api/panel-kapsam/sirketler")
                response = new[] { new PanelSirketDto { Id = 7, SirketAdi = "Fixture Company" } };
            else if (path == "/api/ykc/talepler/getir")
            {
                var detail = new YkcTalepDetayDto
                {
                    Id = 42, Durum = YkcDurumDegerleri.SahaIsleminde,
                    RandevuTarihi = DateTime.Today.AddDays(1), RandevuSaati = "10:30",
                    Atamalar = [new() { Id = 2, RandevuTarihi = DateTime.Today.AddDays(1), RandevuSaati = "10:30" }],
                    ProjeNo = "PRIVATE-SOURCE-PROJECT", EskiCihaz = "PRIVATE-SOURCE-DEVICE", EskiMarka = "PRIVATE-SOURCE-BRAND",
                    Kontroller = [new() { KontrolNo = 1, AtamaId = 1, Sonuc = YkcFr265KontrolSonucDegerleri.Uygun }],
                    Ekran = new() { ImzaEntegrasyonu = new() { KullanilabilirMi = true, ProviderAdi = "API fixture" } }
                };
                if (role == "SertifikaliFirma") YkcFirmaSunumu.Hazirla(detail, resmiForm: false);
                response = detail;
            }
            else if (path == "/api/ykc/talepler/form-pdf")
            {
                var pdf = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(PdfBytes) };
                pdf.Content.Headers.ContentType = new("application/pdf");
                return pdf;
            }
            else if (path == "/api/admin-panel/yetkiler/guncelle")
            {
                Permissions = await request.Content!.ReadFromJsonAsync<AdminYetkiGuncelleDto>(cancellationToken);
                response = new ApiIslemSonuc { Basarili = true, Mesaj = "API fixture saved" };
            }
            else throw new InvalidOperationException("Unexpected fixture route: " + path);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }
    }
}
