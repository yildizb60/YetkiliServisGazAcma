using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;
using YetkiliServisGazAcma.Entities;

namespace Tests.WebNavigation;

internal static class CommissioningPaginationRegression
{
    public static async Task RunAsync()
    {
        // Fixtures only: no live API, database or application server.
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }
        static string[] ExportIds(string html) => Regex.Match(html, "<template data-directory-export-ids>([^<]*)</template>")
            .Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var handler = new PaginationHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
        var http = new DefaultHttpContext
        {
            Session = new MemorySession(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "fixture")], "Fixture"))
        };
        http.Session.SetString("API.UserId", "fixture");
        http.Session.SetString("API.AccessToken", "fixture-token");
        http.Session.SetString("API.Expires", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        var tokens = new ApiJwtTokenService(new HttpContextAccessor { HttpContext = http });
        var options = Options.Create(new ApiIntegrationOptions { Enabled = true });
        var admin = new AdminRaporApiClient(httpClient, options, tokens, NullLogger<AdminRaporApiClient>.Instance);
        var service = new YetkiliServisDevreyeAlmaApiClient(httpClient, options, tokens, NullLogger<YetkiliServisDevreyeAlmaApiClient>.Instance);
        var user = new AppKullanici { Id = "fixture" };
        var bas = new DateTime(2026, 10, 1);
        var bit = new DateTime(2026, 10, 13);
        foreach (var servicePanel in new[] { false, true })
        {
            DevreyeAlmaSayfalamaDto metadata;
            if (servicePanel)
                metadata = (await service.GecmisAsync(user, "Brand", bas, bit, "Customer", "bekliyor", "00123", "000943",
                    "A & B", "abone-artan", "Kombi", sayfa: -4, sayfaBoyutu: 0, odakKayitId: 71))!.Sayfalama;
            else
                metadata = (await admin.DevreyeAlmalarAsync(user, 7, "Brand", "Firm", "City", "1", bas, bit,
                    "00123", "District", "Customer", "000943", "A & B", "abone-artan", "Kombi",
                    sayfa: -4, sayfaBoyutu: 0, odakKayitId: 71))!.Sayfalama;
            var body = handler.Body;
            Check(body.GetProperty("sayfa").GetInt32() == -4 && body.GetProperty("sayfaBoyutu").GetInt32() == 0
                && body.GetProperty("odakKayitId").GetInt32() == 71,
                $"{servicePanel}: transport forwards raw page, size and focus for API validation");
            Check(body.GetProperty("arama").GetString() == "A & B" && body.GetProperty("siralama").GetString() == "abone-artan"
                && body.GetProperty("tesisatNo").GetString() == "00123" && body.GetProperty("sozlesmeNo").GetString() == "000943"
                && body.GetProperty("musteri").GetString() == "Customer" && body.GetProperty("cihazTipi").GetString() == "Kombi"
                && body.GetProperty("marka").GetString() == "Brand" && body.GetProperty("baslangicTarihi").GetDateTime() == bas
                && body.GetProperty("bitisTarihi").GetDateTime() == bit,
                $"{servicePanel}: existing text, date, sort and device filters survive pagination");
            Check(servicePanel || (body.GetProperty("sirketId").GetInt32() == 7 && body.GetProperty("servis").GetString() == "Firm"
                && body.GetProperty("il").GetString() == "City" && body.GetProperty("ilce").GetString() == "District"),
                $"{servicePanel}: existing company and locality scope survives pagination");
            Check(metadata.Sayfa == 4 && metadata.SayfaBoyutu == 20 && metadata.ToplamKayit == 8000
                && metadata.AktarimIdleri.Count == 5001 && metadata.AktarimIdleri[^1] == 5001,
                $"{servicePanel}: normalized metadata and the oversized export sentinel deserialize without truncation");
            if (servicePanel) await service.GecmisAsync(user, null, null, null, null, null);
            else await admin.DevreyeAlmalarAsync(user, null, null, null, null, null, null, null);
            Check(handler.Body.GetProperty("sayfa").GetInt32() == 1 && handler.Body.GetProperty("sayfaBoyutu").GetInt32() == 20
                && handler.Body.GetProperty("odakKayitId").ValueKind == JsonValueKind.Null,
                $"{servicePanel}: omitted page arguments preserve the 1/20/null contract");
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DevreyeAlmaController).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DevreyeAlmaController).Assembly);
        builder.Services.AddSingleton<IUrlHelperFactory, FixtureUrlHelperFactory>();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var query = "?tesisat=00123&sozlesme=000943&musteri=A%20%26%20B&marka=Brand&cihazTipi=Kombi&durum=1"
            + "&servis=Firm&il=City&ilce=District&bas=2026-10-01&bit=2026-10-13&q=%3Csearch%3E&sirala=abone-artan&sirketId=7&sayfa=999&kayitId=42";
        foreach (var panel in new[] { "AdminPanel", "PersonelPanel", "DevreyeAlma" })
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.QueryString = new QueryString(query);
            var action = new ActionContext(context, new RouteData(), new ActionDescriptor());
            var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                ["SeciliSirketId"] = 7,
                ["Kullanici"] = new AppKullanici { Id = "fixture", AdSoyad = "Fixture", KullaniciTipi = panel == "DevreyeAlma" ? 1 : 3 }
            };
            var metadata = new DevreyeAlmaSayfalamaDto
            {
                Sayfa = 3, SayfaBoyutu = 20, ToplamKayit = 85, AktarimIdleri = Enumerable.Range(1, 85).ToList()
            };
            object rows = panel == "DevreyeAlma"
                ? Enumerable.Range(41, 20).Select(id => new YsDevreyeAlmaDto { Id = id, MusteriAdi = "Fixture", TesistatNo = "00123" }).ToList()
                : Enumerable.Range(41, 20).Select(id => new AdminDevreyeAlmaDto { Id = id, MusteriAdi = "Fixture", TesistatNo = "00123" }).ToList();
            data["Sayfalama"] = metadata;
            data.Model = rows;
            var viewPath = $"/Views/{panel}/{(panel == "DevreyeAlma" ? "Gecmis" : "DevreyeAlmalar")}.cshtml";
            async Task<string> Render()
            {
                var result = services.GetRequiredService<IRazorViewEngine>().GetView(null, viewPath, false);
                if (!result.Success) throw new InvalidOperationException("Compiled view missing: " + viewPath);
                using var writer = new StringWriter();
                await result.View.RenderAsync(new ViewContext(action, result.View, data,
                    new TempDataDictionary(context, new MemoryTempData()), writer, new HtmlHelperOptions()));
                return writer.ToString();
            }
            var html = await Render();
            Check(Regex.Matches(html, "class=\"df-commissioning-pager\"").Count == 1 && html.Contains(">85 kay")
                && html.Contains("41-60 / 85") && html.Contains("aria-current=\"page\">3 / 5"),
                panel + ": one common pager shows API totals and normalized page, not the query page or visible count");
            Check(Regex.Matches(html, "data-commissioning-record data-export-id").Count == 20
                && Regex.Matches(html, "class=\"df-commissioning-detail-row\"").Count == 20
                && ExportIds(html).Length == 85 && !html.Contains("name=\"ids\""),
                panel + ": only current-page records/details render while all filtered export IDs remain available");
            var pager = Regex.Match(html, "<footer class=\"df-commissioning-pager\">[\\s\\S]*?</footer>").Value;
            var links = Regex.Matches(pager, "href=\"([^\"]+)\"").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
            Check(links.Count == 4 && links.Select(link => QueryHelpers.ParseQuery(new Uri("https://fixture.invalid" + link).Query)["sayfa"].ToString())
                .SequenceEqual(["1", "2", "4", "5"]), panel + ": first, previous, next and last links use normalized API page");
            var original = QueryHelpers.ParseQuery(query);
            foreach (var link in links)
            {
                var values = QueryHelpers.ParseQuery(new Uri("https://fixture.invalid" + link).Query);
                Check(!values.ContainsKey("kayitId") && !link.Contains('#') && original.Where(x => x.Key is not "sayfa" and not "kayitId")
                    .All(x => values.TryGetValue(x.Key, out var value) && value == x.Value),
                    panel + ": pager retains every filter, q, sort and company but clears focus");
            }
            var form = Regex.Match(html, "<form id=\"commissioningFilters\"[\\s\\S]*?</form>").Value;
            Check(!form.Contains("name=\"sayfa\"") && !form.Contains("name=\"kayitId\"")
                && html.Contains("name=\"q\"") && html.Contains("name=\"sirala\"") && !html.Contains("<search>"),
                panel + ": filter/search/sort submission resets page and focus and encodes query text");
            var fixtureDirectory = Environment.GetEnvironmentVariable("YSGA_PAGINATION_FIXTURE_DIR");
            if (!string.IsNullOrEmpty(fixtureDirectory))
            {
                Directory.CreateDirectory(fixtureDirectory);
                await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, panel + ".html"), html);
            }
            metadata.Sayfa = 1;
            html = await Render();
            Check(Regex.Matches(Regex.Match(html, "<footer class=\"df-commissioning-pager\">[\\s\\S]*?</footer>").Value,
                "aria-disabled=\"true\"").Count == 2, panel + ": first page disables backward navigation");
            metadata.Sayfa = 5;
            html = await Render();
            Check(html.Contains("81-85 / 85") && !html.Contains("rel=\"next\""), panel + ": last page ends at the total and has no next link");
            metadata.AktarimIdleri = Enumerable.Range(1, 5001).ToList();
            metadata.ToplamKayit = 8000;
            html = await Render();
            Check(ExportIds(html).Length == 5001 && ExportIds(html)[^1] == "5001" && !html.Contains("name=\"ids\""),
                panel + ": all-export sends the intentional 5001-ID API error sentinel without truncation");
            data.Remove("Sayfalama");
            html = await Render();
            Check(!html.Contains("data-directory-export-ids") && !html.Contains("class=\"df-commissioning-pager\"") && html.Contains(">20 kay"),
                panel + ": old/nonpaged fixtures retain the visible-record fallback without a synthetic pager");
        }
        Console.WriteLine($"{passed} commissioning pagination transport/Razor checks passed.");

    }

    sealed class PaginationHandler : HttpMessageHandler
    {
        public JsonElement Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post || request.Headers.Authorization?.Parameter != "fixture-token")
                throw new InvalidOperationException("Authenticated POST required");
            Body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                Sayfalama = new DevreyeAlmaSayfalamaDto
                {
                    Sayfa = 4, SayfaBoyutu = 20, ToplamKayit = 8000, AktarimIdleri = Enumerable.Range(1, 5001).ToList()
                }
            }) };
        }
    }

    sealed class FixtureUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context) => new FixtureUrlHelper(context);
    }
    sealed class FixtureUrlHelper(ActionContext context) : IUrlHelper
    {
        public ActionContext ActionContext => context;
        public string? Action(UrlActionContext action) => QueryHelpers.AddQueryString($"/{action.Controller}/{action.Action}",
            new RouteValueDictionary(action.Values).Where(x => x.Value != null).ToDictionary(x => x.Key, x => (string?)x.Value!.ToString()));
        public string? Content(string? path) => path?.Replace("~/", "/", StringComparison.Ordinal);
        public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true && !url.StartsWith("//", StringComparison.Ordinal);
        public string? Link(string? name, object? values) => throw new NotSupportedException();
        public string? RouteUrl(UrlRouteContext context) => throw new NotSupportedException();
    }
}
