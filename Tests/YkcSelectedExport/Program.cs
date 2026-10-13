using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

var databaseName = "YkcSelectedExportTest_" + Guid.NewGuid().ToString("N");
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer($@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true")
    .Options);
var created = false;
var passed = 0;
void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

try
{
    created = await db.Database.EnsureCreatedAsync();
    if (!created) throw new InvalidOperationException("The isolated database already exists.");
    var company = new Dag_Sirket { SirketAdi = "Export limit fixture" };
    var firm = new Ys_Firma { FirmaAdi = "Export limit firm", Sirket = company };
    var user = new AppKullanici { UserName = "export-limit-admin", KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin };
    var role = new IdentityRole("GenelSistemAdmin") { NormalizedName = "GENELSISTEMADMIN" };
    db.AddRange(company, firm, user, role);
    await db.SaveChangesAsync();
    db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
    var records = Enumerable.Range(1, 5001).Select(i => new Ykc_Talep
    {
        FirmaId = firm.Id, SirketId = company.Id, MusteriAdi = "Customer " + i,
        TesisatNo = i <= 5000 ? "MATCH-" + i : "OTHER-5001",
        TalepTarihi = new DateTime(2026, 10, 1).AddSeconds(i), Durum = YkcDurumDegerleri.TalepAlindi
    }).ToArray();
    db.Ykc_Talepler.AddRange(records);
    await db.SaveChangesAsync();

    using var manager = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db),
        Options.Create(new IdentityOptions()), new PasswordHasher<AppKullanici>(), [], [],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
        NullLogger<UserManager<AppKullanici>>.Instance);
    var principal = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, role.Name!)], "Fixture"));
    var api = new YkcApiController(null!, new YkcTalepOkumaService(db), manager, null!, db, null!,
        new YkcYetkiService(db, manager), null!, null!, null!)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
    };
    using var transport = new ExportTransport(api, user);
    using var client = new HttpClient(transport) { BaseAddress = new Uri("https://fixture.invalid/") };
    var http = new DefaultHttpContext { User = principal, Session = new MemorySession() };
    http.Session.SetString("API.UserId", user.Id);
    http.Session.SetString("API.AccessToken", "fixture-token");
    http.Session.SetString("API.Expires", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
    var accessor = new HttpContextAccessor { HttpContext = http };
    var session = new ApiKullaniciOturumu(new AuthApiClient(client), accessor);
    var options = Options.Create(new ApiIntegrationOptions { Enabled = true });
    var tokens = new ApiJwtTokenService(accessor);
    var companies = new PanelKapsamApiClient(client, options, tokens, NullLogger<PanelKapsamApiClient>.Instance);
    var apiClient = new YkcApiClient(client, options, tokens, NullLogger<YkcApiClient>.Instance,
        new AktifSirketService(accessor, session, companies));
    var mvc = new YkcController(session, apiClient, NullLogger<YkcController>.Instance, null!)
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoryTempData())
    };
    mvc.ViewBag.YkcYetkileri = new YkcYetkiOzeti { RaporlariGorebilir = true };

    foreach (var excel in new[] { false, true })
    foreach (var scenario in new[] { "oversized", "single", "limit", "duplicates", "filtered" })
    {
        var count = scenario == "single" ? 1 : scenario is "oversized" or "filtered" ? 5001 : 5000;
        var ids = records.Take(count).Select(x => x.Id).ToList();
        var posted = scenario == "duplicates" ? ids.Concat([ids[0], ids[^1], 0, -1]).ToList() : ids;
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        using var content = new FormUrlEncodedContent(posted.Select(id => new KeyValuePair<string, string>("ids", id.ToString())));
        http.Request.Body = new MemoryStream(await content.ReadAsByteArrayAsync());
        http.Features.Set<Microsoft.AspNetCore.Http.Features.IFormFeature>(new Microsoft.AspNetCore.Http.Features.FormFeature(
            http.Request, new Microsoft.AspNetCore.Http.Features.FormOptions { ValueCountLimit = 6000 }));
        var form = await http.Request.ReadFormAsync();
        var binding = DefaultModelBindingContext.CreateBindingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new FormValueProvider(BindingSource.Form, form, System.Globalization.CultureInfo.InvariantCulture),
            new EmptyModelMetadataProvider().GetMetadataForType(typeof(List<int>)), bindingInfo: null, modelName: "ids");
        await new CollectionModelBinder<int>(new SimpleTypeModelBinder(typeof(int), NullLoggerFactory.Instance),
            NullLoggerFactory.Instance).BindModelAsync(binding);
        var boundIds = (List<int>)binding.Result.Model!;
        Check(boundIds.SequenceEqual(posted) && binding.ModelState.ErrorCount == 0, $"{scenario}, Excel={excel}: form binds every posted ID");
        mvc.TempData.Clear();
        var before = transport.ExportCalls;
        var installation = scenario == "filtered" ? "MATCH-" : null;
        var result = excel
            ? await mvc.RaporExcel(installation, null, null, null, null, null, null, null, null, null, null,
                boundIds, sirketId: company.Id, sayfa: 3, kaynak: "talepler")
            : await mvc.RaporPdf(installation, null, null, null, null, null, null, null, null, null, null,
                boundIds, sirketId: company.Id, sayfa: 3, kaynak: "talepler");
        Check(transport.ExportCalls == before + 1 && transport.LastFilter!.KayitIdleri!.SequenceEqual(ids),
            $"{scenario}, Excel={excel}: MVC and real API client preserve every distinct positive ID in one request");
        if (scenario == "oversized")
        {
            Check(transport.LastResult is BadRequestObjectResult bad && bad.StatusCode == 400
                && JsonSerializer.SerializeToElement(bad.Value).GetProperty("mesaj").GetString()!.Contains("5000"),
                $"Excel={excel}: real API/Core/SQL rejects 5001 matching selected rows with the explicit 5000-row error");
            Check(result is RedirectToActionResult redirect && redirect.ActionName == nameof(YkcController.Talepler)
                && (int)redirect.RouteValues!["sayfa"]! == 3 && (int)redirect.RouteValues["sirketId"]! == company.Id
                && mvc.TempData["Hata"] is string message && message.Contains("5000"),
                $"Excel={excel}: MVC surfaces the API limit message and preserves list return context instead of downloading a truncated file");
        }
        else
        {
            Check(result is FileContentResult file && file.FileContents.Length > 0 && transport.LastResult is FileContentResult,
                $"{scenario}, Excel={excel}: the real selected export succeeds within the row limit");
            if (excel)
            {
                using var archive = new ZipArchive(new MemoryStream(((FileContentResult)result).FileContents));
                using var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var rows = XDocument.Load(sheet).Descendants(ns + "row").Skip(1).ToList();
                Check(rows.Count == (scenario == "single" ? 1 : 5000),
                    scenario + ": generated Excel contains every matching selected row without counting duplicates or filtered-out rows");
            }
            else
            {
                Check(Encoding.ASCII.GetString(((FileContentResult)result).FileContents.AsSpan(0, 5)) == "%PDF-",
                    scenario + ": normal selected export produces a real PDF");
            }
        }
    }
    Console.WriteLine($"{passed} full-path YKC selected-export checks passed. Application records were not used.");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
finally
{
    if (created)
    {
        await db.Database.CloseConnectionAsync();
        SqlConnection.ClearAllPools();
        await db.Database.EnsureDeletedAsync();
    }
}

// Only HTTP hosting is replaced: export requests invoke the real API controller and SQL reader.
sealed class ExportTransport(YkcApiController api, AppKullanici user) : HttpMessageHandler
{
    public int ExportCalls { get; private set; }
    public YkcTalepListeFiltre? LastFilter { get; private set; }
    public IActionResult? LastResult { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization?.Parameter != "fixture-token")
            throw new InvalidOperationException("Missing authenticated API transport.");
        if (request.RequestUri!.AbsolutePath == "/api/auth/me")
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new OturumSonucu
                {
                    Basarili = true, Kullanici = new OturumKullaniciDto(user.Id, user.UserName, user.Email,
                        user.AdSoyad, user.PhoneNumber, user.KullaniciTipi, user.FirmaId, user.SirketId, ["GenelSistemAdmin"])
                })
            };
        if (request.Method != HttpMethod.Post || request.RequestUri.AbsolutePath is not
            ("/api/ykc/talepler/rapor/pdf" or "/api/ykc/talepler/rapor/excel"))
            throw new InvalidOperationException("Unexpected API request: " + request.RequestUri);
        ExportCalls++;
        LastFilter = await request.Content!.ReadFromJsonAsync<YkcTalepListeFiltre>(cancellationToken);
        LastResult = request.RequestUri.AbsolutePath.EndsWith("/excel", StringComparison.Ordinal)
            ? await api.TaleplerRaporExcel(LastFilter) : await api.TaleplerRaporPdf(LastFilter);
        if (LastResult is FileContentResult file)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(file.FileContents) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = file.FileDownloadName };
            return response;
        }
        if (LastResult is ObjectResult error)
            return new HttpResponseMessage((HttpStatusCode)(error.StatusCode ?? 200)) { Content = JsonContent.Create(error.Value) };
        throw new InvalidOperationException("Unexpected API result: " + LastResult.GetType().Name);
    }
}

sealed class MemorySession : ISession
{
    private readonly Dictionary<string, byte[]> values = [];
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => values.Keys;
    public void Clear() => values.Clear();
    public void Remove(string key) => values.Remove(key);
    public void Set(string key, byte[] value) => values[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value)
        => values.TryGetValue(key, out value);
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
