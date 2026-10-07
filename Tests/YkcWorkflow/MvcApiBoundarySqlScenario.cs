using System.IO.Compression;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class MvcApiBoundarySqlScenario
{
    public static async Task RunAsync()
    {
        var name = "MvcApiBoundaryTest_" + Guid.NewGuid().ToString("N");
        var commands = new ReadCommands();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(
            $@"Server=(localdb)\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(commands).Options;
        await using var db = new AppDbContext(options);
        var created = false;
        var passed = 0;
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
            passed++;
        }
        try
        {
            created = await db.Database.EnsureCreatedAsync();
            if (!created) throw new InvalidOperationException("Test database already exists.");
            var primary = new Dag_Sirket { SirketAdi = "Çorumgaz" };
            var secondary = new Dag_Sirket { SirketAdi = "SürmeliGAZ" };
            var admin = new AppKullanici { UserName = "admin", KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin };
            db.AddRange(primary, secondary, admin);
            await db.SaveChangesAsync();
            for (var i = 1; i <= 50; i++)
            {
                var user = new AppKullanici
                {
                    UserName = "person" + i, AdSoyad = $"Personel {i:00}", Email = $"person{i}@fixture.test",
                    KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = i % 2 == 0 ? secondary.Id : primary.Id
                };
                db.Users.Add(user);
                db.Dag_PersonelYetkiler.Add(new()
                {
                    KullaniciId = user.Id, SirketId = user.SirketId.Value,
                    YetkiTipi = i % 5 == 0 ? YetkiTipleri.TAM_YETKI : YetkiTipleri.YKC_TALEP_GOR
                });
            }
            await db.SaveChangesAsync();
            var permissions = new AdminPersonelYetkiApiService(db);
            var page = await permissions.ListeleAsync(admin, null, true);
            Check(page.Personeller.Count == 10 && page.Personeller[0].AdSoyad == "Personel 01"
                && page.Ozet.ToplamPersonel == 50 && page.Ozet.ToplamSayfa == 5,
                "API pages fifty personnel and computes the unfiltered total");
            Check(page.Ozet.YetkiliPersonel == 50 && page.Ozet.TamYetkiAtamalari == 10
                && page.Ozet.Sirketler.Count == 2 && page.SirketYetkileri.Count == 10,
                "API overview includes all pages but returns grants only for visible personnel");
            page = await permissions.ListeleAsync(admin, null, true, new() { Sayfa = int.MaxValue });
            Check(page.Ozet.Sayfa == 5 && page.Personeller[0].AdSoyad == "Personel 41", "API clamps an excessive page");
            page = await permissions.ListeleAsync(admin, null, true, new() { Sayfa = -1 });
            Check(page.Ozet.Sayfa == 1, "API clamps a negative page");
            page = await permissions.ListeleAsync(admin, null, true, new() { Q = "  person49@fixture.test  ", Sayfa = 5 });
            Check(page.Personeller.Single().AdSoyad == "Personel 49" && page.Ozet.EslesenPersonel == 1
                && page.Ozet.ToplamPersonel == 50 && page.Ozet.Sayfa == 1 && page.Ozet.Arama == "person49@fixture.test",
                "API searches all personnel and normalizes search/page without changing overview totals");
            page = await permissions.ListeleAsync(admin, null, true, new() { Q = "PERSONEL 03" });
            Check(page.Personeller.Single().AdSoyad == "Personel 03", "API name search is case insensitive");
            page = await permissions.ListeleAsync(admin, null, true, new() { Q = "sürmeligaz" });
            Check(page.Ozet.EslesenPersonel == 25 && page.Ozet.ToplamSayfa == 3, "API company search respects Turkish casing");
            page = await permissions.ListeleAsync(admin, primary.Id, true, new() { Q = "sürmeligaz" });
            Check(page.Personeller.Count == 0 && page.Ozet.ToplamPersonel == 25 && page.Ozet.Sayfa == 1
                && page.Ozet.Sirketler.All(x => x.SirketId == primary.Id), "Company filtering precedes API search and counters");
            var staff = await db.Users.SingleAsync(x => x.UserName == "person1");
            Check((await permissions.ListeleAsync(staff, primary.Id, false)).Personeller.Count == 0,
                "Personnel cannot gain permission-management rights through API search");

            var firm = new Ys_Firma { FirmaAdi = "AllowedFirm", SirketId = primary.Id };
            var foreign = new Ys_Firma { FirmaAdi = "ForeignFirm", SirketId = secondary.Id };
            var deleted = new Ys_Firma { FirmaAdi = "DeletedFirm", SirketId = primary.Id, SilindiMi = true };
            db.AddRange(firm, foreign, deleted);
            await db.SaveChangesAsync();
            var today = DateTime.Today;
            db.Ys_DevreyeAlmalar.AddRange(
                new() { FirmaId = firm.Id, MusteriAdi = "IŞIL İNCE", MusteriTelefon = "05551234567", AboneNo = "009876",
                    DevreyeAlmaTarihi = today, Durum = DevreyeAlmaDurumDegerleri.Tamamlandi },
                new() { FirmaId = foreign.Id, MusteriAdi = "IŞIL İNCE", DevreyeAlmaTarihi = today },
                new() { FirmaId = deleted.Id, MusteriAdi = "IŞIL İNCE", DevreyeAlmaTarihi = today });
            await db.SaveChangesAsync();
            var reports = new AdminRaporApiService(db);
            foreach (var query in new[] { " ışıl ince ", "0555123", "009876" })
                Check((await reports.DevreyeAlmalarAsync(new() { Musteri = query }, primary.Id)).Islemler.Count == 1,
                    "Customer filter runs in SQL and preserves company/deletion scope: " + query);

            Ys_YetkiBelgesi Certificate(Ys_Firma owner, int state, DateTime expiry, DateTime? decision = null) => new()
            {
                FirmaId = owner.Id, Durum = state, YetkiBelgesiBitisTarihi = expiry,
                OlusturmaTarihi = today, OnayTarihi = decision
            };
            var pending = Certificate(firm, 0, today.AddMonths(1));
            var expired = Certificate(firm, 0, today.AddDays(-1));
            var approved = Certificate(firm, 1, today.AddMonths(1), today.AddHours(23).AddMinutes(59));
            approved.OlusturmaTarihi = today.AddMonths(-2);
            db.Ys_YetkiBelgeleri.AddRange(pending, expired, approved,
                Certificate(firm, 2, today.AddMonths(1), today),
                Certificate(foreign, 0, today.AddMonths(1)), Certificate(deleted, 0, today.AddMonths(1)));
            await db.SaveChangesAsync();
            var certificates = new AdminYetkiBelgesiOnayApiService(db);
            var lists = await certificates.ListeleAsync(primary.Id);
            Check(lists.Bekleyenler.Single().Id == pending.Id && lists.SuresiDolanlar.Single().Id == expired.Id,
                "API alone classifies active versus expired pending certificates");
            foreach (var type in new[] { "bekleyen", "onayli", "reddedilen" })
            {
                var file = (await certificates.RaporAsync(new()
                {
                    Tip = type, BaslangicTarihi = today, BitisTarihi = today
                }, primary.Id, true))!.Value;
                using var zip = new ZipArchive(new MemoryStream(file.Bytes));
                var xml = string.Join("", zip.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.Ordinal))
                    .Select(x => { using var reader = new StreamReader(x.Open()); return reader.ReadToEnd(); }));
                Check(xml.Contains("AllowedFirm") && !xml.Contains("ForeignFirm") && !xml.Contains("DeletedFirm"),
                    "API certificate export retains date/status/company scope: " + type);
            }
            var pdf = await certificates.RaporAsync(new() { Tip = "onayli", BaslangicTarihi = today.AddDays(1), BitisTarihi = today }, primary.Id, false);
            Check(pdf is { } value && value.Bytes.Take(4).SequenceEqual(new byte[] { 37, 80, 68, 70 }),
                "API normalizes reversed dates and produces the certificate PDF");
            Check(await certificates.RaporAsync(new() { Tip = "onayli", BaslangicTarihi = today.AddDays(1), BitisTarihi = today.AddDays(2) }, primary.Id, true) == null,
                "An empty API export cannot silently fall back to unfiltered records");
            try
            {
                await certificates.RaporAsync(new() { Tip = "invalid" }, primary.Id, true);
                throw new InvalidOperationException("Expected invalid type rejection");
            }
            catch (ArgumentException) { Check(true, "API validates export type without MVC"); }

            AdminPanelApiController Controller(AppKullanici user) => new(db, new AdminDashboardService(db),
                new AdminYetkiliServisListeService(db), new AdminYetkiliServisYonetimApiService(db,
                    new SehirFirmaKoduService(new ConfigurationBuilder().Build(), db)), null!,
                reports, certificates, permissions, null!, new AdminKullaniciOkumaApiService(db), null!)
            {
                ControllerContext = new()
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "Fixture"))
                    }
                }
            };
            var filter = new YetkiBelgesiRaporFiltre { SirketId = primary.Id, Tip = "bekleyen" };
            Check(await Controller(staff).YetkiBelgesiRaporPdf(filter) is ForbidResult, "Export endpoint rejects personnel without report rights");
            db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = primary.Id, YetkiTipi = YetkiTipleri.RAPOR_GOR });
            await db.SaveChangesAsync();
            Check(await Controller(staff).YetkiBelgesiRaporExcel(filter) is ForbidResult,
                "General report permission cannot expose certificate approval data");
            db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = primary.Id, YetkiTipi = YetkiTipleri.YETKI_BELGESI_ONAY });
            await db.SaveChangesAsync();
            Check(await Controller(staff).YetkiBelgesiRaporExcel(filter) is FileContentResult,
                "Authorized personnel receive the API-generated certificate file");
            filter.SirketId = secondary.Id;
            Check(await Controller(staff).YetkiBelgesiRaporPdf(filter) is ForbidResult,
                "Changing the export company cannot bypass API authorization");
            filter.SirketId = primary.Id;
            using var manager = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db), Options.Create(new IdentityOptions()),
                new PasswordHasher<AppKullanici>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
                NullLogger<UserManager<AppKullanici>>.Instance);
            var dashboardService = new PersonelDashboardApiService(db, new AdminDashboardService(db),
                new YkcTalepOkumaService(db), new YkcYetkiService(db, manager));
            var dashboard = (PersonelDashboardDto)((OkObjectResult)await Controller(staff)
                .PersonelDashboard(new() { SirketId = primary.Id }, dashboardService)).Value!;
            Check(dashboard.BelgeYetkisi && dashboard.RaporYetkisi && dashboard.OnayBekleyen == 1
                && dashboard.ToplamDevreyeAlma == 1 && dashboard.BuAyDevreyeAlma == 1,
                "Personnel dashboard counts in SQL retain company, expiry and deleted-firm scope");
            Check(dashboard.Ykc != null && !dashboard.ServisYetkisi && dashboard.AktifServis == 0,
                "Dashboard returns only data for granted modules");
            Check(dashboard.AyBaslangici == new DateTime(today.Year, today.Month, 1)
                && dashboard.AyBitisi == dashboard.AyBaslangici.AddMonths(1).AddDays(-1),
                "Dashboard month links and counts use the API date interval");
            Check(await Controller(staff).PersonelDashboard(new() { SirketId = secondary.Id }, dashboardService) is ForbidResult,
                "Personnel cannot select an unauthorized company for dashboard aggregates");
            db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = secondary.Id, YetkiTipi = YetkiTipleri.YETKI_BELGESI_ONAY });
            await db.SaveChangesAsync();
            dashboard = (PersonelDashboardDto)((OkObjectResult)await Controller(staff)
                .PersonelDashboard(new() { SirketId = secondary.Id }, dashboardService)).Value!;
            Check(dashboard.BelgeYetkisi && dashboard.OnayBekleyen == 1 && !dashboard.RaporYetkisi
                && dashboard.ToplamDevreyeAlma == 0 && dashboard.Ykc == null && !dashboard.YkcYetkileri.TalepleriGorebilir,
                "Switching to a certificate-only company never carries YKC or reporting data across companies");
            var reportOnly = new AppKullanici { UserName = "dashboard-report-only", KullaniciTipi = KullaniciTipiDegerleri.Personel };
            db.Users.Add(reportOnly);
            db.Dag_PersonelYetkiler.Add(new() { KullaniciId = reportOnly.Id, SirketId = primary.Id, YetkiTipi = YetkiTipleri.YKC_RAPOR_GOR });
            await db.SaveChangesAsync();
            dashboard = (PersonelDashboardDto)((OkObjectResult)await Controller(reportOnly)
                .PersonelDashboard(new() { SirketId = primary.Id }, dashboardService)).Value!;
            Check(dashboard.YkcYetkileri.RaporlariGorebilir && !dashboard.YkcYetkileri.TalepleriGorebilir
                && dashboard.Ykc == null && dashboard.OnayBekleyen == 0,
                "Report-only personnel receive no request dashboard or certificate counts");
            var companyAdmin = new AppKullanici { UserName = "dashboard-company-admin", SirketId = primary.Id, KullaniciTipi = KullaniciTipiDegerleri.SirketAdmin };
            db.Users.Add(companyAdmin);
            db.Users.Add(new() { UserName = "dashboard-service", FirmaId = firm.Id, KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis });
            db.Users.Add(new() { UserName = "dashboard-foreign-service", FirmaId = foreign.Id, KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis });
            await db.SaveChangesAsync();
            dashboard = (PersonelDashboardDto)((OkObjectResult)await Controller(companyAdmin)
                .PersonelDashboard(new() { SirketId = primary.Id }, dashboardService)).Value!;
            Check(dashboard.AktifServis == 1 && dashboard.ToplamDevreyeAlma == 1 && dashboard.YkcYetkileri.AtamaYapabilir,
                "Company administrators get scoped operational aggregates");
            Check(await Controller(companyAdmin).PersonelDashboard(new() { SirketId = secondary.Id }, dashboardService) is ForbidResult,
                "Company administrators cannot change the dashboard company by posting a different ID");
            dashboard = (PersonelDashboardDto)((OkObjectResult)await Controller(admin)
                .PersonelDashboard(new(), dashboardService)).Value!;
            Check(dashboard.ToplamDevreyeAlma == 2 && dashboard.AktifServis == 2,
                "Only a general administrator may request all-company dashboard totals");
            var counts = (PanelBildirimOzeti)((OkObjectResult)await Controller(staff)
                .BildirimOzeti(new() { SirketId = primary.Id })).Value!;
            Check(counts.OnayBekleyen == 1, "Lightweight notification counts exclude expired and deleted-company records");
            Check(await Controller(companyAdmin).BildirimOzeti(new() { SirketId = secondary.Id }) is ForbidResult,
                "Notification endpoint rejects another company's scope");
            counts = (PanelBildirimOzeti)((OkObjectResult)await Controller(reportOnly)
                .BildirimOzeti(new() { SirketId = primary.Id })).Value!;
            Check(counts.OnayBekleyen == 0 && counts.SuresiBitecek == 0,
                "Report-only personnel cannot obtain certificate notification counts");
            var aggregateService = new AdminDashboardService(db);
            commands.Sql.Clear();
            await aggregateService.BildirimOzetiAsync(primary.Id);
            Check(commands.Sql.Count == 2 && commands.Sql.All(x => x.Contains("COUNT(")),
                "Navbar service executes only two aggregate queries, not recent-record queries");
            db.ChangeTracker.Clear();
            var full = (AdminDashboardApiDto)((OkObjectResult)await Controller(companyAdmin)
                .Dashboard(new() { SirketId = primary.Id })).Value!;
            Check(full.ToplamFirma == 1 && full.ToplamDevreyeAlma == 1 && full.OnayBekleyen == 1
                && full.SonYetkiBelgeleri.Count == 4 && full.SonDevreyeAlmalar.Single().FirmaAdi == "AllowedFirm"
                && full.SonYetkiBelgeleri.All(x => x.SirketAdi == "Çorumgaz"),
                "Shared dashboard contract preserves scoped statistics and flattened recent-record values");
            Check(!db.ChangeTracker.Entries<Ys_YetkiBelgesi>().Any() && !db.ChangeTracker.Entries<Ys_DevreyeAlma>().Any(),
                "Dashboard projects DTOs in SQL instead of loading complete record entities");
            Check(await Controller(reportOnly).Dashboard(new() { SirketId = primary.Id }) is ForbidResult
                && await Controller(staff).Dashboard(new() { SirketId = primary.Id }) is ForbidResult,
                "Personnel cannot bypass module permissions through the administrative dashboard");
            Check(await Controller(companyAdmin).Dashboard(new() { SirketId = secondary.Id }) is ForbidResult,
                "Administrative dashboard retains company isolation");

            foreach (var asPdf in new[] { true, false })
            {
                var exportFilter = new AdminYetkiliServisGetirFiltreDto { Id = firm.Id, SirketId = primary.Id };
                var controller = Controller(companyAdmin);
                Task<IActionResult> Export(AdminYetkiliServisGetirFiltreDto? request) => asPdf
                    ? controller.YetkiliServisPdf(request) : controller.YetkiliServisExcel(request);
                var exported = (FileContentResult)await Export(exportFilter);
                Check(exported.FileDownloadName == $"Yetkili_Servis_{firm.Id}.{(asPdf ? "pdf" : "xlsx")}"
                    && controller.Response.Headers.CacheControl == "private, no-store",
                    "API owns service-record export naming and private caching: " + asPdf);
                if (asPdf)
                    Check(exported.ContentType == "application/pdf" && exported.FileContents.Take(4).SequenceEqual(new byte[] { 37, 80, 68, 70 }),
                        "Service-record API produces the existing PDF format");
                else
                {
                    using var zip = new ZipArchive(new MemoryStream(exported.FileContents));
                    var xml = string.Join("", zip.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.Ordinal))
                        .Select(x => { using var reader = new StreamReader(x.Open()); return reader.ReadToEnd(); }));
                    Check(xml.Contains("AllowedFirm") && !xml.Contains("ForeignFirm"),
                        "Service-record Excel is generated from the scoped API record");
                }
                Check(await Export(null) is BadRequestObjectResult && await Export(new() { Id = 0 }) is BadRequestObjectResult,
                    "API validates service export input without MVC: " + asPdf);
                exportFilter.Id = foreign.Id;
                Check(await Export(exportFilter) is NotFoundObjectResult, "Foreign service ID cannot widen export scope: " + asPdf);
                exportFilter.SirketId = secondary.Id;
                Check(await Export(exportFilter) is ForbidResult, "Foreign company ID cannot widen export scope: " + asPdf);
                Check((asPdf ? await Controller(reportOnly).YetkiliServisPdf(new() { Id = firm.Id, SirketId = primary.Id })
                    : await Controller(reportOnly).YetkiliServisExcel(new() { Id = firm.Id, SirketId = primary.Id })) is ForbidResult,
                    "Report permission does not grant firm-management exports: " + asPdf);
            }
            staff = await db.Users.SingleAsync(x => x.Id == staff.Id);
            staff.AktifMi = false;
            await db.SaveChangesAsync();
            Check(await Controller(staff).YetkiBelgesiRaporPdf(filter) is ForbidResult, "Inactive accounts cannot export certificate data");
            Check(await Controller(staff).PersonelDashboard(new() { SirketId = primary.Id }, dashboardService) is UnauthorizedResult,
                "Inactive personnel cannot obtain dashboard data");
            Check(await Controller(staff).BildirimOzeti(new() { SirketId = primary.Id }) is UnauthorizedResult
                && await Controller(staff).YetkiliServisPdf(new() { Id = firm.Id, SirketId = primary.Id }) is UnauthorizedResult,
                "Inactive accounts cannot obtain notification counts or service-record exports");

            var ownBranch = new Ys_Sube { FirmaId = firm.Id, SubeAdi = "Selected branch", Il = "Çorum", AktifMi = true };
            var sibling = new Ys_Sube { FirmaId = firm.Id, SubeAdi = "Unrequested branch", AktifMi = true };
            var foreignBranch = new Ys_Sube { FirmaId = foreign.Id, SubeAdi = "Foreign branch", AktifMi = true };
            var deletedBranch = new Ys_Sube { FirmaId = firm.Id, SubeAdi = "Deleted branch", SilindiMi = true };
            var deletedFirmBranch = new Ys_Sube { FirmaId = deleted.Id, SubeAdi = "Deleted firm branch" };
            db.AddRange(ownBranch, sibling, foreignBranch, deletedBranch, deletedFirmBranch);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var branchService = new YetkiliServisPanelYonetimApiService(db);
            var branchUser = new AppKullanici
            {
                FirmaId = firm.Id, AktifMi = true, KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis
            };
            commands.Sql.Clear();
            var selectedBranch = await branchService.SubeGetirAsync(ownBranch.Id, branchUser);
            Check(selectedBranch is { SubeAdi: "Selected branch", FirmaAdi: "AllowedFirm" }
                && selectedBranch.FirmaId == firm.Id && commands.Sql.Count == 1,
                "Branch lookup projects only the selected scoped branch in a single SQL query");
            Check(!db.ChangeTracker.Entries<Ys_Sube>().Any() && !db.ChangeTracker.Entries<Ys_Firma>().Any(),
                "Branch lookup does not load tracked branches or the full firm profile");
            Check(await branchService.SubeGetirAsync(foreignBranch.Id, branchUser) == null
                && await branchService.SubeGetirAsync(deletedBranch.Id, branchUser) == null
                && await branchService.SubeGetirAsync(int.MaxValue, branchUser) == null,
                "Foreign, deleted and missing branch IDs return no data");
            branchUser.FirmaId = deleted.Id;
            Check(await branchService.SubeGetirAsync(deletedFirmBranch.Id, branchUser) == null,
                "Deleted firm scope cannot expose a branch");
            branchUser.FirmaId = firm.Id;
            branchUser.AktifMi = false;
            Check(await branchService.SubeGetirAsync(ownBranch.Id, branchUser) == null,
                "Inactive service accounts cannot read a branch");
            branchUser.AktifMi = true;
            branchUser.KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma;
            Check(await branchService.SubeGetirAsync(ownBranch.Id, branchUser) == null,
                "Certified-firm accounts cannot reuse the service branch lookup");


            var inactiveBranch = new Ys_Sube { FirmaId = firm.Id, SubeAdi = "A inactive branch", AktifMi = false };
            db.Add(inactiveBranch);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            branchUser.KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis;
            commands.Sql.Clear();
            var branches = await branchService.SubelerAsync(branchUser);
            Check(branches.Select(x => x.Id).SequenceEqual(new[] { ownBranch.Id, sibling.Id, inactiveBranch.Id })
                && commands.Sql.Count == 1 && branches.All(x => x.FirmaId == firm.Id),
                "Branch list returns only scoped undeleted branches, ordered active first, in one query");
            Check(!db.ChangeTracker.Entries<Ys_Firma>().Any() && !db.ChangeTracker.Entries<Ys_Sube>().Any()
                && !commands.Sql[0].Contains("Ys_YetkiBelgeleri") && !commands.Sql[0].Contains("Ys_DevreyeAlmalar"),
                "Branch list does not fetch the full firm profile, documents or commissioning records");
            branchUser.AktifMi = false;
            Check((await branchService.SubelerAsync(branchUser)).Count == 0, "Inactive accounts cannot list service branches");
            branchUser.AktifMi = true;
            branchUser.FirmaId = deleted.Id;
            Check((await branchService.SubelerAsync(branchUser)).Count == 0, "Deleted firms cannot list service branches");

            var activeBrand = new Ys_Marka { MarkaAdi = "A active brand", AktifMi = true };
            var selectedInactive = new Ys_Marka { MarkaAdi = "B selected inactive", AktifMi = false };
            var otherInactive = new Ys_Marka { MarkaAdi = "C other inactive", AktifMi = false };
            var deletedBrand = new Ys_Marka { MarkaAdi = "D deleted brand", AktifMi = true, SilindiMi = true };
            var category = new UrunKategori { Ad = "Selected category", SiraNo = 2 };
            var firstCategory = new UrunKategori { Ad = "First category", SiraNo = 1 };
            var inactiveCategory = new UrunKategori { Ad = "Hidden category", AktifMi = false };
            db.AddRange(activeBrand, selectedInactive, otherInactive, deletedBrand, category, firstCategory, inactiveCategory);
            await db.SaveChangesAsync();
            db.Ys_FirmaMarkalar.AddRange(
                new() { FirmaId = firm.Id, MarkaId = selectedInactive.Id, YetkiBitisTarihi = today.AddYears(1) },
                new() { FirmaId = firm.Id, MarkaId = deletedBrand.Id, YetkiBitisTarihi = today.AddYears(1) },
                new() { FirmaId = firm.Id, MarkaId = activeBrand.Id, SilindiMi = true, YetkiBitisTarihi = today });
            db.Ys_FirmaKategoriler.Add(new() { FirmaId = firm.Id, KategoriId = category.Id, YetkiBitisTarihi = today.AddYears(1) });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var editorService = new AdminYetkiliServisYonetimApiService(db,
                new SehirFirmaKoduService(new ConfigurationBuilder().Build(), db));
            commands.Sql.Clear();
            var editor = (await editorService.EditorAsync(firm.Id, primary.Id))!;
            Check(editor.Servis.FirmaAdi == "AllowedFirm" && editor.Servis.SirketId == primary.Id
                && editor.SeciliMarkaIds.SequenceEqual(new[] { selectedInactive.Id })
                && editor.SeciliKategoriIds.SequenceEqual(new[] { category.Id }),
                "API editor preserves scoped firm values and existing selected authorizations");
            Check(editor.Markalar.Select(x => x.Id).SequenceEqual(new[] { activeBrand.Id, selectedInactive.Id })
                && !editor.Markalar.Single(x => x.Id == selectedInactive.Id).AktifMi
                && editor.Kategoriler.Select(x => x.Id).SequenceEqual(new[] { firstCategory.Id, category.Id }),
                "API editor includes selected inactive brands, hides unrelated inactive/deleted choices and orders categories");
            Check(commands.Sql.All(x => !x.Contains("Ys_YetkiBelgeleri") && !x.Contains("Ys_Subeler") && !x.Contains("Ys_DevreyeAlmalar"))
                && !db.ChangeTracker.Entries<Ys_Firma>().Any(),
                "API editor loads only form data without tracked firms, documents, branches or transactions");
            var blankEditor = (await editorService.EditorAsync(0, primary.Id))!;
            Check(blankEditor.Servis.Id == 0 && blankEditor.Servis.AktifMi && blankEditor.SeciliMarkaIds.Count == 0
                && blankEditor.Markalar.Single().Id == activeBrand.Id,
                "New firm editor receives active defaults and valid choices directly from API");
            Check(await editorService.EditorAsync(foreign.Id, primary.Id) == null
                && await editorService.EditorAsync(deleted.Id, primary.Id) == null
                && await editorService.EditorAsync(int.MaxValue, primary.Id) == null,
                "API editor cannot expose foreign, deleted or missing firms");
            Check(await Controller(companyAdmin).YetkiliServisEditor(new() { Id = firm.Id, SirketId = primary.Id }) is OkObjectResult
                && await Controller(companyAdmin).YetkiliServisEditor(new() { Id = 0, SirketId = primary.Id }) is OkObjectResult,
                "Authorized company administrators can request edit and create forms");
            Check(await Controller(reportOnly).YetkiliServisEditor(new() { Id = firm.Id, SirketId = primary.Id }) is ForbidResult
                && await Controller(companyAdmin).YetkiliServisEditor(new() { Id = firm.Id, SirketId = secondary.Id }) is ForbidResult,
                "Editor endpoint does not grant management rights to report-only or foreign-company accounts");
            Check(await Controller(companyAdmin).YetkiliServisEditor(new() { Id = foreign.Id, SirketId = primary.Id }) is NotFoundResult
                && await Controller(companyAdmin).YetkiliServisEditor(null) is BadRequestResult,
                "Editor endpoint rejects foreign IDs and invalid requests");
            Check(await Controller(staff).YetkiliServisEditor(new() { Id = firm.Id, SirketId = primary.Id }) is UnauthorizedResult,
                "Inactive accounts cannot obtain firm editor data");

            await ServisEkraniVeKullaniciBilgileriAsync(db, commands, Check);
            await PersonelRaporKurallariAsync(db, Check);
            Console.WriteLine($"{passed} MVC/API boundary SQL checks passed.");
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (created) await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task ServisEkraniVeKullaniciBilgileriAsync(AppDbContext db, ReadCommands commands, Action<bool, string> check)
    {
        var company = new Dag_Sirket { SirketAdi = "Screen owner" };
        var foreignCompany = new Dag_Sirket { SirketAdi = "Foreign screen owner" };
        var firm = new Ys_Firma { FirmaAdi = "Fallback firm", YetkiliKisi = " ", Email = "fallback@fixture.test", Telefon = "05559999999", Sirket = company };
        var foreign = new Ys_Firma { FirmaAdi = "Foreign calendar firm", Sirket = foreignCompany };
        var incomplete = new AppKullanici { UserName = "incomplete-screen-user", AdSoyad = " ", Email = "", PhoneNumber = null,
            Firma = firm, KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis };
        var complete = new AppKullanici { UserName = "complete-screen-user", AdSoyad = "Account name", Email = "account@fixture.test", PhoneNumber = "05558888888",
            Firma = firm, KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma };
        var foreignUser = new AppKullanici { UserName = "foreign-screen-user", Firma = foreign, KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis };
        db.AddRange(incomplete, complete, foreignUser);
        await db.SaveChangesAsync();
        var users = new AdminKullaniciOkumaApiService(db);
        var rows = await users.ListeleAsync(null, company.Id, false);
        var row = rows.Single(x => x.Id == incomplete.Id);
        check(rows.Count == 2 && row.AdSoyad == firm.FirmaAdi && row.Email == firm.Email && row.PhoneNumber == firm.Telefon,
            "API completes blank user fields, skips whitespace contact names and preserves company isolation");
        row = rows.Single(x => x.Id == complete.Id);
        check(row.AdSoyad == complete.AdSoyad && row.Email == complete.Email && row.PhoneNumber == complete.PhoneNumber,
            "Existing account fields take precedence over firm fields");
        foreach (var query in new[] { "Fallback firm", "fallback@", "0555999" })
            check((await users.ListeleAsync(new() { Q = query }, company.Id, false)).Single().Id == incomplete.Id,
                "API searches the completed display values: " + query);
        firm.YetkiliKisi = "Firm contact";
        await db.SaveChangesAsync();
        check((await users.ListeleAsync(new() { Tip = "Servis", Durum = "Aktif", Bagli = "Fallback" }, company.Id, false))
            .Single().AdSoyad == "Firm contact", "API prefers firm contact over firm name and retains list filters");
        check((await users.ListeleAsync(null, null, false)).Count == 0, "A missing company scope never exposes user data");
        var unchanged = await db.Users.AsNoTracking().SingleAsync(x => x.Id == incomplete.Id);
        check(unchanged.AdSoyad == " " && unchanged.Email == "" && unchanged.PhoneNumber == null,
            "Display completion does not update stored account data");

        var month = new DateTime(2024, 2, 1);
        var records = Enumerable.Range(0, 31).Select(i => new Ys_DevreyeAlma
        {
            FirmaId = firm.Id, MusteriAdi = "Calendar customer " + i, Adres = "Calendar address", CihazTipi = "Kombi",
            DevreyeAlmaTarihi = month.AddDays(i % 29), OlusturmaTarihi = month.AddDays(i), Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
        }).ToList();
        records[30].DevreyeAlmaTarihi = month.AddMonths(1).AddSeconds(-1);
        db.Ys_DevreyeAlmalar.AddRange(records);
        db.Ys_DevreyeAlmalar.AddRange(
            new() { FirmaId = firm.Id, DevreyeAlmaTarihi = month.AddSeconds(-1), OlusturmaTarihi = month.AddDays(40) },
            new() { FirmaId = firm.Id, DevreyeAlmaTarihi = month.AddMonths(1), OlusturmaTarihi = month.AddDays(41) },
            new() { FirmaId = firm.Id, DevreyeAlmaTarihi = month, SilindiMi = true, OlusturmaTarihi = month.AddDays(42) },
            new() { FirmaId = foreign.Id, DevreyeAlmaTarihi = month, OlusturmaTarihi = month.AddDays(43) });
        await db.SaveChangesAsync();
        var service = new YetkiliServisPanelOkumaApiService(db, new YetkiliServisIlkKurulumService(db),
            NullLogger<YetkiliServisPanelOkumaApiService>.Instance);
        var filter = new YsPanelDashboardFiltreDto { TakvimTarih = month.AddDays(11).AddHours(17), TakvimGorunum = "gun" };
        var screen = await service.DashboardAsync(firm.Id, filter);
        check(screen.TakvimVerisiTam && screen.TakvimIslemleri.Count == 31 && screen.SonIslemler.Count == 5
            && screen.TakvimIslemleri.All(x => x.FirmaId == firm.Id)
            && screen.TakvimIslemleri.Select(x => x.Id).ToHashSet().SetEquals(records.Select(x => x.Id)),
            "Single API response includes all 31 selected-month records, excluding deleted, foreign and adjacent-month rows");
        check(screen.TakvimTarih == filter.TakvimTarih.Value.Date && screen.TakvimGorunum == "gun"
            && screen.TakvimIslemleri[0].DevreyeAlmaTarihi == month.AddMonths(1).AddSeconds(-1)
            && screen.TakvimIslemleri.All(x => x.Adres == "Calendar address" && x.CihazTipi == "Kombi"),
            "API normalizes calendar date and preserves ordering, address and device fields");
        var today = DateTime.Today;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        check(screen.BuAy == await db.Ys_DevreyeAlmalar.CountAsync(x => x.FirmaId == firm.Id && !x.SilindiMi
            && x.DevreyeAlmaTarihi >= currentMonth && x.DevreyeAlmaTarihi < currentMonth.AddMonths(1)) && screen.Toplam == 33,
            "Selected calendar month does not alter current-month and all-time dashboard totals");
        foreach (var invalidDate in new[] { DateTime.MinValue, DateTime.MaxValue })
        {
            var normalized = await service.DashboardAsync(firm.Id, new() { TakvimTarih = invalidDate, TakvimGorunum = "invalid" });
            check(normalized.TakvimTarih == today && normalized.TakvimGorunum == "ay", "API rejects out-of-range calendar dates and modes");
        }
        var empty = await service.DashboardAsync(firm.Id, new() { TakvimTarih = month.AddYears(1), TakvimGorunum = "yil" });
        check(empty.TakvimVerisiTam && empty.TakvimIslemleri.Count == 0 && empty.TakvimGorunum == "yil",
            "A genuinely empty month is complete, not a calendar failure");
        commands.CalendarFailure = new TimeoutException("Isolated calendar query failure");
        try
        {
            var partial = await service.DashboardAsync(firm.Id, filter);
            check(!partial.TakvimVerisiTam && partial.Toplam == 33 && partial.TakvimIslemleri.Count == 3
                && partial.TakvimIslemleri.Select(x => x.Id).SequenceEqual(screen.SonIslemler
                    .Where(x => x.DevreyeAlmaTarihi >= month && x.DevreyeAlmaTarihi < month.AddMonths(1)).Select(x => x.Id)),
                "API handles calendar failure using only in-month recent records and marks the response incomplete");
            commands.CalendarFailure = new OperationCanceledException("Isolated cancellation");
            try
            {
                await service.DashboardAsync(firm.Id, filter);
                throw new InvalidOperationException("Cancellation must propagate");
            }
            catch (OperationCanceledException) { check(true, "Calendar cancellation is not disguised as partial success"); }
        }
        finally { commands.CalendarFailure = null; }
    }

    private static async Task PersonelRaporKurallariAsync(AppDbContext db, Action<bool, string> check)
    {
        var company = new Dag_Sirket { SirketAdi = "Personnel report A" };
        var other = new Dag_Sirket { SirketAdi = "Personnel report B" };
        var firm = new Ys_Firma { Sirket = company, FirmaAdi = "Report firm A" };
        var foreign = new Ys_Firma { Sirket = other, FirmaAdi = "Report firm B" };
        var staff = new AppKullanici { UserName = "report-boundary-staff", Sirket = company, KullaniciTipi = KullaniciTipiDegerleri.Personel };
        var companyAdmin = new AppKullanici { UserName = "report-boundary-admin", Sirket = company, KullaniciTipi = KullaniciTipiDegerleri.SirketAdmin };
        var admin = new AppKullanici { UserName = "report-boundary-global", KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin };
        db.AddRange(firm, foreign, staff, companyAdmin, admin);
        await db.SaveChangesAsync();
        var month = new DateTime(2026, 9, 1);
        var end = month.AddMonths(1).AddDays(-1);
        var grant = new Dag_PersonelYetki { KullaniciId = staff.Id, SirketId = company.Id, YetkiTipi = YetkiTipleri.RAPOR_GOR };
        db.Dag_PersonelYetkiler.Add(grant);
        db.Ys_DevreyeAlmalar.AddRange(
            new() { FirmaId = firm.Id, DevreyeAlmaTarihi = end.AddHours(23), CihazMarka = "Fixture brand", Durum = DevreyeAlmaDurumDegerleri.Tamamlandi },
            new() { FirmaId = foreign.Id, DevreyeAlmaTarihi = month },
            new() { FirmaId = firm.Id, DevreyeAlmaTarihi = month.AddDays(-1) },
            new() { FirmaId = firm.Id, DevreyeAlmaTarihi = month, SilindiMi = true });
        db.Ykc_Talepler.AddRange(
            new() { FirmaId = firm.Id, SirketId = company.Id, TalepTarihi = month, AtananEkip = "Fixture team" },
            new() { FirmaId = firm.Id, SirketId = company.Id, TalepTarihi = end.AddHours(23), Durum = YkcDurumDegerleri.Tamamlandi },
            new() { FirmaId = foreign.Id, SirketId = other.Id, TalepTarihi = month },
            new() { FirmaId = firm.Id, SirketId = company.Id, TalepTarihi = month.AddMonths(1) },
            new() { FirmaId = firm.Id, SirketId = company.Id, TalepTarihi = month, SilindiMi = true });
        foreach (var status in new[] { 0, 1, 2 })
            db.Ys_YetkiBelgeleri.Add(new()
            {
                FirmaId = firm.Id, Durum = status, OlusturmaTarihi = month, OnayTarihi = status == 0 ? null : end.AddHours(23),
                YetkiBelgesiBitisTarihi = DateTime.Today.AddYears(1)
            });
        db.Ys_YetkiBelgeleri.Add(new() { FirmaId = firm.Id, Durum = 0, OlusturmaTarihi = month, YetkiBelgesiBitisTarihi = DateTime.Today.AddDays(-1) });
        await db.SaveChangesAsync();
        var reports = new PersonelRaporApiService(db, new AdminRaporApiService(db), new YkcTalepOkumaService(db));
        AdminPanelApiController Controller(AppKullanici actor) => new(db, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id)], "Fixture"))
            } }
        };
        PersonelRaporFiltreDto Filter(string? type = null, int? scope = null) => new()
        {
            SirketId = scope ?? company.Id, Tip = type, BaslangicTarihi = month, BitisTarihi = end
        };
        static PersonelRaporDto Body(IActionResult result) => (PersonelRaporDto)((OkObjectResult)result).Value!;
        var screen = Body(await Controller(staff).PersonelRapor(Filter("ykc"), reports));
        check(screen.RaporTipi == "devreye" && screen.IzinliRaporTipleri.SequenceEqual(["devreye"])
            && screen.Toplam == 1 && screen.Tamamlanan == 1 && screen.DurumSayilari.SequenceEqual([1, 0, 0]),
            "API selects only a permitted report and excludes foreign, deleted and out-of-period commissioning records");
        check(screen.DonemEtiketleri.SequenceEqual(["09.2026"]) && screen.DonemSayilari.SequenceEqual([1])
            && screen.KirilimEtiketleri.SequenceEqual(["Fixture brand"]),
            "API supplies matching chart labels and values for the selected interval");
        var defaultScreen = Body(await Controller(staff).PersonelRapor(new() { SirketId = company.Id }, reports));
        check(defaultScreen.BasTarih == new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            && defaultScreen.BitTarih == DateTime.Today, "API retains commissioning report's current-month default");
        check(await Controller(staff).PersonelRapor(Filter(scope: other.Id), reports) is ForbidResult
            && await Controller(companyAdmin).PersonelRapor(Filter(scope: other.Id), reports) is ForbidResult,
            "Neither personnel nor company administrators can post an unauthorized report company");
        db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = company.Id, YetkiTipi = YetkiTipleri.YETKI_BELGESI_ONAY });
        await db.SaveChangesAsync();
        foreach (var type in new[] { "onayli", "bekleyen", "reddedilen" })
        {
            screen = Body(await Controller(staff).PersonelRapor(Filter(" " + type.ToUpperInvariant() + " "), reports));
            check(screen.RaporTipi == type && screen.Toplam == 1 && screen.DurumSayilari.Sum() == 1
                && screen.DonemSayilari.Sum() == 1 && screen.KirilimEtiketleri.SequenceEqual(["Report firm A"])
                && screen.IzinliRaporTipleri.SequenceEqual(["devreye", "onayli", "bekleyen", "reddedilen"]),
                "API normalizes certificate report type and preserves status/date/expiry scope: " + type);
        }
        db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = other.Id, YetkiTipi = YetkiTipleri.YKC_RAPOR_GOR });
        await db.SaveChangesAsync();
        screen = Body(await Controller(staff).PersonelRapor(Filter("bekleyen", other.Id), reports));
        check(screen.RaporTipi == "ykc" && screen.Toplam == 1 && screen.IzinliRaporTipleri.SequenceEqual(["ykc"])
            && screen.DonemEtiketleri.SequenceEqual(["Report firm B"]),
            "Switching company cannot carry commissioning or certificate permissions into a YKC-only company");
        db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = company.Id, YetkiTipi = YetkiTipleri.YKC_RAPOR_GOR });
        await db.SaveChangesAsync();
        screen = Body(await Controller(staff).PersonelRapor(Filter(), reports));
        check(screen.RaporTipi == "ykc" && screen.Toplam == 2 && screen.DonemSayilari.Sum() == 2
            && screen.KirilimEtiketleri.SequenceEqual(["Fixture team"])
            && screen.DurumEtiketleri.Contains(YkcDurumDegerleri.Etiket(YkcDurumDegerleri.TalepAlindi)),
            "YKC-first default and user-facing status names are preserved without leaking commissioning details");
        defaultScreen = Body(await Controller(staff).PersonelRapor(new() { SirketId = company.Id }, reports));
        check(defaultScreen.BasTarih == DateTime.Today.AddDays(-30) && defaultScreen.BitTarih == DateTime.Today,
            "API retains YKC report's thirty-day default");
        var reversed = Filter("ykc");
        reversed.BaslangicTarihi = end.AddHours(17);
        reversed.BitisTarihi = month.AddHours(14);
        screen = Body(await Controller(staff).PersonelRapor(reversed, reports));
        check(screen.Toplam == 2 && screen.BasTarih == month && screen.BitTarih == end,
            "API normalizes reversed dates before querying and returning the filter");
        check(Body(await Controller(companyAdmin).PersonelRapor(Filter("ykc"), reports)).Toplam == 2,
            "Company administrator report remains restricted to its own company");
        var globalFilter = Filter("ykc");
        globalFilter.SirketId = null;
        var global = Body(await Controller(admin).PersonelRapor(globalFilter, reports));
        check(global.Toplam >= 3 && global.DonemEtiketleri.Contains("Report firm A") && global.DonemEtiketleri.Contains("Report firm B"),
            "Global administrator retains authorized all-company reporting");

        grant.SilindiMi = true;
        await db.Dag_PersonelYetkiler.Where(x => x.KullaniciId == staff.Id && x.YetkiTipi == YetkiTipleri.YKC_RAPOR_GOR)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.SilindiMi, true));
        await db.SaveChangesAsync();
        check(await Controller(staff).PersonelRapor(Filter(), reports) is ForbidResult,
            "Certificate approval alone cannot access reports after report permissions are revoked");
        db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = company.Id, YetkiTipi = YetkiTipleri.DAGITIM_SIRKET_YONET });
        await db.SaveChangesAsync();
        var permissions = new AdminPersonelYetkiApiService(db);
        var editor = await permissions.GetirAsync(new() { Id = staff.Id }, companyAdmin, company.Id, false);
        check(!editor.MevcutYetkiler.Contains(YetkiTipleri.DAGITIM_SIRKET_YONET)
            && editor.YetkiSirketMap.Values.All(x => !x.Contains(YetkiTipleri.DAGITIM_SIRKET_YONET))
            && editor.MevcutYetkiler.Contains(YetkiTipleri.YETKI_BELGESI_ONAY),
            "Permission API already removes unsupported grants without MVC filtering");
        staff.AktifMi = false;
        await db.SaveChangesAsync();
        check(await Controller(staff).PersonelRapor(Filter(), reports) is UnauthorizedResult,
            "Inactive personnel cannot request report screen data");
    }

    private sealed class ReadCommands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public Exception? CalendarFailure { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            if (CalendarFailure != null && command.CommandText.Contains("YetkiliServisPanel.Takvim", StringComparison.Ordinal))
                throw CalendarFailure;
            return ValueTask.FromResult(result);
        }
    }
}
