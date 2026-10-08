using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class ReferenceApiRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
            passed++;
        }

        // Every attempted connection is rejected before opening; no database is created or read.
        var guard = new QueryProbe();
        await using var db = Context(guard);
        var brands = new MarkaApiController(new MarkaKatalogApiService(db, new MarkaService(db)))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        Check(await brands.Liste(new() { TumunuGetir = true }) is UnauthorizedResult,
            "Anonymous callers cannot request the brand management catalog");
        brands.HttpContext.User = new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "YetkiliServis")], "Fixture"));
        Check(await brands.Liste(new() { TumunuGetir = true }) is ForbidResult,
            "A service role alone cannot request the brand management catalog");
        Check(await brands.Getir(new() { Id = 1 }) is ForbidResult
            && await brands.Ekle(new() { MarkaAdi = "Rejected" }) is ForbidResult
            && await brands.Guncelle(new() { Id = 1, MarkaAdi = "Rejected" }) is ForbidResult
            && await brands.Sil(new() { Id = 1 }) is ForbidResult,
            "Brand actions reject an account without catalog management permission before accessing records");

        foreach (var action in new[] { nameof(MarkaApiController.Ekle), nameof(MarkaApiController.Guncelle), nameof(MarkaApiController.Sil) })
        {
            var roles = typeof(MarkaApiController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
            Check(roles == "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel",
                "Brand write endpoint retains its role gate, excluding firm accounts: " + action);
        }

        foreach (var role in new[] { "GenelSistemAdmin", "SuperAdmin" })
        {
            brands.HttpContext.User = new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Fixture"));
            var create = await brands.Ekle(new() { MarkaAdi = "  " });
            Check(create is BadRequestObjectResult { Value: ApiIslemSonuc { Basarili: false, Mesaj: "Marka adi zorunludur", Id: null } },
                "Brand creation preserves its validation response for " + role);
            var update = await brands.Guncelle(new() { MarkaAdi = "Name" });
            Check(update is BadRequestObjectResult { Value: ApiIslemSonuc { Basarili: false, Mesaj: "Id zorunludur", Id: null } },
                "Brand update preserves its missing-ID response for " + role);
        }

        var companies = new DagitimSirketApiController(new DagitimSirketApiService(db))
        {
            ControllerContext = brands.ControllerContext
        };
        Check(await companies.Getir(new() { Id = 1 }) is ForbidResult,
            "Company profiles still require a persisted active account even for an administrator role claim");

        using var users = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db), Options.Create(new IdentityOptions()),
            new PasswordHasher<AppKullanici>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            null!, NullLogger<UserManager<AppKullanici>>.Instance);
        var panel = new PanelKapsamApiController(new PanelKapsamApiService(db, users, null!, null!))
        {
            ControllerContext = brands.ControllerContext
        };
        Check(await panel.KullaniciSirketleri() is UnauthorizedResult
            && await panel.PanelKimlik(new() { AktifSirketId = 1 }) is UnauthorizedResult
            && await panel.YkcYetkileri(new() { AktifSirketId = 1 }) is UnauthorizedResult,
            "Panel endpoints require a resolved active account before company selection");
        Check(guard.ConnectionAttempts == 0, "Denied and invalid requests never reach the database");

        var query = await CaptureAsync(context => new UrunKategoriKatalogApiService(context).AktifleriListeleAsync());
        Check(query.Contains("Not(x.SilindiMi)") && query.Contains("AndAlso x.AktifMi")
            && query.Contains("OrderBy(x => x.SiraNo).ThenBy(x => x.Ad)") && query.Contains("new UrunKategoriApiDto"),
            "Category SQL translates with active/deletion filters, stable ordering and shared DTO projection");

        query = await CaptureAsync(context => new DagitimSirketApiService(context).ListeleAsync(null));
        Check(query.Contains("Not(x.SilindiMi)") && query.Contains("Where(x => x.AktifMi)")
            && query.Contains("OrderBy(x => x.SirketAdi)") && query.Contains("new DagitimSirketApiDto"),
            "Public company catalog keeps its active-only default and shared projection");
        query = await CaptureAsync(context => new DagitimSirketApiService(context).ListeleAsync(new() { TumunuGetir = true, AktifMi = false }));
        Check(!query.Contains("Where(x => x.AktifMi)") && query.Contains("x.AktifMi ==") && query.Contains("Not(x.SilindiMi)"),
            "Explicit all-company filtering retains existing inactive-company behavior");

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        query = await CaptureAsync(context => new MarkaKatalogApiService(context, new MarkaService(context)).ListeleAsync(null, anonymous));
        Check(query.Contains("Where(x => x.AktifMi)") && query.Contains("Not(x.SilindiMi)") && query.Contains("new MarkaApiDto"),
            "Anonymous brand catalog remains active-only and DTO-based");
        query = await CaptureAsync(context => new MarkaKatalogApiService(context, new MarkaService(context))
            .ListeleAsync(new() { AktifMi = false }, anonymous));
        Check(query.Contains("Where(x => x.AktifMi)") && query.Contains("x.AktifMi =="),
            "An inactive filter cannot bypass the anonymous brand catalog restriction");
        var companyAdmin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "SirketAdmin")], "Fixture"));
        query = await CaptureAsync(context => new MarkaKatalogApiService(context, new MarkaService(context))
            .ListeleAsync(new() { TumunuGetir = true, Q = "  isi  " }, companyAdmin));
        Check(!query.Contains("Where(x => x.AktifMi)") && query.Contains("Turkish_CI_AS") && query.Contains("StartsWith"),
            "Company-admin catalog listing keeps its separate permission and translatable Turkish prefix search");

        Check(typeof(DagitimSirketApiController).GetMethod(nameof(DagitimSirketApiController.Tumunu))!
                .GetParameters()[0].ParameterType == typeof(DagitimSirketListeFiltreDto)
            && typeof(PanelKapsamApiController).GetMethod(nameof(PanelKapsamApiController.PanelKimlik))!
                .GetParameters()[0].ParameterType == typeof(PanelKimlikIstekDto),
            "Reference controller requests use the shared contracts");
        Check(typeof(IdDto).GetProperty(nameof(IdDto.Id))!.PropertyType == typeof(int),
            "The controller-shared IdDto contract remains available");
        Console.WriteLine($"{passed} no-database reference API checks passed.");
    }

    private static AppDbContext Context(QueryProbe probe) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer("Server=unused;Database=ReferenceApiProbe;Integrated Security=true;TrustServerCertificate=true")
        .EnableServiceProviderCaching(false)
        .AddInterceptors(probe).Options);

    private static async Task<string> CaptureAsync(Func<AppDbContext, Task> query)
    {
        var probe = new QueryProbe();
        await using var db = Context(probe);
        try
        {
            await query(db);
            throw new InvalidOperationException("Expected the database connection guard");
        }
        catch (QueryProbeException)
        {
            return probe.Query ?? throw new InvalidOperationException("Query compilation was not observed");
        }
    }

    private sealed class QueryProbeException : Exception;

    private sealed class QueryProbe : DbConnectionInterceptor, IQueryExpressionInterceptor
    {
        public string? Query { get; private set; }
        public int ConnectionAttempts { get; private set; }

        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        {
            Query = queryExpression.ToString();
            return queryExpression;
        }

        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            ConnectionAttempts++;
            throw new QueryProbeException();
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            ConnectionAttempts++;
            throw new QueryProbeException();
        }
    }
}
