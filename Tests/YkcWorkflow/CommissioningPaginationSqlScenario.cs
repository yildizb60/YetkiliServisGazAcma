using System.Data.Common;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace Tests.YkcWorkflow;

internal static class CommissioningPaginationSqlScenario
{
    public static async Task RunAsync()
    {
        var name = "CommissioningPaging_" + Guid.NewGuid().ToString("N");
        var probe = new QueryProbe();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($@"Server=(localdb)\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(probe).Options;
        await using var db = new AppDbContext(options);
        var created = false;
        var passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
            passed++;
        }
        try
        {
            created = await db.Database.EnsureCreatedAsync();
            if (!created) throw new InvalidOperationException("An isolated paging database must be new.");
            var company = new Dag_Sirket { SirketAdi = "Paging scope" };
            var other = new Dag_Sirket { SirketAdi = "Other scope" };
            db.AddRange(company, other);
            await db.SaveChangesAsync();
            var firm = new Ys_Firma { FirmaAdi = "Paging service", SirketId = company.Id };
            var foreign = new Ys_Firma { FirmaAdi = "Foreign service", SirketId = other.Id };
            db.AddRange(firm, foreign);
            await db.SaveChangesAsync();
            var date = new DateTime(2026, 10, 1);
            var rows = Enumerable.Range(1, 301).Select(i => new Ys_DevreyeAlma
            {
                FirmaId = firm.Id, TesistatNo = "INSTALL-" + i, AboneNo = "SUB-" + i,
                MusteriAdi = "İSMAİL " + i.ToString("D4"), CihazTipi = "Kombi", CihazMarka = "Test",
                DevreyeAlmaTarihi = date.AddMinutes(i), Durum = i % 3
            }).ToList();
            var foreignRow = new Ys_DevreyeAlma { FirmaId = foreign.Id, DevreyeAlmaTarihi = date.AddYears(1) };
            db.AddRange(rows);
            db.Add(foreignRow);
            await db.SaveChangesAsync();
            db.AddRange(rows.Select(r => new Ys_DevreyeAlmaSorguKaydi
            {
                Referans = Guid.NewGuid().ToString("N"), FirmaId = firm.Id, DagitimSirketiId = company.Id,
                DevreyeAlmaId = r.Id, KaynakJson = JsonSerializer.Serialize(new YsDevreyeAlmaKaynak
                { TesisatNo = r.TesistatNo!, AboneNo = r.AboneNo!, SozlesmeNo = "CONTRACT-" + r.Id })
            }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = new DevreyeAlmaOkumaApiService(db, null!, null!);
            var admin = new AdminRaporApiService(db);
            probe.Commands.Clear();
            var first = await service.GecmisAsync(firm.Id, null);
            Check(first.Islemler.Count == 20 && first.Sayfalama.ToplamKayit == 301 && first.Toplam == 301,
                "Service history returns twenty records and the complete count");
            Check(first.Sayfalama.AktarimIdleri.Count == 301 && !first.Sayfalama.AktarimIdleri.Contains(foreignRow.Id),
                "All-filtered export IDs span every page and retain firm scope");
            Check(probe.Commands.Any(sql => sql.Contains("FETCH NEXT") && sql.Contains("[MusteriAdi]")),
                "Full commissioning rows are paged in SQL before materialization");
            var second = await service.GecmisAsync(firm.Id, new() { Sayfa = 2 });
            Check(second.Islemler.Select(x => x.Id).SequenceEqual(rows.AsEnumerable().Reverse().Skip(20).Take(20).Select(x => x.Id)),
                "Second SQL page has the expected stable order without duplicates");
            var last = await service.GecmisAsync(firm.Id, new() { Sayfa = int.MaxValue });
            Check(last.Sayfalama.Sayfa == 16 && last.Islemler.Single().Id == rows[0].Id,
                "Out-of-range pages clamp without integer overflow or empty last pages");
            var focus = await service.GecmisAsync(firm.Id, new() { OdakKayitId = rows[17].Id });
            Check(focus.Islemler.Any(x => x.Id == rows[17].Id) && focus.Sayfalama.Sayfa == 15,
                "Bookmarked details resolve their actual page in the API");
            Check((await service.GecmisAsync(firm.Id, new() { OdakKayitId = foreignRow.Id })).Islemler.All(x => x.FirmaId == firm.Id),
                "A foreign focus ID cannot broaden the service scope");
            var status = await service.GecmisAsync(firm.Id, new() { Durum = "tamamlandi", Sayfa = 2 });
            Check(status.Toplam == 301 && status.Tamamlanan == 101 && status.Sayfalama.ToplamKayit == 101
                && status.Islemler.Count == 20 && status.Islemler.All(x => x.Durum == 1),
                "Status paging preserves pre-status counters and filtered totals");
            foreach (var order in new[] { "tarih-artan", "abone-artan", "abone-azalan", "" })
            {
                var a = await admin.DevreyeAlmalarAsync(new() { Siralama = order, Sayfa = 2 }, company.Id);
                var s = await service.GecmisAsync(firm.Id, new() { Siralama = order, Sayfa = 2 });
                Check(a.Islemler.Select(x => x.Id).SequenceEqual(s.Islemler.Select(x => x.Id))
                    && a.Sayfalama.ToplamKayit == 301, "Admin and service share scoped ordering and pagination: " + order);
            }
            foreach (var contract in new[] { false, true })
            {
                probe.Commands.Clear();
                probe.SourceLookupIds = 0;
                var found = await service.GecmisAsync(firm.Id, new()
                { Arama = contract ? null : "ismail", SozlesmeNo = contract ? "CONTRACT-" : null, Sayfa = 14, Durum = null });
                Check(found.Sayfalama.ToplamKayit == 301 && found.Islemler.Count == 20
                    && found.Islemler.Select(x => x.Id).SequenceEqual(rows.AsEnumerable().Reverse().Skip(260).Take(20).Select(x => x.Id)),
                    "Validated source search crosses batch boundaries without losing pages: " + contract);
                Check(found.Islemler.All(x => x.SozlesmeNo == "CONTRACT-" + x.Id),
                    "Paged records retain validated source contracts: " + contract);
                Check(probe.Commands.Count(sql => sql.Contains("FETCH NEXT")) >= 2,
                    "Source searches use bounded batches: " + contract);
                if (!contract) Check(probe.SourceLookupIds == 20,
                    "Direct Turkish name matches enrich only the displayed page, not all 301 source records");
            }
            var sourceFocus = await service.GecmisAsync(firm.Id, new()
            { Arama = "ismail", OdakKayitId = rows[17].Id, Sayfa = 1 });
            Check(sourceFocus.Sayfalama.Sayfa == 15 && sourceFocus.Islemler.Any(x => x.Id == rows[17].Id),
                "A source-filtered bookmarked record selects its matching page");
            var sourceLast = await service.GecmisAsync(firm.Id, new() { Arama = "ismail", Sayfa = int.MaxValue });
            Check(sourceLast.Sayfalama.Sayfa == 16 && sourceLast.Islemler.Single().Id == rows[0].Id,
                "A source-filtered out-of-range page clamps to its final record");
            var none = await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "missing", Sayfa = 12 });
            Check(none.Sayfalama.Sayfa == 1 && none.Sayfalama.ToplamKayit == 0 && none.Islemler.Count == 0,
                "Unmatched source searches expose an empty first page");
            var exporter = new DevreyeAlmaExportApiService(db);
            var allExcel = await exporter.YetkiliServisRaporExcelAsync(firm.Id, null, null, first.Sayfalama.AktarimIdleri);
            Check(ExcelRowCount(allExcel.Bytes) == 301, "Unselected export includes all matching pages, not just twenty rows");
            var selected = await exporter.YetkiliServisRaporExcelAsync(firm.Id, null, null, [second.Islemler[0].Id]);
            Check(ExcelRowCount(selected.Bytes) == 1, "Selected export still contains only the chosen record");
            var selectedPdf = await exporter.AdminRaporPdfAsync(company.Id, null, null, [second.Islemler[0].Id]);
            Check(System.Text.Encoding.ASCII.GetString(selectedPdf.Bytes, 0, 4) == "%PDF",
                "A paged selection remains downloadable as a scoped PDF");
            var additional = Enumerable.Range(302, 4701).Select(i => new Ys_DevreyeAlma
            { FirmaId = firm.Id, MusteriAdi = "Load " + i, TesistatNo = "LOAD-" + i,
                AboneNo = "LOAD-SUB-" + i, DevreyeAlmaTarihi = date.AddMinutes(i) }).ToList();
            db.AddRange(additional);
            await db.SaveChangesAsync();
            db.AddRange(additional.Select((r, i) => new Ys_DevreyeAlmaSorguKaydi
            {
                Referans = Guid.NewGuid().ToString("N"), FirmaId = firm.Id, DagitimSirketiId = company.Id,
                DevreyeAlmaId = r.Id, KaynakJson = JsonSerializer.Serialize(new YsDevreyeAlmaKaynak
                { TesisatNo = r.TesistatNo!, AboneNo = r.AboneNo!, SozlesmeNo = "0009" + (i + 302).ToString("D6") })
            }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var large = await service.GecmisAsync(firm.Id, null);
            Check(large.Islemler.Count == 20 && large.Sayfalama.ToplamKayit == 5002
                && large.Sayfalama.AktarimIdleri.Count == 5001,
                "Large lists bound row payload and export metadata without truncating the count");
            var limited = false;
            try { await exporter.YetkiliServisRaporExcelAsync(firm.Id, null, null, large.Sayfalama.AktarimIdleri); }
            catch (DevreyeAlmaRaporLimitException) { limited = true; }
            Check(limited, "Oversized all-page export is explicitly rejected, never silently truncated");
            limited = false;
            try { await exporter.AdminRaporExcelAsync(company.Id, null, null, Enumerable.Range(100000, 5001).ToList()); }
            catch (DevreyeAlmaRaporLimitException) { limited = true; }
            Check(limited, "Oversized stale export IDs cannot bypass the explicit limit");

            probe.Commands.Clear();
            probe.SourceLookupIds = 0;
            var timer = Stopwatch.StartNew();
            var baseline = await BaselineSourceScanAsync(db, firm.Id, "0009005002");
            var baselineMs = timer.Elapsed.TotalMilliseconds;
            var baselineCommands = probe.Commands.Count;
            var baselineLookups = probe.SourceLookupIds;
            Check(baseline.SequenceEqual([additional[^1].Id]) && baselineLookups == 5002,
                $"The pre-optimization reference scans all 5,002 scoped JSON sources (matches={baseline.Count}, source IDs={baselineLookups})");
            probe.Commands.Clear();
            probe.SourceLookupIds = 0;
            timer.Restart();
            var fast = await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "0009005002" });
            var fastMs = timer.Elapsed.TotalMilliseconds;
            Check(fast.Islemler.Select(x => x.Id).SequenceEqual(baseline) && fast.Sayfalama.ToplamKayit == 1
                && fast.Sayfalama.AktarimIdleri.SequenceEqual(baseline),
                "Selective SQL candidates preserve counts, ordering and all-filtered export IDs");
            Check(probe.SourceLookupIds is > 0 and <= 2 && probe.Commands.Count < baselineCommands,
                "Selective searches enrich only the matching candidate and final page instead of all sources");
            Check(probe.Commands.Count(sql => sql.Contains("CROSS APPLY OPENJSON")) == 1,
                "The bounded SQL candidate probe executes once, not once per source batch");
            Console.WriteLine($"SEARCH COST 5002 rows: baseline {baselineCommands} SQL / {baselineLookups} source IDs / {baselineMs:F1} ms; optimized {probe.Commands.Count} SQL / {probe.SourceLookupIds} source IDs / {fastMs:F1} ms.");
            var numeric = await service.GecmisAsync(firm.Id, new() { Arama = "0009005002" });
            Check(numeric.Islemler.Select(x => x.Id).SequenceEqual(baseline),
                "The culturally compared toolbar search retains the same contract result");
            var installation = await service.GecmisAsync(firm.Id, new() { Arama = "5002" });
            Check(installation.Islemler.Any(x => x.Id == additional[^1].Id),
                "Numeric toolbar candidates retain installation matches");
            probe.Commands.Clear();
            var broad = await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "0009", Sayfa = 2 });
            Check(broad.Sayfalama.ToplamKayit == 4701 && broad.Islemler.Count == 20
                && broad.Sayfalama.AktarimIdleri.Count == 4701
                && probe.Commands.Count(sql => sql.Contains("CROSS APPLY OPENJSON")) == 1,
                "Broad searches retain complete results and do not repeat the SQL JSON probe");

            var sourceId = additional[^1].Id;
            var sourceRow = await db.Ys_DevreyeAlmaSorguKayitlari.SingleAsync(x => x.DevreyeAlmaId == sourceId);
            var sourceJson = sourceRow.KaynakJson;
            sourceRow.KaynakJson = sourceJson.Replace("\"SozlesmeNo\":", "\"SozlesmeNo\":\"other\",\"SozlesmeNo\":", StringComparison.Ordinal)
                .Replace("0009005002", @"\u0030\u0030\u0030\u0039\u0030\u0030\u0035\u0030\u0030\u0032", StringComparison.Ordinal);
            await db.SaveChangesAsync();
            Check((await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "0009005002" })).Islemler.Single().Id == additional[^1].Id,
                "Escaped JSON strings and duplicate keys cannot hide a valid final contract value");
            sourceRow.KaynakJson = sourceJson.Replace("\"SozlesmeNo\"", "\"\\u0053ozlesmeNo\"", StringComparison.Ordinal);
            await db.SaveChangesAsync();
            Check((await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "0009005002" })).Islemler.Single().Id == sourceId,
                "An escaped JSON property name retains its decoded contract match");
            sourceRow.KaynakJson = sourceJson.Replace("\"0009005002\"", "\"0009005002\",\"SozlesmeNo\":\"other\"", StringComparison.Ordinal);
            await db.SaveChangesAsync();
            Check((await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "0009005002" })).Islemler.Count == 0,
                "A matching earlier duplicate cannot override the final validated property value");
            sourceRow.KaynakJson = sourceJson.Replace("0009005002", "%_'", StringComparison.Ordinal);
            await db.SaveChangesAsync();
            Check((await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "%_'" })).Islemler.Single().Id == sourceId,
                "Contract punctuation is parameterized and matched literally, never as a SQL wildcard");
            sourceRow.KaynakJson = "not-json";
            await db.SaveChangesAsync();
            Check((await service.GecmisAsync(firm.Id, new() { SozlesmeNo = "0009005002" })).Islemler.Count == 0,
                "Malformed JSON cannot break or satisfy the SQL candidate probe");
            sourceRow.KaynakJson = sourceJson;
            await db.SaveChangesAsync();
            Check((await service.GecmisAsync(foreign.Id, new() { SozlesmeNo = "0009005002" })).Islemler.Count == 0,
                "The candidate optimization never widens a foreign firm's scope");
            Console.WriteLine($"{passed} commissioning pagination SQL checks passed.");
        }
        finally
        {
            if (created) await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<List<int>> BaselineSourceScanAsync(AppDbContext db, int firmId, string contract)
    {
        var query = db.Ys_DevreyeAlmalar.AsNoTracking().Where(x => x.FirmaId == firmId && !x.SilindiMi)
            .OrderByDescending(x => x.DevreyeAlmaTarihi).ThenByDescending(x => x.Id)
            .Select(x => new Ys_DevreyeAlma { Id = x.Id, FirmaId = x.FirmaId, TesistatNo = x.TesistatNo,
                AboneNo = x.AboneNo, MusteriAdi = x.MusteriAdi, Durum = x.Durum });
        var ids = new List<int>();
        for (var skip = 0; ; skip += 256)
        {
            var batch = await query.Skip(skip).Take(256).ToListAsync();
            await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, batch);
            ids.AddRange(batch.Where(x => x.SozlesmeNo?.Contains(contract, StringComparison.Ordinal) == true).Select(x => x.Id));
            if (batch.Count < 256) break;
        }
        return ids;
    }

    private static int ExcelRowCount(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Load(sheet).Descendants(ns + "row").Count() - 1;
    }

    private sealed class QueryProbe : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public int SourceLookupIds { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            var from = command.CommandText.IndexOf("FROM", StringComparison.Ordinal);
            if (from > 0 && command.CommandText[..from].Contains("[KaynakJson]", StringComparison.Ordinal))
            {
                var ids = new HashSet<int>();
                foreach (DbParameter parameter in command.Parameters)
                {
                    if (!parameter.ParameterName.StartsWith("@__ids_", StringComparison.Ordinal)
                        && !parameter.ParameterName.StartsWith("@ids", StringComparison.Ordinal)) continue;
                    if (parameter.Value is string json)
                        ids.UnionWith(JsonSerializer.Deserialize<int[]>(json)!);
                    else if (parameter.Value is int id) ids.Add(id);
                }
                SourceLookupIds += ids.Count;
            }
            return new(result);
        }
    }
}
