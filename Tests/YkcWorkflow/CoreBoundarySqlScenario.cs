using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class CoreBoundarySqlScenario
{
    public static async Task RunAsync()
    {
        // Never read application configuration or connect to an application database.
        var databaseName = "CoreBoundarySqlTest_" + Guid.NewGuid().ToString("N");
        var failures = new SaveFailureInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(
                $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(failures).Options;
        var temporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), databaseName));
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
            var company = new Dag_Sirket { SirketAdi = "Core boundary fixture" };
            db.Add(company);
            db.Roles.Add(new IdentityRole("YetkiliServis") { NormalizedName = "YETKILISERVIS" });
            await db.SaveChangesAsync();

            await RegistrationAsync(db, failures, company, Check);
            await CertificateDatesAsync(db, company, Check);
            await CertificateFilesAsync(db, failures, company, temporaryRoot, Check);
            Console.WriteLine($"{passed} core boundary SQL checks passed. Application records were not used.");
        }
        finally
        {
            try
            {
                if (created && db.Database.GetDbConnection().Database == databaseName
                    && databaseName.StartsWith("CoreBoundarySqlTest_", StringComparison.Ordinal))
                    await db.Database.EnsureDeletedAsync();
            }
            finally
            {
                var expectedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), databaseName));
                if (temporaryRoot == expectedRoot && Path.GetFileName(temporaryRoot) == databaseName
                    && databaseName.StartsWith("CoreBoundarySqlTest_", StringComparison.Ordinal)
                    && Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static async Task RegistrationAsync(AppDbContext db, SaveFailureInterceptor failures,
        Dag_Sirket company, Action<bool, string> check)
    {
        var brand = new Ys_Marka { MarkaAdi = "Active brand" };
        var inactiveBrand = new Ys_Marka { MarkaAdi = "Inactive brand", AktifMi = false };
        var deletedBrand = new Ys_Marka { MarkaAdi = "Deleted brand", SilindiMi = true };
        var category = new UrunKategori { Ad = "Active category" };
        var inactiveCategory = new UrunKategori { Ad = "Inactive category", AktifMi = false };
        var deletedCategory = new UrunKategori { Ad = "Deleted category", SilindiMi = true };
        db.AddRange(brand, inactiveBrand, deletedBrand, category, inactiveCategory, deletedCategory);
        await db.SaveChangesAsync();
        using var users = new RegistrationUserManager(db);
        var service = new YetkiliServisService(db, users);
        var sequence = 1000000000;
        Ys_Firma Applicant() => new()
        {
            FirmaAdi = "Applicant", VergiNo = (++sequence).ToString(), SirketId = company.Id,
            Email = $"fixture-{sequence}@example.invalid", Telefon = " 05550000000 ", FaaliyetIli = "Test city"
        };
        const string password = "FixtureOnly123!";

        foreach (var invalidId in new[] { 0, int.MaxValue, inactiveBrand.Id, deletedBrand.Id })
        {
            var firm = Applicant();
            var result = await service.Kayit(firm, password, [brand.Id, invalidId], [category.Id]);
            check(!result.basarili && !await db.Ys_Firmalar.AnyAsync(x => x.VergiNo == firm.VergiNo)
                && !await db.Users.AnyAsync(x => x.UserName == firm.VergiNo),
                $"Invalid/inactive/deleted brand {invalidId} is rejected before registration");
        }
        foreach (var invalidId in new[] { 0, int.MaxValue, inactiveCategory.Id, deletedCategory.Id })
        {
            var firm = Applicant();
            var result = await service.Kayit(firm, password, [brand.Id], [category.Id, invalidId]);
            check(!result.basarili && !await db.Ys_Firmalar.AnyAsync(x => x.VergiNo == firm.VergiNo)
                && !await db.Users.AnyAsync(x => x.UserName == firm.VergiNo),
                $"Invalid/inactive/deleted category {invalidId} is rejected before registration");
        }

        var validFirm = Applicant();
        check((await service.Kayit(validFirm, password, [brand.Id, brand.Id], [category.Id, category.Id], " District ")).basarili,
            "Valid registration accepts and deduplicates selected brands and categories");
        var registeredUser = await db.Users.SingleAsync(x => x.FirmaId == validFirm.Id);
        var brandGrant = await db.Ys_FirmaMarkalar.SingleAsync(x => x.FirmaId == validFirm.Id);
        var categoryGrant = await db.Ys_FirmaKategoriler.SingleAsync(x => x.FirmaId == validFirm.Id);
        check(await users.IsInRoleAsync(registeredUser, "YetkiliServis")
            && registeredUser.SirketId == company.Id && registeredUser.PhoneNumber == "05550000000"
            && await db.Ys_Subeler.AnyAsync(x => x.FirmaId == validFirm.Id && x.Ilce == "District")
            && validFirm.OlusturmaTipi == YetkiliServisOlusturmaTipleri.Kayit,
            "Registration commits the account, role, company, provenance and optional branch together");
        check(brandGrant.YetkiBitisTarihi == brandGrant.OlusturmaTarihi.AddYears(1)
            && categoryGrant.YetkiBitisTarihi == categoryGrant.OlusturmaTarihi.AddYears(1),
            "Brand and category grants retain their exact one-year term");
        var duplicateFirm = Applicant();
        duplicateFirm.VergiNo = validFirm.VergiNo;
        check(!(await service.Kayit(duplicateFirm, password, [brand.Id], [category.Id])).basarili
            && await db.Ys_Firmalar.CountAsync(x => x.VergiNo == validFirm.VergiNo) == 1,
            "Duplicate tax registration leaves the existing firm unchanged");
        check((await service.Kayit(Applicant(), password, [], [])).basarili,
            "Previously optional brand/category selections remain optional");

        var firmCount = await db.Ys_Firmalar.CountAsync();
        var userCount = await db.Users.CountAsync();
        var roleCount = await db.UserRoles.CountAsync();
        var brandCount = await db.Ys_FirmaMarkalar.CountAsync();
        var categoryCount = await db.Ys_FirmaKategoriler.CountAsync();
        var branchCount = await db.Ys_Subeler.CountAsync();
        foreach (var failure in new[] { "password", "role-result", "role-exception", "relationships" })
        {
            var firm = Applicant();
            users.RoleFailure = failure;
            failures.Registration = failure == "relationships";
            try
            {
                var result = await service.Kayit(firm, failure == "password" ? "x" : password,
                    [brand.Id], [category.Id], "District");
                check(failure is "password" or "role-result" && !result.basarili
                    && !string.IsNullOrWhiteSpace(result.mesaj), $"{failure} returns Identity failure details");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Fixture role failure" && failure == "role-exception")
            {
                check(true, "Role exception propagates after rollback");
            }
            catch (DbUpdateException ex) when (ex.Message == "Fixture relationship failure" && failure == "relationships")
            {
                check(true, "Relationship-save exception propagates after rollback");
            }

            check(db.Database.CurrentTransaction == null && db.Entry(company).State == EntityState.Unchanged
                && !db.ChangeTracker.Entries().Any(x => x.State == EntityState.Added),
                $"{failure} releases its transaction and pending writes without clearing unrelated tracked entities");
            await db.SaveChangesAsync();
            check(await db.Ys_Firmalar.CountAsync() == firmCount && await db.Users.CountAsync() == userCount
                && await db.UserRoles.CountAsync() == roleCount && await db.Ys_FirmaMarkalar.CountAsync() == brandCount
                && await db.Ys_FirmaKategoriler.CountAsync() == categoryCount && await db.Ys_Subeler.CountAsync() == branchCount
                && !await db.Ys_Firmalar.AnyAsync(x => x.VergiNo == firm.VergiNo),
                $"{failure} rolls back all registration rows and subsequent SaveChanges cannot replay them");
        }
    }

    private static async Task CertificateDatesAsync(AppDbContext db, Dag_Sirket company, Action<bool, string> check)
    {
        var today = DateTime.Today;
        var firm = new Ys_Firma { FirmaAdi = "Date fixture", SirketId = company.Id };
        var deletedFirm = new Ys_Firma { FirmaAdi = "Deleted date fixture", SirketId = company.Id, SilindiMi = true };
        var foreignFirm = new Ys_Firma
        {
            FirmaAdi = "Other company fixture", Sirket = new Dag_Sirket { SirketAdi = "Other company" }
        };
        db.AddRange(firm, deletedFirm, foreignFirm);
        await db.SaveChangesAsync();
        Ys_YetkiBelgesi Certificate(DateTime expiry, int? firmId = null,
            int status = YetkiBelgesiDurumDegerleri.Onaylandi, bool deleted = false) => new()
        {
            FirmaId = firmId ?? firm.Id, YetkiBelgesiBitisTarihi = expiry, Durum = status, SilindiMi = deleted
        };
        db.AddRange(
            Certificate(today.AddTicks(-1)), Certificate(today), Certificate(today.AddHours(23)),
            Certificate(today.AddDays(30)), Certificate(today.AddDays(31).AddTicks(-1)), Certificate(today.AddDays(31)),
            Certificate(today, status: YetkiBelgesiDurumDegerleri.OnaydaBekliyor),
            Certificate(today, status: YetkiBelgesiDurumDegerleri.Reddedildi), Certificate(today, deleted: true),
            Certificate(today, deletedFirm.Id), Certificate(today, foreignFirm.Id));
        await db.SaveChangesAsync();
        var dashboard = new AdminDashboardService(db);
        check(await dashboard.SuresiBitecekSayisiAsync(company.Id) == 4,
            "Expiry window includes all of today and day 30, excludes yesterday/day 31 and retains status/deletion/company scope");
        check(await dashboard.SuresiBitecekSayisiAsync(null) == 5
            && await dashboard.SuresiBitecekSayisiAsync(foreignFirm.SirketId) == 1,
            "Global and selected-company expiry counts retain their existing scope");
        check(await dashboard.OnayBekleyenSayisiAsync(company.Id) == 1,
            "Pending certificates expiring today remain included separately");
        var certificate = Certificate(today);
        certificate.YetkiBelgesiBaslangicTarihi = today.AddHours(23);
        check(YetkiBelgesiService.GecerliMi(certificate, today.AddHours(12)),
            "Canonical validity includes both start and end dates regardless of time component");
        certificate.YetkiBelgesiBaslangicTarihi = today.AddDays(1);
        check(!YetkiBelgesiService.GecerliMi(certificate, today), "Future-start certificates are not yet valid");
        certificate.YetkiBelgesiBaslangicTarihi = null;
        check(YetkiBelgesiService.GecerliMi(certificate, today), "Legacy certificates without a start date remain supported");
        certificate.YetkiBelgesiBitisTarihi = today.AddTicks(-1);
        check(!YetkiBelgesiService.GecerliMi(certificate, today), "Yesterday's certificate is expired");
        certificate.YetkiBelgesiBitisTarihi = today;
        certificate.SilindiMi = true;
        check(!YetkiBelgesiService.GecerliMi(certificate, today), "Deleted certificates are not valid");
        certificate.SilindiMi = false;
        certificate.Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor;
        check(!YetkiBelgesiService.GecerliMi(certificate, today)
            && YetkiBelgesiService.OnaylanabilirMi(certificate, today.AddHours(23)),
            "Pending certificates remain invalid but approvable throughout their expiry date");
    }

    private static async Task CertificateFilesAsync(AppDbContext db, SaveFailureInterceptor failures,
        Dag_Sirket company, string temporaryRoot, Action<bool, string> check)
    {
        var firm = new Ys_Firma { FirmaAdi = "Upload fixture", SirketId = company.Id };
        db.Add(firm);
        await db.SaveChangesAsync();
        var environment = new FixtureEnvironment(Path.Combine(temporaryRoot, "app"));
        var storageRoot = Path.Combine(temporaryRoot, "documents");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DocumentStorage:RootPath"] = storageRoot
        }).Build();
        var service = new YetkiBelgesiService(db, environment, configuration);
        var baseline = await db.Ys_YetkiBelgeleri.CountAsync();
        foreach (var error in new Exception[] { new IOException("Fixture partial copy"), new OperationCanceledException("Fixture cancelled copy") })
        {
            var file = new PartialCopyFile(error);
            try
            {
                await service.Yukle(firm.Id, file, DateTime.Today.AddYears(1), DateTime.Today, "fixture");
                throw new InvalidOperationException("Expected a copy failure.");
            }
            catch (Exception ex) when (ReferenceEquals(ex, error))
            {
                check(file.BytesCopied > 0, "Copy failure was injected after bytes reached the private file");
            }
            check(!Directory.EnumerateFiles(storageRoot, "*", SearchOption.AllDirectories).Any()
                && await db.Ys_YetkiBelgeleri.CountAsync() == baseline,
                $"{error.GetType().Name} removes the partial private file without recording metadata");
        }

        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nfixture");
        using var input = new MemoryStream(bytes);
        var validFile = new FormFile(input, 0, bytes.Length, "file", "fixture.pdf");
        failures.Certificate = true;
        try
        {
            await service.Yukle(firm.Id, validFile, DateTime.Today.AddYears(1), DateTime.Today, "fixture");
            throw new InvalidOperationException("Expected a metadata failure.");
        }
        catch (DbUpdateException ex) when (ex.Message == "Fixture metadata failure")
        {
            check(!Directory.EnumerateFiles(storageRoot, "*", SearchOption.AllDirectories).Any(),
                "Metadata-save failure removes the fully copied private file");
        }
        await db.SaveChangesAsync();
        check(await db.Ys_YetkiBelgeleri.CountAsync() == baseline
            && !db.ChangeTracker.Entries<Ys_YetkiBelgesi>().Any(x => x.State == EntityState.Added),
            "Failed metadata cannot be persisted by a later SaveChanges");
        check((await service.Yukle(firm.Id, validFile, DateTime.Today.AddYears(1).AddHours(15),
            DateTime.Today.AddHours(12), "fixture")).basarili, "Successful upload still persists after failed attempts");
        var saved = await db.Ys_YetkiBelgeleri.SingleAsync(x => x.FirmaId == firm.Id);
        var downloaded = service.DosyaGetir(saved);
        check(saved.YetkiBelgesiBaslangicTarihi == DateTime.Today
            && saved.YetkiBelgesiBitisTarihi == DateTime.Today.AddYears(1)
            && saved.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
            && saved.DosyaYolu!.StartsWith("private:yetki-belgeleri/", StringComparison.Ordinal)
            && downloaded != null && (await File.ReadAllBytesAsync(downloaded.FizikselYol)).SequenceEqual(bytes),
            "Successful upload preserves canonical dates, pending state and readable private storage");
    }

    private sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        public bool Registration { get; set; }
        public bool Certificate { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Registration && eventData.Context!.ChangeTracker.Entries<Ys_FirmaMarka>().Any(x => x.State == EntityState.Added))
            {
                Registration = false;
                throw new DbUpdateException("Fixture relationship failure");
            }
            if (Certificate && eventData.Context!.ChangeTracker.Entries<Ys_YetkiBelgesi>().Any(x => x.State == EntityState.Added))
            {
                Certificate = false;
                throw new DbUpdateException("Fixture metadata failure");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RegistrationUserManager(AppDbContext db) : UserManager<AppKullanici>(
        new UserStore<AppKullanici>(db), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<AppKullanici>(),
        [new UserValidator<AppKullanici>()], [new PasswordValidator<AppKullanici>()], new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(), null!, NullLogger<UserManager<AppKullanici>>.Instance)
    {
        public string? RoleFailure { get; set; }

        public override async Task<IdentityResult> AddToRoleAsync(AppKullanici user, string role)
        {
            var result = await base.AddToRoleAsync(user, role);
            if (!result.Succeeded) return result;
            // Fail after the actual role row was saved, to exercise database rollback too.
            if (RoleFailure == "role-result")
                return IdentityResult.Failed(new IdentityError { Description = "Fixture role rejection" });
            if (RoleFailure == "role-exception")
                throw new InvalidOperationException("Fixture role failure");
            return result;
        }
    }

    private sealed class PartialCopyFile(Exception error) : IFormFile
    {
        private readonly byte[] _bytes = Encoding.ASCII.GetBytes("%PDF-1.7\npartial fixture");
        public int BytesCopied { get; private set; }
        public string ContentType => "application/pdf";
        public string ContentDisposition => "form-data";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => _bytes.Length;
        public string Name => "file";
        public string FileName => "partial.pdf";
        public Stream OpenReadStream() => new MemoryStream(_bytes);
        public void CopyTo(Stream target) => throw new NotSupportedException();
        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            await target.WriteAsync(_bytes.AsMemory(0, 8), cancellationToken);
            BytesCopied = 8;
            throw error;
        }
    }

    private sealed class FixtureEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CoreBoundarySqlScenario";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(contentRoot, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
