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
        && (int)result.RouteValues!["detayTalepId"]! == 54
        && !result.RouteValues.ContainsKey("talepId")
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
    Check(fixture.Handler.LastReportFilter!.KayitIdleri == null
        && fixture.Handler.LastReportFilter.DetayTalepId == 54
        && fixture.Handler.LastReportFilter.SirketId == 7 && report.ViewData["TalepId"] == null,
        "Legacy detail links locate the record without hiding other company records");
    fixture.Handler.ReportResult.Kayitlar.Add(new() { Id = 55 });
    report = (ViewResult)await fixture.Report(null, detailId: 54);
    Check(report.ViewData["OtomatikDetayTalepId"] is 54 && report.ViewData["TalepId"] == null
        && fixture.Handler.LastReportFilter!.KayitIdleri == null && fixture.Handler.LastReportFilter.DetayTalepId == 54
        && ((YkcRaporSonuc)report.Model!).Kayitlar.Count == 2,
        "Report detail navigation opens the target and retains neighbouring records");
    report = (ViewResult)await fixture.Report(54);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "Normal report navigation does not reopen the modal");
    report = (ViewResult)await fixture.Report(56, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "A record absent from the scoped result cannot open automatically");
    report = (ViewResult)await fixture.Report(null, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "The automatic modal requires an explicit request ID");
    report = (ViewResult)await fixture.Report(-1, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "An invalid request ID cannot trigger an automatic modal");
    report = (ViewResult)await fixture.Report(null, detailId: -1);
    Check(report.ViewData["OtomatikDetayTalepId"] == null && fixture.Handler.LastReportFilter!.DetayTalepId == null,
        "An invalid detail target does not trigger automatic navigation");
    report = (ViewResult)await fixture.Report(54, detailId: 54);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri!.SequenceEqual([54]),
        "An explicit report filter remains separate from the detail target");
}
foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized,
    HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable })
{
    using var fixture = new NavigationFixture("Personel");
    fixture.Handler.CertificateStatus = status;
    var result = (ObjectResult)await fixture.CertificateController.Dosya(26);
    var expected = status is HttpStatusCode.InternalServerError ? 503 : (int)status;
    var message = result.Value as string ?? "";
    Check(result.StatusCode == expected, "Certificate download preserves the correct status for " + status);
    Check(status switch
    {
        HttpStatusCode.NotFound => message.Contains("dosya") && !message.Contains("Veri servisine"),
        HttpStatusCode.Forbidden => message.Contains("yetkiniz"),
        HttpStatusCode.Unauthorized => message.Contains("Oturumunuzun"),
        _ => message.Contains("Veri servisine")
    }, "Certificate download distinguishes the reason for " + status);
}
using (var fixture = new NavigationFixture("Personel"))
{
    var result = (FileContentResult)await fixture.CertificateController.Dosya(26);
    Check(result.FileContents.SequenceEqual(new byte[] { 37, 80, 68, 70 }) && result.ContentType == "application/pdf"
        && fixture.Http.Response.Headers.CacheControl == "private, no-store",
        "A successful certificate download retains the original bytes and private caching");
    fixture.Handler.CertificateUnavailable = true;
    var unavailable = (ObjectResult)await fixture.CertificateController.Dosya(26);
    Check(unavailable.StatusCode == 503 && ((string)unavailable.Value!).Contains("Veri servisine"),
        "A network failure remains a service-unavailable response, not a missing file");
}
foreach (var (role, home) in new[]
{
    ("Personel", "/personel-panel"), ("SirketAdmin", "/AdminPanel"),
    ("GenelSistemAdmin", "/AdminPanel"), ("YetkiliServis", "/ys-panel"), ("SertifikaliFirma", "/ykc")
})
{
    using var fixture = new NavigationFixture(role, reportPermission: false);
    foreach (var previousPage in new[] { "/personel-panel/raporlar", "/ykc/detay/54", "https://example.invalid/" })
    {
        var redirect = (RedirectResult)await fixture.CompanyController.SirketSec(8, previousPage);
        Check(redirect.Url == home && fixture.Http.Session.GetInt32("AktifSirketId:fixture-user") == 8,
            role + ": company switch starts at its home, not an old scoped record or forbidden report");
    }
    var rejected = (RedirectToActionResult)await fixture.CompanyController.SirketSec(99, "/personel-panel/raporlar");
    Check(rejected.ActionName == nameof(PanelSirketController.SirketSec)
        && fixture.Http.Session.GetInt32("AktifSirketId:fixture-user") == 8,
        role + ": an unauthorized company cannot replace the current scope");
}
Check(YkcDurumSunumu.Etiket(YkcDurumDegerleri.TalepAlindi) == "Talep Alındı"
    && YkcDurumSunumu.Etiket(YkcDurumDegerleri.AtamaBekliyor) == "İnceleniyor",
    "Web status labels retain the shared report wording");
foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "YetkiliServis" })
{
    using var fixture = new NavigationFixture(role);
    var start = new DateTime(2026, 9, 1);
    var end = new DateTime(2026, 9, 30);
    var controller = role == "YetkiliServis" ? (Controller)fixture.ServicePanelController : fixture.AdminPanelController;
    foreach (var excel in new[] { false, true })
    {
        fixture.Handler.CommissioningStatus = HttpStatusCode.BadRequest;
        fixture.Handler.CommissioningBody = "{\"basarili\":false,\"mesaj\":\"Tek dosyada en fazla 5000 kayıt dışa aktarılabilir.\"}";
        var before = fixture.Handler.CommissioningCalls;
        var rejected = (RedirectToActionResult)await fixture.CommissioningExport(role == "YetkiliServis", excel, start, end);
        Check(rejected.ActionName == "Raporlar" && (DateTime)rejected.RouteValues!["bas"]! == start
            && (DateTime)rejected.RouteValues["bit"]! == end
            && (role == "YetkiliServis" || (int)rejected.RouteValues["sirketId"]! == 7),
            $"{role}, Excel={excel}: failed downloads return to the same report date and company filters");
        Check(((string?)controller.TempData["Hata"])?.Contains("5000") == true
            && fixture.Handler.CommissioningCalls == before + 1,
            $"{role}, Excel={excel}: validation messages reach the user without outage text or retries");
        fixture.Handler.CommissioningBody = "<html>internal diagnostic text</html>";
        await fixture.CommissioningExport(role == "YetkiliServis", excel, start, end);
        Check((string?)controller.TempData["Hata"] == "Rapor oluşturulamadı. Tarih aralığını ve kayıt seçimini kontrol edin.",
            $"{role}, Excel={excel}: malformed validation payloads use a safe user-facing message");
        fixture.Handler.CommissioningStatus = HttpStatusCode.OK;
        var success = (FileContentResult)await fixture.CommissioningExport(role == "YetkiliServis", excel, start, end);
        Check(success.FileContents.SequenceEqual(new byte[] { 37, 80, 68, 70 }),
            $"{role}, Excel={excel}: successful report downloads retain their original bytes");
    }
}
using (var fixture = new NavigationFixture("SertifikaliFirma"))
{
    var input = new YkcCihazKarsilastirmaIstek
    {
        SorguReferansi = "fixture-reference", TesisatNo = "00123", SozlesmeNo = "00456",
        YeniCihazTipi = "Kombi", YeniMarka = "E.C.A", YeniBacaTipi = "Hermetik", YeniKapasite = "25000"
    };
    var result = (JsonResult)await fixture.Controller.CihazKarsilastir(input);
    Check(result.Value is YkcCihazKarsilastirmaSonuc { Basarili: true, Uyarilar.Count: 1 }
        && fixture.Handler.LastComparison?.SorguReferansi == input.SorguReferansi
        && fixture.Handler.LastComparison.TesisatNo == "00123"
        && fixture.Handler.LastComparison.YeniKapasite == "25000",
        "Firm comparison forwards the authorized source reference and new fields without changing identifiers");
    fixture.Handler.ComparisonUnavailable = true;
    Check(await fixture.Controller.CihazKarsilastir(input) is ObjectResult
        { StatusCode: 503, Value: YkcCihazKarsilastirmaSonuc { Basarili: false } },
        "Comparison outage is reported without marking the device as matching");
    fixture.Controller.ModelState.AddModelError("YeniMarka", "Too long");
    var previousCalls = fixture.Handler.ComparisonCalls;
    Check(await fixture.Controller.CihazKarsilastir(input) is BadRequestResult
        && fixture.Handler.ComparisonCalls == previousCalls, "Invalid comparison fields never reach the API");
}
using (var fixture = new NavigationFixture("Personel"))
{
    Check(await fixture.Controller.CihazKarsilastir(new()) is StatusCodeResult { StatusCode: 403 }
        && fixture.Handler.ComparisonCalls == 0, "Read permission does not authorize creating-device comparison");
}
Check(typeof(YkcController).GetMethod(nameof(YkcController.CihazKarsilastir))!
    .IsDefined(typeof(ValidateAntiForgeryTokenAttribute), inherit: true),
    "New comparison endpoint requires an antiforgery token");
Console.WriteLine($"{passed} navigation checks passed. No application data changed.");

sealed class NavigationFixture : IDisposable
{
    private readonly HttpClient client;
    public FixtureHandler Handler { get; }
    public DefaultHttpContext Http { get; }
    public YkcController Controller { get; }
    public YetkiBelgesiController CertificateController { get; }
    public PanelSirketController CompanyController { get; }
    public YetkiliServisPanelController ServicePanelController { get; }
    public AdminPanelController AdminPanelController { get; }

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
        CertificateController = new YetkiBelgesiController(
            new YetkiBelgesiApiClient(client, options, tokens, NullLogger<YetkiBelgesiApiClient>.Instance), session, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http }
        };
        var companies = new PanelKapsamApiClient(client, options, tokens, NullLogger<PanelKapsamApiClient>.Instance);
        ServicePanelController = new YetkiliServisPanelController(session,
            new YetkiliServisPanelApiClient(client, options, tokens, NullLogger<YetkiliServisPanelApiClient>.Instance), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        AdminPanelController = new AdminPanelController(session, null!,
            new AktifSirketService(accessor, session, companies), null!, null!, null!, null!, null!,
            new AdminRaporApiClient(client, options, tokens, NullLogger<AdminRaporApiClient>.Instance), null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        CompanyController = new PanelSirketController(session, new AktifSirketService(accessor, session, companies))
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        var api = new YkcApiClient(client, options, tokens, NullLogger<YkcApiClient>.Instance,
            new AktifSirketService(accessor, session, companies));
        Controller = new YkcController(session, api, NullLogger<YkcController>.Instance, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        Controller.ViewBag.YkcYetkileri = new YkcYetkiOzeti
        {
            TalepleriGorebilir = true, AtamaYapabilir = true, TalepOlusturabilir = role == "SertifikaliFirma",
            Fr265ImzaIslemiYapabilir = signaturePermission, RaporlariGorebilir = reportPermission
        };
    }

    public Task<IActionResult> Report(int? id, bool openDetail = false, int? detailId = null) => Controller.Raporlar(
        null, null, null, null, null, null, null, null, null, null, null, talepId: id, detayAc: openDetail, detayTalepId: detailId);

    public Task<IActionResult> Export(bool excel, int id, List<int>? ids = null) => excel
        ? Controller.RaporExcel(null, null, null, null, null, null, null, null, null, null, null, ids, id)
        : Controller.RaporPdf(null, null, null, null, null, null, null, null, null, null, null, ids, id);

    public Task<IActionResult> CommissioningExport(bool service, bool excel, DateTime start, DateTime end) => service
        ? excel ? ServicePanelController.RaporlarExcel(start, end, [1]) : ServicePanelController.RaporlarPdf(start, end, [1])
        : excel ? AdminPanelController.RaporlarExcel(start, end, [1], 7) : AdminPanelController.RaporlarPdf(start, end, [1], 7);

    public void Dispose() => client.Dispose();
}

sealed class FixtureHandler(string role) : HttpMessageHandler
{
    public bool Success { get; set; } = true;
    public HttpStatusCode CertificateStatus { get; set; } = HttpStatusCode.OK;
    public bool CertificateUnavailable { get; set; }
    public HttpStatusCode CommissioningStatus { get; set; } = HttpStatusCode.OK;
    public string CommissioningBody { get; set; } = "{}";
    public int CommissioningCalls { get; private set; }
    public int OperationCalls { get; private set; }
    public int ComparisonCalls { get; private set; }
    public bool ComparisonUnavailable { get; set; }
    public YkcCihazKarsilastirmaIstek? LastComparison { get; private set; }
    public YkcRaporSonuc ReportResult { get; } = new();
    public YkcTalepListeFiltre? LastReportFilter { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        object result;
        if (path.StartsWith("/api/admin-panel/devreye-almalar/rapor/", StringComparison.Ordinal)
            || path.StartsWith("/api/ys-panel/raporlar/", StringComparison.Ordinal))
        {
            CommissioningCalls++;
            if (request.Headers.Authorization?.Parameter != "fixture-token")
                throw new InvalidOperationException("Report download must forward the authenticated token");
            return new HttpResponseMessage(CommissioningStatus)
            {
                Content = CommissioningStatus == HttpStatusCode.OK
                    ? new ByteArrayContent([37, 80, 68, 70])
                    : new StringContent(CommissioningBody, System.Text.Encoding.UTF8, "application/json")
            };
        }
        if (path == "/api/yetki-belgesi/dosya-indir")
        {
            if (CertificateUnavailable) throw new HttpRequestException("Fixture network failure");
            if (request.Headers.Authorization?.Parameter != "fixture-token")
                throw new InvalidOperationException("Certificate download must forward the authenticated token");
            var response = new HttpResponseMessage(CertificateStatus) { Content = new ByteArrayContent([37, 80, 68, 70]) };
            response.Content.Headers.ContentType = new("application/pdf");
            return response;
        }
        if (path == "/api/auth/me")
            result = new OturumSonucu { Basarili = true, Kullanici = new OturumKullaniciDto(
                "fixture-user", "fixture", null, "Fixture", null, role switch
                {
                    "GenelSistemAdmin" => KullaniciTipiDegerleri.GenelSistemAdmin,
                    "SirketAdmin" => KullaniciTipiDegerleri.SirketAdmin,
                    "YetkiliServis" => KullaniciTipiDegerleri.YetkiliServis,
                    "SertifikaliFirma" => KullaniciTipiDegerleri.SertifikaliFirma,
                    _ => KullaniciTipiDegerleri.Personel
                }, null, 7, [role]) };
        else if (path == "/api/panel-kapsam/sirketler")
            result = new[] { new { Id = 7, SirketAdi = "Fixture company" }, new { Id = 8, SirketAdi = "Second company" } };
        else if (path.StartsWith("/api/ykc/talepler/rapor", StringComparison.Ordinal))
        {
            LastReportFilter = await request.Content!.ReadFromJsonAsync<YkcTalepListeFiltre>(cancellationToken);
            if (path.EndsWith("/pdf", StringComparison.Ordinal) || path.EndsWith("/excel", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
            result = ReportResult;
        }
        else if (path == "/api/ykc/cihaz-karsilastir")
        {
            ComparisonCalls++;
            if (request.Headers.Authorization?.Parameter != "fixture-token")
                throw new InvalidOperationException("Comparison must use the authenticated API token");
            if (ComparisonUnavailable) throw new HttpRequestException("Fixture comparison unavailable");
            LastComparison = await request.Content!.ReadFromJsonAsync<YkcCihazKarsilastirmaIstek>(cancellationToken);
            result = new YkcCihazKarsilastirmaSonuc { Basarili = true, Uyarilar = ["Marka proje kaydıyla farklı."] };
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
