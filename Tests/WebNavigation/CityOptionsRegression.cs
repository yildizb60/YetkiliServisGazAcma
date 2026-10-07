using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class CityOptionsRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
            passed++;
            Console.WriteLine("PASS: " + label);
        }

        foreach (var type in new[] { typeof(AdminPanelController), typeof(PersonelPanelController) })
            Check(type.GetConstructors().SelectMany(x => x.GetParameters())
                .All(x => !typeof(SehirFirmaKodlari).IsAssignableFrom(x.ParameterType)),
                type.Name + ": MVC does not depend on local city policy");

        foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "Personel" })
        {
            using var fixture = new CityFixture(role);
            var admin = fixture.Prepare<AdminPanelController>();
            var personnel = fixture.Prepare<PersonelPanelController>();
            var actions = new (Controller Controller, Func<Task<IActionResult>> Call, string View, string Path, bool Codes)[]
            {
                (admin, () => admin.YetkiliServisler("query", "API Z", 1, "azalan"),
                    "AdminPanel/YetkiliServisler", "/api/admin-panel/yetkili-servisler/liste", false),
                (admin, () => admin.YetkiliServisEkle(),
                    "AdminPanel/YetkiliServisEkle", "/api/admin-panel/yetkili-servisler/editor", true),
                (admin, () => admin.YetkiliServisDuzenle(12),
                    "AdminPanel/YetkiliServisDuzenle", "/api/admin-panel/yetkili-servisler/editor", true),
                (admin, () => admin.DevreyeAlmalar(null, null, "API Z", null, null),
                    "AdminPanel/DevreyeAlmalar", "/api/admin-panel/devreye-almalar/liste", false),
                (personnel, () => personnel.YetkiliServisler("query", "API Z", "1", "azalan"),
                    "PersonelPanel/YetkiliServisler", "/api/admin-panel/yetkili-servisler/liste", false),
                (personnel, () => personnel.YetkiliServisEkle(),
                    "PersonelPanel/YetkiliServisEkle", "/api/admin-panel/yetkili-servisler/editor", false),
                (personnel, () => personnel.YetkiliServisDuzenle(12),
                    "PersonelPanel/YetkiliServisDuzenle", "/api/admin-panel/yetkili-servisler/editor", false),
                (personnel, () => personnel.DevreyeAlmalar(null, null, null, null, "API Z", null, null, null),
                    "PersonelPanel/DevreyeAlmalar", "/api/admin-panel/devreye-almalar/liste", false)
            };
            foreach (var action in actions)
            {
                fixture.Handler.Paths.Clear();
                Check(await action.Call() is ViewResult view && view.ViewName == "~/Views/" + action.View + ".cshtml",
                    role + ": existing view is preserved: " + action.View);
                Check(action.Controller.ViewData["Sehirler"] is List<string> cities && cities.SequenceEqual(["API Z", "API A"]),
                    role + ": city list preserves API values, ordering and List<string> type: " + action.View);
                Check(fixture.Handler.Paths.Count(x => x == action.Path) == 1
                    && fixture.Handler.Paths.All(x => x == action.Path || CityHandler.IdentityPaths.Contains(x)),
                    role + ": cities arrive in the existing aggregate without extra option requests: " + action.View);
                Check(fixture.Handler.LastAggregate.GetProperty("sirketId").ValueKind == (role == "GenelSistemAdmin" ? JsonValueKind.Null : JsonValueKind.Number)
                    && (role == "GenelSistemAdmin" || fixture.Handler.LastAggregate.GetProperty("sirketId").GetInt32() == 7),
                    role + ": company scope is unchanged: " + action.View);
                if (action.Codes)
                    Check(action.Controller.ViewData["SehirFirmaKodlari"] is Dictionary<string, string> codes
                        && codes.Count == 2 && codes["API Z"] == "API_Z" && codes["API A"] == "API_A",
                        role + ": editor renders the API city/company code map");
            }

            fixture.Handler.EmptyOptions = true;
            await admin.YetkiliServisEkle();
            Check(admin.ViewData["Sehirler"] is List<string> { Count: 0 }
                && admin.ViewData["SehirFirmaKodlari"] is Dictionary<string, string> { Count: 0 },
                role + ": empty API editor options cannot activate local city defaults");
            await personnel.YetkiliServisler(null, null, null, null);
            Check(personnel.ViewData["Sehirler"] is List<string> { Count: 0 },
                role + ": empty service list options cannot activate local city defaults");
            fixture.Handler.Status = HttpStatusCode.ServiceUnavailable;
            await personnel.DevreyeAlmalar(null, null, null, null, null, null, null, null);
            Check(personnel.ViewData["Sehirler"] is List<string> { Count: 0 } && personnel.TempData["Hata"] != null,
                role + ": report outage retains its warning without local city fallback");
        }
        Console.WriteLine($"{passed} MVC city option checks passed without a server or database.");
    }

    private sealed class CityFixture : IDisposable
    {
        private readonly HttpClient http;
        private readonly ServiceProvider services;
        private readonly DefaultHttpContext context;
        public CityHandler Handler { get; }

        public CityFixture(string role)
        {
            Handler = new CityHandler(role);
            http = new HttpClient(Handler) { BaseAddress = new Uri("https://fixture.invalid/") };
            context = new DefaultHttpContext
            {
                Session = new CitySession(),
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "city-user"), new Claim(ClaimTypes.Role, role)], "Fixture"))
            };
            context.Session.SetString("API.UserId", "city-user");
            context.Session.SetString("API.AccessToken", "city-token");
            context.Session.SetString("API.Expires", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
            context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            var registrations = new ServiceCollection().AddLogging()
                .AddSingleton(http)
                .AddSingleton<IOptions<ApiIntegrationOptions>>(Options.Create(new ApiIntegrationOptions { Enabled = true }))
                .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context })
                .AddSingleton<ApiJwtTokenService>().AddSingleton<ApiKullaniciOturumu>()
                .AddSingleton<AktifSirketService>().AddSingleton<PanelGorunumService>();
            foreach (var type in typeof(AuthApiClient).Assembly.GetTypes()
                .Where(x => x.IsPublic && x.Name.EndsWith("ApiClient", StringComparison.Ordinal)))
                registrations.AddTransient(type);
            services = registrations.BuildServiceProvider();
        }

        public T Prepare<T>() where T : Controller
        {
            var controller = ActivatorUtilities.CreateInstance<T>(services);
            controller.ControllerContext = new() { HttpContext = context };
            controller.TempData = new TempDataDictionary(context, new CityTempData());
            return controller;
        }

        public void Dispose() { services.Dispose(); http.Dispose(); }
    }

    private sealed class CitySession : ISession
    {
        private readonly Dictionary<string, byte[]> data = new();
        public bool IsAvailable => true;
        public string Id => "city-fixture";
        public IEnumerable<string> Keys => data.Keys;
        public void Clear() => data.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => data.Remove(key);
        public void Set(string key, byte[] value) => data[key] = value;
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => data.TryGetValue(key, out value);
    }

    private sealed class CityTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class CityHandler(string role) : HttpMessageHandler
    {
        public static readonly string[] IdentityPaths = ["/api/auth/me", "/api/panel-kapsam/sirketler",
            "/api/personel-panel/yetkilerim", "/api/admin-panel/kullanicilar/yonetim-yetkisi"];
        public List<string> Paths { get; } = [];
        public JsonElement LastAggregate { get; private set; }
        public bool EmptyOptions { get; set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (request.Headers.Authorization?.Parameter != "city-token")
                throw new InvalidOperationException("Expected scoped bearer authentication");
            var identity = IdentityPaths.Contains(path);
            if (!identity) LastAggregate = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            List<string> cities = EmptyOptions ? [] : ["API Z", "API A"];
            object result = path switch
            {
                "/api/auth/me" => new OturumSonucu { Basarili = true, Kullanici = new("city-user", "fixture", null,
                    "Fixture", null, role == "GenelSistemAdmin" ? KullaniciTipiDegerleri.GenelSistemAdmin
                        : role == "SirketAdmin" ? KullaniciTipiDegerleri.SirketAdmin : KullaniciTipiDegerleri.Personel,
                    null, role == "GenelSistemAdmin" ? null : 7, [role]) },
                "/api/panel-kapsam/sirketler" => new[] { new PanelSirketDto { Id = 7, SirketAdi = "Company" } },
                "/api/personel-panel/yetkilerim" => new { yetkiler = new[] { YetkiTipleri.TAM_YETKI } },
                "/api/admin-panel/kullanicilar/yonetim-yetkisi" => new { yetkiliMi = true },
                "/api/admin-panel/yetkili-servisler/liste" => new AdminYetkiliServisListeDto { Sehirler = cities },
                "/api/admin-panel/devreye-almalar/liste" => new AdminDevreyeAlmaListeDto { Sehirler = cities },
                "/api/admin-panel/yetkili-servisler/editor" => new AdminYetkiliServisEditorDto
                {
                    Servis = new() { Id = 12, FirmaAdi = "Fixture service" }, Sehirler = cities,
                    SehirFirmaKodlari = EmptyOptions ? new() : new() { ["API Z"] = "API_Z", ["API A"] = "API_A" }
                },
                _ => throw new InvalidOperationException("Unexpected city options request: " + path)
            };
            return new HttpResponseMessage(identity ? HttpStatusCode.OK : Status) { Content = JsonContent.Create(result) };
        }
    }
}
