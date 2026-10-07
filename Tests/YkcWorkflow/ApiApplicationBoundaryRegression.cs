using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

public static class ApiApplicationBoundaryRegression
{
    // No provider, application configuration, SQL connection, or database fixture is used.
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        foreach (var result in new[] { ApiIslemSonuc.BasariliSonuc("Saved"), ApiIslemSonuc.Basarisiz("Rejected") })
        {
            var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            var body = System.Text.Json.JsonSerializer.Deserialize<ApiIslemSonuc>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            Check(body?.Basarili == result.Basarili && body.Mesaj == result.Mesaj && body.Id == null,
                "Shared operation result preserves success and failure wire values: " + result.Basarili);
        }
        Check(YkcIslemSonuc.BasariliSonuc("Saved", 42) is { Basarili: true, Id: 42 }
            && YkcIslemSonuc.BasariliSonuc("Saved").GetType() == typeof(YkcIslemSonuc),
            "Shared result helpers preserve the specialized YKC contract and record id");

        var today = new DateTime(2026, 10, 7);
        Ys_YetkiBelgesi Certificate(int id, DateTime? start, DateTime end, int status) => new()
        {
            Id = id, YetkiBelgesiBaslangicTarihi = start, YetkiBelgesiBitisTarihi = end,
            Durum = status, OlusturmaTarihi = today.AddMinutes(id)
        };
        var valid = Certificate(1, today, today, YetkiBelgesiDurumDegerleri.Onaylandi);
        var future = Certificate(2, today.AddDays(1), today.AddDays(30), YetkiBelgesiDurumDegerleri.Onaylandi);
        var firm = new Ys_Firma { Id = 1, YetkiBelgeleri = [valid, future] };
        var profile = YetkiliServisPanelOkumaApiService.ProfilHazirla(firm, today.AddHours(23));
        Check(profile.GecerliYetkiBelgesiVar && profile.GecerliYetkiBelgesiId == valid.Id
            && profile.GosterilenYetkiBelgesiId == valid.Id, "Profile includes the entire expiry day and excludes future starts");
        Check(profile.YetkiBelgeleri.Count == 2, "Profile keeps the existing certificate contract intact");
        valid.SilindiMi = true;
        profile = YetkiliServisPanelOkumaApiService.ProfilHazirla(firm, today);
        Check(!profile.GecerliYetkiBelgesiVar && profile.GecerliYetkiBelgesiId == null,
            "Deleted and future certificates cannot grant current validity");
        var pending = Certificate(3, null, today, YetkiBelgesiDurumDegerleri.OnaydaBekliyor);
        firm.YetkiBelgeleri!.Add(pending);
        profile = YetkiliServisPanelOkumaApiService.ProfilHazirla(firm, today);
        Check(profile.BekleyenYetkiBelgesiVar && profile.GosterilenYetkiBelgesiId == pending.Id
            && profile.YetkiBelgesiDurumu == "Onay Bekliyor", "Unexpired pending certificate remains the display fallback");
        profile = YetkiliServisPanelOkumaApiService.ProfilHazirla(new Ys_Firma
        {
            YetkiBelgeleri = [Certificate(4, null, today.AddDays(-1), YetkiBelgesiDurumDegerleri.OnaydaBekliyor)]
        }, today);
        Check(!profile.BekleyenYetkiBelgesiVar && profile.YetkiBelgesiDurumu == "Süresi Doldu",
            "Expired pending certificate keeps the expired display state");
        profile = YetkiliServisPanelOkumaApiService.ProfilHazirla(new Ys_Firma(), today);
        Check(!profile.GecerliYetkiBelgesiVar && profile.GosterilenYetkiBelgesiId == null,
            "Empty profile does not fabricate a certificate");

        var actor = new AppKullanici { Id = "actor", SirketId = 10, FirmaId = 20, AktifMi = true };
        var admin = new AdminKullaniciYonetimApiService(null!, null!, NullLogger<AdminPanelApiController>.Instance);
        var adminResult = await admin.KullaniciEkleAsync(null, actor, 10, false);
        Check(!adminResult.Yetkisiz && adminResult.Sonuc?.Basarili == false,
            "Missing account data remains a business failure, not a forbidden response");
        adminResult = await admin.KullaniciEkleAsync(new() { Rol = "GenelSistemAdmin" }, actor, 10, false);
        Check(!adminResult.Yetkisiz && adminResult.Sonuc?.Basarili == false,
            "Company administrator cannot create global administrators");
        adminResult = await admin.KullaniciEkleAsync(new()
        {
            Rol = "Personel", SirketId = 11, Sifre = "valid123"
        }, actor, 10, false);
        Check(adminResult.Yetkisiz && adminResult.Sonuc == null,
            "Cross-company account creation remains a forbidden outcome before writes");
        adminResult = await admin.PersonelEkleAsync(new() { SirketId = 11 }, actor, 10, false);
        Check(adminResult.Yetkisiz, "Personnel creation uses the same validated company scope");
        foreach (var (password, expected) in new[]
        {
            ("", "Sifre zorunludur"), ("a1234", "en az 6"), ("ABC123", "kucuk harf"), ("abcdef", "rakam")
        })
        {
            adminResult = await admin.PersonelEkleAsync(new()
            {
                SirketId = 10, AdSoyad = "Password fixture", Email = "password@fixture.test", Telefon = "05551234567", Sifre = password
            }, actor, 10, false);
            Check(!adminResult.Yetkisiz && adminResult.Sonuc is { Basarili: false } rejected
                && rejected.Mesaj!.Contains(expected, StringComparison.Ordinal),
                "API enforces personnel password policy without MVC or persistence: " + expected);
        }
        adminResult = await admin.KullaniciGuncelleAsync(null, actor, 10, false);
        Check(!adminResult.Yetkisiz && adminResult.Sonuc?.Basarili == false,
            "Missing account update data retains its business-error contract");

        var internalQuery = new IcTesisatDevreyeAlmaApiService(null!);
        Check(await internalQuery.ListeleAsync(new(), new AppKullanici(), ["Personel"]) == null,
            "Internal-installation personnel queries reject missing scope without querying data");
        Check(await internalQuery.ListeleAsync(new(), new AppKullanici(), ["SirketAdmin"]) == null,
            "Company administrators without company scope do not get global records");
        Check(await new YetkiBelgesiSilmeApiService(null!).SilAsync(
            Certificate(1, null, today, YetkiBelgesiDurumDegerleri.Onaylandi), "actor") != null,
            "Approved certificate deletion is rejected before its conditional update");

        using var snapshots = new YkcSorguKaydiService();
        var installations = new YkcTesisatApiService(null!, null!, null!, null!, snapshots);
        foreach (var number in new[] { "", "0", "-1", "+1", "1x", "9223372036854775808" })
        {
            var result = await installations.SorgulaAsync(new() { TesisatNo = number, SozlesmeNo = "2" }, actor, default);
            Check(!result.Basarili, "YKC installation number rejected before external I/O: " + number);
        }
        var source = new YkcTalepKaydetDto
        {
            FirmaId = 99, SirketId = 10, TesisatNo = "1", SozlesmeNo = "2",
            EskiMarka = "Private source", IzinliYeniCihazTipleri = new(StringComparer.OrdinalIgnoreCase) { ["Kombi"] = "K" }
        };
        var reference = await snapshots.EkleAsync(actor.Id, source);
        var comparison = new YkcCihazKarsilastirmaIstek
        {
            SorguReferansi = reference, TesisatNo = "1", SozlesmeNo = "2", YeniCihazTipi = "Kombi"
        };
        Check(!(await installations.KarsilastirAsync(comparison, actor, default)).Basarili,
            "Comparison rejects a source reference from another firm");
        Check(!(await installations.KarsilastirAsync(comparison, new AppKullanici { Id = "other" }, default)).Basarili,
            "Comparison rejects a reference belonging to another account");
        Check(source.EskiMarka == "Private source" && source.FirmaId == 99,
            "Rejected advisory comparison does not mutate the source snapshot");

        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        using var users = new ProbeUsers(db, new AppKullanici { Id = "inactive", AktifMi = false });
        var flow = new OturumAkisApiService(users, null!, null!, null!,
            Options.Create(new SertifikaliFirmaKimlikOptions()), Options.Create(new SmsOptions()),
            new EphemeralDataProtectionProvider(), null!, db);
        var auth = new AuthController(users, null!, flow)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        Check(await auth.Token(new("inactive@example.test", "secret")) is ObjectResult { StatusCode: 401 },
            "Inactive login retains HTTP 401");
        Check(await auth.Verify(new("forged-challenge", "123456")) is ObjectResult { StatusCode: 400 },
            "Forged SMS challenge retains HTTP 400");
        Check(await auth.Reset(new("forged-challenge", "123456", "secret123")) is ObjectResult { StatusCode: 400 },
            "Forged reset challenge retains HTTP 400");
        Check(await auth.Profile(new("Name", "name@example.test", "05000000000")) is UnauthorizedResult,
            "Inactive profile edits retain empty HTTP 401");
        var forgot = (OturumSonucu)((OkObjectResult)await auth.Forgot(new("inactive@example.test"))).Value!;
        Check(forgot.Basarili && forgot.Token == null && forgot.Dogrulama?.Length == 128,
            "Inactive forgot-password keeps an unusable opaque challenge and no token");
        var profileFailure = await flow.ProfileAsync(actor, new("Name", "name@example.test", "invalid"));
        Check(profileFailure.Hata == OturumAkisHatasi.GecersizIstek && actor.AdSoyad == null,
            "Invalid profile phone is rejected before changing the account");

        var root = Path.Combine(Path.GetTempPath(), "ApiBoundaryUpload_" + Guid.NewGuid().ToString("N"));
        try
        {
            var uploads = new YkcBelgeYuklemeApiService(new YkcTalepService(db), new TestEnvironment(root));
            var rejected = await uploads.YukleAsync(1, null, null, actor, false, 10);
            Check(!rejected.Basarili && !Directory.Exists(root), "Missing upload cannot create a storage directory");
            rejected = await uploads.KaydetAsync(new()
            {
                TalepId = 1, DosyaAdi = "form.pdf", IcerikTipi = "application/pdf",
                DepolamaTuru = YkcDepolamaTuruDegerleri.Private, DosyaYolu = "ykc/2/other.pdf"
            }, actor, false, 10);
            Check(!rejected.Basarili, "Metadata registration rejects another request's private storage key");
            rejected = await uploads.YukleAsync(1, YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai,
                new ProbeUpload(false), actor, false, 10);
            Check(!rejected.Basarili && !Directory.Exists(root), "Manual upload cannot create a signed final document");
            var copy = new ProbeUpload(true);
            try
            {
                await uploads.YukleAsync(1, null, copy, actor, false, 10);
                throw new Exception("Expected copy failure");
            }
            catch (IOException ex) when (ex.Message == "fixture copy failure") { }
            Check(copy.Copied && !Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any(),
                "Partial upload is removed after CopyToAsync throws");
            var metadata = new ProbeUpload(false);
            try
            {
                await uploads.YukleAsync(1, null, metadata, actor, false, 10);
                throw new Exception("Expected provider-free metadata failure");
            }
            catch (InvalidOperationException) { }
            Check(metadata.Copied && !Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any(),
                "Completed upload is removed when metadata persistence throws without a database provider");
        }
        finally
        {
            if (Directory.Exists(root) && PrivateDocumentStorage.IsInRoot(root, Path.GetTempPath()))
                Directory.Delete(root, true);
        }

        Type[] thinControllers = [typeof(AuthController), typeof(IcTesisatApiController),
            typeof(YetkiliServisPanelApiController), typeof(YetkiliServisDevreyeAlmaApiController)];
        foreach (var controller in thinControllers)
        {
            Check(!controller.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(x => typeof(DbContext).IsAssignableFrom(x.FieldType)), controller.Name + " does not retain a DbContext");
            Check(ActivatorUtilities.CreateFactory(controller, Type.EmptyTypes) != null,
                controller.Name + " has an unambiguous DI constructor");
        }
        Type[] services = [typeof(AdminKullaniciOkumaApiService), typeof(AdminKullaniciYonetimApiService),
            typeof(DevreyeAlmaOkumaApiService), typeof(DevreyeAlmaSorguApiService), typeof(IcTesisatDevreyeAlmaApiService),
            typeof(YetkiliServisPanelOkumaApiService), typeof(YetkiliServisProfilApiService), typeof(YetkiBelgesiOkumaApiService),
            typeof(YetkiBelgesiSilmeApiService), typeof(YkcTesisatApiService), typeof(YkcBelgeYuklemeApiService), typeof(OturumAkisApiService)];
        foreach (var service in services)
            Check(service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .All(x => !typeof(IActionResult).IsAssignableFrom(x.ReturnType)
                    && !x.ReturnType.GenericTypeArguments.Any(t => typeof(IActionResult).IsAssignableFrom(t))),
                service.Name + " returns application data instead of MVC results");
        Console.WriteLine($"API application boundary checks: {passed} passed; no database used.");
    }

    private sealed class ProbeUsers(AppDbContext db, AppKullanici user) : UserManager<AppKullanici>(
        new UserStore<AppKullanici>(db), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<AppKullanici>(),
        [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<AppKullanici>>.Instance)
    {
        public override Task<AppKullanici?> FindByEmailAsync(string email) => Task.FromResult<AppKullanici?>(user);
        public override Task<AppKullanici?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult<AppKullanici?>(user);
    }

    private sealed class ProbeUpload(bool failCopy) : IFormFile
    {
        private static readonly byte[] Bytes = "%PDF-1.4\n%%EOF"u8.ToArray();
        public bool Copied { get; private set; }
        public string ContentType => "application/pdf";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public long Length => Bytes.Length;
        public string Name => "Dosya";
        public string FileName => "fixture.pdf";
        public Stream OpenReadStream() => new MemoryStream(Bytes, false);
        public void CopyTo(Stream target) => throw new NotSupportedException();
        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            await target.WriteAsync(Bytes, cancellationToken);
            Copied = true;
            if (failCopy) throw new IOException("fixture copy failure");
        }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BoundaryRegression";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
