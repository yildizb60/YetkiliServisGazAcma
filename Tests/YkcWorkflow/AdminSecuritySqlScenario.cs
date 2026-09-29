using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    }
}
