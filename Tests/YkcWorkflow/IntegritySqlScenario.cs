using System.Text.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class IntegritySqlScenario
{
    public static async Task RunAsync()
    {
        var name = "YsIntegrityTest_" + Guid.NewGuid().ToString("N");
        var documentRoot = Path.Combine(Path.GetTempPath(), name);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(
            $@"Server=(localdb)\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true").Options;
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
            var company = new Dag_Sirket { SirketAdi = "Integrity fixture" };
            var firm = new Ys_Firma { FirmaAdi = "Fixture service", Sirket = company, AktifMi = true };
            var otherFirm = new Ys_Firma { FirmaAdi = "Other firm", Sirket = company, AktifMi = true, VergiNo = "1234567890" };
            var brand = new Ys_Marka { MarkaAdi = "Isı", AktifMi = true };
            var addedBrand = new Ys_Marka { MarkaAdi = "Vaillant", AktifMi = true };
            var inactiveBrand = new Ys_Marka { MarkaAdi = "Inactive", AktifMi = false };
            var category = new UrunKategori { Ad = "Kombi" };
            var addedCategory = new UrunKategori { Ad = "Ocak" };
            db.AddRange(company, firm, otherFirm, brand, addedBrand, inactiveBrand, category, addedCategory);
            await db.SaveChangesAsync();
            var user = new AppKullanici
            {
                UserName = "grant-editor", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis,
                FirmaId = firm.Id, SirketId = company.Id
            };
            db.Users.Add(user);
            var oldDate = DateTime.Today.AddYears(-2);
            var expiry = DateTime.Today.AddYears(3);
            var oldBrand = new Ys_FirmaMarka
            {
                FirmaId = firm.Id, MarkaId = brand.Id, OlusturmaTarihi = oldDate,
                OlusturanKullanici = "original-author", YetkiBitisTarihi = expiry
            };
            var oldCategory = new Ys_FirmaKategori
            {
                FirmaId = firm.Id, KategoriId = category.Id, OlusturmaTarihi = oldDate, YetkiBitisTarihi = expiry
            };
            db.AddRange(oldBrand, oldCategory);
            await db.SaveChangesAsync();
            var panel = new YetkiliServisPanelYonetimApiService(db);
            Check(!(await panel.MarkaEkleAsync(new() { MarkaAdi = "Forbidden" }, user)).Basarili,
                "Service cannot create a shared brand");
            Check(!(await panel.MarkaDuzenleAsync(new() { Id = brand.Id, MarkaAdi = "Forbidden" }, user)).Basarili
                && await db.Ys_Markalar.CountAsync() == 3 && (await db.Ys_Markalar.FindAsync(brand.Id))!.MarkaAdi == "Isı",
                "Service cannot rename a shared brand");

            var admin = new AdminYetkiliServisYonetimApiService(db,
                new SehirFirmaKoduService(new ConfigurationBuilder().Build(), db));
            AdminYetkiliServisKaydetDto Edit(List<int>? brands, List<int>? categories) => new()
            {
                Id = firm.Id, FirmaAdi = firm.FirmaAdi!, AktifMi = true, MarkaIds = brands, KategoriIds = categories
            };
            Check((await admin.GuncelleAsync(Edit([brand.Id], [category.Id]), user, company.Id)).Basarili,
                "Unchanged firm grants can be saved");
            db.ChangeTracker.Clear();
            var retainedBrand = await db.Ys_FirmaMarkalar.SingleAsync();
            var retainedCategory = await db.Ys_FirmaKategoriler.SingleAsync();
            Check(retainedBrand.Id == oldBrand.Id && retainedBrand.YetkiBitisTarihi == expiry
                && retainedBrand.OlusturmaTarihi == oldDate && retainedBrand.OlusturanKullanici == "original-author"
                && retainedCategory.Id == oldCategory.Id && retainedCategory.YetkiBitisTarihi == expiry,
                "Unchanged brand and category IDs, dates and authors are preserved");
            Check((await admin.GuncelleAsync(Edit([brand.Id, addedBrand.Id, addedBrand.Id], [category.Id, addedCategory.Id]), user, company.Id)).Basarili,
                "Only new grants are added and duplicate selections are normalized");
            var newBrand = await db.Ys_FirmaMarkalar.SingleAsync(x => x.MarkaId == addedBrand.Id);
            var newCategory = await db.Ys_FirmaKategoriler.SingleAsync(x => x.KategoriId == addedCategory.Id);
            Check(newBrand.YetkiBitisTarihi == newBrand.OlusturmaTarihi.AddYears(1)
                && newCategory.YetkiBitisTarihi == newCategory.OlusturmaTarihi.AddYears(1),
                "New brand and category grants last exactly one year");
            Check((await admin.GuncelleAsync(Edit([addedBrand.Id], [addedCategory.Id]), user, company.Id)).Basarili,
                "Removed selections can be saved");
            db.ChangeTracker.Clear();
            Check(await db.Ys_FirmaMarkalar.AnyAsync(x => x.Id == oldBrand.Id && x.SilindiMi && x.SilenKullanici == user.UserName)
                && await db.Ys_FirmaKategoriler.AnyAsync(x => x.Id == oldCategory.Id && x.SilindiMi && x.YetkiBitisTarihi == expiry),
                "Removed grants remain in history with original dates");
            var profile = await db.Ys_Firmalar.Include(x => x.FirmaMarkalar).SingleAsync(x => x.Id == firm.Id);
            Check(YsPanelFirmaDto.FromEntity(profile).FirmaMarkalar.All(x => x.MarkaId != brand.Id),
                "Removed grants do not reappear in the service profile");
            Check((await admin.GuncelleAsync(Edit([brand.Id, addedBrand.Id], [category.Id, addedCategory.Id]), user, company.Id)).Basarili
                && await db.Ys_FirmaMarkalar.CountAsync(x => x.MarkaId == brand.Id) == 2,
                "Re-adding a removed grant creates a new record without rewriting history");
            Check(!(await admin.GuncelleAsync(Edit([int.MaxValue], []), user, company.Id)).Basarili
                && await db.Ys_FirmaKategoriler.CountAsync(x => !x.SilindiMi) == 2,
                "Invalid brand selections cannot partially remove categories");
            Check(!(await admin.GuncelleAsync(Edit([], []), user, company.Id + 1000)).Basarili,
                "Firm update retains company isolation");
            Check((await panel.MarkaGuncelleAsync(new() { MarkaIds = [brand.Id, addedBrand.Id] }, user)).Basarili
                && (await db.Ys_FirmaMarkalar.SingleAsync(x => x.Id == newBrand.Id)).YetkiBitisTarihi == newBrand.YetkiBitisTarihi,
                "Service can select own brands without resetting their expiry");
            Check((await admin.GuncelleAsync(Edit(null, null), user, company.Id)).Basarili
                && await db.Ys_FirmaMarkalar.CountAsync(x => !x.SilindiMi) == 2,
                "Omitted selections do not clear existing grants");
            var brands = new MarkaService(db);
            Check((await brands.ListeleAsync(new() { TumunuGetir = true, Q = "ISI", AktifMi = true })).Single().Id == brand.Id,
                "API search supports Turkish casing and active filtering");
            Check((await brands.ListeleAsync(new() { TumunuGetir = true, AktifMi = false })).Single().Id == inactiveBrand.Id
                && (await brands.ListeleAsync(new())).All(x => x.AktifMi),
                "Public catalogue excludes inactive brands; authorized filter can find them");
            var catalogue = new MarkaKatalogApiService(db, brands);
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "GenelSistemAdmin")], "Fixture"));
            foreach (var active in new[] { false, true })
            {
                var saved = await catalogue.EkleAsync(new() { MarkaAdi = "New brand " + active, AktifMi = active }, principal);
                Check(saved.Veri is { Basarili: true, Id: not null }
                    && await db.Ys_Markalar.AnyAsync(x => x.Id == saved.Veri.Id && x.AktifMi == active),
                    "Brand creation preserves requested active state: " + active);
            }
            await db.UrunKategoriler.Where(x => x.Id == addedCategory.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, false));
            await db.Ys_Markalar.Where(x => x.Id == addedBrand.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SilindiMi, true));
            db.ChangeTracker.Clear();
            var editor = (await admin.EditorAsync(firm.Id, company.Id))!;
            Check(!editor.SeciliKategoriIds.Contains(addedCategory.Id)
                && editor.SeciliKategoriIds.All(id => editor.Kategoriler.Any(x => x.Id == id))
                && editor.Servis.Markalar.All(x => x.Id != addedBrand.Id),
                "Editor excludes inactive categories and deleted brands from active selections and DTOs");
            Check(await db.Ys_FirmaKategoriler.AnyAsync(x => x.Id == newCategory.Id && !x.SilindiMi
                    && x.YetkiBitisTarihi == newCategory.YetkiBitisTarihi),
                "Reading the editor does not alter existing authorization history");
            Check((await admin.GuncelleAsync(Edit(editor.SeciliMarkaIds, editor.SeciliKategoriIds), user, company.Id)).Basarili
                && await db.Ys_FirmaKategoriler.AnyAsync(x => x.Id == newCategory.Id && x.SilindiMi
                    && x.YetkiBitisTarihi == newCategory.YetkiBitisTarihi),
                "Filtered editor can save while removed grants retain historical dates");

            var legacy = new Ykc_Talep { TesisatNo = "123", SirketId = company.Id, FirmaId = otherFirm.Id };
            db.Ykc_Talepler.Add(legacy);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                DROP INDEX IX_Ykc_Talepler_SorguReferansi ON dbo.Ykc_Talepler;
                ALTER TABLE dbo.Ykc_Talepler DROP COLUMN SorguReferansi;
                """);
            var migration = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(),
                "DatabaseScripts", "2026-10-07_ykc_talep_tekrar_gonderim.sql"));
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET QUOTED_IDENTIFIER OFF; SET ARITHABORT OFF;");
            await db.Database.ExecuteSqlRawAsync(migration);
            await db.Database.ExecuteSqlRawAsync(migration);
            await db.Database.CloseConnectionAsync();
            db.ChangeTracker.Clear();
            Check(await db.Ykc_Talepler.AnyAsync(x => x.Id == legacy.Id && x.SorguReferansi == null),
                "Migration is repeatable and preserves historical requests with no reference");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Ykc_Talepler ADD Vkn nvarchar(32) NULL;");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.Ykc_Talepler SET Vkn = {otherFirm.VergiNo} WHERE Id = {legacy.Id}");
            var removeVkn = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(),
                "DatabaseScripts", "2026-10-08_ykc_talep_vkn_kaldir.sql"));
            await db.Database.ExecuteSqlRawAsync(removeVkn);
            await db.Database.ExecuteSqlRawAsync(removeVkn);
            db.ChangeTracker.Clear();
            Check(await db.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*) AS [Value] FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.Ykc_Talepler') AND name = N'Vkn'
                """).SingleAsync() == 0,
                "VKN migration removes only the redundant column and can run twice");
            Check(await db.Ykc_Talepler.CountAsync() == 1
                && await db.Ykc_Talepler.AnyAsync(x => x.Id == legacy.Id && x.FirmaId == otherFirm.Id
                    && x.SirketId == company.Id && x.TesisatNo == "123")
                && await db.Ys_Firmalar.AnyAsync(x => x.Id == otherFirm.Id && x.VergiNo == "1234567890"),
                "Removing request VKN preserves requests, firm links and the firm's tax number");
            var source = new YkcTalepKaydetDto
            {
                FirmaId = firm.Id, SirketId = company.Id, TesisatNo = "123456", SozlesmeNo = "001234",
                ProjeNo = "INTERNAL-PROJECT", MusteriAdi = "Fixture Customer", Il = "Test", Ilce = "Test",
                EskiCihazTipi = "Kombi", EskiMarka = "Old brand", EskiKapasite = "20000",
                IzinliYeniCihazTipleri = new(StringComparer.OrdinalIgnoreCase) { ["Kombi"] = "K" }
            };
            var reference = await new SqlYkcSorguKaydiService(db).EkleAsync(user.Id, source);
            YkcTalepKaydetDto Request(string key) => new()
            {
                SorguReferansi = key, TesisatNo = source.TesisatNo, SozlesmeNo = source.SozlesmeNo,
                YeniCihazTipi = "Kombi", YeniMarka = "Vaillant", YeniBacaTipi = "Hermetik",
                YeniKapasite = "20000", IkinciElCihazMi = false
            };
            async Task<YkcIslemSonuc> Submit()
            {
                await using var context = new AppDbContext(options);
                return await new YkcTalepService(context, new SqlYkcSorguKaydiService(context)).OlusturAsync(Request(reference), user);
            }
            var results = await Task.WhenAll(Submit(), Submit(), Submit());
            Check(results.All(x => x.Basarili) && results.Select(x => x.Id).Distinct().Count() == 1,
                "Concurrent request submissions return one request ID");
            var requestId = results[0].Id!.Value;
            Check(await db.Ykc_Talepler.CountAsync(x => x.SorguReferansi == reference) == 1
                && await db.Ykc_Fr265Kontroller.CountAsync(x => x.TalepId == requestId) == 5
                && await db.Ykc_IslemGecmisi.CountAsync(x => x.TalepId == requestId) == 1,
                "Retries do not duplicate requests, controls or history");
            Check((await Submit()).Id == requestId, "Later retry returns the original request");
            var service = new YkcTalepService(db, new SqlYkcSorguKaydiService(db));
            var foreign = new AppKullanici { Id = "different-user", FirmaId = otherFirm.Id, SirketId = company.Id };
            Check(!(await service.OlusturAsync(Request(reference), foreign)).Basarili,
                "Another user cannot replay a captured reference");
            user.KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma;
            await db.Ykc_Talepler.Where(x => x.Id == requestId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.AtananEkip, "Private team").SetProperty(x => x.HedefUygulama, "INTERNAL-TARGET"));
            var list = await new YkcTalepOkumaService(db).ListeAsync(new(), user, false);
            var report = await new YkcTalepOkumaService(db).RaporAsync(new(), user, false);
            Check(list.Talepler.Single().ProjeNo == null && report.Kayitlar.Single().ProjeNo == null,
                "Firm list and report responses omit internal project information");
            Check(report.HedefOzetleri.Count == 0 && report.EkipOzetleri.Count == 0,
                "Firm report service omits internal routing aggregates as well as row values");
            var driftedFirm = new AppKullanici
            {
                Id = user.Id, FirmaId = firm.Id, SirketId = company.Id, KullaniciTipi = KullaniciTipiDegerleri.Personel
            };
            var read = new YkcTalepOkumaService(db);
            var driftedList = await read.ListeAsync(new(), driftedFirm, false);
            var driftedDashboard = await read.DashboardOzetAsync(driftedFirm, false);
            var driftedReport = await read.RaporAsync(new(), driftedFirm, false);
            var driftedExport = await read.RaporKayitlariAsync(new(), driftedFirm, false, 100);
            Check(driftedList.Talepler.Single().ProjeNo == null && driftedList.Talepler.Single().AtananEkip == null
                && driftedDashboard.SonTalepler.Single().ProjeNo == null && driftedDashboard.SonTalepler.Single().HedefUygulama == null
                && driftedReport.EkipOzetleri.Count == 0 && driftedReport.Kayitlar.Single().EskiMarka == null
                && driftedExport.Single().ProjeNo == null && driftedExport.Single().AtananEkip == null,
                "Firm-bound reads stay private when account type and firm role are inconsistent");
            var internalReport = await read.RaporAsync(new(), new AppKullanici { SirketId = company.Id }, false);
            Check(internalReport.EkipOzetleri.Single().Ad == "Private team"
                && internalReport.HedefOzetleri.Any(x => x.Ad == "INTERNAL-TARGET")
                && internalReport.Kayitlar.Single(x => x.Id == requestId).ProjeNo == source.ProjeNo,
                "Internal personnel retain source details and operational report aggregates");
            db.Roles.Add(new IdentityRole("SertifikaliFirma") { NormalizedName = "SERTIFIKALIFIRMA" });
            await db.SaveChangesAsync();
            await db.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.KullaniciTipi, KullaniciTipiDegerleri.Personel));
            db.ChangeTracker.Clear();
            using var users = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db), Options.Create(new IdentityOptions()),
                new PasswordHasher<AppKullanici>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
                null!, NullLogger<UserManager<AppKullanici>>.Instance);
            var storedActor = (await users.FindByIdAsync(user.Id))!;
            Check((await users.AddToRoleAsync(storedActor, "SertifikaliFirma")).Succeeded,
                "Fixture creates a persisted firm role with a mismatched account type");
            var api = new YkcApiController(service, read, users, null!, db, null!, new YkcYetkiService(db, users),
                null!, new YkcPlanlamaOkumaService(db), null!)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([
                            new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, "SertifikaliFirma")], "Fixture"))
                    }
                }
            };
            var apiList = (YkcTalepListeSonuc)((OkObjectResult)await api.TaleplerListe(new())).Value!;
            Check(apiList.Talepler.Single().ProjeNo == null && apiList.Talepler.Single().AtananEkip == null,
                "Actual list endpoint hides internal fields despite role/type mismatch");
            Check(await api.TaleplerRapor(new()) is ObjectResult { StatusCode: 403 }
                && await api.TaleplerRaporExcel(new()) is ObjectResult { StatusCode: 403 },
                "Privacy hardening does not grant firm users report or export access");
            Check(await api.Takvim(new() { Baslangic = DateTime.MaxValue, Bitis = DateTime.MaxValue }) is BadRequestObjectResult,
                "Extreme calendar input returns HTTP 400 instead of a server error");
            var calendar = new YkcPlanlamaOkumaService(db);
            var extremeDates = new YkcTakvimFiltre { Baslangic = DateTime.MaxValue.Date.AddDays(-2), Bitis = DateTime.MinValue };
            await calendar.TakvimAsync(extremeDates, user, false);
            Check(extremeDates.Bitis == DateTime.MaxValue.Date.AddDays(-1),
                "Near-maximum calendar range remains bounded without arithmetic overflow");
            var normalDates = new YkcTakvimFiltre { Baslangic = DateTime.Today, Bitis = DateTime.Today.AddDays(-1) };
            await calendar.TakvimAsync(normalDates, user, false);
            Check(normalDates.Bitis == DateTime.Today.AddDays(6), "Normal reversed calendar range retains its seven-day fallback");
            var installationList = await new IcTesisatDevreyeAlmaApiService(db).ListeleAsync(
                new() { Sayfa = int.MaxValue, SayfaBoyutu = 500 }, new AppKullanici(), ["GenelSistemAdmin"]);
            Check(installationList is { Sayfa: 1 }, "Extreme page number resolves an empty scoped list without SQL OFFSET overflow");
            var detail = (await new YkcTalepOkumaService(db).GetirAsync(requestId, user, false))!;
            YkcFirmaSunumu.Hazirla(detail, resmiForm: false);
            var json = JsonSerializer.Serialize(detail);
            Check(!json.Contains("INTERNAL-PROJECT") && !json.Contains("\"ProjeNo\"") && detail.EskiKapasite == null,
                "Normal firm detail JSON contains neither the project value nor the project field");
            var official = (await new YkcTalepOkumaService(db).GetirAsync(requestId, user, false))!;
            YkcFirmaSunumu.Hazirla(official, resmiForm: true);
            Check(official.ProjeNo == source.ProjeNo && official.EskiKapasite == source.EskiKapasite,
                "Official FR265 data retains project and device values");
            var uploads = new YkcBelgeYuklemeApiService(service, new TestEnvironment { ContentRootPath = documentRoot });
            var storage = Path.Combine(documentRoot, "App_Data", "ykc-belgeler");
            foreach (var (fileName, contentType, bytes) in new[]
            {
                ("../../../outside.pdf", "application/pdf", "%PDF-1.4\n%%EOF"u8.ToArray()),
                (@"C:\outside\device.pdf", "application/pdf", "%PDF-1.4\n%%EOF"u8.ToArray()),
                ("technical attachment.jpeg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 }),
                ("device.png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            })
            {
                using var input = new MemoryStream(bytes);
                var upload = new FormFile(input, 0, bytes.Length, "Dosya", fileName)
                { Headers = new HeaderDictionary(), ContentType = contentType };
                Check((await uploads.YukleAsync(requestId, null, upload, user, false, company.Id)).Basarili,
                    "Supported technical upload succeeds: " + contentType);
                var stored = await db.Ykc_FormDosyalari.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
                var generatedName = stored.DosyaYolu!.Split('/').Last();
                var physical = Path.Combine(storage, requestId.ToString(), generatedName);
                Check(Guid.TryParseExact(Path.GetFileNameWithoutExtension(generatedName), "N", out _)
                    && stored.DosyaYolu.StartsWith($"ykc/{requestId}/", StringComparison.Ordinal)
                    && PrivateDocumentStorage.IsInRoot(physical, storage)
                    && File.Exists(physical) && (await File.ReadAllBytesAsync(physical)).SequenceEqual(bytes)
                    && stored.BelgeHash == Convert.ToHexString(SHA256.HashData(bytes))
                    && stored.DosyaAdi == Path.GetFileName(fileName.Replace('\\', '/')),
                    "Private upload preserves content, display name and hash without trusting the supplied path");
            }
            var savedFiles = Directory.GetFiles(storage, "*", SearchOption.AllDirectories).Length;
            using (var input = new MemoryStream("%PDF-1.4\n%%EOF"u8.ToArray()))
            {
                var upload = new FormFile(input, 0, input.Length, "Dosya", "foreign.pdf")
                { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
                Check(!(await uploads.YukleAsync(requestId, null, upload, foreign, false, company.Id)).Basarili
                    && Directory.GetFiles(storage, "*", SearchOption.AllDirectories).Length == savedFiles,
                    "Cross-firm upload is rejected without leaving a physical file");
            }
            var another = await new SqlYkcSorguKaydiService(db).EkleAsync(user.Id, source);
            var fresh = await service.OlusturAsync(Request(another), user);
            Check(fresh.Basarili && fresh.Id != requestId,
                "A fresh source query can create a legitimate subsequent request");
            await db.Ykc_Talepler.Where(x => x.Id == requestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SilindiMi, true));
            db.ChangeTracker.Clear();
            Check(!(await Submit()).Basarili, "Deleting a request does not make its reference reusable");
            db.Ykc_Talepler.Add(new() { SorguReferansi = reference });
            var uniqueRejected = false;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { uniqueRejected = true; }
            Check(uniqueRejected, "Database uniqueness blocks writers that bypass the service");
            Console.WriteLine($"{passed} integrity SQL checks passed.");
        }
        finally
        {
            if (Directory.Exists(documentRoot) && PrivateDocumentStorage.IsInRoot(documentRoot, Path.GetTempPath())
                && Path.GetFileName(documentRoot).StartsWith("YsIntegrityTest_", StringComparison.Ordinal))
                Directory.Delete(documentRoot, true);
            if (created && db.Database.GetDbConnection().Database == name && name.StartsWith("YsIntegrityTest_", StringComparison.Ordinal))
                await db.Database.EnsureDeletedAsync();
        }
    }
}
