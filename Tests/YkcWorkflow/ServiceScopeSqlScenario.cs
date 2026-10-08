using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class ServiceScopeSqlScenario
{
    public static async Task RunAsync()
    {
        // The application connection string is never read by this regression fixture.
        var databaseName = "ServiceScopeSqlTest_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(
            $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true").Options;
        await using var db = new AppDbContext(options);
        var created = false;
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        try
        {
            created = await db.Database.EnsureCreatedAsync();
            if (!created) throw new InvalidOperationException("The isolated database already exists.");
            var primary = new Dag_Sirket { SirketAdi = "Primary display name", Il = "CityA" };
            var secondary = new Dag_Sirket { SirketAdi = "COMPANY_B", Il = "CityB" };
            var inactive = new Dag_Sirket { SirketAdi = "COMPANY_INACTIVE", Il = "CityInactive", AktifMi = false };
            var deleted = new Dag_Sirket { SirketAdi = "COMPANY_DELETED", Il = "CityDeleted", SilindiMi = true };
            var unconfigured = new Dag_Sirket { SirketAdi = "Unconfigured", Il = "UnknownCity" };
            db.AddRange(primary, secondary, inactive, deleted, unconfigured);
            db.Roles.Add(new IdentityRole("YetkiliServis") { NormalizedName = "YETKILISERVIS" });
            await db.SaveChangesAsync();
            var ownFirm = new Ys_Firma { FirmaAdi = "Own service", VergiNo = "1000000001", SirketId = primary.Id, FaaliyetIli = "CityA" };
            var foreignFirm = new Ys_Firma { FirmaAdi = "Other company service", SirketId = secondary.Id, FaaliyetIli = "CityB" };
            var sameCompanyFirm = new Ys_Firma { FirmaAdi = "Same company different service", SirketId = primary.Id };
            var deletableFirm = new Ys_Firma { FirmaAdi = "Deletable own service", SirketId = primary.Id };
            db.AddRange(ownFirm, foreignFirm, sameCompanyFirm, deletableFirm);
            await db.SaveChangesAsync();
            var admin = new AppKullanici { UserName = "company-admin", KullaniciTipi = KullaniciTipiDegerleri.SirketAdmin, SirketId = primary.Id };
            var general = new AppKullanici { UserName = "system-admin", KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin };
            var legacyGeneral = new AppKullanici { UserName = "legacy-system-admin", KullaniciTipi = KullaniciTipiDegerleri.SirketAdmin };
            var staff = new AppKullanici { UserName = "staff", KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = primary.Id };
            var service = new AppKullanici { UserName = "service", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, FirmaId = ownFirm.Id, SirketId = primary.Id };
            var certified = new AppKullanici { UserName = "certified", KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma, FirmaId = ownFirm.Id, SirketId = primary.Id };
            db.Users.AddRange(admin, general, legacyGeneral, staff, service, certified);
            await db.SaveChangesAsync();
            using var users = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db), Options.Create(new IdentityOptions()),
                new PasswordHasher<AppKullanici>(), [new UserValidator<AppKullanici>()], [new PasswordValidator<AppKullanici>()],
                new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<AppKullanici>>.Instance);
            var registration = new YetkiliServisService(db, users);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SehirFirmaKodlari:CityA"] = "COMPANY_A",
                ["SehirFirmaKodlari:CityB"] = "COMPANY_B",
                ["SehirFirmaKodlari:SecondCityB"] = "COMPANY_B",
                ["SehirFirmaKodlari:CityInactive"] = "COMPANY_INACTIVE",
                ["SehirFirmaKodlari:CityDeleted"] = "COMPANY_DELETED",
                ["SehirFirmaKodlari:CityMissing"] = "COMPANY_MISSING"
            }).Build();
            var cities = new SehirFirmaKoduService(configuration, db);
            YetkiliServislerController Controller(AppKullanici? user) => new(
                new YetkiliServisRehberApiService(db), new YetkiliServisBasvuruApiService(db, registration, cities),
                new YetkiliServisKayitYonetimApiService(db, cities))
            {
                ControllerContext = new()
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = user == null ? new ClaimsPrincipal(new ClaimsIdentity()) :
                            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id),
                                new Claim(ClaimTypes.Name, user.UserName!)], "Fixture"))
                    }
                }
            };
            YetkiliServisKaydetDto Update(Ys_Firma firm, string city) => new()
            {
                Id = firm.Id, FirmaAdi = "Updated service", FaaliyetIli = city, AktifMi = true, VergiNo = firm.VergiNo
            };
            var companyApi = Controller(admin);
            Check(await companyApi.Getir(new() { Id = ownFirm.Id }) is OkObjectResult, "Company admin reads its own service");
            Check(await companyApi.Getir(new() { Id = foreignFirm.Id }) is NotFoundObjectResult, "Company admin cannot read another company's private service details");
            Check(await companyApi.Guncelle(Update(foreignFirm, "InjectedCity")) is NotFoundObjectResult, "Cross-company update is rejected before city resolution");
            Check(await companyApi.Sil(new() { Id = foreignFirm.Id }) is NotFoundObjectResult, "Cross-company deletion is rejected");
            Check(await db.Ys_Firmalar.AsNoTracking().AnyAsync(f => f.Id == foreignFirm.Id && f.FirmaAdi == "Other company service" && !f.SilindiMi)
                && !await db.Dag_Sirketler.AnyAsync(c => c.Il == "InjectedCity"), "Rejected writes leave the foreign service and company registry unchanged");
            Check(await companyApi.Guncelle(Update(ownFirm, "CityInactive")) is OkObjectResult, "Company admin can update its own service");
            Check(await db.Ys_Firmalar.AsNoTracking().AnyAsync(f => f.Id == ownFirm.Id && f.SirketId == primary.Id)
                && !await db.Dag_Sirketler.AsNoTracking().AnyAsync(c => c.Id == inactive.Id && c.AktifMi),
                "Changing the city neither transfers company ownership nor reactivates the target company");
            Check(await companyApi.Sil(new() { Id = deletableFirm.Id }) is OkObjectResult, "Company admin retains own-service deletion");
            Check(await Controller(general).Getir(new() { Id = foreignFirm.Id }) is OkObjectResult
                && await Controller(general).Guncelle(Update(foreignFirm, "CityB")) is OkObjectResult,
                "System admin retains cross-company read and update access");
            Check(await Controller(legacyGeneral).Getir(new() { Id = foreignFirm.Id }) is OkObjectResult, "Legacy system-admin classification remains supported");

            foreach (var account in new[] { service, certified })
            {
                Check(await Controller(account).Getir(new() { Id = ownFirm.Id }) is OkObjectResult, "Firm account can read its own details");
                Check(await Controller(account).Getir(new() { Id = sameCompanyFirm.Id }) is NotFoundObjectResult
                    && await Controller(account).Getir(new() { Id = foreignFirm.Id }) is NotFoundObjectResult,
                    "Firm accounts cannot read another firm even inside the same company");
                Check(await Controller(account).Guncelle(Update(ownFirm, "CityA")) is NotFoundObjectResult
                    && await Controller(account).Sil(new() { Id = ownFirm.Id }) is NotFoundObjectResult,
                    "Firm accounts cannot use legacy administrative write actions");
            }
            Check(await Controller(staff).Getir(new() { Id = ownFirm.Id }) is NotFoundObjectResult, "Personnel company membership alone does not grant private service access");
            var grant = new Dag_PersonelYetki { KullaniciId = staff.Id, SirketId = secondary.Id, YetkiTipi = YetkiTipleri.YKC_TALEP_GOR };
            db.Add(grant);
            await db.SaveChangesAsync();
            Check(await Controller(staff).Getir(new() { Id = foreignFirm.Id }) is NotFoundObjectResult, "An unrelated module grant does not authorize service details");
            grant.YetkiTipi = YetkiTipleri.KULLANICI_YONET;
            await db.SaveChangesAsync();
            Check(await Controller(staff).Getir(new() { Id = foreignFirm.Id }) is OkObjectResult
                && await Controller(staff).Getir(new() { Id = ownFirm.Id }) is NotFoundObjectResult,
                "Personnel can read service details only in the company of the management grant");
            grant.SilindiMi = true;
            await db.SaveChangesAsync();
            Check(await Controller(staff).Getir(new() { Id = foreignFirm.Id }) is NotFoundObjectResult, "Revoked management grants are ignored");
            grant.SilindiMi = false;
            grant.YetkiTipi = YetkiTipleri.TAM_YETKI;
            await db.SaveChangesAsync();
            Check(await Controller(staff).Getir(new() { Id = foreignFirm.Id }) is OkObjectResult, "Full company permission permits private service reads");
            admin.AktifMi = false;
            await db.SaveChangesAsync();
            Check(await companyApi.Getir(new() { Id = ownFirm.Id }) is UnauthorizedResult
                && await companyApi.Guncelle(Update(ownFirm, "CityA")) is UnauthorizedResult
                && await companyApi.Sil(new() { Id = ownFirm.Id }) is UnauthorizedResult, "Inactive users cannot read or write through legacy actions");
            general.ArsivlemeTarihi = DateTime.Now;
            general.AktifMi = false;
            await db.SaveChangesAsync();
            Check(await Controller(general).Getir(new() { Id = foreignFirm.Id }) is UnauthorizedResult, "An archived system admin cannot read private details");
            Check(await Controller(null).Getir(new() { Id = ownFirm.Id }) is UnauthorizedResult
                && await Controller(null).Guncelle(Update(ownFirm, "CityA")) is UnauthorizedResult
                && await Controller(null).Sil(new() { Id = ownFirm.Id }) is UnauthorizedResult, "Anonymous users cannot invoke private service actions");

            var anonymous = Controller(null);
            YetkiliServisBasvuruDto Application(string city, string tax = "2000000001") => new()
            {
                FirmaAdi = "Public applicant", VergiNo = tax, Email = $"service-{tax}@example.invalid", Sifre = "FixtureOnly123!",
                Telefon = "05550000000", TcKimlikNo = "10000000000", FaaliyetIli = city, MarkaIdleri = [], KategoriIdleri = []
            };
            var companyCount = await db.Dag_Sirketler.CountAsync();
            var firmCount = await db.Ys_Firmalar.CountAsync();
            var userCount = await db.Users.CountAsync();
            foreach (var city in new[] { "CityInactive", "CityDeleted", "CityMissing", "UnknownCity", "ArbitraryCity" })
                Check(await anonymous.Kayit(Application(city)) is BadRequestObjectResult, $"Public registration rejects unavailable or unconfigured city: {city}");
            Check(await db.Dag_Sirketler.CountAsync() == companyCount && await db.Ys_Firmalar.CountAsync() == firmCount && await db.Users.CountAsync() == userCount,
                "Rejected public registrations create no companies, firms or accounts");
            Check(!await db.Dag_Sirketler.AsNoTracking().AnyAsync(c => c.Id == inactive.Id && c.AktifMi)
                && await db.Dag_Sirketler.AsNoTracking().AnyAsync(c => c.Id == deleted.Id && c.SilindiMi),
                "Public registration cannot reactivate or restore companies");
            Check(await cities.AktifSirketIdBulAsync(" CityA ") == primary.Id, "Configured city resolves an existing company with its display name");
            Check(await anonymous.Kayit(Application("CityA", "1000000001")) is BadRequestObjectResult
                && await db.Dag_Sirketler.CountAsync() == companyCount, "A duplicate-VKN rejection does not mutate the company registry");
            Check(await anonymous.Kayit(Application("CityA")) is OkObjectResult
                && await db.Ys_Firmalar.AnyAsync(f => f.VergiNo == "2000000001" && f.SirketId == primary.Id),
                "A valid public application still creates a service under the existing active company");
            var beforeFormattedRetry = await db.Ys_Firmalar.CountAsync();
            foreach (var formatted in new[] { "200-000-0001", "200.000.0001", " 2000000001 " })
            {
                var duplicate = Application("CityA", formatted);
                duplicate.Email = "formatted-retry@example.invalid";
                Check(await anonymous.Kayit(duplicate) is BadRequestObjectResult
                    && await db.Ys_Firmalar.CountAsync() == beforeFormattedRetry,
                    "Formatted tax number cannot bypass duplicate registration: " + formatted);
            }
            var formattedApplication = Application("CityA", "200-000-0004");
            Check(await anonymous.Kayit(formattedApplication) is OkObjectResult
                && await db.Ys_Firmalar.AnyAsync(x => x.VergiNo == "2000000004")
                && await db.Users.AnyAsync(x => x.UserName == "2000000004"),
                "New formatted tax number uses the same canonical firm and login identity");
            Check(await anonymous.Kayit(Application("SecondCityB", "2000000002")) is OkObjectResult
                && await db.Ys_Firmalar.AnyAsync(f => f.VergiNo == "2000000002" && f.SirketId == secondary.Id)
                && await db.Dag_Sirketler.CountAsync() == companyCount,
                "Multiple configured cities share their existing distribution company without creating another company");
            db.Dag_Sirketler.Add(new Dag_Sirket { SirketAdi = "Duplicate primary", Il = "CityA" });
            await db.SaveChangesAsync();
            Check(await anonymous.Kayit(Application("CityA", "2000000003")) is BadRequestObjectResult
                && !await db.Ys_Firmalar.AnyAsync(f => f.VergiNo == "2000000003"), "Ambiguous company matches fail closed instead of choosing the first company");
            db.Roles.Add(new IdentityRole("GenelSistemAdmin") { NormalizedName = "GENELSISTEMADMIN" });
            await db.SaveChangesAsync();
            using var roleUsers = new RoleProbeUsers(db);
            var logs = new RoleProbeLogger();
            var accountService = new AdminKullaniciYonetimApiService(db, roleUsers, logs);
            foreach (var mode in new[] { "missing", "failure", "success" })
            {
                if (mode == "failure")
                {
                    db.Roles.Add(new IdentityRole("SuperAdmin") { NormalizedName = "SUPERADMIN" });
                    await db.SaveChangesAsync();
                }
                roleUsers.FailLegacyRole = mode == "failure";
                var warningCount = logs.Warnings;
                var result = await accountService.KullaniciEkleAsync(new()
                {
                    Rol = "GenelSistemAdmin", AdSoyad = "Admin fixture", Email = mode + "@example.invalid",
                    Telefon = "05550000000", Sifre = "FixtureOnly123!"
                }, general, null, true);
                var savedUser = (await roleUsers.FindByEmailAsync(mode + "@example.invalid"))!;
                Check(result.Sonuc is { Basarili: true } && await roleUsers.IsInRoleAsync(savedUser, "GenelSistemAdmin")
                    && (mode == "success" ? await roleUsers.IsInRoleAsync(savedUser, "SuperAdmin")
                        : logs.Warnings == warningCount + 1),
                    "Primary admin role remains usable and optional legacy role outcome is observed: " + mode);
            }
            Console.WriteLine($"{passed} service scope and public registration checks passed.");
        }
        finally
        {
            if (created && db.Database.GetDbConnection().Database == databaseName
                && databaseName.StartsWith("ServiceScopeSqlTest_", StringComparison.Ordinal))
                await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class RoleProbeUsers(AppDbContext db) : UserManager<AppKullanici>(
        new UserStore<AppKullanici>(db), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<AppKullanici>(),
        [new UserValidator<AppKullanici>()], [new PasswordValidator<AppKullanici>()], new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(), null!, NullLogger<UserManager<AppKullanici>>.Instance)
    {
        public bool FailLegacyRole { get; set; }
        public override Task<IdentityResult> AddToRoleAsync(AppKullanici user, string role)
            => FailLegacyRole && role == "SuperAdmin"
                ? Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "FixtureLegacyRoleFailure" }))
                : base.AddToRoleAsync(user, role);
    }

    private sealed class RoleProbeLogger : ILogger<AdminPanelApiController>
    {
        public int Warnings { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings++;
        }
    }
}
