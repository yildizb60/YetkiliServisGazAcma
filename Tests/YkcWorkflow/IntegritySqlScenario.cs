using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
            var otherFirm = new Ys_Firma { FirmaAdi = "Other firm", Sirket = company, AktifMi = true };
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

            var legacy = new Ykc_Talep { TesisatNo = "123", SirketId = company.Id };
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
            var list = await new YkcTalepOkumaService(db).ListeAsync(new(), user, false);
            var report = await new YkcTalepOkumaService(db).RaporAsync(new(), user, false);
            Check(list.Talepler.Single().ProjeNo == null && report.Kayitlar.Single().ProjeNo == null,
                "Firm list and report responses omit internal project information");
            var detail = (await new YkcTalepOkumaService(db).GetirAsync(requestId, user, false))!;
            YkcFirmaSunumu.Hazirla(detail, resmiForm: false);
            var json = JsonSerializer.Serialize(detail);
            Check(!json.Contains("INTERNAL-PROJECT") && !json.Contains("\"ProjeNo\"") && detail.EskiKapasite == null,
                "Normal firm detail JSON contains neither the project value nor the project field");
            var official = (await new YkcTalepOkumaService(db).GetirAsync(requestId, user, false))!;
            YkcFirmaSunumu.Hazirla(official, resmiForm: true);
            Check(official.ProjeNo == source.ProjeNo && official.EskiKapasite == source.EskiKapasite,
                "Official FR265 data retains project and device values");
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
            if (created && db.Database.GetDbConnection().Database == name && name.StartsWith("YsIntegrityTest_", StringComparison.Ordinal))
                await db.Database.EnsureDeletedAsync();
        }
    }
}
