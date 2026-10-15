using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class AdminSecuritySqlScenario
{
    public static async Task RunProfileResetAsync()
    {
        var databaseName = "YsProfileResetTest_" + Guid.NewGuid().ToString("N");
        var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
        var passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }
        try
        {
            await db.Database.EnsureCreatedAsync();
            var protection = new EphemeralDataProtectionProvider();
            var services = new ServiceCollection().AddLogging().AddSingleton(db)
                .AddSingleton<IDataProtectionProvider>(protection);
            services.AddIdentityCore<AppKullanici>(options => options.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
            using var provider = services.BuildServiceProvider();
            var manager = provider.GetRequiredService<UserManager<AppKullanici>>();
            var company = new Dag_Sirket { SirketAdi = "Profile reset fixture" };
            var firm = new Ys_Firma { FirmaAdi = "Profile fixture", Sirket = company, VergiNo = "9000000000" };
            db.Ys_Firmalar.Add(firm);
            await db.SaveChangesAsync();
            var smsOptions = Options.Create(new SmsOptions { Provider = "Fixture", HashSecret = Guid.NewGuid().ToString("N") });
            var sender = new ResetSmsProbe();
            var sms = new SmsDogrulamaService(db, smsOptions, sender,
                new SehirFirmaKoduService(new ConfigurationBuilder().Build(), db));
            var tokenService = new OturumTokenService(manager, new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Jwt:Key"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") }).Build());
            var flow = new OturumAkisApiService(manager, null!, sms, null!,
                Options.Create(new SertifikaliFirmaKimlikOptions()), smsOptions, protection, tokenService, db);
            var profile = new YetkiliServisProfilApiService(db, manager);
            var auth = new AuthController(manager, tokenService, flow)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var sequence = 0;
            async Task<AppKullanici> Account(string kind = "email", string? id = null)
            {
                var number = ++sequence;
                var email = $"profile-{number}@example.test";
                var user = new AppKullanici
                {
                    UserName = kind == "vkn" ? (9000000000L + number).ToString()
                        : kind == "custom" ? $"service-{number}" : email,
                    Email = email, AdSoyad = "Original Name", PhoneNumber = "905551234567",
                    EmailConfirmed = true, PhoneNumberConfirmed = true, AktifMi = true, LockoutEnabled = true,
                    KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, FirmaId = firm.Id, SirketId = company.Id
                };
                if (id != null) user.Id = id;
                var result = await manager.CreateAsync(user, "Original123!");
                if (!result.Succeeded) throw new InvalidOperationException(string.Join(";", result.Errors.Select(x => x.Description)));
                return user;
            }
            foreach (var route in new[] { "auth", "service" })
            foreach (var kind in new[] { "vkn", "email", "custom" })
            {
                var user = await Account(kind);
                var username = user.UserName;
                var email = $"changed-{sequence}@example.test";
                async Task<bool> Update(string name, string address, string phone) => route == "auth"
                    ? (await flow.ProfileAsync(user, new(name, address, phone))).Sonuc.Basarili
                    : (await profile.GuncelleAsync(new() { AdSoyad = name, Email = address, Telefon = phone }, user)).Basarili;
                Check(await Update("Changed Name", " " + email + " ", " 905559876543 "),
                    $"{route}/{kind}: valid profile update succeeds");
                await db.Entry(user).ReloadAsync();
                Check(user.UserName == (kind == "email" ? email : username)
                    && user.NormalizedUserName == manager.NormalizeName(user.UserName)
                    && user.Email == email && user.PhoneNumber == "905559876543",
                    $"{route}/{kind}: persisted username is preserved unless it was email-based");
                Check(!user.EmailConfirmed && !user.PhoneNumberConfirmed,
                    $"{route}/{kind}: changed contacts lose their verification flags");
                Check((await flow.FindAsync(user.UserName!))?.Id == user.Id && (await flow.FindAsync(email))?.Id == user.Id,
                    $"{route}/{kind}: updated email and original login identity still resolve correctly");
                user.EmailConfirmed = user.PhoneNumberConfirmed = true;
                await manager.UpdateAsync(user);
                Check(await Update(user.AdSoyad!, " " + email.ToUpperInvariant() + " ", " " + user.PhoneNumber + " ")
                    && user.EmailConfirmed && user.PhoneNumberConfirmed,
                    $"{route}/{kind}: unchanged contacts keep verification after trimming and case comparison");
                foreach (var invalid in new[] { "", "invalid", "123", "555<script>1234567" })
                {
                    Check(!await Update("Must not change", "invalid-change@example.test", invalid)
                        && user.AdSoyad == "Changed Name" && user.PhoneNumber == "905559876543"
                        && user.EmailConfirmed && user.PhoneNumberConfirmed,
                        $"{route}/{kind}: invalid phone is rejected before any profile mutation ({invalid.Length})");
                }
            }
            var partial = await Account("vkn");
            Check((await profile.GuncelleAsync(new() { AdSoyad = "Partial Name" }, partial)).Basarili
                && partial.UserName!.All(char.IsAsciiDigit) && partial.PhoneNumberConfirmed && partial.EmailConfirmed,
                "Partial service profile preserves omitted contacts and VKN");

            var active = await Account();
            var inactive = await Account();
            inactive.AktifMi = false;
            await manager.UpdateAsync(inactive);
            var archived = await Account();
            archived.AktifMi = false;
            archived.ArsivlemeTarihi = DateTime.Now;
            await manager.UpdateAsync(archived);
            var locked = await Account();
            await manager.SetLockoutEndDateAsync(locked, DateTimeOffset.UtcNow.AddMinutes(15));
            var invalidPhone = await Account();
            invalidPhone.PhoneNumber = "invalid";
            await manager.UpdateAsync(invalidPhone);
            async Task<OturumSonucu> Forgot(string email)
            {
                var response = await auth.Forgot(new(email));
                Check(response is OkObjectResult, "Forgot always returns HTTP 200 for account/SMS outcomes");
                return (OturumSonucu)((OkObjectResult)response).Value!;
            }
            var known = await Forgot(active.Email!);
            var realCode = sender.LastCode;
            var calls = sender.Calls;
            var replies = new List<OturumSonucu>();
            foreach (var address in new[] { "unknown@example.test", inactive.Email!, archived.Email!, locked.Email!, invalidPhone.Email! })
                replies.Add(await Forgot(address));
            Check(sender.Calls == calls, "Unknown, blocked and invalid-phone accounts do not send SMS");
            sender.Fail = true;
            replies.Add(await Forgot(active.Email!));
            sender.Fail = false;
            Check(await db.SmsDogrulamaKodlari.CountAsync() == 1,
                "Decoys and failed deliveries create no usable verification records");
            foreach (var reply in replies)
            {
                Check(reply.Basarili && reply.Mesaj == known.Mesaj && reply.Dogrulama?.Length == known.Dogrulama?.Length
                    && reply.Token == null && reply.Kullanici == null && reply.Bitis == null,
                    "Known/unknown/SMS failure responses share the same message, fields and encrypted-token length");
                Check(reply.Dogrulama != known.Dogrulama && reply.Dogrulama!.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'),
                    "Decoy has the same randomized protected-token encoding as a real challenge");
                var decoyReset = await flow.ResetAsync(new(reply.Dogrulama!, realCode, "Replacement123!"));
                var realReset = await flow.ResetAsync(new(known.Dogrulama!, "000000", "Replacement123!"));
                Check(!decoyReset.Sonuc.Basarili && decoyReset.Hata == realReset.Hata && decoyReset.Sonuc.Mesaj == realReset.Sonuc.Mesaj,
                    "Decoy and real challenge invalid-code failures return the same response");
                var weakDecoy = await flow.ResetAsync(new(reply.Dogrulama!, realCode, "x"));
                var weakReal = await flow.ResetAsync(new(known.Dogrulama!, realCode, "x"));
                Check(!weakDecoy.Sonuc.Basarili && weakDecoy.Sonuc.Mesaj == weakReal.Sonuc.Mesaj,
                    "Password-policy failures have the same response for decoy and real challenges");
            }
            var request = new SifreYenileIstegi(known.Dogrulama!, realCode, "Replacement123!");
            Check(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request,
                new System.ComponentModel.DataAnnotations.ValidationContext(request), [], true)
                && request.Dogrulama.Length <= 4096,
                "Protected challenge fits the existing reset request length contract");
            Check(!(await flow.ResetAsync(request)).Sonuc.Basarili,
                "Repeated wrong-code attempts still exhaust the real challenge");
            var longId = await Account(id: new string('a', 450));
            var longIdReply = await Forgot(longId.Email!);
            Check(longIdReply.Dogrulama!.Length == known.Dogrulama!.Length,
                "Maximum-length ASCII Identity IDs do not alter the protected challenge length");
            known = await Forgot(active.Email!);
            realCode = sender.LastCode;
            var previousStamp = active.SecurityStamp;
            Check(!(await flow.VerifyAsync(new(known.Dogrulama!, realCode), CancellationToken.None)).Sonuc.Basarili,
                "Reset challenge cannot be used as a login challenge");
            Check(!(await flow.ResetAsync(new(known.Dogrulama!, realCode, "x"))).Sonuc.Basarili,
                "Weak password is rejected without consuming a valid reset code");
            Check((await flow.ResetAsync(new(known.Dogrulama!, realCode, "Replacement123!"))).Sonuc.Basarili,
                "Real SMS challenge and code still reset the password");
            await db.Entry(active).ReloadAsync();
            Check(await manager.CheckPasswordAsync(active, "Replacement123!") && !await manager.CheckPasswordAsync(active, "Original123!")
                && active.SecurityStamp != previousStamp,
                "Successful reset persists the new password and revokes the previous security stamp");
            Check(!(await flow.ResetAsync(new(known.Dogrulama!, realCode, "ThirdPassword123!"))).Sonuc.Basarili,
                "Completed reset challenge cannot be replayed");
            smsOptions.Value.TestMode = true;
            var testKnown = await Forgot(active.Email!);
            var testUnknown = await Forgot("unknown@example.test");
            Check(testKnown.Mesaj!.StartsWith("Test SMS modu:", StringComparison.Ordinal)
                && testKnown.Mesaj.Length == testUnknown.Mesaj!.Length && testKnown.Dogrulama!.Length == testUnknown.Dogrulama!.Length,
                "Development test codes retain the same response shape for real and decoy accounts");
            Check((await flow.ResetAsync(new(testKnown.Dogrulama!, testKnown.Mesaj.Split(' ')[^1], "Development123!"))).Sonuc.Basarili
                && !(await flow.ResetAsync(new(testUnknown.Dogrulama!, testUnknown.Mesaj!.Split(' ')[^1], "Development123!"))).Sonuc.Basarili,
                "Development reset test flow works but a decoy test code never authenticates");
            smsOptions.Value.TestMode = false;
            var loginPurpose = "G:LEGACY-PROFILE-FIXTURE";
            await sms.KodGonderAsync(active, loginPurpose);
            var legacyLogin = protection.CreateProtector("API.Auth.Challenge.v1").ToTimeLimitedDataProtector().Protect(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    UserId = active.Id, Purpose = "GIRIS", SmsPurpose = loginPurpose,
                    Stamp = active.SecurityStamp, ExternalReference = (string?)null
                }), TimeSpan.FromMinutes(5));
            await provider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new(KullaniciRolAdlari.YetkiliServis));
            var verified = await flow.VerifyAsync(new(legacyLogin, sender.LastCode), CancellationToken.None);
            Check(verified.Sonuc.Basarili && verified.Sonuc.Token != null,
                "Previously issued login payloads without IsDecoy still authenticate with a valid SMS code");
            Console.WriteLine($"{passed} profile/reset SQL checks passed.");
        }
        finally
        {
            if (db.Database.GetDbConnection().Database != databaseName
                || !databaseName.StartsWith("YsProfileResetTest_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to remove a non-fixture database.");
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class ResetSmsProbe : ISmsProvider
    {
        public string ProviderName => "Fixture";
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public string LastCode { get; private set; } = "";
        public Task<SmsGonderimSonucu> GonderAsync(string telefon, string mesaj, string? firmaKodu = null)
        {
            Calls++;
            LastCode = mesaj.Split(' ')[^1];
            return Task.FromResult(Fail ? new SmsGonderimSonucu(false, Hata: "Private provider error") : new SmsGonderimSonucu(true, "fixture"));
        }
    }

    public static async Task RunAsync(AppDbContext db, UserManager<AppKullanici> manager,
        AppKullanici admin, AppKullanici staff, int companyId, int otherCompanyId, Action<bool, string> check)
    {
        var permissions = new AdminPersonelYetkiApiService(db);
        AdminYetkiGuncelleDto Rights(string right) => new()
        {
            Id = staff.Id, SirketIds = [companyId], Yetkiler = new() { [companyId] = [right] }
        };
        AdminPanelApiController Controller(AppKullanici actor) => new(db, null!, null!, null!, null!, null!,
            null!, permissions, null!, new AdminKullaniciOkumaApiService(db),
            new AdminKullaniciYonetimApiService(db, manager, NullLogger<AdminPanelApiController>.Instance))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id)], "test"))
                }
            }
        };
        static ApiIslemSonuc Body(IActionResult result) => (ApiIslemSonuc)((OkObjectResult)result).Value!;

        check((await permissions.GuncelleAsync(Rights(YetkiTipleri.KULLANICI_YONET), admin, companyId, true)).Basarili,
            "Administrator can grant service-management permission");
        db.ChangeTracker.Clear();
        var count = await db.Dag_PersonelYetkiler.CountAsync();
        check(await Controller(staff).YetkiGuncelle(Rights(YetkiTipleri.TAM_YETKI)) is ForbidResult,
            "Personnel cannot promote themselves through the permissions API");
        check(await Controller(staff).YetkilerListe(new() { SirketId = companyId }) is ForbidResult
            && await Controller(staff).YetkiGetir(new() { Id = staff.Id, SirketId = companyId }) is ForbidResult,
            "Personnel cannot read permission-management endpoints");
        check(await Controller(staff).Kullanicilar(new() { SirketId = companyId }) is ForbidResult,
            "Service management does not grant user account or password administration");
        check(await Controller(staff).PersonelEkle(new() { SirketId = companyId }) is ForbidResult,
            "Service management does not grant personnel account creation");
        check(!(await permissions.GuncelleAsync(Rights(YetkiTipleri.TAM_YETKI), staff, companyId, true)).Basarili
            && await db.Dag_PersonelYetkiler.CountAsync() == count,
            "Permission service independently rejects self-escalation without changing history");
        var companyAdmin = new AppKullanici
        {
            UserName = "company-admin", KullaniciTipi = KullaniciTipiDegerleri.SirketAdmin, SirketId = companyId
        };
        db.Users.Add(companyAdmin);
        await db.SaveChangesAsync();
        check(!(await permissions.GuncelleAsync(Rights(YetkiTipleri.TAM_YETKI), companyAdmin, otherCompanyId, false)).Basarili,
            "Company administrator cannot manage permissions in another company");
        check((await permissions.GuncelleAsync(Rights(YetkiTipleri.TAM_YETKI), companyAdmin, companyId, false)).Basarili,
            "Company administrator retains permission management in their company");
        check(await Controller(staff).YetkiGuncelle(Rights(YetkiTipleri.RAPOR_GOR)) is ForbidResult,
            "Even full personnel permission does not delegate administrator privilege");

        db.ChangeTracker.Clear();
        var previousStamp = (await db.Users.FindAsync(staff.Id))!.SecurityStamp;
        var history = await db.Dag_PersonelYetkiler.AsNoTracking().Where(x => x.KullaniciId == staff.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.SilindiMi, x.SilinmeTarihi, x.OlusturmaTarihi, x.SilenKullanici }).ToListAsync();
        check(!Body(await Controller(admin).KullaniciSil(new() { Id = admin.Id })).Basarili,
            "Administrator cannot archive their own account");
        check(Body(await Controller(admin).KullaniciSil(new() { Id = staff.Id, SadecePersonel = true })).Basarili,
            "User deletion archives the account");
        db.ChangeTracker.Clear();
        var archived = (await db.Users.FindAsync(staff.Id))!;
        var after = await db.Dag_PersonelYetkiler.AsNoTracking().Where(x => x.KullaniciId == staff.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.SilindiMi, x.SilinmeTarihi, x.OlusturmaTarihi, x.SilenKullanici }).ToListAsync();
        check(!archived.AktifMi && archived.ArsivlemeTarihi != null && archived.ArsivleyenKullaniciId == admin.Id
            && archived.SecurityStamp != previousStamp && history.SequenceEqual(after),
            "Archive revokes access and records actor/time without losing any permission history");
        check(await Controller(archived).YetkilerListe(new() { SirketId = companyId }) is UnauthorizedResult,
            "Archived account cannot execute administrator API requests");
        check(!Body(await Controller(admin).KullaniciDurum(new() { Id = staff.Id, AktifMi = true })).Basarili,
            "Normal active/passive switch cannot reactivate an archived account");
        check(!(await permissions.GuncelleAsync(Rights(YetkiTipleri.RAPOR_GOR), admin, null, true)).Basarili,
            "Archived account cannot receive new personnel permissions");
        var listed = (List<AdminKullaniciListeDto>)((OkObjectResult)await Controller(admin).Kullanicilar(null)).Value!;
        check(listed.All(x => x.Id != staff.Id), "Archived account is absent from operational user lists");
        try
        {
            await db.Users.Where(x => x.Id == staff.Id).ExecuteDeleteAsync();
            throw new InvalidOperationException("History owner was physically deleted.");
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            check(await db.Users.AnyAsync(x => x.Id == staff.Id), "SQL foreign key blocks cascading deletion of permission history");
        }
        try
        {
            await db.Users.Where(x => x.Id == staff.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, true));
            throw new InvalidOperationException("Archived account was activated.");
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            check(true, "SQL constraint prevents an archived account from becoming active");
        }

        // Exercise replacement accounts with Identity validation and SQL username uniqueness enabled.
        manager.Options.User.RequireUniqueEmail = true;
        manager.UserValidators.Add(new UserValidator<AppKullanici>());
        db.Roles.Add(new IdentityRole("YetkiliServis") { NormalizedName = "YETKILISERVIS" });
        var firm = new Ys_Firma
        {
            FirmaAdi = "Archive replacement fixture", SirketId = companyId,
            VergiNo = "9000000001", TcKimlikNo = "10000000001"
        };
        db.Ys_Firmalar.Add(firm);
        await db.SaveChangesAsync();
        AdminKullaniciKaydetDto Account(string email, string password = "Replacement123!") => new()
        {
            AdSoyad = "Service fixture", Email = email, Telefon = "905551234567", Sifre = password,
            Rol = "YetkiliServis", FirmaId = firm.Id, SirketId = companyId
        };
        check(Body(await Controller(admin).KullaniciEkle(Account("archived-service@example.test", "Original123!"))).Basarili,
            "Initial service account is created using the firm's tax number");
        var original = (await manager.FindByEmailAsync("archived-service@example.test"))!;
        check(original.UserName == firm.VergiNo && await manager.IsInRoleAsync(original, "YetkiliServis"),
            "Initial tax-number username and service role remain unchanged");
        check(Body(await Controller(admin).KullaniciSil(new() { Id = original.Id })).Basarili,
            "Service account can be archived without removing its firm");
        db.ChangeTracker.Clear();
        original = (await manager.FindByIdAsync(original.Id))!;
        var originalSnapshot = new { original.UserName, original.Email, original.SecurityStamp, original.ArsivlemeTarihi, original.ArsivleyenKullaniciId };
        var userCount = await db.Users.CountAsync();
        var archivedEmail = Body(await Controller(admin).KullaniciEkle(Account("  ARCHIVED-SERVICE@example.test  ")));
        check(!archivedEmail.Basarili && archivedEmail.Mesaj?.Contains("arşivlenmiş", StringComparison.Ordinal) == true
            && archivedEmail.Mesaj?.Contains("farklı bir e-posta", StringComparison.Ordinal) == true,
            "Archived email returns an actionable warning even with mixed case and whitespace");
        var personnelEmail = Body(await Controller(admin).PersonelEkle(new()
        {
            AdSoyad = "Personnel fixture", Email = original.Email, Telefon = "905551234567",
            Sifre = "Replacement123!", SirketId = companyId
        }));
        check(!personnelEmail.Basarili && personnelEmail.Mesaj == archivedEmail.Mesaj
            && await db.Users.CountAsync() == userCount,
            "Personnel creation gives the same archive warning without inserting an account");
        var outsideScope = Account("outside@example.test");
        outsideScope.SirketId = otherCompanyId;
        check(await Controller(companyAdmin).KullaniciEkle(outsideScope) is ForbidResult,
            "Replacement account creation still enforces company scope");
        check(Body(await Controller(companyAdmin).KullaniciEkle(Account("replacement@example.test"))).Basarili,
            "Company administrator can create a replacement for an archived firm's account");
        var replacement = (await manager.FindByEmailAsync("replacement@example.test"))!;
        check(replacement.Id != original.Id && replacement.UserName == replacement.Email
            && replacement.FirmaId == firm.Id && replacement.ArsivlemeTarihi == null
            && await manager.IsInRoleAsync(replacement, "YetkiliServis"),
            "Replacement has a distinct identity and retains the existing firm and service role");

        var authFlow = new OturumAkisApiService(manager, null!, null!, null!,
            Options.Create(new SertifikaliFirmaKimlikOptions()), Options.Create(new SmsOptions()),
            new EphemeralDataProtectionProvider(), null!, db);
        var auth = new AuthController(manager, null!, authFlow)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        Task<AppKullanici?> Find(string login) => authFlow.FindAsync(login);
        check((await Find(firm.VergiNo))?.Id == replacement.Id
            && (await Find(firm.TcKimlikNo))?.Id == replacement.Id
            && (await Find(" REPLACEMENT@example.test "))?.Id == replacement.Id,
            "Tax number, national ID and replacement email resolve to the new service account");
        check(await manager.CheckPasswordAsync(replacement, "Replacement123!")
            && !await manager.CheckPasswordAsync(replacement, "Original123!"),
            "Replacement requires its own password, never the archived account's password");
        check((await Find(original.Email!))?.Id == original.Id
            && await auth.Token(new(original.Email!, "Original123!")) is ObjectResult { StatusCode: 401 },
            "Archived email is not redirected to the replacement and cannot log in");
        var duplicate = Body(await Controller(admin).KullaniciEkle(Account("duplicate@example.test")));
        check(!duplicate.Basarili && duplicate.Mesaj?.Contains("giris hesabi zaten var", StringComparison.Ordinal) == true,
            "Existing unarchived firm account still prevents a second login account");
        check(Body(await Controller(admin).KullaniciDurum(new() { Id = replacement.Id, AktifMi = false })).Basarili,
            "Replacement account can be made inactive");
        check(!Body(await Controller(admin).KullaniciEkle(Account("inactive-duplicate@example.test"))).Basarili
            && await Find(firm.VergiNo) == null,
            "Inactive is not archived: duplicate accounts and tax-number login remain blocked");
        check(Body(await Controller(admin).KullaniciSil(new() { Id = replacement.Id })).Basarili
            && Body(await Controller(admin).KullaniciEkle(Account("second-replacement@example.test"))).Basarili,
            "Repeated archive and replacement works without renaming or deleting prior accounts");
        var secondReplacement = (await manager.FindByEmailAsync("second-replacement@example.test"))!;
        check((await Find(firm.VergiNo))?.Id == secondReplacement.Id,
            "Tax-number login follows the only active replacement after multiple archives");
        db.ChangeTracker.Clear();
        var retained = (await manager.FindByIdAsync(original.Id))!;
        check(originalSnapshot.Equals(new { retained.UserName, retained.Email, retained.SecurityStamp, retained.ArsivlemeTarihi, retained.ArsivleyenKullaniciId })
            && !retained.AktifMi && await manager.IsInRoleAsync(retained, "YetkiliServis")
            && await db.Users.CountAsync(x => x.FirmaId == firm.Id) == 3,
            "Replacement preserves archived identity, archive audit fields and role history");
        db.Users.Add(new AppKullanici
        {
            UserName = "ambiguous-fixture", FirmaId = firm.Id, SirketId = companyId,
            KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis
        });
        await db.SaveChangesAsync();
        check(await Find(firm.VergiNo) == null && await Find(firm.TcKimlikNo) == null,
            "Ambiguous active service accounts never resolve to an arbitrary identity");
    }
}
