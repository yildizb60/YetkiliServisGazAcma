using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class AdminSecuritySqlScenario
{
    public static async Task RunAsync(AppDbContext db, UserManager<AppKullanici> manager,
        AppKullanici admin, AppKullanici staff, int companyId, int otherCompanyId, Action<bool, string> check)
    {
        var permissions = new AdminPersonelYetkiApiService(db);
        AdminYetkiGuncelleDto Rights(string right) => new()
        {
            Id = staff.Id, SirketIds = [companyId], Yetkiler = new() { [companyId] = [right] }
        };
        AdminPanelApiController Controller(AppKullanici actor) => new(db, manager, null!, null!, null!, null!, null!,
            null!, permissions, null!, NullLogger<AdminPanelApiController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id)], "test"))
                }
            }
        };
        static AdminIslemSonucDto Body(IActionResult result) => (AdminIslemSonucDto)((OkObjectResult)result).Value!;

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

        var auth = new AuthController(manager, null!, null!, null!,
            Options.Create(new SertifikaliFirmaKimlikOptions()), Options.Create(new SmsOptions()),
            new EphemeralDataProtectionProvider(), null!, db);
        var find = typeof(AuthController).GetMethod("FindAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Task<AppKullanici?> Find(string login) => (Task<AppKullanici?>)find.Invoke(auth, [login])!;
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
