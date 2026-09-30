using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;
using YetkiliServisGazAcma.Models;

// All HTTP responses are fixtures; no network requests or application data changes.
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "Personel" })
{
    using var fixture = new NavigationFixture(role);
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi }, "takvim");
    Check(result.ActionName == nameof(YkcController.Raporlar)
        && (int)result.RouteValues!["talepId"]! == 54
        && result.RouteValues["detayAc"] is true
        && fixture.Controller.TempData.ContainsKey("Basarili"),
        role + ": successful completion requests the exact report modal and keeps the success message");
}

using (var fixture = new NavigationFixture("Personel"))
{
    fixture.Handler.Success = false;
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi }, "takvim");
    Check(result.ActionName == nameof(YkcController.Detay) && (string?)result.RouteValues!["kaynak"] == "takvim"
        && fixture.Controller.TempData.ContainsKey("Hata"), "Failed completion stays on detail with its error and source");
}

using (var fixture = new NavigationFixture("Personel", reportPermission: false))
{
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi });
    Check(result.ActionName == nameof(YkcController.Detay), "Completion without report permission stays on detail");
    var report = (RedirectResult)await fixture.Report(54, openDetail: true);
    Check(report.Url == "/yetkisiz-erisim" && fixture.Handler.LastReportFilter == null,
        "A report URL cannot bypass report permission");
}

using (var fixture = new NavigationFixture("Personel", signaturePermission: false))
{
    var result = (RedirectResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi });
    Check(result.Url == "/yetkisiz-erisim" && fixture.Handler.OperationCalls == 0,
        "Completion still requires signature permission before calling the API");
}

using (var fixture = new NavigationFixture("Personel"))
{
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.SahaIsleminde });
    Check(result.ActionName == nameof(YkcController.Detay), "An intermediate status stays on detail");
    var signature = (RedirectToActionResult)await fixture.Controller.ImzaDurumSorgula(54);
    Check(signature.ActionName == nameof(YkcController.Detay), "Signature status alone does not complete the request");

    var report = (ViewResult)await fixture.Report(54);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri!.SequenceEqual([54])
        && (int)report.ViewData["TalepId"]! == 54, "Report filtering uses the exact request ID");
    Check(fixture.Handler.LastReportFilter.SirketId == 7, "Exact-record navigation retains the active company scope");
    await fixture.Report(null);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri == null, "All reports clears the request restriction");
    await fixture.Report(-1);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri is { Count: 0 }, "An invalid ID cannot expand to all records");

    foreach (var excel in new[] { false, true })
    {
        await fixture.Export(excel, 54);
        Check(fixture.Handler.LastReportFilter!.KayitIdleri!.SequenceEqual([54]),
            (excel ? "Excel" : "PDF") + " export retains the focused request");
        fixture.Http.Request.QueryString = new QueryString("?ids=55");
        await fixture.Export(excel, 54, [55]);
        Check(fixture.Handler.LastReportFilter!.KayitIdleri is { Count: 0 },
            "Selected export IDs cannot widen the focused report");
        fixture.Http.Request.QueryString = QueryString.Empty;
    }
}

using (var fixture = new NavigationFixture("Personel"))
{
    fixture.Handler.ReportResult.Kayitlar.Add(new() { Id = 54 });
    var report = (ViewResult)await fixture.Report(54, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] is 54,
        "The automatic modal targets a returned, scoped report record");
    report = (ViewResult)await fixture.Report(54);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "Normal report navigation does not reopen the modal");
    report = (ViewResult)await fixture.Report(55, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "A record absent from the scoped result cannot open automatically");
    report = (ViewResult)await fixture.Report(null, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "The automatic modal requires an explicit request ID");
    report = (ViewResult)await fixture.Report(-1, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "An invalid request ID cannot trigger an automatic modal");
}
Console.WriteLine($"{passed} navigation checks passed. No application data changed.");

sealed class NavigationFixture : IDisposable
{
    private readonly HttpClient client;
    public FixtureHandler Handler { get; }
    public DefaultHttpContext Http { get; }
    public YkcController Controller { get; }

    public NavigationFixture(string role, bool reportPermission = true, bool signaturePermission = true)
    {
        Handler = new FixtureHandler(role);
        client = new HttpClient(Handler) { BaseAddress = new Uri("https://fixture.invalid/") };
        Http = new DefaultHttpContext
        {
            Session = new MemorySession(),
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "fixture-user"), new Claim(ClaimTypes.Role, role)], "Fixture"))
        };
        Http.Session.SetString("API.UserId", "fixture-user");
        Http.Session.SetString("API.AccessToken", "fixture-token");
        Http.Session.SetString("API.Expires", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        var accessor = new HttpContextAccessor { HttpContext = Http };
        var session = new ApiKullaniciOturumu(new AuthApiClient(client), accessor);
        var options = Options.Create(new ApiIntegrationOptions { Enabled = true });
        var tokens = new ApiJwtTokenService(accessor);
        var companies = new PanelKapsamApiClient(client, options, tokens, NullLogger<PanelKapsamApiClient>.Instance);
        var api = new YkcApiClient(client, options, tokens, NullLogger<YkcApiClient>.Instance,
            new AktifSirketService(accessor, session, companies));
        Controller = new YkcController(session, api, NullLogger<YkcController>.Instance, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        Controller.ViewBag.YkcYetkileri = new YkcYetkiOzeti
        {
            TalepleriGorebilir = true, AtamaYapabilir = true,
            Fr265ImzaIslemiYapabilir = signaturePermission, RaporlariGorebilir = reportPermission
        };
    }

    public Task<IActionResult> Report(int? id, bool openDetail = false) => Controller.Raporlar(
        null, null, null, null, null, null, null, null, null, null, null, talepId: id, detayAc: openDetail);

    public Task<IActionResult> Export(bool excel, int id, List<int>? ids = null) => excel
        ? Controller.RaporExcel(null, null, null, null, null, null, null, null, null, null, null, ids, id)
        : Controller.RaporPdf(null, null, null, null, null, null, null, null, null, null, null, ids, id);

    public void Dispose() => client.Dispose();
}

sealed class FixtureHandler(string role) : HttpMessageHandler
{
    public bool Success { get; set; } = true;
    public int OperationCalls { get; private set; }
    public YkcRaporSonuc ReportResult { get; } = new();
    public YkcTalepListeFiltre? LastReportFilter { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        object result;
        if (path == "/api/auth/me")
            result = new OturumSonucu { Basarili = true, Kullanici = new OturumKullaniciDto(
                "fixture-user", "fixture", null, "Fixture", null, KullaniciTipiDegerleri.Personel, null, 7, [role]) };
        else if (path == "/api/panel-kapsam/sirketler")
            result = new[] { new { Id = 7, SirketAdi = "Fixture company" } };
        else if (path.StartsWith("/api/ykc/talepler/rapor", StringComparison.Ordinal))
        {
            LastReportFilter = await request.Content!.ReadFromJsonAsync<YkcTalepListeFiltre>(cancellationToken);
            if (path.EndsWith("/pdf", StringComparison.Ordinal) || path.EndsWith("/excel", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
            result = ReportResult;
        }
        else if (path is "/api/ykc/talepler/durum-guncelle" or "/api/ykc/talepler/imza-durum-sorgula")
        {
            OperationCalls++;
            result = new YkcIslemSonuc { Basarili = Success, Mesaj = "Fixture result", Id = 54 };
        }
        else throw new InvalidOperationException("Unexpected fixture route: " + path);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
    }
}

sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

sealed class MemorySession : ISession
{
    private readonly Dictionary<string, byte[]> data = [];
    public bool IsAvailable => true;
    public string Id => "fixture-session";
    public IEnumerable<string> Keys => data.Keys;
    public void Clear() => data.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => data.Remove(key);
    public void Set(string key, byte[] value) => data[key] = value;
    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => data.TryGetValue(key, out value);
}
