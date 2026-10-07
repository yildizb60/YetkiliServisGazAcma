using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Xml.Linq;
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
using YetkiliServisGazAcma.Business.Services.Online;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class CommissioningSqlScenario
{
    public static async Task RunAsync()
    {
        var databaseName = "YsCommissioningSqlTest_" + Guid.NewGuid().ToString("N");
        var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        await using var db = new AppDbContext(options);
        var created = false;
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        try
        {
            created = await db.Database.EnsureCreatedAsync();
            if (!created) throw new InvalidOperationException("The isolated test database already exists.");

            var company = new Dag_Sirket { SirketAdi = "Test distributor", Il = "Çorum" };
            var baymak = new Ys_Marka { MarkaAdi = "BAYMAK" };
            var arcelik = new Ys_Marka { MarkaAdi = "ARÇELİK" };
            var kombi = new UrunKategori { Ad = "KOMBİ" };
            var ocak = new UrunKategori { Ad = "Ocak" };
            db.AddRange(company, baymak, arcelik, kombi, ocak);
            await db.SaveChangesAsync();

            var firstFirm = new Ys_Firma
            {
                FirmaAdi = "Test service A", FaaliyetIli = "Çorum", SirketId = company.Id,
                OlusturmaTipi = YetkiliServisOlusturmaTipleri.Kayit
            };
            var secondFirm = new Ys_Firma
            {
                FirmaAdi = "Test service B", FaaliyetIli = "Çorum", SirketId = company.Id,
                OlusturmaTipi = YetkiliServisOlusturmaTipleri.Kayit
            };
            db.AddRange(firstFirm, secondFirm);
            await db.SaveChangesAsync();

            foreach (var firm in new[] { firstFirm, secondFirm })
            {
                db.Ys_YetkiBelgeleri.Add(new Ys_YetkiBelgesi
                {
                    FirmaId = firm.Id, Durum = YetkiBelgesiDurumDegerleri.Onaylandi,
                    YetkiBelgesiBaslangicTarihi = DateTime.Today.AddDays(-1),
                    YetkiBelgesiBitisTarihi = DateTime.Today.AddDays(30)
                });
                foreach (var brand in new[] { baymak, arcelik })
                    db.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = firm.Id, MarkaId = brand.Id,
                        YetkiBitisTarihi = DateTime.Today.AddDays(30)
                    });
                foreach (var category in new[] { kombi, ocak })
                    db.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                    {
                        FirmaId = firm.Id, KategoriId = category.Id,
                        YetkiBitisTarihi = DateTime.Today.AddDays(30)
                    });
            }
            var firstUser = new AppKullanici
            {
                UserName = "test-service-a", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis,
                FirmaId = firstFirm.Id
            };
            var secondUser = new AppKullanici
            {
                UserName = "test-service-b", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis,
                FirmaId = secondFirm.Id
            };
            db.Users.AddRange(firstUser, secondUser);
            await db.SaveChangesAsync();

            var soap = new CommissioningSoapHandler();
            using var http = new HttpClient(soap);
            var online = new OnlineCihazBilgileriClient(http,
                Options.Create(new OnlineServiceOptions { Enabled = true, Endpoint = "https://online.test/soap" }),
                NullLogger<OnlineCihazBilgileriClient>.Instance);
            var config = new ConfigurationBuilder().Build();
            var codes = new SehirFirmaKoduService(config, db);
            var manager = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db),
                Options.Create(new IdentityOptions()), new PasswordHasher<AppKullanici>(), [], [],
                new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
                NullLogger<UserManager<AppKullanici>>.Instance);

            YetkiliServisDevreyeAlmaApiController Controller(AppKullanici user)
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, user.Id)], "test"));
                var setup = new YetkiliServisIlkKurulumService(db);
                var authorization = new DevreyeAlmaYetkiDogrulamaService(db);
                return new YetkiliServisDevreyeAlmaApiController(manager,
                    new DevreyeAlmaKayitApiService(db, setup, authorization), new DevreyeAlmaExportApiService(db),
                    new DevreyeAlmaOkumaApiService(db, setup, authorization),
                    new DevreyeAlmaSorguApiService(db, online, codes, setup, authorization))
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext { User = principal }
                    }
                };
            }

            static T Body<T>(IActionResult result) => (result as OkObjectResult)?.Value is T value
                ? value : throw new InvalidOperationException($"Unexpected API result: {result.GetType().Name}");

            static YsDevreyeAlmaKaydetDto SaveRequest(string? reference, string serial)
            {
                var clientJson = JsonSerializer.Serialize(new
                {
                    SorguReferansi = reference,
                    CihazModeli = "Technician model", SeriNo = serial,
                    TeknisyenAdi = "Test technician", TeknisyenYetkiBelgesiNo = "TEST-123",
                    TesistatNo = "999999", MusteriAdi = "Forged customer", Adres = "Forged address",
                    CihazTipi = "Kazan", CihazMarka = "FORGED", CihazKapasite = "99999"
                });
                return JsonSerializer.Deserialize<YsDevreyeAlmaKaydetDto>(clientJson)!;
            }

            var firstController = Controller(firstUser);
            var queryRequest = new YsTesisatSorguDto { TesistatNo = "1000149", SozlesmeNo = "241584" };
            var firstQuery = Body<YsTesisatSorguSonucDto>(await firstController.TesisatSorgula(queryRequest));
            Check(firstQuery.Basarili && firstQuery.Cihazlar.Count == 3
                && firstQuery.Cihazlar.Select(x => x.SorguReferansi).Distinct().Count() == 3
                && soap.RequestCount == 1,
                "SOAP query persists a separate SQL reference for each source device");

            async Task<YsMarkaKontrolSonucDto> CheckDevice(string? reference, AppKullanici? user = null)
            {
                db.ChangeTracker.Clear();
                return Body<YsMarkaKontrolSonucDto>(await Controller(user ?? firstUser).MarkaKontrol(
                    new YsMarkaKontrolDto { SorguReferansi = reference }));
            }
            var stoveReference = firstQuery.Cihazlar[1].SorguReferansi;
            var stoveGrant = db.Ys_FirmaKategoriler.Where(x => x.FirmaId == firstFirm.Id && x.KategoriId == ocak.Id);
            Check((await CheckDevice(stoveReference)).Yetkili, "Device selection accepts an authorized source category and brand");
            await stoveGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.SilindiMi, true));
            var deniedStove = await CheckDevice(stoveReference);
            Check(!deniedStove.Yetkili && deniedStove.Mesaj == "Ocak cihaz tipinde işlem yetkiniz yok.",
                "Stove selection immediately explains the missing category permission despite valid brand permission");
            Check((await CheckDevice(firstQuery.Cihazlar[0].SorguReferansi)).Yetkili,
                "An unauthorized stove does not block an authorized boiler");
            await stoveGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.SilindiMi, false)
                .SetProperty(x => x.YetkiBitisTarihi, DateTime.Today.AddDays(-1)));
            Check(!(await CheckDevice(stoveReference)).Yetkili, "Expired category permission blocks device selection");
            await stoveGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.YetkiBitisTarihi, DateTime.Today));
            await db.UrunKategoriler.Where(x => x.Id == ocak.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, false));
            Check(!(await CheckDevice(stoveReference)).Yetkili, "Inactive category blocks device selection");
            await db.UrunKategoriler.Where(x => x.Id == ocak.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, true)
                .SetProperty(x => x.SilindiMi, true));
            Check(!(await CheckDevice(stoveReference)).Yetkili, "Deleted category blocks device selection");
            await db.UrunKategoriler.Where(x => x.Id == ocak.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SilindiMi, false));
            Check((await CheckDevice(stoveReference)).Yetkili, "Category permission remains valid on its last day");
            var stoveBrandGrant = db.Ys_FirmaMarkalar.Where(x => x.FirmaId == firstFirm.Id && x.MarkaId == arcelik.Id);
            await stoveBrandGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.YetkiBitisTarihi, DateTime.Today.AddDays(-1)));
            var deniedBrand = await CheckDevice(stoveReference);
            Check(!deniedBrand.Yetkili && deniedBrand.Mesaj!.Contains("markasında"),
                "Device selection still checks brand permission after category approval");
            await stoveBrandGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.YetkiBitisTarihi, DateTime.Today.AddDays(30)));
            Check(!(await CheckDevice(null)).Yetkili && !(await CheckDevice(new string('0', 64))).Yetkili,
                "Missing or invented source references cannot authorize device selection");
            Check(!(await CheckDevice(stoveReference, secondUser)).Yetkili,
                "Device selection cannot use another firm's source reference");
            var sameFirmUser = new AppKullanici { UserName = "test-service-a-other",
                KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis, FirmaId = firstFirm.Id };
            db.Users.Add(sameFirmUser);
            await db.SaveChangesAsync();
            Check(!(await CheckDevice(stoveReference, sameFirmUser)).Yetkili,
                "Device selection cannot use another user's source reference within the same firm");
            var stoveQuery = db.Ys_DevreyeAlmaSorguKayitlari.Where(x => x.Referans == stoveReference);
            await stoveQuery.ExecuteUpdateAsync(s => s.SetProperty(x => x.GecerlilikTarihi, DateTime.UtcNow.AddMinutes(-1)));
            Check(!(await CheckDevice(stoveReference)).Yetkili, "Expired source reference blocks device selection");
            await stoveQuery.ExecuteUpdateAsync(s => s.SetProperty(x => x.GecerlilikTarihi, DateTime.UtcNow.AddMinutes(20)));
            var originalSourceJson = await stoveQuery.Select(x => x.KaynakJson).SingleAsync();
            await stoveQuery.ExecuteUpdateAsync(s => s.SetProperty(x => x.KaynakJson, "invalid-json"));
            Check(!(await CheckDevice(stoveReference)).Yetkili, "Malformed source data fails closed during device selection");
            await stoveQuery.ExecuteUpdateAsync(s => s.SetProperty(x => x.KaynakJson, originalSourceJson));
            Check(!await db.Ys_DevreyeAlmalar.AnyAsync()
                && !await db.Ys_DevreyeAlmaSorguKayitlari.AnyAsync(x => x.DevreyeAlmaId != null),
                "Device permission checks do not save a record or consume a source reference");

            async Task Rejected(string label)
            {
                db.ChangeTracker.Clear();
                var result = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                    SaveRequest(firstQuery.Cihazlar[0].SorguReferansi, "SERIAL-A")));
                Check(!result.Basarili && !await db.Ys_DevreyeAlmalar.AnyAsync()
                    && !await db.Ys_DevreyeAlmaSorguKayitlari.AnyAsync(x => x.DevreyeAlmaId != null), label);
            }
            await db.Ys_Firmalar.Where(x => x.Id == firstFirm.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, false));
            await Rejected("Firm disabled after querying cannot commission a device");
            Check(!Body<YsTesisatSorguSonucDto>(await firstController.TesisatSorgula(queryRequest)).Basarili,
                "Inactive firm cannot query source devices");
            await db.Ys_Firmalar.Where(x => x.Id == firstFirm.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, true));
            var categoryGrant = db.Ys_FirmaKategoriler.Where(x => x.FirmaId == firstFirm.Id && x.KategoriId == kombi.Id);
            await categoryGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.SilindiMi, true));
            await Rejected("Having stove permission does not authorize boiler commissioning");
            await categoryGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.SilindiMi, false)
                .SetProperty(x => x.YetkiBitisTarihi, DateTime.Today.AddDays(-1)));
            await Rejected("Expired device category permission is rejected");
            await categoryGrant.ExecuteUpdateAsync(s => s.SetProperty(x => x.YetkiBitisTarihi, DateTime.Today));
            await db.UrunKategoriler.Where(x => x.Id == kombi.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, false));
            await Rejected("Inactive product category is rejected");
            await db.UrunKategoriler.Where(x => x.Id == kombi.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AktifMi, true));
            await db.Ys_YetkiBelgeleri.Where(x => x.FirmaId == firstFirm.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.YetkiBelgesiBitisTarihi, DateTime.Today.AddDays(-1)));
            await Rejected("Certificate expired after querying prevents saving");
            await db.Ys_YetkiBelgeleri.Where(x => x.FirmaId == firstFirm.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.YetkiBelgesiBitisTarihi, DateTime.Today.AddDays(30)));
            db.ChangeTracker.Clear();
            var firstSave = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                SaveRequest(firstQuery.Cihazlar[0].SorguReferansi, "SERIAL-A")));
            var firstRecord = await db.Ys_DevreyeAlmalar.AsNoTracking().SingleAsync();
            Check(firstSave.Basarili && firstRecord.TesistatNo == "1000149"
                && firstRecord.MusteriAdi == "Source customer"
                && firstRecord.Adres == "Source address"
                && firstRecord.CihazTipi == "Kombi" && firstRecord.CihazMarka == "BAYMAK"
                && firstRecord.CihazKapasite == "20000"
                && firstRecord.CihazModeli == "Technician model",
                "Save ignores forged client source fields and retains technician-entered fields");
            Check(!(await CheckDevice(firstQuery.Cihazlar[0].SorguReferansi)).Yetkili,
                "Consumed source reference cannot reopen an editable device form");

            await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { firstRecord });
            Check(firstRecord.SozlesmeNo == "241584" && firstRecord.SozlesmeNo != firstRecord.AboneNo,
                "Commissioning contract number comes from the consumed source query, not the subscriber number");
            Check(AdminDevreyeAlmaDto.FromEntity(firstRecord).SozlesmeNo == "241584"
                && YsDevreyeAlmaDto.FromEntity(firstRecord).SozlesmeNo == "241584",
                "Admin and service API contracts expose the source contract number consistently");
            var adminList = await new AdminRaporApiService(db).DevreyeAlmalarAsync(null, company.Id);
            var serviceList = Body<YsDevreyeAlmaGecmisDto>(await firstController.Gecmis(null));
            Check(adminList.Islemler.Single().SozlesmeNo == "241584"
                && serviceList.Islemler.Single().SozlesmeNo == "241584",
                "Both commissioning list endpoints populate the contract number");
            var legacyRecord = new Ys_DevreyeAlma { Id = -1, FirmaId = firstFirm.Id, AboneNo = "subscriber-only" };
            await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { legacyRecord });
            Check(legacyRecord.SozlesmeNo == null, "Legacy records do not relabel a subscriber number as a contract number");
            var validSource = await db.Ys_DevreyeAlmaSorguKayitlari.AsNoTracking()
                .SingleAsync(x => x.DevreyeAlmaId == firstRecord.Id);
            var source = JsonSerializer.Deserialize<YsDevreyeAlmaKaynak>(validSource.KaynakJson)!;
            source.SozlesmeNo = "other-contract";
            var invalidSources = new[]
            {
                new Ys_DevreyeAlmaSorguKaydi { Referans = Guid.NewGuid().ToString("N"), DevreyeAlmaId = firstRecord.Id,
                    FirmaId = secondFirm.Id, DagitimSirketiId = validSource.DagitimSirketiId,
                    KullaniciId = validSource.KullaniciId, KaynakJson = JsonSerializer.Serialize(source) },
                new Ys_DevreyeAlmaSorguKaydi { Referans = Guid.NewGuid().ToString("N"), DevreyeAlmaId = firstRecord.Id,
                    FirmaId = firstFirm.Id, DagitimSirketiId = validSource.DagitimSirketiId + 1000,
                    KullaniciId = validSource.KullaniciId, KaynakJson = JsonSerializer.Serialize(source) },
                new Ys_DevreyeAlmaSorguKaydi { Referans = Guid.NewGuid().ToString("N"), DevreyeAlmaId = firstRecord.Id,
                    FirmaId = firstFirm.Id, DagitimSirketiId = validSource.DagitimSirketiId,
                    KullaniciId = validSource.KullaniciId, KaynakJson = "not-json" }
            };
            db.Ys_DevreyeAlmaSorguKayitlari.AddRange(invalidSources);
            await db.SaveChangesAsync();
            await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { firstRecord });
            Check(firstRecord.SozlesmeNo == "241584", "Wrong firm, wrong distributor and malformed sources cannot replace the contract number");
            invalidSources[0].FirmaId = firstFirm.Id;
            await db.SaveChangesAsync();
            await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { firstRecord });
            Check(firstRecord.SozlesmeNo == null, "Conflicting contract numbers are not silently resolved to an arbitrary source");
            source.TesisatNo = "different-installation";
            invalidSources[0].KaynakJson = JsonSerializer.Serialize(source);
            await db.SaveChangesAsync();
            await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { firstRecord });
            Check(firstRecord.SozlesmeNo == "241584", "A source with different installation information cannot replace the contract number");
            db.Ys_DevreyeAlmaSorguKayitlari.RemoveRange(invalidSources);
            await db.SaveChangesAsync();

            var replay = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                SaveRequest(firstQuery.Cihazlar[0].SorguReferansi, "SERIAL-REPLAY")));
            Check(!replay.Basarili && await db.Ys_DevreyeAlmalar.CountAsync() == 1,
                "Consumed query reference cannot be replayed");

            var secondQuery = Body<YsTesisatSorguSonucDto>(await firstController.TesisatSorgula(queryRequest));
            Check(secondQuery.Basarili && secondQuery.Cihazlar[0].KaydedildiMi
                && !secondQuery.Cihazlar[1].KaydedildiMi && !secondQuery.Cihazlar[2].KaydedildiMi,
                "New query marks only the commissioned source slot as completed");
            var sameSource = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                SaveRequest(secondQuery.Cihazlar[0].SorguReferansi, "SERIAL-OTHER")));
            Check(!sameSource.Basarili && await db.Ys_DevreyeAlmalar.CountAsync() == 1,
                "Same source device cannot be commissioned again after a fresh query");

            var sameSerial = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                SaveRequest(secondQuery.Cihazlar[1].SorguReferansi, "serial-a")));
            Check(!sameSerial.Basarili && await db.Ys_DevreyeAlmalar.CountAsync() == 1,
                "Another source device cannot reuse the same installation serial");

            var identicalSource = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                SaveRequest(secondQuery.Cihazlar[2].SorguReferansi, "SERIAL-B")));
            var otherSource = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                SaveRequest(secondQuery.Cihazlar[1].SorguReferansi, "SERIAL-C")));
            Check(identicalSource.Basarili && otherSource.Basarili
                && await db.Ys_DevreyeAlmalar.CountAsync() == 3,
                "Two identical source devices and a different device can each be commissioned once");

            var secondController = Controller(secondUser);
            var otherFirmQuery = Body<YsTesisatSorguSonucDto>(await secondController.TesisatSorgula(queryRequest));
            var otherFirmSave = Body<YsDevreyeAlmaIslemSonucDto>(await secondController.Kaydet(
                SaveRequest(otherFirmQuery.Cihazlar[0].SorguReferansi, "SERIAL-D")));
            Check(!otherFirmSave.Basarili && otherFirmQuery.Cihazlar.All(x => x.KaydedildiMi)
                && await db.Ys_DevreyeAlmalar.CountAsync() == 3,
                "Another service firm under the same distributor cannot duplicate a device");

            var stolenReference = Body<YsDevreyeAlmaIslemSonucDto>(await secondController.Kaydet(
                SaveRequest(secondQuery.Cihazlar[0].SorguReferansi, "SERIAL-E")));
            Check(!stolenReference.Basarili, "Another user cannot use a query reference");

            var expiringQuery = Body<YsTesisatSorguSonucDto>(await firstController.TesisatSorgula(queryRequest));
            var expiringReference = expiringQuery.Cihazlar[0].SorguReferansi!;
            await db.Ys_DevreyeAlmaSorguKayitlari.Where(x => x.Referans == expiringReference)
                .ExecuteUpdateAsync(x => x.SetProperty(k => k.GecerlilikTarihi, DateTime.UtcNow.AddMinutes(-1)));
            db.ChangeTracker.Clear();
            var expired = Body<YsDevreyeAlmaIslemSonucDto>(await firstController.Kaydet(
                SaveRequest(expiringReference, "SERIAL-F")));
            Check(!expired.Basarili, "Expired query reference is rejected");

            await using (var duplicateDb = new AppDbContext(options))
            {
                duplicateDb.Ys_DevreyeAlmalar.Add(new Ys_DevreyeAlma
                {
                    FirmaId = secondFirm.Id, KaynakCihazAnahtari = firstRecord.KaynakCihazAnahtari,
                    SeriAnahtari = YsDevreyeAlmaKaynak.SeriAnahtari(company.Id, "1000149", "SERIAL-NEW"),
                    DevreyeAlmaTarihi = DateTime.Now
                });
                try
                {
                    await duplicateDb.SaveChangesAsync();
                    throw new InvalidOperationException("SQL unique index accepted a duplicate source device.");
                }
                catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException sql
                    && sql.Number is 2601 or 2627)
                {
                    Check(true, "SQL unique index rejects cross-firm duplicate source key");
                }
            }

            Check(await db.Ys_DevreyeAlmalar.CountAsync() == 3,
                "Failed duplicate attempts leave committed records unchanged");
            await VerifyLegacyContractsAsync(db, firstFirm, firstRecord, firstController, Check);
            Console.WriteLine($"{passed} commissioning SQL checks passed. Isolated database: {databaseName}.");
        }
        finally
        {
            if (created && databaseName.StartsWith("YsCommissioningSqlTest_", StringComparison.Ordinal)
                && db.Database.GetDbConnection().Database == databaseName)
                await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task VerifyLegacyContractsAsync(AppDbContext db, Ys_Firma firm,
        Ys_DevreyeAlma currentRecord, YetkiliServisDevreyeAlmaApiController serviceController,
        Action<bool, string> check)
    {
        var otherCompany = new Dag_Sirket { SirketAdi = "Other legacy distributor" };
        var legacy = new Ys_DevreyeAlma
        {
            FirmaId = firm.Id, TesistatNo = "1000200", AboneNo = "subscriber-legacy",
            DevreyeAlmaTarihi = DateTime.Today.AddMonths(-1)
        };
        var wrongSubscriber = new Ys_DevreyeAlma
        {
            FirmaId = firm.Id, TesistatNo = legacy.TesistatNo, AboneNo = "another-subscriber"
        };
        db.AddRange(otherCompany, legacy, wrongSubscriber);
        await db.SaveChangesAsync();

        Ykc_Talep Source(string contract, string sourceType = "OnlineServis", int? companyId = null) => new()
        {
            SirketId = companyId ?? firm.SirketId, TesisatNo = legacy.TesistatNo,
            AboneNo = legacy.AboneNo, SozlesmeNo = contract, KaynakTipi = sourceType
        };
        var onlineSource = Source("123456");
        var deletedSource = Source("deleted-contract");
        deletedSource.SilindiMi = true;
        db.AddRange(onlineSource, Source(" 123456 "), Source("manual-contract", "Manuel"),
            Source("other-company-contract", companyId: otherCompany.Id), deletedSource,
            new Ykc_Talep
            {
                SirketId = firm.SirketId, TesisatNo = currentRecord.TesistatNo,
                AboneNo = currentRecord.AboneNo, SozlesmeNo = "not-the-consumed-contract", KaynakTipi = "OnlineServis"
            });
        await db.SaveChangesAsync();

        var countBefore = await db.Ys_DevreyeAlmalar.CountAsync();
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { legacy, wrongSubscriber, currentRecord });
        check(legacy.SozlesmeNo == "123456",
            "A legacy contract is recovered from matching online company, installation and subscriber records");
        check(wrongSubscriber.SozlesmeNo == null,
            "A different subscriber on the same installation cannot inherit the contract");
        check(currentRecord.SozlesmeNo == "241584",
            "A consumed commissioning source takes precedence over later YKC contracts");
        check(AdminDevreyeAlmaDto.FromEntity(legacy).SozlesmeNo == "123456"
            && YsDevreyeAlmaDto.FromEntity(legacy).SozlesmeNo == "123456",
            "Both admin and service DTOs expose the recovered legacy contract");

        var conflict = Source("654321");
        db.Add(conflict);
        await db.SaveChangesAsync();
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { legacy });
        check(legacy.SozlesmeNo == null, "Conflicting legacy contracts remain unresolved instead of choosing the newest");
        db.Remove(conflict);
        await db.SaveChangesAsync();

        var invalidSource = new Ys_DevreyeAlmaSorguKaydi
        {
            Referans = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            DevreyeAlmaId = legacy.Id, FirmaId = firm.Id, DagitimSirketiId = firm.SirketId,
            KullaniciId = "legacy-source-fixture", KaynakJson = "not-json"
        };
        db.Add(invalidSource);
        await db.SaveChangesAsync();
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { legacy });
        check(legacy.SozlesmeNo == null,
            "An invalid linked commissioning source cannot be bypassed using an unrelated lookup");
        db.Remove(invalidSource);
        await db.SaveChangesAsync();
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(db, new[] { legacy });
        check(legacy.SozlesmeNo == "123456" && await db.Ys_DevreyeAlmalar.CountAsync() == countBefore
            && !db.ChangeTracker.HasChanges(),
            "Legacy enrichment is repeatable and never changes saved commissioning records");
        var list = await new AdminRaporApiService(db).DevreyeAlmalarAsync(null, firm.SirketId);
        check(list.Islemler.Single(x => x.Id == legacy.Id).SozlesmeNo == "123456",
            "The actual admin list endpoint includes the recovered legacy contract");
        var history = (YsDevreyeAlmaGecmisDto)((OkObjectResult)await serviceController.Gecmis(null)).Value!;
        check(history.Islemler.Single(x => x.Id == legacy.Id).SozlesmeNo == "123456",
            "The actual authorized-service history endpoint includes the recovered legacy contract");
    }

    private sealed class CommissioningSoapHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!body.Contains("<tesisatNo>1000149</tesisatNo>", StringComparison.Ordinal)
                || !body.Contains("<sozlesmeNo>241584</sozlesmeNo>", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected SOAP query.");

            var result = new XElement("YS_CihazBilgileriGetirResult",
                new XElement("HataKodu", "0"),
                new XElement("tesisatno", "1000149"),
                new XElement("sozlesmeno", "241584"),
                new XElement("carikod", "2820377717"),
                new XElement("cariad", "Source customer"),
                new XElement("adres", "Source address"),
                Device("Kombi", "BAYMAK", "20000", "K"),
                Device("Ocak", "ARÇELİK", "7740", "O"),
                Device("Kombi", "BAYMAK", "20000", "K"));
            var response = new XDocument(new XElement("Envelope", new XElement("Body",
                new XElement("YS_CihazBilgileriGetirResponse", result))));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response.ToString())
            };
        }

        private static XElement Device(string type, string brand, string capacity, string typeCode) =>
            new("CihazDto", new XElement("cihaztipi", type), new XElement("cihazmarka", brand),
                new XElement("cihazkapasite", capacity), new XElement("cihaztipkodu", typeCode),
                new XElement("projeno", "PROJECT-1"));
    }
}
