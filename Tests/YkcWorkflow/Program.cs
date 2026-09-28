using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

// Isolated LocalDB database: never reads the application's connection string or records.
var databaseName = "YkcWorkflowTest_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
await using var db = new AppDbContext(options);
var passed = 0;
var databaseCreated = false;
var documentRoot = Path.Combine(Path.GetTempPath(), databaseName);
void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
try
{
    databaseCreated = await db.Database.EnsureCreatedAsync();
    var company = new Dag_Sirket { SirketAdi = "Workflow fixture" };
    var otherCompany = new Dag_Sirket { SirketAdi = "Other scope" };
    db.AddRange(company, otherCompany);
    await db.SaveChangesAsync();
    var admin = new AppKullanici { UserName = "audit-admin", KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin };
    var staff = new AppKullanici { UserName = "test-staff", KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = company.Id };
    db.Users.AddRange(admin, staff);
    var request = new Ykc_Talep
    {
        SirketId = company.Id, Il = "Test", Bolge = "Test", MusteriAdi = "Workflow fixture",
        TesisatNo = "TEST-ONLY", Durum = YkcDurumDegerleri.AtamaBekliyor,
        Kontroller = Enumerable.Range(1, 5).Select(no => new Ykc_Fr265Kontrol { KontrolNo = no }).ToList()
    };
    db.Ykc_Talepler.Add(request);
    await db.SaveChangesAsync();
    var id = request.Id;
    db.ChangeTracker.Clear();
    var service = new YkcTalepService(db);
    YkcKontrolKaydetDto Control(int no, string result = YkcFr265KontrolSonucDegerleri.UygunDegil) => new()
    {
        TalepId = id,
        Kontroller = [new() { KontrolNo = no, Sonuc = result, Aciklama = "CONTROL_" + no }]
    };
    YkcAtamaKaydetDto Appointment() => new()
    {
        TalepId = id, AtananKullaniciTipi = "Mühendis", RandevuTarihi = DateTime.Today.AddDays(1), RandevuSaati = "09:00"
    };
    for (var no = 1; no <= 11; no++)
    {
        db.ChangeTracker.Clear();
        var appointed = await service.AtamaYapAsync(Appointment(), admin, true);
        Check(appointed.Basarili, $"Appointment before control {no}: {appointed.Mesaj}");
        db.ChangeTracker.Clear();
        var expectedCycle = YkcKontrolAkisKurali.DonemNo(no);
        var detail = await service.GetirAsync(id, admin, true);
        Check(detail!.KontrolDonemi == expectedCycle && detail.AktifKontroller.Count == 5,
            $"Control {no} has the correct five-slot cycle");
        Check((await service.ListeAsync(new() { KontrolNo = YkcKontrolAkisKurali.FormKontrolNo(no) }, admin, true)).Toplam == 1,
            $"List filter finds active control {no}");
        if (no is 6 or 11)
        {
            var count = await db.Ykc_Fr265Kontroller.CountAsync();
            var repeated = await service.AtamaYapAsync(Appointment(), admin, true);
            Check(repeated.Basarili && await db.Ykc_Fr265Kontroller.CountAsync() == count,
                "Rescheduling the same cycle does not create another five slots");
        }

        // Advance only this fixture's clock; production transition still enforces appointment time.
        await db.Ykc_Talepler.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RandevuTarihi, DateTime.Today.AddDays(-1)).SetProperty(x => x.RandevuSaati, "09:00"));
        db.ChangeTracker.Clear();
        var started = await service.DurumGuncelleAsync(new() { TalepId = id, Durum = YkcDurumDegerleri.SahaIsleminde }, admin, true);
        Check(started.Basarili, $"Appointment {no} transitions to control");
        db.ChangeTracker.Clear();
        if (no is 6 or 11)
        {
            Check(!(await service.KontrolleriKaydetAsync(Control(no - 5), admin, true)).Basarili,
                "Stale previous-cycle submission is rejected");
            db.ChangeTracker.Clear();
        }
        Check(!(await service.KontrolleriKaydetAsync(Control(no), staff, false, otherCompany.Id)).Basarili,
            "Other company cannot enter the control result");
        db.ChangeTracker.Clear();
        YkcIslemSonuc saved;
        if (no == 7)
        {
            await using var otherDb = new AppDbContext(options);
            var results = await Task.WhenAll(service.KontrolleriKaydetAsync(Control(no), admin, true),
                new YkcTalepService(otherDb).KontrolleriKaydetAsync(Control(no), admin, true));
            Check(results.Count(x => x.Basarili) == 1, "Concurrent control submissions save exactly once");
            saved = results.Single(x => x.Basarili);
        }
        else
            saved = await service.KontrolleriKaydetAsync(Control(no, no == 11 ? YkcFr265KontrolSonucDegerleri.Uygun : YkcFr265KontrolSonucDegerleri.UygunDegil), admin, true);
        Check(saved.Basarili, $"Control {no} saves: {saved.Mesaj}");
        db.ChangeTracker.Clear();
        if (no <= 10)
        {
            Check((await db.Ykc_Talepler.FindAsync(id))!.Durum == YkcDurumDegerleri.AtamaBekliyor,
                "Unsuccessful control requires another appointment");
            Check(!(await service.KontrolleriKaydetAsync(Control(no + 1), admin, true)).Basarili,
                "Cannot bypass the appointment step");
        }
    }

    db.ChangeTracker.Clear();
    var final = (await service.GetirAsync(id, admin, true))!;
    Check(final.KontrolDonemi == 3 && final.Kontroller.Count(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil) == 10,
        "Ten previous unsuccessful results are preserved in the third cycle");
    Check(final.AktifKontroller[0].Sonuc == YkcFr265KontrolSonucDegerleri.Uygun && final.AktifKontroller[0].FormKontrolNo == 1,
        "Third-cycle success uses signature slot one, not eleven");
    Check(await db.Ykc_IslemGecmisi.CountAsync(x => x.TalepId == id && x.IslemTipi == "KontrolDonemiAcildi") == 2,
        "Exactly two cycle openings are recorded");
    using (var zip = new ZipArchive(new MemoryStream(new YkcFr265FormService().WordOlustur(final).Bytes)))
    using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
    {
        var xml = await reader.ReadToEndAsync();
        Check(xml.Contains("CONTROL_11") && !xml.Contains("CONTROL_10"), "Official document uses only the third cycle");
    }
    var gate = typeof(YkcImzaAkisService).GetMethod("ImzaGonderimineHazirMi", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    Check((bool)gate.Invoke(null, [final, null])!, "Third-cycle successful form is eligible for signature");
    final.AktifKontroller[0].Sonuc = YkcFr265KontrolSonucDegerleri.UygunDegil;
    Check(!(bool)gate.Invoke(null, [final, null])!, "Unsuccessful current cycle cannot be signed");
    db.ChangeTracker.Clear();
    var signing = new RecordingSignatureProvider();
    var signatureFlow = new YkcImzaAkisService(db, service, new YkcFr265FormService(), signing,
        new TestEnvironment { ContentRootPath = documentRoot }, NullLogger<YkcImzaAkisService>.Instance);
    var sent = await signatureFlow.ImzayaGonderAsync(id, admin, true);
    Check(sent.Basarili && signing.Request?.KontrolNo == 1 && signing.Request.BelgeBytes.Length > 0,
        "Third-cycle PDF reaches the signature provider with the correct form slot");
    db.ChangeTracker.Clear();
    Check(!(await service.AtamaYapAsync(Appointment(), admin, true)).Basarili,
        "Signature in progress prevents appointment or cycle changes");

    var permissions = new AdminPersonelYetkiApiService(db);
    AdminYetkiGuncelleDto Rights(params string[] rights) => new()
    {
        Id = staff.Id, SirketIds = [company.Id], Yetkiler = new() { [company.Id] = rights.ToList() }
    };
    db.ChangeTracker.Clear();
    Check((await permissions.GuncelleAsync(Rights(YetkiTipleri.YKC_TALEP_GOR), admin, null, true)).Basarili, "Permission granted");
    var manager = new UserManager<AppKullanici>(new UserStore<AppKullanici>(db), Options.Create(new IdentityOptions()),
        new PasswordHasher<AppKullanici>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
        NullLogger<UserManager<AppKullanici>>.Instance);
    var authorization = new YkcYetkiService(db, manager);
    Check(await authorization.YetkiliMiAsync(staff, YetkiTipleri.YKC_TALEP_GOR, company.Id), "Granted right authorizes the selected company");
    var grant = await db.Dag_PersonelYetkiler.AsNoTracking().SingleAsync();
    db.ChangeTracker.Clear();
    Check((await permissions.GuncelleAsync(Rights(YetkiTipleri.YKC_TALEP_GOR), admin, null, true)).Basarili
        && await db.Dag_PersonelYetkiler.CountAsync() == 1, "Unchanged permission keeps its original grant");
    Check((await permissions.GuncelleAsync(Rights(), admin, company.Id, false)).Basarili, "Company-scoped revocation succeeds");
    db.ChangeTracker.Clear();
    var revoked = await db.Dag_PersonelYetkiler.AsNoTracking().SingleAsync();
    Check(revoked.SilindiMi && revoked.SilinmeTarihi.HasValue && revoked.SilenKullanici == admin.UserName
        && revoked.OlusturanKullanici == grant.OlusturanKullanici && revoked.OlusturmaTarihi == grant.OlusturmaTarihi,
        "Revocation retains original grant and records actor and time");
    Check((await permissions.GetirAsync(new() { Id = staff.Id }, admin, null, true)).MevcutYetkiler.Count == 0,
        "Revoked permission is not selected in the editor");
    Check(!(await authorization.YetkiliMiAsync(staff, YetkiTipleri.YKC_TALEP_GOR, company.Id)),
        "Archived grant does not authorize access");
    Check((await permissions.GuncelleAsync(Rights(YetkiTipleri.RAPOR_GOR), admin, null, true)).Basarili
        && await db.Dag_PersonelYetkiler.CountAsync() == 2, "Regrant creates a new record, retaining the revocation");
    db.Dag_PersonelYetkiler.Add(new() { KullaniciId = staff.Id, SirketId = otherCompany.Id, YetkiTipi = YetkiTipleri.MARKA_YONET });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    Check((await permissions.GuncelleAsync(Rights(), admin, company.Id, false)).Basarili
        && await db.Dag_PersonelYetkiler.AnyAsync(x => x.SirketId == otherCompany.Id && !x.SilindiMi),
        "Company administrator cannot revoke another company's grant");
    db.ChangeTracker.Clear();
    await using var concurrent = new AppDbContext(options);
    var parallelGrants = await Task.WhenAll(
        permissions.GuncelleAsync(Rights(YetkiTipleri.RAPOR_GOR), admin, company.Id, false),
        new AdminPersonelYetkiApiService(concurrent).GuncelleAsync(Rights(YetkiTipleri.RAPOR_GOR), admin, company.Id, false));
    Check(parallelGrants.All(x => x.Basarili)
        && await db.Dag_PersonelYetkiler.CountAsync(x => x.SirketId == company.Id && !x.SilindiMi) == 1,
        "Concurrent grants do not duplicate active permissions or overwrite history");
    Console.WriteLine($"{passed} workflow checks passed.");
}
finally
{
    if (databaseCreated && db.Database.GetDbConnection().Database == databaseName && databaseName.StartsWith("YkcWorkflowTest_", StringComparison.Ordinal))
        await db.Database.EnsureDeletedAsync();
    var fullRoot = Path.GetFullPath(documentRoot);
    if (fullRoot.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && Path.GetFileName(fullRoot) == databaseName && Directory.Exists(fullRoot))
        Directory.Delete(fullRoot, recursive: true);
}

sealed class RecordingSignatureProvider : IYkcImzaProvider
{
    public string ProviderAdi => "Test only";
    public bool KullanilabilirMi => true;
    public bool DemoModuMu => true;
    public YkcImzaGonderIstek? Request { get; private set; }
    public Task<YkcImzaGonderSonuc> GonderAsync(YkcImzaGonderIstek istek, CancellationToken cancellationToken = default)
    {
        Request = istek;
        return Task.FromResult(new YkcImzaGonderSonuc { Basarili = true, ProviderDocumentId = "TEST-ONLY-" + Guid.NewGuid() });
    }
    public Task<YkcImzaDurumSonuc> DurumSorgulaAsync(string providerDocumentId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "YkcWorkflowTests";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
