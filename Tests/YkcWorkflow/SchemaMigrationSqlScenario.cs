using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class SchemaMigrationSqlScenario
{
    public static async Task RunAsync()
    {
        var name = "YsSecurityMigrationTest_" + Guid.NewGuid().ToString("N");
        var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true";
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
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
            if (!created) throw new InvalidOperationException("Test database already exists.");
            var company = new Dag_Sirket { SirketAdi = "Migration fixture" };
            var user = new AppKullanici { UserName = "history-owner", KullaniciTipi = KullaniciTipiDegerleri.Personel };
            db.AddRange(company, user);
            await db.SaveChangesAsync();
            db.Dag_PersonelYetkiler.Add(new() { KullaniciId = user.Id, SirketId = company.Id, YetkiTipi = YetkiTipleri.RAPOR_GOR });
            Ykc_Talep Request(bool reschedule, bool signed = false) => new()
            {
                SirketId = company.Id, Durum = YkcDurumDegerleri.SahaIsleminde,
                RandevuTarihi = reschedule ? DateTime.Today.AddDays(1) : DateTime.Today.AddDays(-2), RandevuSaati = "09:00",
                Kontroller = Enumerable.Range(1, 5).Select(no => new Ykc_Fr265Kontrol
                {
                    KontrolNo = no, Sonuc = no == 1 ? YkcFr265KontrolSonucDegerleri.Uygun : YkcFr265KontrolSonucDegerleri.Bekliyor,
                    KontrolTarihi = no == 1 ? DateTime.Now.AddDays(-1) : null,
                    KontrolEdenKullaniciId = no == 1 ? user.Id : null
                }).ToList(),
                Atamalar = [new Ykc_Atama
                {
                    OlusturmaTarihi = DateTime.Now.AddDays(-3), RandevuTarihi = DateTime.Today.AddDays(-2), RandevuSaati = "09:00"
                }],
                ImzaSurecleri = signed ? [new Ykc_ImzaSureci { ProviderDocumentId = "TEST-SIGNED", Durum = YkcImzaDurumDegerleri.ImzaBekliyor }] : []
            };
            var good = Request(false);
            var stale = Request(true);
            var signed = Request(true, true);
            db.AddRange(good, stale, signed);
            await db.SaveChangesAsync();
            var oldStaleAssignment = await db.Ykc_Atamalar.SingleAsync(x => x.TalepId == stale.Id);
            db.Ykc_Atamalar.AddRange(new[] { stale, signed }.Select(t => new Ykc_Atama
            {
                TalepId = t.Id, RandevuTarihi = t.RandevuTarihi, RandevuSaati = t.RandevuSaati,
                OlusturmaTarihi = DateTime.Now.AddHours(-1)
            }));
            await db.SaveChangesAsync();

            // Reproduce the pre-upgrade schema, including its destructive cascading foreign key.
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE dbo.Ykc_Fr265Kontroller DROP CONSTRAINT FK_Ykc_Fr265Kontroller_Ykc_Atamalar_AtamaId;
                DROP INDEX IX_Ykc_Fr265Kontroller_AtamaId ON dbo.Ykc_Fr265Kontroller;
                ALTER TABLE dbo.Ykc_Fr265Kontroller DROP COLUMN AtamaId;
                ALTER TABLE dbo.Ys_AspNetUsers DROP CONSTRAINT CK_Ys_AspNetUsers_ArsivPasif;
                ALTER TABLE dbo.Ys_AspNetUsers DROP COLUMN ArsivlemeTarihi, ArsivleyenKullaniciId;
                ALTER TABLE dbo.Ys_Dag_PersonelYetkiler DROP CONSTRAINT FK_Ys_Dag_PersonelYetkiler_Ys_AspNetUsers_KullaniciId;
                ALTER TABLE dbo.Ys_Dag_PersonelYetkiler ADD CONSTRAINT FK_Legacy_Permissions_User
                    FOREIGN KEY (KullaniciId) REFERENCES dbo.Ys_AspNetUsers(Id) ON DELETE CASCADE;
                """);
            db.ChangeTracker.Clear();
            var sql = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(),
                "DatabaseScripts", "2026-09-29_yetki_arsiv_randevu_guvenligi.sql"));
            async Task Migrate()
            {
                await using var sqlConnection = new SqlConnection(connection);
                await sqlConnection.OpenAsync();
                foreach (var batch in Regex.Split(
                    sql,
                    @"^\s*GO\s*$",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(2)))
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var command = new SqlCommand(batch, sqlConnection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            await Migrate();
            Check(await db.Users.CountAsync() == 1 && await db.Dag_PersonelYetkiler.CountAsync() == 1
                && await db.Ykc_Fr265Kontroller.CountAsync(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun) == 3,
                "Schema upgrade preserves accounts, grants and completed control results");
            Check(await db.Ykc_Fr265Kontroller.Where(x => x.TalepId == stale.Id && x.KontrolNo == 1)
                .AllAsync(x => x.AtamaId == oldStaleAssignment.Id), "Historical result is linked to its original appointment, not the new one");
            Check(await db.Ykc_Talepler.Where(x => x.Id == stale.Id).AllAsync(x => x.Durum == YkcDurumDegerleri.Atandi)
                && await db.Ykc_Fr265Kontroller.CountAsync(x => x.TalepId == stale.Id) == 10,
                "Legacy future appointment returns to planned state with fresh control slots");
            Check(await db.Ykc_Talepler.Where(x => x.Id == good.Id).AllAsync(x => x.Durum == YkcDurumDegerleri.SahaIsleminde)
                && await db.Ykc_Fr265Kontroller.CountAsync(x => x.TalepId == good.Id) == 5,
                "Valid current appointment and its successful result remain unchanged");
            Check(await db.Ykc_Fr265Kontroller.CountAsync(x => x.TalepId == signed.Id) == 5
                && await db.Ykc_Talepler.Where(x => x.Id == signed.Id).AllAsync(x => x.Durum == YkcDurumDegerleri.SahaIsleminde),
                "Already submitted signature workflow is not rewritten by migration");
            await Migrate();
            Check(await db.Ykc_Fr265Kontroller.CountAsync() == 20 && await db.Ykc_IslemGecmisi.CountAsync() == 1,
                "Migration can run twice without duplicating controls or history");
            Check(!await db.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*) AS [Value] FROM sys.foreign_keys
                WHERE parent_object_id = OBJECT_ID('dbo.Ys_Dag_PersonelYetkiler')
                AND referenced_object_id = OBJECT_ID('dbo.Ys_AspNetUsers') AND delete_referential_action <> 0
                """).AnyAsync(x => x != 0), "Legacy cascading permission deletion is removed");
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE dbo.Ykc_Talepler ADD RandevuId nvarchar(64) NULL,
                    IsEmriNo nvarchar(64) NULL, Aufnr nvarchar(64) NULL,
                    CallCenterTetiklendiMi bit NOT NULL CONSTRAINT DF_Test_CallCenterTetiklendiMi DEFAULT (0);
                """);
            var cleanup = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(),
                "DatabaseScripts", "2026-10-08_ykc_kullanilmayan_entegrasyon_alanlari.sql"));
            var protectedValues = new[]
            {
                (Name: "RandevuId", Set: "UPDATE dbo.Ykc_Talepler SET RandevuId=N'APPOINTMENT-123' WHERE Id={0}",
                    Clear: "UPDATE dbo.Ykc_Talepler SET RandevuId=NULL WHERE Id={0}"),
                (Name: "IsEmriNo", Set: "UPDATE dbo.Ykc_Talepler SET IsEmriNo=N'ORDER-123' WHERE Id={0}",
                    Clear: "UPDATE dbo.Ykc_Talepler SET IsEmriNo=NULL WHERE Id={0}"),
                (Name: "CallCenterTetiklendiMi", Set: "UPDATE dbo.Ykc_Talepler SET CallCenterTetiklendiMi=1 WHERE Id={0}",
                    Clear: "UPDATE dbo.Ykc_Talepler SET CallCenterTetiklendiMi=0 WHERE Id={0}"),
                (Name: "Aufnr", Set: "UPDATE dbo.Ykc_Talepler SET Aufnr=N'000000123456' WHERE Id={0}",
                    Clear: "UPDATE dbo.Ykc_Talepler SET Aufnr=NULL WHERE Id={0}")
            };
            const string remainingColumnsSql = """
                SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Ykc_Talepler')
                    AND name IN (N'RandevuId',N'IsEmriNo',N'Aufnr',N'CallCenterTetiklendiMi')
                """;
            foreach (var value in protectedValues)
            {
                await db.Database.ExecuteSqlRawAsync(value.Set, good.Id);
                try
                {
                    await db.Database.ExecuteSqlRawAsync(cleanup);
                    throw new InvalidOperationException("Cleanup discarded an external identifier or dispatch record.");
                }
                catch (SqlException ex) when (ex.Number == 51020)
                {
                    Check(await db.Database.SqlQueryRaw<int>(remainingColumnsSql).SingleAsync() == 4,
                        "Cleanup rolls back every column removal when populated: " + value.Name);
                }
                await db.Database.ExecuteSqlRawAsync(value.Clear, good.Id);
            }
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.Ykc_Talepler SET Aufnr={"string"} WHERE Id={good.Id}");
            await db.Database.ExecuteSqlRawAsync(cleanup);
            await db.Database.ExecuteSqlRawAsync(cleanup);
            db.ChangeTracker.Clear();
            Check(await db.Database.SqlQueryRaw<int>(remainingColumnsSql).SingleAsync() == 0,
                "Unused integration columns can be removed repeatedly, including the literal Swagger example");
            Check(await db.Ykc_Talepler.CountAsync() == 3 && await db.Ykc_Fr265Kontroller.CountAsync() == 20
                && await db.Ykc_IslemGecmisi.CountAsync() == 1 && await db.Ykc_Atamalar.CountAsync() == 5
                && await db.Ykc_ImzaSurecleri.AnyAsync(x => x.TalepId == signed.Id && x.ProviderDocumentId == "TEST-SIGNED"),
                "Cleanup preserves requests, appointment history, controls and active signature references");
            Check(protectedValues.All(value => db.Model.FindEntityType(typeof(Ykc_Talep))!.FindProperty(value.Name) is null
                    && typeof(YkcTalepKaydetDto).GetProperty(value.Name) is null
                    && typeof(YkcTalepDetayDto).GetProperty(value.Name) is null),
                "Removed integration placeholders no longer exist in the entity or public request contracts");
            Console.WriteLine($"{passed} migration SQL checks passed.");
        }
        finally
        {
            if (created && db.Database.GetDbConnection().Database == name && name.StartsWith("YsSecurityMigrationTest_", StringComparison.Ordinal))
                await db.Database.EnsureDeletedAsync();
        }
    }
}
