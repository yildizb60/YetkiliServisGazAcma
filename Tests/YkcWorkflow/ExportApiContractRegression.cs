using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace Tests.YkcWorkflow;

internal static class ExportApiContractRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }
        foreach (var (controller, action, request) in new[]
        {
            (typeof(AdminPanelApiController), nameof(AdminPanelApiController.DevreyeAlmaRaporPdf), typeof(AdminDevreyeAlmaRaporExportFiltreDto)),
            (typeof(AdminPanelApiController), nameof(AdminPanelApiController.DevreyeAlmaRaporExcel), typeof(AdminDevreyeAlmaRaporExportFiltreDto)),
            (typeof(YetkiliServisPanelApiController), nameof(YetkiliServisPanelApiController.RaporlarPdf), typeof(YsPanelRaporFiltreDto)),
            (typeof(YetkiliServisPanelApiController), nameof(YetkiliServisPanelApiController.RaporlarExcel), typeof(YsPanelRaporFiltreDto)),
            (typeof(YkcApiController), nameof(YkcApiController.TaleplerRaporPdf), typeof(YkcTalepListeFiltre)),
            (typeof(YkcApiController), nameof(YkcApiController.TaleplerRaporExcel), typeof(YkcTalepListeFiltre))
        })
        {
            var method = controller.GetMethod(action)!;
            Check(method.IsDefined(typeof(HttpPostAttribute)) && !method.IsDefined(typeof(HttpGetAttribute))
                && method.GetParameters().Single().ParameterType == request
                && method.GetParameters().Single().IsDefined(typeof(FromBodyAttribute))
                && controller.IsDefined(typeof(AuthorizeAttribute)),
                controller.Name + "." + action + ": existing authorized API POST body contract is unchanged");
        }

        // SQL is compiled, but both sync and async connections are blocked before opening.
        foreach (int? company in new int?[] { null, 7 })
        {
            var query = await CaptureAsync(db => new DevreyeAlmaExportApiService(db)
                .AdminRaporExcelAsync(company, null, null, Enumerable.Range(1000000, 5000).ToList()));
            Check(query.Contains(".Contains(x.Id)") && query.Contains("Take(")
                && query.Contains("OrderByDescending(x => x.DevreyeAlmaTarihi).ThenByDescending(x => x.Id)")
                && !query.Contains("x.DevreyeAlmaTarihi >="),
                $"Commissioning company={company}: selected IDs use API-owned bounded, stable export query without a default date restriction");
        }
        var serviceQuery = await CaptureAsync(db => new DevreyeAlmaExportApiService(db)
            .YetkiliServisRaporExcelAsync(9, null, null, [42]));
        Check(serviceQuery.Contains("x.FirmaId ==") && serviceQuery.Contains(".Contains(x.Id)")
            && serviceQuery.Contains("Take("), "Service bulk export keeps firm scope and selected-ID intersection at the API data layer");

        var admin = new AppKullanici { Id = "fixture-admin", KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin };
        var filter = new YkcTalepListeFiltre
        {
            SirketId = 7, KayitIdleri = [42, 43], SozlesmeNo = "000943", TesisatNo = "000123",
            MusteriAdi = "Customer", CihazTipi = "Kombi", Sirala = "abone-artan", Sayfa = 3, SayfaBoyutu = 10,
            BaslangicTarihi = new(2026, 9, 1), BitisTarihi = new(2026, 9, 30),
            RandevuBaslangicTarihi = new(2026, 10, 1), RandevuBitisTarihi = new(2026, 10, 31)
        };
        var scopedQuery = await CaptureAsync(db => new YkcTalepOkumaService(db).RaporKayitlariAsync(filter, admin, true, 5001, 7));
        Check(scopedQuery.Contains("x.SirketId ==") && scopedQuery.Contains(".Contains(x.Id)")
            && scopedQuery.Contains("x.SozlesmeNo.Contains(") && scopedQuery.Contains("x.TesisatNo.Contains(")
            && scopedQuery.Contains("x.TalepTarihi >=") && scopedQuery.Contains("x.TalepTarihi <")
            && scopedQuery.Contains("x.RandevuTarihi.Value >=") && scopedQuery.Contains("x.RandevuTarihi.Value <")
            && scopedQuery.Contains("OrderBy(") && scopedQuery.Contains("Turkish_CI_AS") && scopedQuery.Contains("Take(")
            && !scopedQuery.Contains("Skip("),
            "YKC export intersects IDs/company/GET filters, retains sorting and exports beyond the return page");
        var allQuery = await CaptureAsync(db => new YkcTalepOkumaService(db).RaporKayitlariAsync(new(), admin, true, 5001));
        Check(!allQuery.Contains("x.SirketId ==") && !allQuery.Contains(".Contains(x.Id)")
            && !allQuery.Contains("Skip("), "Intentional all-company, unselected YKC report remains unrestricted by page or company");
        var firmQuery = await CaptureAsync(db => new YkcTalepOkumaService(db).RaporKayitlariAsync(new() { KayitIdleri = [42] },
            new AppKullanici { FirmaId = 9, KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma }, true, 5001, 7));
        Check(firmQuery.Contains("x.FirmaId ==") && firmQuery.Contains("x.SirketId ==")
            && firmQuery.Contains(".Contains(x.Id)"), "Privileged role flags never bypass firm and company scope in selected YKC export");

        foreach (var sort in new[] { "tarih-artan", "tarih-azalan", "abone-artan", "abone-azalan" })
        {
            var sortedQuery = await CaptureAsync(db => new YkcTalepOkumaService(db).RaporKayitlariAsync(new()
            {
                Q = "000943", Sirala = sort, CihazTipi = "\u015eofben", TalepId = 42,
                Sayfa = 3, SayfaBoyutu = 10
            }, admin, true, 5001, 7));
            var expectedOrder = sort switch
            {
                "tarih-artan" => "OrderBy(x => x.TalepTarihi).ThenBy(x => x.Id)",
                "abone-artan" => "OrderBy(x =>",
                "abone-azalan" => "OrderByDescending(x =>",
                _ => "OrderByDescending(x => x.TalepTarihi).ThenByDescending(x => x.Id)"
            };
            Check(sortedQuery.Contains(expectedOrder) && sortedQuery.Contains("x.Id ==")
                && sortedQuery.Contains("x.SirketId ==") && sortedQuery.Contains("Turkish_CI_AS")
                && !sortedQuery.Contains("Skip("),
                $"YKC {sort}: free text, device alias and request ID compile to scoped SQL without export pagination");
        }

        // Execute the actual filter predicates on fixtures without a database or provider emulation.
        var records = new[]
        {
            new Ykc_Talep { Id = 42, SirketId = 7, FirmaId = 9, TesisatNo = "000123", SozlesmeNo = "000943",
                EskiMarka = "OldBrand", YeniMarka = "NewBrand", TalepTarihi = new(2026, 9, 1), RandevuTarihi = new(2026, 10, 31, 23, 59, 59) },
            new Ykc_Talep { Id = 43, SirketId = 7, FirmaId = 10, TesisatNo = "000943", SozlesmeNo = "000123",
                TalepTarihi = new(2026, 9, 2), RandevuTarihi = new(2026, 11, 1) },
            new Ykc_Talep { Id = 44, SirketId = 8, FirmaId = 9, TesisatNo = "000123", SozlesmeNo = "000943" },
            new Ykc_Talep { Id = 45, SirketId = 8, FirmaId = 11, TesisatNo = "000943", SozlesmeNo = "000123" }
        }.AsQueryable();
        var apply = typeof(YkcTalepOkumaService).GetMethod("FiltreleriUygulaAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        async Task<int[]> FilterIds(YkcTalepListeFiltre criteria, AppKullanici user, bool privileged, int? company = null)
        {
            var filtered = await (Task<IQueryable<Ykc_Talep>>)apply.Invoke(null, [records, criteria, user, privileged, company])!;
            return filtered.Select(x => x.Id).ToArray();
        }
        Check((await FilterIds(new() { SozlesmeNo = "000943" }, admin, true, 7)).SequenceEqual([42]),
            "Contract search preserves leading zeros and does not match the installation-number field");
        Check((await FilterIds(new() { TesisatNo = "000943" }, admin, true, 7)).SequenceEqual([43]),
            "Installation search is independent of contract search");
        Check((await FilterIds(new() { KayitIdleri = [42, 43, 44, 45], SozlesmeNo = "000943" }, admin, true, 7)).SequenceEqual([42]),
            "Selected IDs remain intersected with both company scope and report filters");
        Check((await FilterIds(new() { KayitIdleri = [] }, admin, true)).Length == 0,
            "Explicit empty API selection never becomes filtered-all");
        var firm = new AppKullanici { FirmaId = 9, KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma };
        Check((await FilterIds(new() { KayitIdleri = [42, 43, 44, 45] }, firm, true, 7)).SequenceEqual([42]),
            "A privileged flag cannot expand selected records beyond firm and company intersection");
        Check((await FilterIds(new(), new() { KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma }, true)).Length == 0,
            "A firm account without a firm ID remains fail-closed even with a privileged flag");
        Check((await FilterIds(new() { SirketId = 8 }, new() { SirketId = 7, KullaniciTipi = KullaniciTipiDegerleri.Personel }, false)).SequenceEqual([42, 43]),
            "An unverified filter company cannot expand personnel scope");
        Check((await FilterIds(new() { Marka = "OldBrand" }, firm, false, 7)).Length == 0
            && (await FilterIds(new() { Marka = "NewBrand" }, firm, false, 7)).SequenceEqual([42])
            && (await FilterIds(new() { Marka = "OldBrand" }, admin, true, 7)).SequenceEqual([42]),
            "Firm brand search uses only its visible new device; internal search also includes the old device");
        Check((await FilterIds(new()
        {
            RandevuBaslangicTarihi = new(2026, 10, 31), RandevuBitisTarihi = new(2026, 10, 31)
        }, admin, true)).SequenceEqual([42]),
            "Appointment date range includes the final day's last second and excludes the following midnight and null dates");
        Check((await FilterIds(new() { TalepId = 43, KayitIdleri = [42] }, admin, true, 7)).Length == 0,
            "Request-ID and selected-row filters cannot replace one another");
        Console.WriteLine($"{passed} no-database export API contract checks passed.");
    }

    private static async Task<string> CaptureAsync(Func<AppDbContext, Task> action)
    {
        var probe = new QueryProbe();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=ExportContractProbe;Integrated Security=true;TrustServerCertificate=true")
            .EnableServiceProviderCaching(false).AddInterceptors(probe).Options);
        try { await action(db); }
        catch (BlockedConnectionException)
        {
            return probe.Query ?? throw new InvalidOperationException("Export SQL was not compiled");
        }
        throw new InvalidOperationException("Export query did not encounter the no-database guard");
    }

    private sealed class BlockedConnectionException : Exception;
    private sealed class QueryProbe : DbConnectionInterceptor, IQueryExpressionInterceptor
    {
        public string? Query { get; private set; }
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        {
            Query = queryExpression.ToString();
            return queryExpression;
        }
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => throw new BlockedConnectionException();
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => throw new BlockedConnectionException();
    }
}
