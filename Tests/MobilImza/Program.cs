using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
byte[] Pdf(string text) => Document.Create(d => d.Page(p => p.Content().Text(text))).GeneratePdf();
var sourcePdf = Pdf("LOCAL INTEGRATION FIXTURE - UNSIGNED");
var signedPdf = Pdf("LOCAL INTEGRATION FIXTURE - NOT A REAL SIGNATURE");
var sourceHash = Convert.ToHexString(SHA256.HashData(sourcePdf));
MobilImzaBildirimi Notification() => new()
{
    BelgeVersiyonu = 1, KaynakBelgeHash = sourceHash, DosyaAdi = "test-signed.pdf", PdfBase64 = Convert.ToBase64String(signedPdf),
    Imzalar = Enumerable.Range(1, 3).Select(x => new MobilImzaTamamlananImzaci(x, DateTimeOffset.UtcNow.AddSeconds(-1))).ToList()
};
var options = new MobilImzaOptions { Enabled = true, SablonSurumu = YkcFr265PdfService.TasarimSurumu,
    ImzaAlanlari = Enumerable.Range(1,3).Select(x => new MobilImzaAlani(x, 2, 20 + x * 100, 50, 90, 20, 1)).ToList() };
Check(options.SablonGecerli(1), "complete, bounded fixture coordinates accepted");
Check(!options.SablonGecerli(2), "unconfigured control coordinates denied");
options.ImzaAlanlari[0] = options.ImzaAlanlari[0] with { X = double.NaN };
Check(!options.SablonGecerli(1), "nonfinite coordinates denied");
options.ImzaAlanlari[0] = options.ImzaAlanlari[0] with { X = 120 };
Check(MobilImzaBildirimKontrolu.Dogrula(Notification()).Hata == null, "bounded PDF notification accepted");
var invalid = Notification(); invalid.PdfBase64 = "bad-base64";
Check(MobilImzaBildirimKontrolu.Dogrula(invalid).Hata != null, "invalid Base64 denied");
invalid = Notification(); invalid.DosyaAdi = "../outside.pdf";
Check(MobilImzaBildirimKontrolu.Dogrula(invalid).Hata != null, "path traversal denied");
invalid = Notification(); invalid.DosyaUrl = "http://127.0.0.1/private";
Check(MobilImzaBildirimKontrolu.Dogrula(invalid).Hata != null, "non-HTTPS callback URL denied and never fetched");
invalid = Notification(); invalid.Imzalar.Add(invalid.Imzalar[0]);
Check(MobilImzaBildirimKontrolu.Dogrula(invalid).Hata != null, "duplicate signer denied");
invalid = Notification(); invalid.Imzalar.Add(null!);
Check(MobilImzaBildirimKontrolu.Dogrula(invalid).Hata != null, "null signer denied without an exception");
invalid = Notification(); invalid.PdfBase64 = ""; invalid.DosyaUrl = "https://example.invalid/test.pdf";
Check(MobilImzaBildirimKontrolu.Dogrula(invalid).Hata != null, "URL-only callback cannot mark a document signed");

var root = Path.Combine(Path.GetTempPath(), "ykc-imza-test-" + Guid.NewGuid().ToString("N"));
var env = new TestEnvironment { ContentRootPath = root };
var key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
options.Istemciler = [new MobilImzaIstemci { Ad = "fixture-client", ApiKeySha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))), SirketIdleri = [1] }];
var services = new ServiceCollection().AddLogging().AddSingleton<IWebHostEnvironment>(env).Configure<MobilImzaOptions>(o =>
    { o.Enabled = options.Enabled; o.Istemciler = options.Istemciler; });
services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, MobilImzaAuthenticationHandler>(MobilImzaAuthenticationHandler.SchemeName, _ => { });
using var provider = services.BuildServiceProvider();
async Task<AuthenticateResult> Authenticate(string? credential)
{
    using var scope = provider.CreateScope();
    var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    context.Connection.RemoteIpAddress = IPAddress.Loopback;
    if (credential != null) context.Request.Headers["X-Imza-Key"] = credential;
    return await context.AuthenticateAsync(MobilImzaAuthenticationHandler.SchemeName);
}
Check(!(await Authenticate(null)).Succeeded, "missing integration credential denied");
Check(!(await Authenticate(new string('x',64))).Succeeded, "incorrect integration credential denied");
var auth = await Authenticate(key);
Check(auth.Succeeded && auth.Principal!.FindFirst(MobilImzaAuthenticationHandler.CompanyClaim)?.Value == "1", "valid key grants only its configured company");
provider.GetRequiredService<IOptions<MobilImzaOptions>>().Value.Enabled = false;
Check(!(await Authenticate(key)).Succeeded, "disabled integration denies even a valid key");
provider.GetRequiredService<IOptions<MobilImzaOptions>>().Value.Enabled = true;
env.EnvironmentName = "Production";
Check(!(await Authenticate(key)).Succeeded, "production integration requires HTTPS");
env.EnvironmentName = "Development";

var mobile = new MobilYkcImzaProvider(Options.Create(options), env);
try
{
    var request = new YkcImzaGonderIstek { TalepId = 1, BelgeVersiyonu = 1, KontrolNo = 1,
        BelgeHash = sourceHash, TekrarsizIstekAnahtari = "fixture-" + Guid.NewGuid().ToString("N"), BelgeBytes = sourcePdf };
    var queued = await mobile.GonderAsync(request);
    Check(queued.Basarili, "configured document queues without claiming a remote signature");
    var repeat = await mobile.GonderAsync(request);
    Check(queued.ProviderDocumentId == repeat.ProviderDocumentId, "queue key is idempotent");
    request.KontrolNo = 5;
    Check(!(await mobile.GonderAsync(request)).Basarili, "unknown signature layout cannot queue");

    if (args.Contains("--local-database"))
    {
        var repository = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder().SetBasePath(Path.Combine(repository, "YetkiliServisGazAcma.API"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json").Build();
        var connection = config.GetConnectionString("DefaultConnection")!;
        var server = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).DataSource.Split('\\')[0];
        if (server != "." && !server.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !server.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) && !server.Equals("(localdb)", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Database fixture is restricted to the local machine.");
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        await using var db = new AppDbContext(dbOptions);
        var company = await db.Dag_Sirketler.Where(x => !x.SilindiMi).Select(x => x.Id).FirstAsync();
        var fixture = new Ykc_Talep { MusteriAdi = "__IMZA_TEST_" + Guid.NewGuid().ToString("N"), SirketId = company,
            Durum = YkcDurumDegerleri.SahaIsleminde, Fr265BelgeVersiyonNo = 1, OlusturanKullanici = "IntegrationTest" };
        db.Ykc_Talepler.Add(fixture);
        await db.SaveChangesAsync();
        var fixtureId = fixture.Id;
        try
        {
            var draftPath = Path.Combine(root, "App_Data", "ykc-belgeler", fixtureId.ToString(), "source.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(draftPath)!);
            await File.WriteAllBytesAsync(draftPath, sourcePdf);
            var draft = new Ykc_FormDosya { TalepId = fixtureId, DosyaAdi = "source.pdf", DosyaYolu = $"ykc/{fixtureId}/source.pdf",
                BelgeHash = sourceHash, DosyaTuru = YkcFormDosyaTuruDegerleri.Fr265ImzayaGonderilen,
                DepolamaTuru = YkcDepolamaTuruDegerleri.Private, IcerikTipi = "application/pdf" };
            var process = new Ykc_ImzaSureci { TalepId = fixtureId, BelgeVersiyonu = 1, ProviderDocumentId = queued.ProviderDocumentId,
                BelgeHash = sourceHash, Durum = YkcImzaDurumDegerleri.ImzaBekliyor,
                Imzacilar = Enumerable.Range(1,3).Select(x => new Ykc_Imzaci { SiraNo = x, Rol = "Fixture " + x, Durum = YkcImzaciDurumDegerleri.Bekliyor }).ToList() };
            db.Ykc_FormDosyalari.Add(draft); db.Ykc_ImzaSurecleri.Add(process);
            await db.SaveChangesAsync();
            var documentId = process.Id;
            db.ChangeTracker.Clear();
            var flow = new YkcImzaAkisService(db, null!, null!, mobile, env, NullLogger<YkcImzaAkisService>.Instance);
            Check((await flow.MobilBekleyenlerAsync([company], 0, 100, default)).Any(x => x.BelgeId == documentId), "ready fixture appears in pending list");
            Check(!(await flow.MobilBekleyenlerAsync([int.MaxValue], 0, 100, default)).Any(), "other company cannot list document");
            Check(await flow.MobilPaketAsync([int.MaxValue], documentId, default) == null, "other company cannot read PDF");
            var packet = await flow.MobilPaketAsync([company], documentId, default);
            Check(packet != null && Convert.FromBase64String(packet.PdfBase64).SequenceEqual(sourcePdf) && packet.ImzaAlanlari.Count == 3, "packet contains the exact stored PDF and frozen coordinate list");
            Check((await flow.MobilImzalandiAsync([int.MaxValue], documentId, Notification(), default)).StatusCode == 404, "cross-company callback denied");
            db.ChangeTracker.Clear();
            invalid = Notification(); invalid.KaynakBelgeHash = new string('0',64);
            Check((await flow.MobilImzalandiAsync([company], documentId, invalid, default)).StatusCode == 409, "wrong source hash denied");
            db.ChangeTracker.Clear();
            invalid = Notification(); invalid.Imzalar.RemoveAt(0);
            Check((await flow.MobilImzalandiAsync([company], documentId, invalid, default)).StatusCode == 400, "incomplete signer list denied");
            db.ChangeTracker.Clear();
            await using var concurrentDb = new AppDbContext(dbOptions);
            var concurrentFlow = new YkcImzaAkisService(concurrentDb, null!, null!, mobile, env, NullLogger<YkcImzaAkisService>.Instance);
            var concurrentResults = await Task.WhenAll(flow.MobilImzalandiAsync([company], documentId, Notification(), default),
                concurrentFlow.MobilImzalandiAsync([company], documentId, Notification(), default));
            Check(concurrentResults.All(x => x.StatusCode == 200) && concurrentResults.Count(x => x.Tekrar) == 1, "simultaneous callbacks produce one result and one safe replay");
            db.ChangeTracker.Clear();
            Check(!(await flow.MobilBekleyenlerAsync([company], 0, 100, default)).Any(x => x.BelgeId == documentId), "signed document removed from pending list");
            Check(await flow.MobilPaketAsync([company], documentId, default) == null, "signed document cannot be downloaded for signing again");
            Check((await flow.MobilImzalandiAsync([company], documentId, Notification(), default)).Tekrar, "identical callback replay is safe");
            Check(await db.Ykc_FormDosyalari.CountAsync(x => x.TalepId == fixtureId && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai) == 1, "replay does not create duplicate final files");
            db.ChangeTracker.Clear();
            invalid = Notification(); invalid.PdfBase64 = Convert.ToBase64String(sourcePdf);
            Check((await flow.MobilImzalandiAsync([company], documentId, invalid, default)).StatusCode == 409, "conflicting replay denied");
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (await db.Ykc_Talepler.AnyAsync(x => x.Id == fixtureId && x.MusteriAdi == fixture.MusteriAdi && x.OlusturanKullanici == "IntegrationTest"))
            {
                await db.Ykc_ImzaSurecleri.Where(x => x.TalepId == fixtureId).ExecuteUpdateAsync(s => s.SetProperty(x => x.NihaiDosyaId, (int?)null));
                await db.Ykc_Imzacilar.Where(x => x.ImzaSureci!.TalepId == fixtureId).ExecuteDeleteAsync();
                await db.Ykc_ImzaSurecleri.Where(x => x.TalepId == fixtureId).ExecuteDeleteAsync();
                await db.Ykc_FormDosyalari.Where(x => x.TalepId == fixtureId).ExecuteDeleteAsync();
                await db.Ykc_IslemGecmisi.Where(x => x.TalepId == fixtureId).ExecuteDeleteAsync();
                await db.Ykc_Talepler.Where(x => x.Id == fixtureId).ExecuteDeleteAsync();
                Console.WriteLine("Local fixture removed; existing records were not changed.");
            }
        }
    }
}
finally
{
    var fullRoot = Path.GetFullPath(root);
    if (fullRoot.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && Path.GetFileName(fullRoot).StartsWith("ykc-imza-test-", StringComparison.Ordinal) && Directory.Exists(fullRoot))
        Directory.Delete(fullRoot, recursive:true);
}
Console.WriteLine($"PASS: {passed} mobile signature checks.");

sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "MobilImzaTests";
    public string ContentRootPath { get; set; } = "";
    public string WebRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
