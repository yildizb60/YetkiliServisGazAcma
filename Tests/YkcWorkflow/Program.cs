using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
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
var normalizeMarka = typeof(YetkiliServisDevreyeAlmaApiController).GetMethod(
    "NormalizeMarka", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
string NormalizeMarka(string? value) => (string)normalizeMarka.Invoke(null, [value])!;
Check(NormalizeMarka("Vaillant") == NormalizeMarka("VAILLANT"), "Service brand matches uppercase source spelling");
Check(NormalizeMarka("Arçelik") == NormalizeMarka("ARÇELİK"), "Service brand matches Turkish uppercase spelling");
Check(NormalizeMarka(" baymak ") == NormalizeMarka("BAYMAK"), "Service brand ignores case and surrounding whitespace");
var commissioningSaveFields = typeof(YsDevreyeAlmaKaydetDto).GetProperties().Select(x => x.Name).ToHashSet();
Check(commissioningSaveFields.Contains("SorguReferansi")
    && !commissioningSaveFields.Overlaps(["TesistatNo", "AboneNo", "MusteriAdi", "Adres",
        "CihazTipi", "CihazMarka", "CihazKapasite"]),
    "Commissioning save API accepts a source reference, not client-supplied source fields");
var commissioningIndexes = db.Model.FindEntityType(typeof(Ys_DevreyeAlma))!.GetIndexes().ToList();
Check(commissioningIndexes.Any(x => x.IsUnique && x.Properties.Any(p => p.Name == "KaynakCihazAnahtari"))
    && commissioningIndexes.Any(x => x.IsUnique && x.Properties.Any(p => p.Name == "SeriAnahtari")),
    "Commissioning duplicate keys have database uniqueness constraints");
if (args.Contains("--brand-only", StringComparer.Ordinal))
    return;
if (args.Contains("--commissioning-only", StringComparer.Ordinal))
{
    await CommissioningSqlScenario.RunAsync();
    return;
}
if (args.Contains("--schema-only", StringComparer.Ordinal))
{
    await SchemaMigrationSqlScenario.RunAsync();
    return;
}
try
{
    databaseCreated = await db.Database.EnsureCreatedAsync();
    var emptyHome = (HomeOzetDto)((OkObjectResult)await new HomeApiController(db).Ozet()).Value!;
    Check(emptyHome.TamamlanmaOrani == 0, "An empty system does not report one hundred percent completion");
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
    var directPlan = await service.DurumGuncelleAsync(new() { TalepId = id, Durum = YkcDurumDegerleri.Atandi }, admin, true);
    Check(!directPlan.Basarili && await db.Ykc_Atamalar.CountAsync(x => x.TalepId == id) == 0
        && (await db.Ykc_Talepler.FindAsync(id))!.Durum == YkcDurumDegerleri.AtamaBekliyor,
        "A status update cannot mark a request planned without an appointment");
    db.ChangeTracker.Clear();
    YkcKontrolKaydetDto Control(int no, string result = YkcFr265KontrolSonucDegerleri.UygunDegil) => new()
    {
        TalepId = id,
        Kontroller = [new() { KontrolNo = no, Sonuc = result, Aciklama = "CONTROL_" + no }]
    };
    YkcAtamaKaydetDto Appointment() => new()
    {
        TalepId = id, AtananKullaniciTipi = "Mühendis", RandevuTarihi = DateTime.Today.AddDays(1), RandevuSaati = "09:00"
    };
    async Task AdvanceAppointment()
    {
        await db.Ykc_Talepler.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RandevuTarihi, DateTime.Today.AddDays(-1)).SetProperty(x => x.RandevuSaati, "09:00"));
        var assignmentId = await db.Ykc_Atamalar.Where(x => x.TalepId == id).MaxAsync(x => x.Id);
        await db.Ykc_Atamalar.Where(x => x.Id == assignmentId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RandevuTarihi, DateTime.Today.AddDays(-1)).SetProperty(x => x.RandevuSaati, "09:00"));
        db.ChangeTracker.Clear();
    }
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
        await AdvanceAppointment();
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
    var oldAssignment = (await service.GetirAsync(id, admin, true))!.AktifAtamaId;
    Check((await service.AtamaYapAsync(Appointment(), admin, true)).Basarili, "Successful control may be followed by a new appointment before signature");
    db.ChangeTracker.Clear();
    var rescheduled = (await service.GetirAsync(id, admin, true))!;
    Check(rescheduled.Durum == YkcDurumDegerleri.Atandi && rescheduled.KontrolDonemi == 4
        && rescheduled.Kontroller.Single(x => x.KontrolNo == 11).AtamaId == oldAssignment,
        "Rescheduling returns to planned state and preserves the previous successful control with its appointment");
    Check(!(await service.DurumGuncelleAsync(new() { TalepId = id, Durum = YkcDurumDegerleri.SahaIsleminde }, admin, true)).Basarili,
        "Future rescheduled appointment cannot enter control stage");
    Check(!(await signatureFlow.ImzayaGonderAsync(id, admin, true)).Basarili && signing.Request == null,
        "Old successful result cannot send the rescheduled form to the provider");
    await db.Ykc_Talepler.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Durum, YkcDurumDegerleri.SahaIsleminde));
    db.ChangeTracker.Clear();
    Check(!(await service.KontrolleriKaydetAsync(Control(16, YkcFr265KontrolSonucDegerleri.Uygun), admin, true)).Basarili,
        "Even a stale control-stage state cannot save results before appointment time");
    await AdvanceAppointment();
    Check((await service.KontrolleriKaydetAsync(Control(16, YkcFr265KontrolSonucDegerleri.Uygun), admin, true)).Basarili,
        "New appointment accepts its own control result");
    db.ChangeTracker.Clear();
    var current = (await service.GetirAsync(id, admin, true))!;
    Check(current.AktifKontroller[0].AtamaId == current.AktifAtamaId && current.AktifAtamaId != oldAssignment,
        "Control result is bound to the current assignment");
    current.AktifKontroller[0].AtamaId = oldAssignment;
    Check(!(bool)gate.Invoke(null, [current, null])!, "Signature gate rejects another appointment's successful result");
    var providerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var providerRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    signing.BeforeSend = async () =>
    {
        providerEntered.SetResult();
        await providerRelease.Task.WaitAsync(TimeSpan.FromSeconds(30));
    };
    var sending = signatureFlow.ImzayaGonderAsync(id, admin, true);
    try
    {
        await providerEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await using var appointmentDb = new AppDbContext(options);
        Check(!(await new YkcTalepService(appointmentDb).AtamaYapAsync(Appointment(), admin, true)).Basarili,
            "Appointment cannot change while the signature provider is processing the form");
    }
    finally
    {
        providerRelease.TrySetResult();
    }
    var sent = await sending;
    Check(sent.Basarili && signing.Request?.KontrolNo == 1 && signing.Request.BelgeBytes.Length > 0,
        "Rescheduled and rechecked PDF reaches the signature provider with the correct form slot");
    db.ChangeTracker.Clear();
    Check(!(await service.AtamaYapAsync(Appointment(), admin, true)).Basarili,
        "Signature in progress prevents appointment or cycle changes");

    signing.PollResult = new YkcImzaDurumSonuc
    {
        Basarili = true,
        Durum = YkcImzaDurumDegerleri.Tamamlandi,
        NihaiBelgeAdi = $"FR265_Imzali_{id}.pdf",
        NihaiBelgeIcerikTipi = "application/pdf",
        NihaiBelgeBytes = YkcFr265PdfService.Olustur((await service.GetirAsync(id, admin, true))!).Bytes,
        Imzacilar = signing.Request!.Imzacilar.Select(x => new YkcImzaProviderImzaciDurumu
        {
            SiraNo = x.SiraNo, Durum = YkcImzaciDurumDegerleri.Imzaladi, ImzaTarihi = DateTime.Now
        }).ToList()
    };
    var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var pollRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    signing.BeforePoll = async () =>
    {
        pollEntered.TrySetResult();
        await pollRelease.Task.WaitAsync(TimeSpan.FromSeconds(30));
    };
    var firstPoll = signatureFlow.ImzaDurumunuSorgulaAsync(id, admin, true);
    await using var pollDb = new AppDbContext(options);
    var secondFlow = new YkcImzaAkisService(pollDb, new YkcTalepService(pollDb), new YkcFr265FormService(), signing,
        new TestEnvironment { ContentRootPath = documentRoot }, NullLogger<YkcImzaAkisService>.Instance);
    Task<YkcIslemSonuc>? secondPoll = null;
    try
    {
        await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        secondPoll = secondFlow.ImzaDurumunuSorgulaAsync(id, admin, true);
        await Task.Delay(100);
        Check(!secondPoll.IsCompleted, "A second signature poll waits for the first document transaction");
    }
    finally
    {
        pollRelease.TrySetResult();
    }
    var pollResults = await Task.WhenAll(firstPoll, secondPoll!);
    Check(pollResults.All(x => x.Basarili), "Concurrent signature polls return a consistent result");
    db.ChangeTracker.Clear();
    var finalFiles = await db.Ykc_FormDosyalari.AsNoTracking().Where(x => x.TalepId == id
        && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai && !x.SilindiMi).ToListAsync();
    Check(finalFiles.Count == 1 && await db.Ykc_IslemGecmisi.CountAsync(x => x.TalepId == id
        && x.IslemTipi == "FR265ImzaliNihaiBelgeAlindi") == 1,
        "Concurrent signature polls persist one final PDF and one history event");
    var reportRecord = (await service.RaporAsync(new() { KayitIdleri = [id] }, admin, true)).Kayitlar.Single();
    Check(reportRecord.ImzaliNihaiBelgeVar && reportRecord.ImzaliNihaiDosyaId == finalFiles[0].Id,
        "Report record exposes the authorized final PDF reference");

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

    Check((await permissions.GuncelleAsync(Rights(YetkiTipleri.TAM_YETKI), admin, company.Id, true)).Basarili,
        "Full rights are granted only in the selected company");
    var allRights = (await permissions.ListeleAsync(admin, null, true)).SirketYetkileri[staff.Id];
    Check(allRights.Count == 2
        && allRights.Single(x => x.SirketId == company.Id).Yetkiler.SequenceEqual([YetkiTipleri.TAM_YETKI])
        && allRights.Single(x => x.SirketId == otherCompany.Id).Yetkiler.SequenceEqual([YetkiTipleri.MARKA_YONET]),
        "Permission summary keeps full and limited company rights separate");
    var scopedRights = (await permissions.ListeleAsync(admin, otherCompany.Id, true)).SirketYetkileri[staff.Id];
    Check(scopedRights.Count == 1 && scopedRights[0].SirketId == otherCompany.Id
        && !scopedRights[0].Yetkiler.Contains(YetkiTipleri.TAM_YETKI),
        "Scoped permission summary does not expose another company's full rights");
    Check((await permissions.GuncelleAsync(Rights(), admin, company.Id, true)).Basarili,
        "Summary fixture can revoke company rights");
    var noRights = (await permissions.ListeleAsync(admin, company.Id, true)).SirketYetkileri[staff.Id];
    Check(noRights.Count == 1 && noRights[0].Yetkiler.Count == 0,
        "An assigned company with revoked rights is not displayed as fully authorized");

    await AdminSecuritySqlScenario.RunAsync(db, manager, admin, staff, company.Id, otherCompany.Id, Check);

    var approvalFirm = new Ys_Firma
    {
        FirmaAdi = "Approval contact fixture", SirketId = company.Id,
        YetkiliKisi = "Test Contact", Telefon = "05551234567", VergiNo = "9000000042"
    };
    var certificate = new Ys_YetkiBelgesi
    {
        Firma = approvalFirm, Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor,
        YetkiBelgesiBitisTarihi = DateTime.Today.AddDays(30)
    };
    db.Ys_YetkiBelgeleri.Add(certificate);
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var approvalList = await new AdminYetkiBelgesiOnayApiService(db).ListeleAsync(company.Id);
    var approval = approvalList.Bekleyenler.Single(x => x.Id == certificate.Id);
    Check(approval.FirmaYetkiliKisi == "Test Contact" && approval.FirmaTelefon == "05551234567",
        "Approval list preserves the firm's existing contact person and phone");
    Check(!(await new AdminYetkiBelgesiOnayApiService(db).ListeleAsync(otherCompany.Id))
        .Bekleyenler.Any(x => x.Id == certificate.Id), "Approval contacts remain company scoped");
    db.Ys_DevreyeAlmalar.AddRange(
        new() { FirmaId = approvalFirm.Id, Durum = DevreyeAlmaDurumDegerleri.Tamamlandi, DevreyeAlmaTarihi = DateTime.Today },
        new() { FirmaId = approvalFirm.Id, Durum = DevreyeAlmaDurumDegerleri.Bekliyor, DevreyeAlmaTarihi = DateTime.Today },
        new() { FirmaId = approvalFirm.Id, Durum = DevreyeAlmaDurumDegerleri.Tamamlandi, DevreyeAlmaTarihi = DateTime.Today, SilindiMi = true });
    await db.SaveChangesAsync();
    var home = (HomeOzetDto)((OkObjectResult)await new HomeApiController(db).Ozet()).Value!;
    Check(home.DevreyeCount == 1 && home.TamamlanmaOrani == 50,
        "Completion percentage counts completed records and excludes archived records");
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
    public bool DemoModuMu => false;
    public YkcImzaGonderIstek? Request { get; private set; }
    public Func<Task>? BeforeSend { get; set; }
    public Func<Task>? BeforePoll { get; set; }
    public YkcImzaDurumSonuc? PollResult { get; set; }
    public async Task<YkcImzaGonderSonuc> GonderAsync(YkcImzaGonderIstek istek, CancellationToken cancellationToken = default)
    {
        if (BeforeSend != null) await BeforeSend();
        Request = istek;
        return new YkcImzaGonderSonuc { Basarili = true, ProviderDocumentId = "TEST-ONLY-" + Guid.NewGuid() };
    }
    public async Task<YkcImzaDurumSonuc> DurumSorgulaAsync(string providerDocumentId, CancellationToken cancellationToken = default)
    {
        if (BeforePoll != null) await BeforePoll();
        return PollResult ?? throw new NotSupportedException();
    }
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
