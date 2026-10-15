using System.Security.Claims;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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

internal static class PanelReportSqlScenario
{
    public static async Task RunAsync()
    {
        var databaseName = "PanelReportSqlTest_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true")
            .Options;
        await using var db = new AppDbContext(options);
        var created = false;
        var passed = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        try
        {
            created = await db.Database.EnsureCreatedAsync();
            if (!created) throw new InvalidOperationException("The isolated database already exists.");
            var primary = new Dag_Sirket { SirketAdi = "Primary", Il = "Corum" };
            var secondary = new Dag_Sirket { SirketAdi = "Secondary", Il = "Kastamonu" };
            var unrelated = new Dag_Sirket { SirketAdi = "Unrelated" };
            var inactive = new Dag_Sirket { SirketAdi = "Inactive", AktifMi = false };
            db.AddRange(primary, secondary, unrelated, inactive);
            await db.SaveChangesAsync();
            var staff = new AppKullanici { UserName = "panel-staff", KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = primary.Id };
            var admin = new AppKullanici { UserName = "panel-admin", KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin };
            var serviceFirm = new Ys_Firma { FirmaAdi = "Service", SirketId = primary.Id, FaaliyetIli = "Corum" };
            var certifiedFirm = new Ys_Firma { FirmaAdi = "Certified", SirketId = primary.Id, FaaliyetIli = "CertifiedCity" };
            var foreignFirm = new Ys_Firma { FirmaAdi = "Foreign service", SirketId = secondary.Id, FaaliyetIli = "Kastamonu" };
            var inactiveFirm = new Ys_Firma { FirmaAdi = "Inactive service", SirketId = primary.Id, AktifMi = false };
            db.AddRange(staff, admin, serviceFirm, certifiedFirm, foreignFirm, inactiveFirm);
            await db.SaveChangesAsync();
            var serviceUser = new AppKullanici { UserName = "service-user", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, FirmaId = serviceFirm.Id, SirketId = primary.Id };
            db.Users.AddRange(serviceUser,
                new AppKullanici { UserName = "second-service-user", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, FirmaId = serviceFirm.Id },
                new AppKullanici { UserName = "certified-user", KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma, FirmaId = certifiedFirm.Id },
                new AppKullanici { UserName = "foreign-service-user", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, FirmaId = foreignFirm.Id },
                new AppKullanici { UserName = "inactive-service-user", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, FirmaId = inactiveFirm.Id });
            var grant = new Dag_PersonelYetki { KullaniciId = staff.Id, SirketId = secondary.Id, YetkiTipi = YetkiTipleri.YKC_TALEP_GOR };
            db.Dag_PersonelYetkiler.AddRange(grant,
                new Dag_PersonelYetki { KullaniciId = staff.Id, SirketId = inactive.Id, YetkiTipi = YetkiTipleri.TAM_YETKI },
                new Dag_PersonelYetki { KullaniciId = serviceUser.Id, SirketId = secondary.Id, YetkiTipi = YetkiTipleri.TAM_YETKI });
            db.Ys_Subeler.AddRange(
                new Ys_Sube { FirmaId = serviceFirm.Id, SubeAdi = "Service branch", Il = "Corum", Ilce = "Merkez" },
                new Ys_Sube { FirmaId = certifiedFirm.Id, SubeAdi = "Certified branch", Il = "CertifiedCity", Ilce = "CertifiedDistrict" });
            await db.SaveChangesAsync();

            DagitimSirketApiController CompanyController(AppKullanici user) => new(new DagitimSirketApiService(db))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "Fixture"))
                    }
                }
            };
            var companyController = CompanyController(staff);
            Check(await companyController.Getir(new() { Id = primary.Id }) is OkObjectResult,
                "Personnel can view the primary company profile");
            Check(await companyController.Getir(new() { Id = secondary.Id }) is OkObjectResult,
                "A current additional-company grant permits its profile");
            Check(await companyController.Getir(new() { Id = unrelated.Id }) is ForbidResult,
                "A company without a grant remains forbidden");
            Check(await companyController.Getir(new() { Id = inactive.Id }) is ForbidResult,
                "A grant does not enable an inactive company");
            Check(await CompanyController(serviceUser).Getir(new() { Id = secondary.Id }) is ForbidResult,
                "A stale personnel grant cannot expand a service account's company scope");
            Check(await CompanyController(admin).Getir(new() { Id = unrelated.Id }) is OkObjectResult,
                "System admin retains access to other companies");
            grant.SilindiMi = true;
            await db.SaveChangesAsync();
            Check(await companyController.Getir(new() { Id = secondary.Id }) is ForbidResult,
                "Revoked grants retained in history do not authorize company profiles");
            grant.SilindiMi = false;
            staff.AktifMi = false;
            await db.SaveChangesAsync();
            Check(await companyController.Getir(new() { Id = secondary.Id }) is ForbidResult,
                "Inactive personnel cannot use an existing company grant");
            staff.ArsivlemeTarihi = DateTime.Now;
            await db.SaveChangesAsync();
            Check(await companyController.Getir(new() { Id = secondary.Id }) is ForbidResult,
                "Archived personnel cannot use an existing company grant");

            var directory = new YetkiliServislerController(new YetkiliServisRehberApiService(db), null!, null!);
            var directoryResult = (YetkiliServisSayfaliDto)((OkObjectResult)await directory.Liste(new() { PageSize = 100 })).Value!;
            Check(directoryResult.TotalCount == 2
                && directoryResult.Items.Select(x => x.Id).Order().SequenceEqual(new[] { serviceFirm.Id, foreignFirm.Id }.Order()),
                "Public directory includes service firms once and excludes certified or inactive firms");
            var secondPage = (YetkiliServisSayfaliDto)((OkObjectResult)await directory.ListeGet(new() { Page = 2, PageSize = 1 })).Value!;
            Check(secondPage.TotalCount == 2 && secondPage.TotalPages == 2 && secondPage.Items.Count == 1,
                "Public directory pagination uses the filtered firm types");
            var certifiedSearch = (YetkiliServisSayfaliDto)((OkObjectResult)await directory.Liste(new() { Q = "Certified" })).Value!;
            Check(certifiedSearch.TotalCount == 0, "Search cannot expose a certified firm as an authorized service");
            var directoryOptions = (YetkiliServisFiltreSecenekleriDto)((OkObjectResult)await directory.FiltreSecenekleri(null)).Value!;
            Check(!directoryOptions.Iller.Contains("Certifiedcity") && !directoryOptions.Ilceler.Contains("Certifieddistrict")
                && directoryOptions.Ilceler.Contains("Merkez"),
                "Directory location filters exclude certified-only firm branches");

            var today = DateTime.Today;
            Ys_YetkiBelgesi Certificate(int status, DateTime uploaded, DateTime? decided = null, Ys_Firma? firm = null)
                => new()
                {
                    FirmaId = (firm ?? serviceFirm).Id, Durum = status, OlusturmaTarihi = uploaded,
                    OnayTarihi = decided, YetkiBelgesiBitisTarihi = today.AddDays(60)
                };
            db.Ys_YetkiBelgeleri.AddRange(
                Certificate(YetkiBelgesiDurumDegerleri.OnaydaBekliyor, today.AddHours(8)),
                Certificate(YetkiBelgesiDurumDegerleri.OnaydaBekliyor, today.AddHours(11)),
                Certificate(YetkiBelgesiDurumDegerleri.Onaylandi, today.AddMonths(-2), today.AddHours(10)),
                Certificate(YetkiBelgesiDurumDegerleri.Onaylandi, today.AddMonths(-1), today.AddHours(12)),
                Certificate(YetkiBelgesiDurumDegerleri.Onaylandi, today, today.AddHours(13)),
                Certificate(YetkiBelgesiDurumDegerleri.Reddedildi, today.AddDays(-7), today.AddHours(14)),
                Certificate(YetkiBelgesiDurumDegerleri.Onaylandi, today, today.AddDays(-2)),
                Certificate(YetkiBelgesiDurumDegerleri.Onaylandi, today),
                Certificate(YetkiBelgesiDurumDegerleri.OnaydaBekliyor, today, firm: foreignFirm),
                Certificate(YetkiBelgesiDurumDegerleri.OnaydaBekliyor, today.AddDays(1)));
            var expired = Certificate(YetkiBelgesiDurumDegerleri.OnaydaBekliyor, today);
            expired.YetkiBelgesiBitisTarihi = today.AddDays(-1);
            var deleted = Certificate(YetkiBelgesiDurumDegerleri.OnaydaBekliyor, today);
            deleted.SilindiMi = true;
            db.AddRange(expired, deleted);
            await db.SaveChangesAsync();

            var reports = new AdminRaporApiService(db);
            foreach (var (type, count, status) in new[]
            {
                ("bekleyen", 2, YetkiBelgesiDurumDegerleri.OnaydaBekliyor),
                ("onayli", 3, YetkiBelgesiDurumDegerleri.Onaylandi),
                ("reddedilen", 1, YetkiBelgesiDurumDegerleri.Reddedildi)
            })
            {
                var report = await reports.RaporlarOzetAsync(new() { Tip = type, BaslangicTarihi = today, BitisTarihi = today }, primary.Id, true, true);
                Check(report.YetkiBelgesiOnayli + report.YetkiBelgesiBekleyen + report.YetkiBelgesiReddedilen == count
                    && report.YetkiBelgesiIslemler.Count == count && report.YetkiBelgesiIslemler.All(x => x.Durum == status),
                    type + ": counters and records use the same status, company and date scope");
                Check(report.ChartDurumData.Sum() == count && report.ChartAylikData.Sum() == count
                    && report.ChartMarkaData.Sum() == count,
                    type + ": charts and firm distribution match the selected report count");
                if (type != "bekleyen")
                {
                    var exported = await new AdminYetkiBelgesiOnayApiService(db).GecmisAsync(
                        new() { BaslangicTarihi = today, BitisTarihi = today, Durum = status }, primary.Id);
                    Check(exported.Islemler.Select(x => x.Id).Order().SequenceEqual(report.YetkiBelgesiIslemler.Select(x => x.Id).Order()),
                        type + ": decision-date counters match the certificate export records");
                }
            }
            var emptyReport = await reports.RaporlarOzetAsync(new()
            {
                Tip = "bekleyen", BaslangicTarihi = today.AddYears(-1), BitisTarihi = today.AddYears(-1)
            }, primary.Id, true, true);
            Check(emptyReport.ChartDurumData.Sum() == 0 && emptyReport.ChartAylikData.Sum() == 0
                && emptyReport.YetkiBelgesiIslemler.Count == 0, "An empty period has no unrelated status counts");

            Ys_DevreyeAlma Commissioning(string installation, DateTime commissioned, DateTime createdAt, Ys_Firma? firm = null)
                => new()
                {
                    FirmaId = (firm ?? serviceFirm).Id, TesistatNo = installation, AboneNo = "000009",
                    DevreyeAlmaTarihi = commissioned, OlusturmaTarihi = createdAt,
                    Durum = DevreyeAlmaDurumDegerleri.Tamamlandi, CihazKapasite = "2.5", CihazMarka = "Fixture"
                };
            var current = Commissioning("00000123", today, today.AddDays(-20));
            var lastMinute = Commissioning("00000456", today.AddDays(1).AddTicks(-1), today.AddDays(-10));
            var previous = Commissioning("previous", today.AddDays(-2), today.AddHours(9));
            var foreign = Commissioning("foreign", today.AddHours(12), today, foreignFirm);
            db.AddRange(current, lastMinute, previous, foreign,
                Commissioning("next-day", today.AddDays(1), today));
            await db.SaveChangesAsync();
            db.Ys_DevreyeAlmaSorguKayitlari.Add(new()
            {
                Referans = Guid.NewGuid().ToString("N"), FirmaId = serviceFirm.Id,
                DagitimSirketiId = primary.Id, DevreyeAlmaId = current.Id, KullaniciId = serviceUser.Id,
                KaynakJson = JsonSerializer.Serialize(new YsDevreyeAlmaKaynak
                {
                    TesisatNo = current.TesistatNo!, AboneNo = current.AboneNo!, SozlesmeNo = "0000943"
                })
            });
            Ykc_Talep Request(DateTime date, int? company = null) => new()
            {
                SirketId = company ?? primary.Id, FirmaId = certifiedFirm.Id,
                TalepTarihi = date, Durum = YkcDurumDegerleri.TalepAlindi, AtananEkip = "Private team"
            };
            db.Ykc_Talepler.AddRange(Request(today), Request(today.AddDays(-1)),
                Request(today.AddDays(1)), Request(today, secondary.Id));
            var reportStaff = new AppKullanici { UserName = "reports-only", KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = primary.Id };
            db.Users.Add(reportStaff);
            db.Dag_PersonelYetkiler.Add(new() { KullaniciId = reportStaff.Id, SirketId = primary.Id, YetkiTipi = YetkiTipleri.RAPOR_GOR });
            await db.SaveChangesAsync();

            AdminRaporOzetFiltreDto Filter(string type) => new() { Tip = type, SirketId = primary.Id, BaslangicTarihi = today, BitisTarihi = today };
            var commissioningSummary = await reports.RaporlarOzetAsync(Filter("devreye"), primary.Id, true, true);
            var commissioningList = await reports.DevreyeAlmalarAsync(new() { BaslangicTarihi = today, BitisTarihi = today }, primary.Id);
            Check(commissioningSummary.DevreyeSayisi == 2 && commissioningSummary.ChartAylikData.Sum() == 2
                && commissioningList.Islemler.Select(x => x.Id).SequenceEqual(new[] { lastMinute.Id, current.Id }),
                "Commissioning list and summary use the actual date, including day-end and excluding foreign/next-day records");
            Check(commissioningSummary.OperasyonTalepSayisi == 1 && commissioningSummary.OperasyonAylikData.Sum() == 1,
                "Monthly operation graph cannot count requests outside the selected dates or company");
            var reversed = await reports.DevreyeAlmalarAsync(new() { BaslangicTarihi = today, BitisTarihi = today.AddDays(-2) }, primary.Id);
            Check(reversed.Islemler.Count == 3, "Reversed commissioning dates select the same inclusive interval");

            List<string> ExcelColumn(byte[] bytes, string header)
            {
                using var archive = new ZipArchive(new MemoryStream(bytes));
                using var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var rows = XDocument.Load(sheet).Descendants(ns + "row").ToList();
                var column = rows[0].Elements(ns + "c").Select((c, i) => (c, i)).Single(x => x.c.Value == header).i;
                return rows.Skip(1).Select(r => r.Elements(ns + "c").ElementAt(column).Value).ToList();
            }
            var exports = new DevreyeAlmaExportApiService(db);
            var adminExcel = await exports.AdminRaporExcelAsync(primary.Id, today, today, null);
            var serviceExcel = await exports.YetkiliServisRaporExcelAsync(serviceFirm.Id, today, today, null);
            foreach (var output in new[] { adminExcel, serviceExcel })
            {
                Check(ExcelColumn(output.Bytes, "Tesisat No").SequenceEqual(new[] { "00000456", "00000123" })
                    && ExcelColumn(output.Bytes, "Sözleşme No").SequenceEqual(new[] { "", "0000943" }),
                    "Company and service exports match list records and retain the sourced contract with leading zeroes");
            }
            Check(ExcelColumn((await exports.AdminExcelAsync(current.Id, primary.Id))!.Bytes, "Sözleşme No").Single() == "0000943"
                && ExcelColumn((await exports.YetkiliServisExcelAsync(current.Id, serviceFirm.Id))!.Bytes, "Sözleşme No").Single() == "0000943",
                "Single-record Excel exports also resolve the verified source contract");
            Check(await exports.AdminExcelAsync(current.Id, secondary.Id) == null
                && await exports.YetkiliServisExcelAsync(current.Id, foreignFirm.Id) == null,
                "Adding source contract data does not bypass company or firm export scope");
            var selected = await exports.AdminRaporExcelAsync(primary.Id, null, null, [current.Id, current.Id, foreign.Id]);
            Check(ExcelColumn(selected.Bytes, "Tesisat No").SequenceEqual(new[] { "00000123" })
                && selected.DosyaAdi.Contains($"{today:yyyyMMdd}_{today:yyyyMMdd}"),
                "Selected export deduplicates IDs and derives its date range only from authorized commissioning dates");
            var reversedAdmin = await exports.AdminRaporExcelAsync(primary.Id, today, today.AddDays(-2), null);
            var reversedService = await exports.YetkiliServisRaporExcelAsync(serviceFirm.Id, today, today.AddDays(-2), null);
            Check(ExcelColumn(reversedAdmin.Bytes, "Tesisat No").Count == 3
                && ExcelColumn(reversedService.Bytes, "Tesisat No").Count == 3,
                "Reversed date exports agree across administrator and service roles");
            var defaultSummary = await reports.RaporlarOzetAsync(null, primary.Id, true, true);
            var defaultExcel = await exports.AdminRaporExcelAsync(primary.Id, null, null, null);
            Check(ExcelColumn(defaultExcel.Bytes, "Tesisat No").Count == defaultSummary.DevreyeSayisi,
                "Default report and default export use the same date interval");

            using var manager = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db),
                Options.Create(new IdentityOptions()), new PasswordHasher<AppKullanici>(), [], [],
                new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
                NullLogger<UserManager<AppKullanici>>.Instance);
            var historyController = new YetkiliServisDevreyeAlmaApiController(manager, null!, exports,
                new DevreyeAlmaOkumaApiService(db, new YetkiliServisIlkKurulumService(db), new DevreyeAlmaYetkiDogrulamaService(db)), null!)
            {
                ControllerContext = CompanyController(serviceUser).ControllerContext
            };
            var history = (YsDevreyeAlmaGecmisDto)((OkObjectResult)await historyController.Gecmis(new()
            {
                BaslangicTarihi = today, BitisTarihi = today
            })).Value!;
            Check(history.Islemler.Select(x => x.Id).SequenceEqual(new[] { lastMinute.Id, current.Id }),
                "Service history uses commissioning date ordering and preserves inclusive day boundaries");
            var reversedHistory = (YsDevreyeAlmaGecmisDto)((OkObjectResult)await historyController.Gecmis(new()
            {
                BaslangicTarihi = today, BitisTarihi = today.AddDays(-2)
            })).Value!;
            Check(reversedHistory.Islemler.Select(x => x.Id).SequenceEqual(new[] { lastMinute.Id, current.Id, previous.Id }),
                "Reversed service history dates agree with the download without including another firm's records");
            var serviceReports = new YetkiliServisPanelApiController(manager, null!, exports,
                new YetkiliServisRaporApiService(db),
                new YetkiliServisPanelOkumaApiService(db, new YetkiliServisIlkKurulumService(db),
                    NullLogger<YetkiliServisPanelOkumaApiService>.Instance),
                new YetkiliServisProfilApiService(db, manager))
            {
                ControllerContext = CompanyController(serviceUser).ControllerContext
            };
            var serviceSummary = (YsPanelRaporSonucDto)((OkObjectResult)await serviceReports.Raporlar(new()
            {
                Bas = today, Bit = today.AddDays(-2)
            })).Value!;
            Check(serviceSummary.BasTarih == today.AddDays(-2) && serviceSummary.BitTarih == today
                && serviceSummary.DevreyeSayisi == 3 && serviceSummary.SonIslemler.Count == 3
                && serviceSummary.ChartAylikData.Sum() == 3,
                "Service report counters, graph and list normalize reversed dates consistently");
            Check(commissioningSummary.ChartMarkaLabels.SequenceEqual(new[] { "Fixture" })
                && commissioningSummary.ChartMarkaData.Sum() == 2 && serviceSummary.ChartMarkaData.Sum() == 3,
                "Both report roles count stored device brands even when no catalog brand is linked");
            var selectedSummary = (YsPanelRaporSonucDto)((OkObjectResult)await serviceReports.Raporlar(new()
            {
                Ids = [current.Id, current.Id, foreign.Id], Limit = 1
            })).Value!;
            Check(selectedSummary.DevreyeSayisi == 1 && selectedSummary.ChartAylikData.Sum() == 1
                && selectedSummary.ChartMarkaData.Sum() == 1 && selectedSummary.SonIslemler.Single().Id == current.Id,
                "Selected-record totals and charts exclude unselected and foreign records and ignore duplicate IDs");
            var emptySelection = (YsPanelRaporSonucDto)((OkObjectResult)await serviceReports.Raporlar(new()
            {
                Ids = [foreign.Id, int.MaxValue]
            })).Value!;
            Check(emptySelection.DevreyeSayisi == 0 && emptySelection.ChartAylikData.Sum() == 0
                && emptySelection.ChartMarkaData.Sum() == 0 && emptySelection.SonIslemler.Count == 0,
                "An inaccessible selection cannot fall back to all records in its date range");
            current.Marka = new() { MarkaAdi = "Catalog brand" };
            lastMinute.CihazMarka = " ";
            previous.CihazMarka = " Fallback brand ";
            await db.SaveChangesAsync();
            var brandSummary = (YsPanelRaporSonucDto)((OkObjectResult)await serviceReports.Raporlar(new()
            {
                Bas = today.AddDays(-2), Bit = today, Limit = 1
            })).Value!;
            Check(brandSummary.ChartMarkaLabels.Order().SequenceEqual(
                    new[] { "Catalog brand", "Fallback brand", "Marka belirtilmemiş" }.Order())
                && brandSummary.ChartMarkaData.Sum() == 3 && brandSummary.DevreyeSayisi == 3
                && brandSummary.SonIslemler.Count == 1,
                "Brand groups use the catalog first, trim source brands and retain missing brands without applying the display limit");

            AdminPanelApiController ReportController(AppKullanici user) => new(db, null!, null!, null!, null!,
                reports, new AdminYetkiBelgesiOnayApiService(db), null!, exports, new AdminKullaniciOkumaApiService(db), null!)
            {
                ControllerContext = CompanyController(user).ControllerContext
            };
            var reportController = ReportController(reportStaff);
            var restricted = (AdminRaporOzetDto)((OkObjectResult)await reportController.RaporlarOzet(Filter("devreye"))).Value!;
            Check(restricted.DevreyeSayisi == 2 && restricted.SonIslemler.Count == 2
                && restricted.OperasyonTalepSayisi == 0 && restricted.OperasyonAylikData.Sum() == 0
                && restricted.OperasyonFirmaLabels.Count == 0 && restricted.OperasyonEkipLabels.Count == 0
                && restricted.YetkiBelgesiOnayli == 0 && restricted.YetkiBelgesiBekleyen == 0 && restricted.YetkiBelgesiReddedilen == 0,
                "Commissioning-only permission cannot expose certificate or YKC data in another report's response");
            Check(await reportController.RaporlarOzet(Filter(" ONAYLI ")) is ForbidResult
                && await reportController.RaporlarOzet(Filter("operasyon")) is ForbidResult,
                "Report type changes cannot bypass module permissions");
            var documentGrant = new Dag_PersonelYetki { KullaniciId = reportStaff.Id, SirketId = primary.Id, YetkiTipi = YetkiTipleri.YETKI_BELGESI_ONAY };
            var operationGrant = new Dag_PersonelYetki { KullaniciId = reportStaff.Id, SirketId = secondary.Id, YetkiTipi = YetkiTipleri.YKC_RAPOR_GOR };
            db.AddRange(documentGrant, operationGrant);
            await db.SaveChangesAsync();
            Check(await reportController.RaporlarOzet(Filter("bekleyen")) is OkObjectResult
                && await reportController.RaporlarOzet(Filter("ykc")) is ForbidResult,
                "Certificate permission works, but a YKC permission in another company cannot leak into this scope");
            operationGrant.SirketId = primary.Id;
            await db.SaveChangesAsync();
            Check(await reportController.RaporlarOzet(Filter("operasyon")) is OkObjectResult,
                "A matching current YKC report permission enables the operation report");
            operationGrant.SilindiMi = true;
            documentGrant.SilindiMi = true;
            await db.SaveChangesAsync();
            Check(await reportController.RaporlarOzet(Filter("onayli")) is ForbidResult
                && await reportController.RaporlarOzet(Filter("operasyon")) is ForbidResult,
                "Revoked module grants retained for history no longer authorize reports");
            var adminSummary = (AdminRaporOzetDto)((OkObjectResult)await ReportController(admin).RaporlarOzet(Filter("operasyon"))).Value!;
            Check(adminSummary.OperasyonTalepSayisi == 1 && adminSummary.YetkiBelgesiBekleyen == 2,
                "System administrators retain complete authorized report summaries");
            foreach (var excel in new[] { false, true })
            {
                var certificateController = ReportController(admin);
                var filter = new YetkiBelgesiRaporFiltre
                { SirketId = primary.Id, Tip = "bekleyen", BaslangicTarihi = today, BitisTarihi = today };
                var document = excel ? await certificateController.YetkiBelgesiRaporExcel(filter)
                    : await certificateController.YetkiBelgesiRaporPdf(filter);
                Check(document is FileContentResult { FileContents.Length: > 0 }
                    && certificateController.Response.Headers.CacheControl == "private, no-store"
                    && certificateController.Response.Headers.Pragma == "no-cache"
                    && certificateController.Response.Headers.Expires == "0",
                    "Certificate report returns intact private non-cacheable file: Excel=" + excel);
            }

            var bulk = Enumerable.Range(1, 5001).Select(i => Commissioning($"bulk-{i}", today, today)).ToList();
            db.Ys_DevreyeAlmalar.AddRange(bulk);
            await db.SaveChangesAsync();
            foreach (var excel in new[] { false, true })
            {
                var adminResult = excel
                    ? await ReportController(admin).DevreyeAlmaRaporExcel(new() { SirketId = primary.Id, BaslangicTarihi = today, BitisTarihi = today })
                    : await ReportController(admin).DevreyeAlmaRaporPdf(new() { SirketId = primary.Id, BaslangicTarihi = today, BitisTarihi = today });
                var serviceResult = excel
                    ? await serviceReports.RaporlarExcel(new() { Ids = bulk.Select(x => x.Id).ToList() })
                    : await serviceReports.RaporlarPdf(new() { Bas = today, Bit = today });
                Check(adminResult is BadRequestObjectResult a && serviceResult is BadRequestObjectResult s
                    && JsonSerializer.Serialize(a.Value).Contains("5000") && JsonSerializer.Serialize(s.Value).Contains("5000"),
                    $"Excel={excel}: company and service endpoints reject oversize reports instead of returning truncated files");
            }
            var exactLimit = (FileContentResult)await serviceReports.RaporlarExcel(new() { Ids = bulk.Take(5000).Select(x => x.Id).ToList() });
            Check(ExcelColumn(exactLimit.FileContents, "Tesisat No").Count == 5000,
                "Exactly 5000 selected records export completely, including the last row");
            var repeatedIds = Enumerable.Repeat(current.Id, 5001).Append(foreign.Id).ToList();
            var smallSelection = await exports.AdminRaporExcelAsync(primary.Id, today, today, repeatedIds);
            Check(ExcelColumn(smallSelection.Bytes, "Tesisat No").SequenceEqual(new[] { "00000123" }),
                "Duplicate and foreign IDs do not inflate the authorized export row limit");
            var foreignExport = await exports.AdminRaporExcelAsync(secondary.Id, today, today, null);
            Check(ExcelColumn(foreignExport.Bytes, "Tesisat No").SequenceEqual(new[] { "foreign" }),
                "Another company's large report cannot block an otherwise small scoped download");
            var pendingService = new YkcTalepOkumaService(db);
            var pendingCases = new List<Ykc_Talep>();
            for (var i = 0; i < 17; i++)
            {
                var request = new Ykc_Talep
                {
                    SirketId = primary.Id, FirmaId = certifiedFirm.Id, MusteriAdi = "Pending fixture " + i,
                    TesisatNo = "PENDING-" + i, Durum = YkcDurumDegerleri.SahaIsleminde,
                    RandevuTarihi = today.AddDays(-1), RandevuSaati = "09:00", TalepTarihi = today.AddMinutes(i)
                };
                var file = new Ykc_FormDosya { Talep = request, DosyaTuru = YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai };
                request.FormDosyalari.Add(file);
                request.ImzaSurecleri.Add(new Ykc_ImzaSureci
                {
                    Durum = YkcImzaDurumDegerleri.Tamamlandi, ProviderDocumentId = "pending-fixture-" + i, NihaiDosya = file
                });
                pendingCases.Add(request);
            }
            pendingCases[2].Durum = YkcDurumDegerleri.Tamamlandi;
            pendingCases[3].Durum = YkcDurumDegerleri.Iptal;
            pendingCases[4].Durum = YkcDurumDegerleri.Atandi;
            pendingCases[5].RandevuTarihi = today.AddDays(1);
            pendingCases[6].RandevuSaati = "invalid";
            pendingCases[7].RandevuTarihi = null;
            pendingCases[8].ImzaSurecleri.Single().ProviderDocumentId = "";
            pendingCases[9].ImzaSurecleri.Single().Durum = YkcImzaDurumDegerleri.ImzaBekliyor;
            pendingCases[10].FormDosyalari.Single().SilindiMi = true;
            pendingCases[11].ImzaSurecleri.Single().SilindiMi = true;
            pendingCases[12].FormDosyalari.Single().DosyaTuru = YkcFormDosyaTuruDegerleri.TeknikEk;
            pendingCases[13].SilindiMi = true;
            pendingCases[14].SirketId = secondary.Id;
            pendingCases[14].FirmaId = foreignFirm.Id;
            pendingCases[15].Durum = YkcDurumDegerleri.TalepAlindi;
            pendingCases[16].Durum = YkcDurumDegerleri.AtamaBekliyor;
            db.AddRange(pendingCases);
            await db.SaveChangesAsync();

            var pendingSummary = await pendingService.DashboardOzetAsync(staff, false, primary.Id);
            Check(pendingSummary.TamamlamaBekleyen == 2,
                "Completion queue excludes closed, future, malformed, unsigned, deleted and foreign requests");
            foreach (var (kind, count) in new[]
            {
                (YkcBekleyenIsDegerleri.Inceleme, pendingSummary.IncelemeBekleyen),
                (YkcBekleyenIsDegerleri.Randevu, pendingSummary.RandevuBekleyen),
                (YkcBekleyenIsDegerleri.Tamamlama, pendingSummary.TamamlamaBekleyen)
            })
            {
                var list = await pendingService.ListeAsync(new() { BekleyenIs = kind }, staff, false, primary.Id);
                var scopedIds = await db.Ykc_Talepler.Where(x => x.SirketId == primary.Id).Select(x => x.Id).ToListAsync();
                Check(list.Toplam == count && list.Talepler.All(x => scopedIds.Contains(x.Id)),
                    $"Dashboard count and list share the same company-scoped pending rule: {kind}");
            }
            var secondPending = await pendingService.ListeAsync(new()
            {
                BekleyenIs = YkcBekleyenIsDegerleri.Tamamlama, Sayfa = 2, SayfaBoyutu = 1
            }, staff, false, primary.Id);
            Check(secondPending.Toplam == 2 && secondPending.Sayfa == 2 && secondPending.Talepler.Single().Id == pendingCases[0].Id,
                "Completion queue applies filtering before pagination");
            var extraFilter = await pendingService.ListeAsync(new()
            {
                BekleyenIs = YkcBekleyenIsDegerleri.Tamamlama, TesisatNo = pendingCases[1].TesisatNo
            }, staff, false, primary.Id);
            Check(extraFilter.Toplam == 1 && extraFilter.Talepler.Single().Id == pendingCases[1].Id,
                "Pending work filter is preserved alongside the user's installation filter");
            Check((await pendingService.DashboardOzetAsync(staff, false, secondary.Id)).TamamlamaBekleyen == 1,
                "Switching the authorized company changes the pending completion count");
            var queueFirm = new AppKullanici { FirmaId = certifiedFirm.Id, KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma };
            Check((await pendingService.ListeAsync(new() { BekleyenIs = YkcBekleyenIsDegerleri.Tamamlama }, queueFirm, true)).Toplam == 2,
                "Pending filters never broaden a firm account's scope");
            Check((await pendingService.ListeAsync(new() { BekleyenIs = "unknown" }, staff, false, primary.Id)).Toplam == 0,
                "An unknown pending filter does not silently display every record");
            pendingCases[0].Durum = YkcDurumDegerleri.Tamamlandi;
            await db.SaveChangesAsync();
            Check((await pendingService.DashboardOzetAsync(staff, false, primary.Id)).TamamlamaBekleyen == 1,
                "Completed requests leave the pending count on the next read");
            await VerifyYkcDirectoryAsync(db, primary, secondary, certifiedFirm, serviceFirm, foreignFirm,
                admin, Check, ExcelColumn);
            await VerifyServiceDashboardRecentOrderAsync(db, primary, serviceFirm, foreignFirm, Check);
            Console.WriteLine($"{passed} panel/report SQL checks passed. Application records were not used.");
        }
        finally
        {
            if (created)
            {
                await db.Database.CloseConnectionAsync();
                SqlConnection.ClearAllPools();
                await db.Database.EnsureDeletedAsync();
            }
        }
    }

    private static async Task VerifyServiceDashboardRecentOrderAsync(AppDbContext db, Dag_Sirket company,
        Ys_Firma siblingFirm, Ys_Firma foreignFirm, Action<bool, string> check)
    {
        var firm = new Ys_Firma { FirmaAdi = "Dashboard recent order", SirketId = company.Id };
        db.Ys_Firmalar.Add(firm);
        await db.SaveChangesAsync();
        var date = new DateTime(2026, 10, 13, 10, 0, 0);
        var records = new[] { -30, -5, -1, 0, 0, -10, -20 }.Select((days, index) => new Ys_DevreyeAlma
        {
            FirmaId = firm.Id, TesistatNo = "DASHBOARD-ORDER-" + index,
            DevreyeAlmaTarihi = date.AddDays(days), OlusturmaTarihi = date.AddDays(10 - index),
            Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
        }).ToArray();
        db.Ys_DevreyeAlmalar.AddRange(records);
        db.Ys_DevreyeAlmalar.AddRange(
            new Ys_DevreyeAlma { FirmaId = siblingFirm.Id, DevreyeAlmaTarihi = date.AddDays(1) },
            new Ys_DevreyeAlma { FirmaId = foreignFirm.Id, DevreyeAlmaTarihi = date.AddDays(2) },
            new Ys_DevreyeAlma { FirmaId = firm.Id, DevreyeAlmaTarihi = date.AddDays(3), SilindiMi = true });
        await db.SaveChangesAsync();

        var setup = new YetkiliServisIlkKurulumService(db);
        var dashboard = await new YetkiliServisPanelOkumaApiService(db, setup,
            NullLogger<YetkiliServisPanelOkumaApiService>.Instance).DashboardAsync(firm.Id);
        var expectedIds = new[] { records[3].Id, records[4].Id }.OrderDescending()
            .Concat(new[] { records[2].Id, records[1].Id, records[5].Id }).ToArray();
        check(dashboard.SonIslemler.Select(x => x.Id).SequenceEqual(expectedIds),
            "Service dashboard takes the latest five commissioning dates, breaks ties by descending ID, and excludes deleted/other-firm records regardless of creation date");
        var history = await new DevreyeAlmaOkumaApiService(db, setup,
            new DevreyeAlmaYetkiDogrulamaService(db)).GecmisAsync(firm.Id, null);
        check(dashboard.Toplam == records.Length
            && dashboard.SonIslemler.Select(x => x.Id).SequenceEqual(history.Islemler.Take(5).Select(x => x.Id)),
            "Service dashboard recent records match default history ordering without limiting the total count");
    }

    private static async Task VerifyYkcDirectoryAsync(AppDbContext db, Dag_Sirket primary, Dag_Sirket secondary,
        Ys_Firma firm, Ys_Firma siblingFirm, Ys_Firma foreignFirm, AppKullanici admin, Action<bool, string> check,
        Func<byte[], string, List<string>> excelColumn)
    {
        const string prefix = "YKC-TOOLBAR-";
        const string rawType = "legacy/type-42";
        var date = new DateTime(2026, 1, 10);
        var names = new string?[] { null, "", "Hale", "I\u015f\u0131k", "ipek", "Zeki" };
        var ownRecords = Enumerable.Range(0, 24).Select(index => new Ykc_Talep
        {
            FirmaId = firm.Id, SirketId = primary.Id, TesisatNo = prefix + index.ToString("D3"),
            SozlesmeNo = "toolbar-contract-" + index.ToString("D3"), AboneNo = "private-subscriber",
            MusteriAdi = index == 9 ? "\u0131\u015f\u0131k" : names[index % names.Length],
            TalepTarihi = date.AddMinutes(index % 4), YeniCihazTipi = index % 2 == 0 ? rawType : "Toolbar stove",
            EskiCihazTipi = "private-old-type", ProjeNo = "private-project", AtananEkip = "private-team",
            HedefUygulama = "private-routing", Durum = index % 3 == 0 ? YkcDurumDegerleri.Iptal : YkcDurumDegerleri.TalepAlindi
        }).ToArray();
        var sibling = new Ykc_Talep
        {
            FirmaId = siblingFirm.Id, SirketId = primary.Id, TesisatNo = prefix + "sibling",
            MusteriAdi = "Aylin", TalepTarihi = date, YeniCihazTipi = rawType
        };
        var foreign = new Ykc_Talep
        {
            FirmaId = foreignFirm.Id, SirketId = secondary.Id, TesisatNo = ownRecords[^1].TesisatNo,
            SozlesmeNo = ownRecords[^1].SozlesmeNo, MusteriAdi = names[3], YeniCihazTipi = rawType,
            TalepTarihi = date.AddDays(1)
        };
        var deleted = new Ykc_Talep
        {
            FirmaId = firm.Id, SirketId = primary.Id, TesisatNo = prefix + "deleted", SilindiMi = true,
            MusteriAdi = names[3], YeniCihazTipi = rawType, TalepTarihi = date.AddDays(1)
        };
        db.AddRange(ownRecords);
        db.AddRange(sibling, foreign, deleted);
        await db.SaveChangesAsync();
        var reader = new YkcTalepOkumaService(db);
        var personnel = new AppKullanici { KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = secondary.Id };
        var companyRecords = ownRecords.Append(sibling).ToArray();
        var turkish = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true);
        foreach (var sort in new string?[] { null, "", "tarih-artan", "abone-artan", "abone-azalan", "unsupported-sort" })
        {
            var expected = (sort switch
            {
                "tarih-artan" => companyRecords.OrderBy(x => x.TalepTarihi).ThenBy(x => x.Id),
                "abone-artan" => companyRecords.OrderBy(x => x.MusteriAdi ?? "", turkish).ThenByDescending(x => x.Id),
                "abone-azalan" => companyRecords.OrderByDescending(x => x.MusteriAdi ?? "", turkish).ThenByDescending(x => x.Id),
                _ => companyRecords.OrderByDescending(x => x.TalepTarihi).ThenByDescending(x => x.Id)
            }).ToArray();
            foreach (var (actor, privileged) in new[] { (personnel, false), (admin, true) })
            {
                var filter = new YkcTalepListeFiltre { Q = prefix, Sirala = sort, Sayfa = 2, SayfaBoyutu = 10 };
                var list = await reader.ListeAsync(filter, actor, privileged, primary.Id);
                var report = await reader.RaporAsync(filter, actor, privileged, primary.Id);
                var exported = await reader.RaporKayitlariAsync(filter, actor, privileged, 100, primary.Id);
                var pageIds = expected.Skip(10).Take(10).Select(x => x.Id).ToArray();
                check(list.Toplam == expected.Length && report.Toplam == expected.Length
                    && report.DurumOzetleri.Sum(x => x.Sayi) == expected.Length && list.Sayfa == 2 && report.Sayfa == 2
                    && list.Talepler.Select(x => x.Id).SequenceEqual(pageIds) && report.Kayitlar.Select(x => x.Id).SequenceEqual(pageIds)
                    && exported.Select(x => x.Id).SequenceEqual(expected.Select(x => x.Id)),
                    $"YKC SQL sorting, pagination, totals and full export share company scope: {sort}, admin={privileged}");
                check(excelColumn(YkcRaporExcelService.Olustur(exported, true), "Talep No")
                    .SequenceEqual(expected.Select(x => x.Id.ToString())),
                    $"YKC Excel preserves the API's complete filtered sort order: {sort}, admin={privileged}");
            }
            foreach (var targetIndex in new[] { 0, 9, 10, expected.Length - 1 })
            {
                var focused = await reader.RaporAsync(new()
                {
                    Q = prefix, Sirala = sort, Sayfa = 99, SayfaBoyutu = 10, DetayTalepId = expected[targetIndex].Id
                }, personnel, false, primary.Id);
                var page = targetIndex / 10 + 1;
                check(focused.Sayfa == page && focused.Toplam == expected.Length
                    && focused.Kayitlar.Select(x => x.Id).SequenceEqual(expected.Skip((page - 1) * 10).Take(10).Select(x => x.Id)),
                    $"Detail focus follows SQL date/name ordering, null names and descending name-ID ties: {sort}, index={targetIndex}");
            }
            foreach (var outsideId in new[] { foreign.Id, deleted.Id, int.MaxValue })
            {
                var focused = await reader.RaporAsync(new()
                {
                    Q = prefix, Sirala = sort, Sayfa = 2, SayfaBoyutu = 10, DetayTalepId = outsideId
                }, personnel, false, primary.Id);
                check(focused.Sayfa == 2 && focused.Toplam == expected.Length
                    && focused.Kayitlar.Select(x => x.Id).SequenceEqual(expected.Skip(10).Take(10).Select(x => x.Id)),
                    $"Inaccessible or missing detail targets cannot move the selected sorted page: {sort}, id={outsideId}");
            }
        }

        var qCases = new (string Q, int[] Ids)[]
        {
            (" \u0131\u015f\u0131k ", ownRecords.Where(x => turkish.Equals(x.MusteriAdi, names[3])).Select(x => x.Id).ToArray()),
            (ownRecords[^1].TesisatNo!, [ownRecords[^1].Id]),
            (ownRecords[^1].SozlesmeNo!, [ownRecords[^1].Id]),
            (firm.FirmaAdi!, ownRecords.Select(x => x.Id).ToArray()),
            (ownRecords[^1].Id.ToString(), [ownRecords[^1].Id]),
            ("#" + ownRecords[^1].Id, [ownRecords[^1].Id])
        };
        foreach (var (q, ids) in qCases)
        {
            var filter = new YkcTalepListeFiltre { Q = q, TesisatNo = prefix, SayfaBoyutu = 100 };
            var list = await reader.ListeAsync(filter, personnel, false, primary.Id);
            var report = await reader.RaporAsync(filter, personnel, false, primary.Id);
            var exported = await reader.RaporKayitlariAsync(filter, personnel, false, 100, primary.Id);
            check(list.Toplam == ids.Length && report.Toplam == ids.Length
                && list.Talepler.Select(x => x.Id).Order().SequenceEqual(ids.Order())
                && report.Kayitlar.Select(x => x.Id).Order().SequenceEqual(ids.Order())
                && exported.Select(x => x.Id).Order().SequenceEqual(ids.Order()),
                "YKC q searches customer, installation, contract, exact request ID and firm only inside the resolved scope: " + q);
        }
        var columnFilter = new YkcTalepListeFiltre
        {
            Q = prefix, Sirala = "abone-azalan", CihazTipi = " " + rawType + " ", TalepId = ownRecords[2].Id,
            MusteriAdi = ownRecords[2].MusteriAdi, TesisatNo = ownRecords[2].TesisatNo,
            SozlesmeNo = ownRecords[2].SozlesmeNo, Durum = ownRecords[2].Durum,
            BaslangicTarihi = date, BitisTarihi = date
        };
        check((await reader.ListeAsync(columnFilter, personnel, false, primary.Id)).Talepler.Single().Id == ownRecords[2].Id
            && (await reader.RaporAsync(columnFilter, personnel, false, primary.Id)).Kayitlar.Single().Id == ownRecords[2].Id
            && (await reader.RaporKayitlariAsync(columnFilter, personnel, false, 100, primary.Id)).Single().Id == ownRecords[2].Id,
            "TalepId and toolbar/device filters combine with existing customer, contract, installation, integer status and date filters");
        columnFilter.KayitIdleri = [sibling.Id, foreign.Id];
        check((await reader.RaporKayitlariAsync(columnFilter, personnel, false, 100, primary.Id)).Count == 0,
            "Request-ID and selected-export filters intersect; they never replace each other or broaden scope");
        var typeFilter = new YkcTalepListeFiltre { Q = prefix, CihazTipi = rawType, Sirala = "abone-artan", SayfaBoyutu = 100 };
        var typedReport = await reader.RaporAsync(typeFilter, personnel, false, primary.Id);
        var typedList = await reader.ListeAsync(typeFilter, personnel, false, primary.Id);
        var typedExport = await reader.RaporKayitlariAsync(typeFilter, personnel, false, 100, primary.Id);
        var typedIds = companyRecords.Where(x => x.YeniCihazTipi == rawType).Select(x => x.Id).Order().ToArray();
        check(typedReport.Toplam == typedIds.Length && typedList.Toplam == typedIds.Length
            && typedReport.DurumOzetleri.Sum(x => x.Sayi) == typedIds.Length
            && typedList.Talepler.Select(x => x.Id).SequenceEqual(typedExport.Select(x => x.Id))
            && typedReport.Kayitlar.Select(x => x.Id).Order().SequenceEqual(typedIds),
            "New-device-type filtering happens before counting, ordering and pagination without catalog normalization");
        typeFilter.Sayfa = 2;
        typeFilter.SayfaBoyutu = 10;
        typeFilter.DetayTalepId = ownRecords[1].Id;
        check((await reader.RaporAsync(typeFilter, personnel, false, primary.Id)).Sayfa == 2,
            "A detail target excluded by the selected device filter cannot override the requested page");

        foreach (var q in new[] { "unmatched-toolbar", "%", "_", "private-old-type", "private-project", "private-team" })
        {
            var filter = new YkcTalepListeFiltre { Q = q, TesisatNo = prefix, Sirala = "abone-artan" };
            check((await reader.ListeAsync(filter, personnel, false, primary.Id)).Toplam == 0
                && (await reader.RaporAsync(filter, personnel, false, primary.Id)).Toplam == 0
                && (await reader.RaporKayitlariAsync(filter, personnel, false, 100, primary.Id)).Count == 0,
                "Unmatched, literal wildcard and hidden operational fields cannot leak through q: " + q);
        }
        var firmUser = new AppKullanici
        {
            KullaniciTipi = KullaniciTipiDegerleri.Personel, FirmaId = firm.Id, SirketId = secondary.Id
        };
        var privateFilter = new YkcTalepListeFiltre { Q = prefix, Sirala = "abone-artan", CihazTipi = rawType, SayfaBoyutu = 100 };
        var privateList = await reader.ListeAsync(privateFilter, firmUser, true, primary.Id);
        var privateReport = await reader.RaporAsync(privateFilter, firmUser, true, primary.Id);
        var privateExport = await reader.RaporKayitlariAsync(privateFilter, firmUser, true, 100, primary.Id);
        var ownTypeIds = ownRecords.Where(x => x.YeniCihazTipi == rawType).Select(x => x.Id).Order().ToArray();
        check(privateList.Toplam == ownTypeIds.Length && privateReport.Toplam == ownTypeIds.Length
            && privateList.Talepler.Select(x => x.Id).Order().SequenceEqual(ownTypeIds)
            && privateReport.Kayitlar.Select(x => x.Id).Order().SequenceEqual(ownTypeIds)
            && privateExport.Select(x => x.Id).Order().SequenceEqual(ownTypeIds)
            && privateList.Talepler.All(x => x.ProjeNo == null && x.AtananEkip == null && x.ProjedekiCihazBilgisi == null)
            && privateReport.HedefOzetleri.Count == 0 && privateReport.EkipOzetleri.Count == 0
            && privateReport.Kayitlar.Concat(privateExport).All(x => x.ProjeNo == null && x.EskiCihazTipi == null
                && x.AtananEkip == null && x.HedefUygulama == null),
            "Sorted/filtered firm-bound list, report and export retain firm privacy despite stale privilege and account type");
        privateFilter.TalepId = sibling.Id;
        check((await reader.ListeAsync(privateFilter, firmUser, true, primary.Id)).Toplam == 0
            && (await reader.RaporKayitlariAsync(privateFilter, firmUser, true, 100, primary.Id)).Count == 0,
            "Even same-company sibling requests cannot be reached using the exact request-ID column filter");
        privateFilter.TalepId = null;
        privateFilter.CihazTipi = "private-old-type";
        check((await reader.ListeAsync(privateFilter, firmUser, true, primary.Id)).Toplam == 0,
            "Device filtering cannot infer the hidden project-side device type of a firm request");
        await VerifyYkcFilterPrivacyAndDatesAsync(db, primary, secondary, firm, siblingFirm, foreignFirm, admin, check);
    }

    private static async Task VerifyYkcFilterPrivacyAndDatesAsync(AppDbContext db, Dag_Sirket primary, Dag_Sirket secondary,
        Ys_Firma firm, Ys_Firma siblingFirm, Ys_Firma foreignFirm, AppKullanici admin, Action<bool, string> check)
    {
        const string prefix = "YKC-FILTER-INTEGRITY-";
        const string oldBrand = "private-source-brand";
        const string newBrand = "visible-new-brand";
        var requestDay = new DateTime(2026, 1, 10);
        var appointmentDay = new DateTime(2026, 2, 20);
        var types = new string?[] { "Sofben", "\u015eofben", "Kombi", "Ocak", "Diger", "Di\u011fer", "legacy/type-42", null };
        var records = types.Select((type, index) => new Ykc_Talep
        {
            FirmaId = firm.Id, SirketId = primary.Id, TesisatNo = prefix + index,
            MusteriAdi = "Filter integrity " + index, YeniCihazTipi = type, EskiCihazTipi = "Sofben",
            EskiMarka = oldBrand, YeniMarka = newBrand, TalepTarihi = requestDay.AddDays(index),
            RandevuTarihi = appointmentDay.AddHours(12), Durum = YkcDurumDegerleri.TalepAlindi
        }).ToArray();
        records[0].RandevuTarihi = appointmentDay;
        records[1].RandevuTarihi = appointmentDay.AddDays(1).AddTicks(-1);
        records[2].RandevuTarihi = appointmentDay.AddDays(1);
        records[3].RandevuTarihi = appointmentDay.AddDays(-1);
        records[4].RandevuTarihi = null;
        foreach (var index in new[] { 2, 3, 4 }) records[index].TalepTarihi = appointmentDay;
        Ykc_Talep Extra(Ys_Firma owner, int companyId, string suffix, bool deleted = false) => new()
        {
            FirmaId = owner.Id, SirketId = companyId, TesisatNo = prefix + suffix,
            MusteriAdi = "Filter integrity " + suffix, YeniCihazTipi = "Sofben", EskiCihazTipi = "Sofben",
            EskiMarka = oldBrand, YeniMarka = newBrand, TalepTarihi = requestDay,
            RandevuTarihi = appointmentDay, SilindiMi = deleted, Durum = YkcDurumDegerleri.TalepAlindi
        };
        var sibling = Extra(siblingFirm, primary.Id, "sibling");
        var foreign = Extra(foreignFirm, secondary.Id, "foreign");
        var deleted = Extra(firm, primary.Id, "deleted", true);
        db.AddRange(records);
        db.AddRange(sibling, foreign, deleted);
        await db.SaveChangesAsync();
        var reader = new YkcTalepOkumaService(db);
        var personnel = new AppKullanici { KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = secondary.Id };
        var certified = new AppKullanici { KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma, FirmaId = firm.Id };
        var driftedFirm = new AppKullanici { KullaniciTipi = KullaniciTipiDegerleri.Personel, FirmaId = firm.Id };

        async Task Matches(YkcTalepListeFiltre filter, AppKullanici actor, bool privileged, int[] ids, string label)
        {
            filter.TesisatNo = prefix;
            filter.SayfaBoyutu = 100;
            filter.Sirala = "abone-artan";
            var list = await reader.ListeAsync(filter, actor, privileged, primary.Id);
            var report = await reader.RaporAsync(filter, actor, privileged, primary.Id);
            var exported = await reader.RaporKayitlariAsync(filter, actor, privileged, 100, primary.Id);
            check(list.Toplam == ids.Length && report.Toplam == ids.Length
                && report.DurumOzetleri.Sum(x => x.Sayi) == ids.Length
                && list.Talepler.Select(x => x.Id).Order().SequenceEqual(ids.Order())
                && report.Kayitlar.Select(x => x.Id).SequenceEqual(list.Talepler.Select(x => x.Id))
                && exported.Select(x => x.Id).SequenceEqual(list.Talepler.Select(x => x.Id)), label);
        }

        var selectedIds = records.Select(x => x.Id).Concat([sibling.Id, foreign.Id, deleted.Id]).ToList();
        foreach (var (actor, privileged, privateView) in new[]
        {
            (personnel, false, false), (admin, true, false), (certified, false, true), (driftedFirm, true, true)
        })
        {
            foreach (var brand in new[] { oldBrand, newBrand, "unmatched-brand" })
            {
                var ids = brand == "unmatched-brand" || (brand == oldBrand && privateView)
                    ? Array.Empty<int>()
                    : records.Select(x => x.Id).Concat(privateView ? Array.Empty<int>() : [sibling.Id]).ToArray();
                await Matches(new() { Marka = brand, Q = prefix, KayitIdleri = selectedIds, DetayTalepId = records[0].Id },
                    actor, privileged, ids,
                    $"Brand filtering hides old source brands from firm counters/list/report/export while retaining internal old/new search: {brand}, firm={privateView}, admin={privileged}");
            }
        }

        var typeCases = new (string? Token, int[] OwnIds, bool IncludesSibling)[]
        {
            ("\u015eofben", [records[0].Id, records[1].Id], true),
            ("Sofben", [records[0].Id, records[1].Id], true),
            (" SOFBEN ", [records[0].Id, records[1].Id], true),
            ("Di\u011fer", [records[4].Id, records[5].Id], false),
            ("DIGER", [records[4].Id, records[5].Id], false),
            ("Kombi", [records[2].Id], false),
            ("Ocak", [records[3].Id], false),
            ("legacy/type-42", [records[6].Id], false),
            ("unmatched-raw-type", [], false),
            (null, records.Select(x => x.Id).ToArray(), true)
        };
        foreach (var (actor, privateView) in new[] { (personnel, false), (certified, true) })
        {
            foreach (var (token, ownIds, includesSibling) in typeCases)
            {
                var ids = ownIds.Concat(!privateView && includesSibling ? [sibling.Id] : Array.Empty<int>()).ToArray();
                await Matches(new() { CihazTipi = token }, actor, false, ids,
                    $"Device display/source aliases match in scoped SQL without whitelisting Kombi/Ocak/unknown/null types or searching hidden old types: {token}, firm={privateView}");
            }
        }

        var dateCases = new (YkcTalepListeFiltre Filter, int[] OwnIndexes, bool IncludesSibling, string Label)[]
        {
            (new() { RandevuBaslangicTarihi = appointmentDay.AddHours(15), RandevuBitisTarihi = appointmentDay.AddHours(8) },
                [0, 1, 5, 6, 7], true, "Appointment range includes midnight/day-end, excludes next-day and unscheduled records regardless of request date"),
            (new() { RandevuBaslangicTarihi = appointmentDay }, [0, 1, 2, 5, 6, 7], true,
                "Appointment start alone filters the appointment field and excludes null/before-start dates"),
            (new() { RandevuBitisTarihi = appointmentDay }, [0, 1, 3, 5, 6, 7], true,
                "Appointment end alone is inclusive through day-end and excludes null/next-day dates"),
            (new() { RandevuBaslangicTarihi = appointmentDay.AddDays(1), RandevuBitisTarihi = appointmentDay.AddDays(1) }, [2], false,
                "A next-day appointment is selected by its own date, not the request creation date"),
            (new() { BaslangicTarihi = appointmentDay, BitisTarihi = appointmentDay }, [2, 3, 4], false,
                "Existing bas/bit still filter request dates, including unscheduled requests when no appointment filter is set"),
            (new() { BaslangicTarihi = requestDay, BitisTarihi = requestDay.AddDays(1),
                RandevuBaslangicTarihi = appointmentDay, RandevuBitisTarihi = appointmentDay }, [0, 1], true,
                "Request and appointment date ranges intersect rather than replacing each other's semantics")
        };
        foreach (var (actor, privateView) in new[] { (personnel, false), (certified, true) })
        {
            foreach (var (filter, ownIndexes, includesSibling, label) in dateCases)
            {
                var ids = ownIndexes.Select(x => records[x].Id)
                    .Concat(!privateView && includesSibling ? [sibling.Id] : Array.Empty<int>()).ToArray();
                await Matches(filter, actor, false, ids, label + ": firm=" + privateView);
            }
        }
    }
}
