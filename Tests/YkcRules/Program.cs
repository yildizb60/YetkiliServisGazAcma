using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using System.Globalization;
using System.Xml.Linq;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Business.Services.Online;
using YetkiliServisGazAcma.Entities;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

string ExcelParcasi(byte[] bytes, string path)
{
    using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
    using var reader = new StreamReader(archive.GetEntry(path)?.Open()
        ?? throw new InvalidOperationException($"Excel parçası bulunamadı: {path}"));
    return reader.ReadToEnd();
}

XElement ExcelHucresi(byte[] bytes, string baslik, int satir = 2)
{
    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var xml = XDocument.Parse(ExcelParcasi(bytes, "xl/worksheets/sheet1.xml"));
    var rows = xml.Descendants(ns + "row").ToList();
    var column = rows[0].Elements(ns + "c").Select((cell, index) => (cell, index))
        .Single(x => x.cell.Value == baslik).index;
    return rows[satir - 1].Elements(ns + "c").ElementAt(column);
}

bool ExcelSayisi(XElement cell, decimal expected)
    => (string?)cell.Attribute("t") == "n"
        && decimal.TryParse(cell.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && Math.Abs(value - expected) < .00000001m;

bool ExcelTarihi(XElement cell, DateTime expected)
    => ExcelSayisi(cell, (decimal)expected.ToOADate()) && (string?)cell.Attribute("s") is "3" or "4";

string[][] Fr265KontrolHucreleri(byte[] bytes, int kontrolNo)
{
    using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
    using var stream = archive.GetEntry("word/document.xml")!.Open();
    XNamespace ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    var tables = XDocument.Load(stream).Descendants(ns + "tbl")
        .Where(x => x.Value.Contains("Gaz Dağıtım Şirketi Yetkilisi") && x.Value.Contains("Kaşe / İmza"));
    return tables.ElementAt(kontrolNo - 1).Elements(ns + "tr")
        .Select(row => row.Elements(ns + "tc")
            .Select(cell => string.Concat(cell.Descendants(ns + "t").Select(text => text.Value))).ToArray()).ToArray();
}

// No database or external providers: checks cannot alter application records.
var commissioningSource = new YsDevreyeAlmaKaynak
{
    TesisatNo = "1000149",
    SozlesmeNo = "241584",
    MusteriAdi = "HÜSEYİN SOYLU",
    Adres = "Çorum",
    CihazTipi = "Kombi",
    CihazMarka = "BAYMAK",
    CihazKapasite = "20000"
};
var firstDeviceKey = YsDevreyeAlmaKaynak.CihazAnahtari(7, commissioningSource, "42", "K", 0);
Check(firstDeviceKey == YsDevreyeAlmaKaynak.CihazAnahtari(7, commissioningSource, "42", "K", 0),
    "Commissioning source device keeps its key across repeated queries");
commissioningSource.CihazMarka = "baymak";
Check(firstDeviceKey == YsDevreyeAlmaKaynak.CihazAnahtari(7, commissioningSource, "42", "K", 0),
    "Source device key ignores brand casing");
commissioningSource.CihazMarka = "BAYMAK";
Check(firstDeviceKey != YsDevreyeAlmaKaynak.CihazAnahtari(7, commissioningSource, "42", "K", 1),
    "Two identical source devices have separate commissioning slots");
Check(firstDeviceKey != YsDevreyeAlmaKaynak.CihazAnahtari(8, commissioningSource, "42", "K", 0),
    "Commissioning source identity is scoped to the distribution company");
Check(YsDevreyeAlmaKaynak.SeriAnahtari(7, "1000149", " ab-123 ")
    == YsDevreyeAlmaKaynak.SeriAnahtari(7, "1000149", "AB-123"),
    "Serial duplicate key ignores surrounding spaces and casing");
Check(YsDevreyeAlmaKaynak.SeriAnahtari(7, "1000149", "AB-123")
    != YsDevreyeAlmaKaynak.SeriAnahtari(7, "1000150", "AB-123"),
    "Serial duplicate key is scoped to the installation");

var listDevice = YkcTalepDto.FromEntity(new Ykc_Talep
{
    EskiCihazTipi = "Kombi",
    EskiMarka = "Buderus",
    EskiBacaTipi = null,
    EskiKapasite = "20000",
    YeniCihazTipi = "Kombi",
    YeniMarka = "Bosch",
    YeniBacaTipi = "Hermetik",
    YeniKapasite = "2460"
});
Check(listDevice.ProjedekiCihazBilgisi?.BacaTipi == null
      && listDevice.ProjedekiCihazBilgisi?.Marka == "Buderus"
      && listDevice.YeniCihazBilgisi?.BacaTipi == "Hermetik",
    "Request list preserves missing source chimney and entered new-device chimney");
var firmFileUser = new AppKullanici { KullaniciTipi = KullaniciTipiDegerleri.SertifikaliFirma, FirmaId = 7, SirketId = 3 };
Check(YkcYetkiService.FirmaDosyasinaErisimVarMi(firmFileUser, new Ykc_Talep { FirmaId = 7, SirketId = 3 }),
    "Certified firm can access its own request file");
Check(!YkcYetkiService.FirmaDosyasinaErisimVarMi(firmFileUser, new Ykc_Talep { FirmaId = 8, SirketId = 3 }),
    "Certified firm cannot access another firm's file in the same company");
firmFileUser.FirmaId = null;
Check(!YkcYetkiService.FirmaDosyasinaErisimVarMi(firmFileUser, new Ykc_Talep { FirmaId = 8, SirketId = 3 }),
    "Certified firm without a firm assignment cannot use company scope for files");

var onlineHandler = new RecordingOnlineHandler();
using var onlineHttp = new HttpClient(onlineHandler);
var disabledOnline = new OnlineCihazBilgileriClient(onlineHttp,
    Options.Create(new OnlineServiceOptions()), NullLogger<OnlineCihazBilgileriClient>.Instance);
var disabledResult = await disabledOnline.YSCihazBilgileriGetirAsync("CORUMGAZ", 1000132, 432237);
Check(!disabledResult.Basarili && onlineHandler.Calls == 0,
    "Unconfigured online service cannot call a default test endpoint");
var missingEndpoint = new OnlineCihazBilgileriClient(onlineHttp,
    Options.Create(new OnlineServiceOptions { Enabled = true }), NullLogger<OnlineCihazBilgileriClient>.Instance);
var missingEndpointResult = await missingEndpoint.YSCihazBilgileriGetirAsync("CORUMGAZ", 1000132, 432237);
Check(!missingEndpointResult.Basarili && onlineHandler.Calls == 0,
    "Enabled online service requires an explicit endpoint");
var configuredOnline = new OnlineCihazBilgileriClient(onlineHttp,
    Options.Create(new OnlineServiceOptions { Enabled = true, Endpoint = "https://example.invalid/Online.svc" }),
    NullLogger<OnlineCihazBilgileriClient>.Instance);
await configuredOnline.YSCihazBilgileriGetirAsync("CORUMGAZ", 1000132, 432237);
Check(onlineHandler.Calls == 1 && onlineHandler.LastUri?.AbsoluteUri == "https://example.invalid/Online.svc",
    "Configured online service uses its explicit endpoint");

var adminSetup = YetkiliServisIlkKurulumService.Degerlendir(
    YetkiliServisOlusturmaTipleri.Admin, true, true, true, true);
Check(adminSetup.zorunluMu && adminSetup.tamamlandiMi && adminSetup.eksikler.Count == 0,
    "Admin-created firm with active setup can operate");
var inactiveBranch = YetkiliServisIlkKurulumService.Degerlendir(
    YetkiliServisOlusturmaTipleri.Admin, true, true, false, true);
Check(inactiveBranch.zorunluMu && !inactiveBranch.tamamlandiMi && inactiveBranch.eksikler.Contains("Aktif sube kaydi"),
    "Inactive branch does not complete initial setup");
var missingSetup = YetkiliServisIlkKurulumService.Degerlendir(
    YetkiliServisOlusturmaTipleri.Admin, false, false, true, false);
Check(!missingSetup.tamamlandiMi && missingSetup.eksikler.Count == 3,
    "Missing brand, category and document remain visible");
var registered = YetkiliServisIlkKurulumService.Degerlendir(
    YetkiliServisOlusturmaTipleri.Kayit, false, false, false, false);
Check(!registered.zorunluMu && registered.tamamlandiMi,
    "Self-registered firm is not assigned admin first-setup gate");

var approvalDay = new DateTime(2026, 9, 14);
var approvalDocument = new Ys_YetkiBelgesi { YetkiBelgesiBitisTarihi = approvalDay };
Check(YetkiBelgesiService.OnaylanabilirMi(approvalDocument, approvalDay), "Certificate can be approved on its last valid day");
approvalDocument.YetkiBelgesiBitisTarihi = approvalDay.AddDays(-1);
Check(!YetkiBelgesiService.OnaylanabilirMi(approvalDocument, approvalDay), "Expired certificate cannot be approved");
approvalDocument.YetkiBelgesiBitisTarihi = approvalDay.AddDays(1);
approvalDocument.Durum = YetkiBelgesiDurumDegerleri.Onaylandi;
Check(!YetkiBelgesiService.OnaylanabilirMi(approvalDocument, approvalDay), "Already decided certificate cannot be approved again");
approvalDocument.Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor;
approvalDocument.SilindiMi = true;
Check(!YetkiBelgesiService.OnaylanabilirMi(approvalDocument, approvalDay), "Deleted certificate cannot be approved");

var deletableDocument = new Ys_YetkiBelgesi { Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor };
Check(YetkiBelgesiService.SilinebilirMi(deletableDocument), "Pending certificate can be deleted");
deletableDocument.Durum = YetkiBelgesiDurumDegerleri.Reddedildi;
Check(YetkiBelgesiService.SilinebilirMi(deletableDocument), "Rejected certificate can be deleted");
deletableDocument.Durum = YetkiBelgesiDurumDegerleri.Onaylandi;
Check(!YetkiBelgesiService.SilinebilirMi(deletableDocument), "Approved certificate cannot be deleted");
deletableDocument.Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor;
deletableDocument.SilindiMi = true;
Check(!YetkiBelgesiService.SilinebilirMi(deletableDocument), "Deleted certificate cannot be deleted again");
Check(!YetkiBelgesiService.SilinebilirMi(null), "Missing certificate cannot be deleted");

using var snapshots = new YkcSorguKaydiService();
var reference = snapshots.Ekle("firm-a", new YkcTalepKaydetDto {
    TesisatNo = "100", SozlesmeNo = "200", FirmaId = 7, SirketId = 3,
    EskiCihazTipi = "Kombi", EskiMarka = "Source brand", EskiKapasite = "20000",
    ProjeNo = "source-project", SayacNo = "source-meter", Bolge = "source-region",
    MusteriAdi = "Source customer",
    IzinliYeniCihazTipleri = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["Kombi"] = "KMB"
    }
});
YkcTalepKaydetDto Request() => new() {
    SorguReferansi = reference, TesisatNo = "100", SozlesmeNo = "200",
    FirmaId = 99, SirketId = 99, EskiMarka = "Forged", EskiKapasite = "1",
    ProjeNo = "forged-project", SayacNo = "forged-meter", Bolge = "forged-region",
    YeniCihazTipi = "Kombi", YeniMarka = "User entered", YeniKapasite = "25000"
};
var valid = Request();
Check(snapshots.Uygula("firm-a", valid), "Owner can use query reference");
Check(valid.FirmaId == 7 && valid.SirketId == 3, "Client cannot replace company/firm scope");
Check(valid.EskiMarka == "Source brand" && valid.EskiKapasite == "20000", "Old device comes from server snapshot");
Check(valid.ProjeNo == "source-project" && valid.SayacNo == "source-meter" && valid.Bolge == "source-region", "Source identifiers cannot be overwritten");
Check(valid.YeniMarka == "User entered" && valid.YeniKapasite == "25000", "New device values remain user input");
Check(!snapshots.Uygula("firm-b", Request()), "Other user cannot reuse query reference");
var otherInstallation = Request(); otherInstallation.TesisatNo = "101";
Check(!snapshots.Uygula("firm-a", otherInstallation), "Other installation cannot reuse query reference");
var otherContract = Request(); otherContract.SozlesmeNo = "201";
Check(!snapshots.Uygula("firm-a", otherContract), "Other contract cannot reuse query reference");
var invented = Request(); invented.SorguReferansi = "invented";
Check(!snapshots.Uygula("firm-a", invented), "Invented reference rejected");
var forgedDeviceType = Request(); forgedDeviceType.YeniCihazTipi = "Şofben";
Check(!snapshots.Uygula("firm-a", forgedDeviceType), "Device type outside the service response is rejected");
var missing = Request(); missing.SorguReferansi = null;
Check(!snapshots.Uygula("firm-a", missing), "Missing reference rejected");
var padded = Request(); padded.TesisatNo = "00100"; padded.SozlesmeNo = " 00200 ";
Check(snapshots.Uygula("firm-a", padded) && padded.TesisatNo == "100" && padded.SozlesmeNo == "200",
    "Leading zeros normalize to queried identifiers");
var invalidNumber = Request(); invalidNumber.TesisatNo = "+100";
Check(!snapshots.Uygula("firm-a", invalidNumber), "Non-digit identifier rejected");

var storageFixture = Path.Combine(Path.GetTempPath(), "ykc-storage-test-" + Guid.NewGuid().ToString("N"));
try
{
    var environment = new StorageTestEnvironment { ContentRootPath = Path.Combine(storageFixture, "app") };
    var persistentRoot = Path.Combine(storageFixture, "persistent");
    var storageConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DocumentStorage:RootPath"] = persistentRoot
    }).Build();
    var legacy = PrivateDocumentStorage.LegacyRoot(environment, "ykc-belgeler");
    Check(PrivateDocumentStorage.Root(environment, null, "ykc-belgeler") == legacy,
        "Unconfigured private storage keeps the existing path");
    Check(PrivateDocumentStorage.Root(environment, storageConfig, "ykc-belgeler") == Path.Combine(persistentRoot, "ykc-belgeler"),
        "Configured private storage is outside the application folder");
    Directory.CreateDirectory(legacy);
    File.WriteAllText(Path.Combine(legacy, "existing.pdf"), "fixture");
    Check(PrivateDocumentStorage.ExistingFile(environment, storageConfig, "ykc-belgeler", "existing.pdf") == Path.Combine(legacy, "existing.pdf"),
        "Previously stored private documents remain readable");
    Check(PrivateDocumentStorage.ExistingFile(environment, storageConfig, "ykc-belgeler", "../secret.txt") == null,
        "Private storage rejects path traversal");
    var invalidStorageConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DocumentStorage:RootPath"] = Path.Combine(environment.ContentRootPath, "App_Data")
    }).Build();
    var invalidRootRejected = false;
    try
    {
        PrivateDocumentStorage.Root(environment, invalidStorageConfig, "ykc-belgeler");
    }
    catch (InvalidOperationException)
    {
        invalidRootRejected = true;
    }
    Check(invalidRootRejected, "Private storage cannot be configured inside the publish folder");
}
finally
{
    var full = Path.GetFullPath(storageFixture);
    if (Path.GetFileName(full).StartsWith("ykc-storage-test-", StringComparison.Ordinal)
        && PrivateDocumentStorage.IsInRoot(full, Path.GetTempPath())
        && Directory.Exists(full))
        Directory.Delete(full, recursive: true);
}

var appointment = new DateTime(2030, 1, 2, 15, 0, 0);
Check(YkcRandevuKurali.Cakisiyor(appointment, appointment, 0), "Same minute conflicts without invented visit duration");
Check(!YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(1), 0), "Different minute allowed with default interval");
Check(YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(9), 10), "Configured interval respected");
Check(!YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(10), 10), "Exact interval boundary allowed");
Check(YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(-9), 10), "Interval is symmetric");
Check(YkcRandevuKurali.Cakisiyor(appointment.Date, appointment.Date.AddMinutes(-5), 10), "Interval crosses midnight");
Check(YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(29), 30), "Appointments inside the 30-minute team window conflict");
Check(!YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(30), 30), "Exactly 30 minutes apart is allowed");
Check(YkcRandevuKurali.GecerliSaatDilimi(new TimeSpan(14, 0, 0), 30)
      && YkcRandevuKurali.GecerliSaatDilimi(new TimeSpan(14, 30, 0), 30),
    "Whole and half hours are valid 30-minute appointment slots");
Check(!YkcRandevuKurali.GecerliSaatDilimi(new TimeSpan(14, 15, 0), 30),
    "Quarter-hour values are rejected for 30-minute appointment slots");
Check(!YkcRandevuKurali.MesaiSaatindeMi(new TimeSpan(5, 30, 0)), "05:30 is outside working hours");
Check(YkcRandevuKurali.MesaiSaatindeMi(new TimeSpan(8, 0, 0)), "08:00 is the first appointment slot");
Check(YkcRandevuKurali.MesaiSaatindeMi(new TimeSpan(17, 30, 0)), "17:30 is the last 30-minute appointment slot");
Check(!YkcRandevuKurali.MesaiSaatindeMi(new TimeSpan(18, 0, 0)), "18:00 is closing time");
Check(YkcTakvimGorunumKurali.DoneminTumKayitlariGerekli(new DateTime(2026, 9, 7), new DateTime(2026, 9, 13)),
    "Exact Monday-Sunday range requests complete week records");
Check(YkcTakvimGorunumKurali.DoneminTumKayitlariGerekli(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)),
    "Exact calendar month requests complete month records");
Check(!YkcTakvimGorunumKurali.DoneminTumKayitlariGerekli(new DateTime(2026, 9, 2), new DateTime(2026, 9, 8)),
    "Arbitrary seven-day range remains paged");
Check(YkcKontrolAkisKurali.YeniRandevuGerekli(YkcFr265KontrolSonucDegerleri.UygunDegil),
    "Unsuccessful control requires a new appointment");
Check(!YkcKontrolAkisKurali.YeniRandevuGerekli(YkcFr265KontrolSonucDegerleri.Uygun),
    "Successful control continues to form and signature");
var fiveUnsuccessfulControls = Enumerable.Range(1, 5)
    .Select(no => new Ykc_Fr265Kontrol { KontrolNo = no, Sonuc = YkcFr265KontrolSonucDegerleri.UygunDegil })
    .ToList();
Check(YkcKontrolAkisKurali.KontrolAlaniDolduMu(fiveUnsuccessfulControls),
    "Five unsuccessful controls mark the form control area as exhausted");
Check(YkcKontrolAkisKurali.SiradakiKontrolNo(fiveUnsuccessfulControls) == 1,
    "After five unsuccessful controls the next appointment starts at control one");
var subsequentControls = fiveUnsuccessfulControls.Concat(Enumerable.Range(6, 5)
    .Select(no => new Ykc_Fr265Kontrol { KontrolNo = no })).ToList();
Check(!YkcKontrolAkisKurali.KontrolAlaniDolduMu(subsequentControls)
    && YkcKontrolAkisKurali.SiradakiKontrolNo(subsequentControls) == 1,
    "A new cycle has five free slots without erasing the previous cycle");
foreach (var kontrol in subsequentControls) kontrol.Sonuc = YkcFr265KontrolSonucDegerleri.UygunDegil;
Check(YkcKontrolAkisKurali.KontrolAlaniDolduMu(subsequentControls)
    && YkcKontrolAkisKurali.SiradakiKontrolNo(subsequentControls) == 1,
    "A second unsuccessful five-control cycle can be followed by another cycle");
Check(YkcKontrolAkisKurali.DonemNo(11) == 3 && YkcKontrolAkisKurali.FormKontrolNo(11) == 1
    && YkcKontrolAkisKurali.FormKontrolNo(15) == 5,
    "Third cycle maps to the official form slots one through five");
fiveUnsuccessfulControls[^1].Sonuc = YkcFr265KontrolSonucDegerleri.Uygun;
Check(!YkcKontrolAkisKurali.KontrolAlaniDolduMu(fiveUnsuccessfulControls),
    "A successful control does not mark the control area as exhausted");

Check(YkcCihazUyumKurali.Kapasite("20000,5", out var capacity) && capacity == 20000.5m, "Decimal comma accepted");
Check(!YkcCihazUyumKurali.Kapasite("string", out _), "Placeholder capacity rejected");
Check(!YkcCihazUyumKurali.Kapasite("0", out _) && !YkcCihazUyumKurali.Kapasite("-5", out _), "Capacity must be positive");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "B", "X", "Y", "20000", "25000").Count == 3,
    "Mismatches produce warnings without mutating values");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, "20000", "20000.0").Count == 0,
    "Equal device values do not warn");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, "20000", "15000").Count == 0,
    "Lower capacity does not require an alteration project");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, "20000", "25000")
        .SequenceEqual(["Yeni cihazın kapasitesi projedeki kapasiteden yüksek. Tadilat projesi gereklidir."]),
    "Higher capacity explicitly requires an alteration project");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, "20000,5", "20000.50").Count == 0,
    "Equivalent decimal capacities do not warn");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, "20000,5", "20000,51").Count == 1,
    "Even a fractional capacity increase requires an alteration project");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "B", "X", "Y", "20000", "15000").Count == 2,
    "Lower capacity does not suppress independent brand and flue warnings");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, null, "25000").Count == 0,
    "Missing source capacity cannot establish a capacity increase");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, "invalid", "25000").Count == 0,
    "Unparseable source capacity cannot establish a capacity increase");
foreach (var marka in new[] { "E.C.A", "E.C.A.", " e.c.a ", "E C A", "E. C. A.", "E\tC\u00a0A" })
    Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "ECA", marka, null, "Hermetik", "21070", "2600").Count == 0,
        "Brand abbreviation formatting and lower capacity do not warn: " + marka);
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "E.C.A", "eca", null, null, "21070", "21070").Count == 0,
    "Brand formatting is ignored in either source or replacement device");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "ECA", "E.C.A", null, "Hermetik", "21070", "22000")
        .SequenceEqual(["Yeni cihazın kapasitesi projedeki kapasiteden yüksek. Tadilat projesi gereklidir."]),
    "Equivalent brand does not suppress a genuine capacity increase");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "ECA", "E.C.A Plus", null, null, "21070", "2600")
        .SequenceEqual(["Marka proje kaydıyla farklı. İncelemede kontrol edin."]),
    "Brand matching does not use prefix or fuzzy comparisons");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A+B", "AB", null, null, "21070", "2600").Count == 1,
    "Meaningful brand symbols are not removed");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "ARÇELİK", "Arçelik", null, null, "21070", "2600").Count == 0
    && YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "VAILLANT", "Vaillant", null, null, "21070", "2600").Count == 0,
    "Brand case matching handles Turkish and international spellings");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", null, "E.C.A", null, null, "21070", "2600").Count == 0,
    "Missing source brand does not invent a mismatch");
var previewDevice = new YkcTalepKaydetDto
{
    EskiMarka = "ECA", YeniMarka = "E.C.A", EskiBacaTipi = "Hermetik", YeniBacaTipi = " hermetik ",
    EskiKapasite = "20000,5", YeniKapasite = "20000.50"
};
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Count == 0,
    "Firm preview ignores brand formatting, case and equivalent decimal capacity");
previewDevice.YeniMarka = "Bosch";
previewDevice.YeniBacaTipi = "Bacali";
previewDevice.YeniKapasite = "25000";
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Count == 3
    && YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Last().Contains("Tadilat projesi"),
    "Firm preview advises on all three mismatches and preserves the higher-capacity rule");
previewDevice.YeniMarka = "E.C.A";
previewDevice.YeniBacaTipi = "Hermetik";
previewDevice.YeniKapasite = "15000";
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Single().Contains("düşük")
    && !YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Single().Contains("Tadilat"),
    "Lower capacity is informational, not a renovation-project requirement");
previewDevice.EskiBacaTipi = null;
previewDevice.YeniKapasite = "20000.50";
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Count == 0,
    "Missing source flue type is silent until the integration provides it");
previewDevice.EskiBacaTipi = "-";
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Count == 0,
    "A missing flue placeholder is not treated as a mismatch");
previewDevice.EskiMarka = null;
previewDevice.EskiKapasite = null;
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Count == 2
    && YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).All(x => x.Contains("karşılaştırılamadı")),
    "Missing brand and capacity do not invent a technical mismatch");
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(new()).Count == 0,
    "An untouched form does not display advisory messages");
previewDevice.YeniMarka = "";
previewDevice.YeniKapasite = "invalid";
Check(YkcCihazUyumKurali.TalepOncesiUyarilar(previewDevice).Count == 0,
    "Incomplete numeric input does not produce a capacity comparison");

YkcTalepDetayDto Detail() => new() {
    ProjeNo = "Internal project",
    EskiMarka = "Source brand", EskiKapasite = "20000", YeniMarka = "New brand", MusteriAdi = "Customer",
    AtananEkip = "Internal team", HedefUygulama = "Internal target",
    Atamalar = new() { new YkcAtamaDto() },
    Gecmis = new() { new YkcGecmisDto { IslemTipi = "AtamaYapildi", KullaniciAdi = "Internal user", Aciklama = "Internal note" } }
};
var official = Detail();
YkcFirmaSunumu.Hazirla(official, resmiForm: true);
Check(official.EskiMarka == "Source brand" && official.EskiKapasite == "20000", "Official form retains source device fields");
Check(official.ProjeNo == "Internal project", "Official form project contract remains unchanged");
Check(official.Atamalar.Count == 0 && official.AtananEkip == null && official.HedefUygulama == null,
    "Official form response does not expose internal routing");
Check(official.Gecmis[0].Aciklama == null && official.Gecmis[0].KullaniciAdi == null,
    "Official form response does not expose internal assignment note");
var screen = Detail();
YkcFirmaSunumu.Hazirla(screen, resmiForm: false);
Check(screen.EskiMarka == null && screen.EskiKapasite == null && screen.YeniMarka == "New brand" && screen.MusteriAdi == "Customer",
    "Firm screen hides source device without losing request data");
Check(screen.ProjeNo == null, "Firm detail does not disclose the internal project number");
var form = new YkcTalepDetayDto {
    Id = 42, TesisatNo = "1000132", MusteriAdi = "Test Abone",
    FirmaAdi = "Test Sertifikali Firma", FirmaYetkiliKisi = "Test Yetkili",
    TalepTarihi = new DateTime(2026, 9, 10), TuketimNoktasi = "Daire 4", BaglantiNesnesi = "Bina 12",
    Adres = "Test Mahallesi, Test Sokak No: 4/2, Merkez",
    EskiCihazTipi = "Kombi", YeniCihazTipi = "Kombi", EskiMarka = "Proje markasi", YeniMarka = "Yeni marka",
    EskiKapasite = "20000", YeniKapasite = "20000", IkinciElCihazMi = false,
    Kontroller = new() { new YkcFr265KontrolDto {
        KontrolNo = 1, Sonuc = YkcFr265KontrolSonucDegerleri.Uygun,
        KontrolEdenAdi = "Kontrol Personeli", KontrolTarihi = new DateTime(2026, 10, 2, 9, 30, 0)
    } }
};
var wordForm = new YkcFr265FormService().WordOlustur(form);
using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(wordForm.Bytes)))
using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
{
    var xml = reader.ReadToEnd();
    Check(xml.Contains("Daire 4") && xml.Contains("Bina 12"), "Official form retains supplied unit and building fields");
}
var formPdf = YkcFr265PdfService.Olustur(form);
var draftCells = Fr265KontrolHucreleri(wordForm.Bytes, 1);
Check(draftCells[1].SequenceEqual(["Adı Soyadı: Kontrol Personeli", "Adı Soyadı: Test Abone", "Firma / Yetkili: Test Yetkili"]),
    "Unsigned FR265 preview fills the completed control's staff, subscriber and firm representative");
Check(draftCells[2].All(x => x == "Tarih: 02.10.2026")
    && draftCells[3].SequenceEqual(["İmza", "İmza", "Kaşe / İmza"]),
    "Unsigned control uses the recorded control date, leaves signatures empty and does not claim anyone signed");
Check(Fr265KontrolHucreleri(wordForm.Bytes, 2)[1].SequenceEqual(["Adı Soyadı", "Adı Soyadı", "Firma / Yetkili"])
    && Fr265KontrolHucreleri(wordForm.Bytes, 2)[2].All(x => x == "Tarih"),
    "Unperformed controls retain empty identity and date placeholders");
var originalControls = form.Kontroller;
form.Kontroller = Enumerable.Range(1, 5).Select(no => new YkcFr265KontrolDto
{
    KontrolNo = no, Sonuc = no == 5 ? YkcFr265KontrolSonucDegerleri.Uygun : YkcFr265KontrolSonucDegerleri.UygunDegil,
    KontrolEdenAdi = $"Kontrol Personeli {no}", KontrolTarihi = new DateTime(2026, 10, no),
    Aciklama = no == 5 ? null : $"Kontrol {no} uygunsuzluk nedeni"
}).ToList();
var allControlsWord = new YkcFr265FormService().WordOlustur(form);
Check(Enumerable.Range(1, 5).All(no => {
    var cells = Fr265KontrolHucreleri(allControlsWord.Bytes, no);
    return cells[1][0] == $"Adı Soyadı: Kontrol Personeli {no}" && cells[2].All(x => x == $"Tarih: 0{no}.10.2026");
}), "Each completed control retains its own author and date, including unsuccessful checks");
var allControlsPdf = YkcFr265PdfService.Olustur(form);
form.Kontroller = originalControls;
var signedOptions = new YkcFr265BelgeSecenekleri
{
    ImzaliNihaiMi = true,
    Imzalar = Enumerable.Range(1, 3).Select(no => new YkcFr265ImzaSatiri {
        SiraNo = no, AdSoyad = $"Imzaci {no}", ImzaTarihi = new DateTime(2026, 10, no + 3)
    }).ToList()
};
var signedCells = Fr265KontrolHucreleri(new YkcFr265FormService().WordOlustur(form, signedOptions).Bytes, 1);
Check(signedCells[1].SequenceEqual(["Adı Soyadı: Imzaci 2", "Adı Soyadı: Imzaci 3", "Firma / Yetkili: Imzaci 1"])
    && signedCells[2].SequenceEqual(["Tarih: 05.10.2026", "Tarih: 06.10.2026", "Tarih: 04.10.2026"])
    && signedCells[3].All(x => x.Contains("İmzalandı")),
    "Final form preserves actual signer identities, individual signature dates and signed markers");
form.Kontroller[0].KontrolEdenAdi = null;
form.Kontroller[0].KontrolTarihi = null;
form.FirmaYetkiliKisi = null;
var missingCells = Fr265KontrolHucreleri(new YkcFr265FormService().WordOlustur(form).Bytes, 1);
Check(missingCells[1][0] == "Adı Soyadı:" && missingCells[2].All(x => x == "Tarih:")
    && missingCells[1][2] == "Firma / Yetkili: Test Sertifikali Firma",
    "Missing control author and date are not invented; firm name is used when the representative is missing");
form.FirmaYetkiliKisi = "Test Yetkili";
form.Kontroller[0].KontrolEdenAdi = "PREVIOUS_CONTROL_STAFF";
form.Kontroller[0].KontrolTarihi = new DateTime(2026, 10, 2);
form.Kontroller[0].Aciklama = "PREVIOUS_CYCLE_ONLY";
form.Kontroller.AddRange(Enumerable.Range(6, 5).Select(no => new YkcFr265KontrolDto { KontrolNo = no }));
form.Kontroller.Single(x => x.KontrolNo == 6).Sonuc = YkcFr265KontrolSonucDegerleri.UygunDegil;
form.Kontroller.Single(x => x.KontrolNo == 6).Aciklama = "CURRENT_CYCLE_ONLY";
form.Kontroller.Single(x => x.KontrolNo == 6).KontrolEdenAdi = "CURRENT_CONTROL_STAFF";
form.Kontroller.Single(x => x.KontrolNo == 6).KontrolTarihi = new DateTime(2026, 10, 5);
using (var cycleZip = new System.IO.Compression.ZipArchive(new MemoryStream(new YkcFr265FormService().WordOlustur(form).Bytes)))
using (var cycleReader = new StreamReader(cycleZip.GetEntry("word/document.xml")!.Open()))
{
    var xml = cycleReader.ReadToEnd();
    Check(xml.Contains("CURRENT_CYCLE_ONLY") && !xml.Contains("PREVIOUS_CYCLE_ONLY"),
        "Official form contains only the active cycle, retaining old results in the model");
    Check(form.KontrolDonemi == 2 && form.AktifKontroller.Count == 5 && form.Kontroller.Count == 6,
        "DTO keeps historical controls and exposes five active form slots");
    Check(xml.Contains("CURRENT_CONTROL_STAFF") && !xml.Contains("PREVIOUS_CONTROL_STAFF")
        && xml.Contains("05.10.2026") && !xml.Contains("02.10.2026"),
        "Preview signature tables use only the active cycle's control author and date");
}
var demoPdf = YkcFr265PdfService.ImzaliNihaiOlustur(form, new() { ImzaliNihaiMi = true, ImzaTarihi = form.TalepTarihi });
Check(System.Text.Encoding.ASCII.GetString(formPdf.Bytes, 0, 5) == "%PDF-", "Draft renders as PDF");
Check(demoPdf.ContentType == "application/pdf" && demoPdf.DosyaAdi.Contains(YkcFr265PdfService.TasarimSurumu),
    "Demo final uses the versioned Word-template PDF layout");

var raporKaydi = new YkcRaporKayitDto
{
    Id = 88,
    TalepTarihi = new DateTime(2026, 9, 15, 10, 30, 0),
    TesisatNo = "1000132",
    SozlesmeNo = "432237",
    AboneNo = "34760217156",
    MusteriAdi = "Berrin Yıldız",
    FirmaAdi = "Demo Sertifikalı Firma",
    SirketAdi = "Çorumgaz Doğalgaz A.Ş.",
    EskiCihazTipi = "Kombi",
    EskiMarka = "Kaynak Marka",
    EskiKapasite = "20000 kcal/h",
    EskiBacaTipi = "Hermetik Kaynak",
    YeniCihazTipi = "Kombi",
    YeniMarka = "Yeni Marka",
    YeniModel = "=HYPERLINK(\"https://example.invalid\")",
    YeniKapasite = "24000 kcal/h",
    YeniBacaTipi = "Yoğuşmalı Yeni",
    Il = "Çorum",
    Ilce = "Merkez",
    Durum = YkcDurumDegerleri.Tamamlandi,
    ImzaliNihaiBelgeVar = true
};
var icOperasyonExcel = YkcRaporExcelService.Olustur(new[] { raporKaydi }, icOperasyon: true);
var icOperasyonExcelXml = ExcelParcasi(icOperasyonExcel, "xl/worksheets/sheet1.xml");
Check(icOperasyonExcel[0] == (byte)'P' && icOperasyonExcel[1] == (byte)'K', "YKC export is a real XLSX package");
Check(icOperasyonExcelXml.Contains("Projedeki Baca Tipi") && icOperasyonExcelXml.Contains("Hermetik Kaynak") && icOperasyonExcelXml.Contains("Berrin Yıldız"),
    "Internal XLSX contains source-device chimney data and report fields");
Check(icOperasyonExcelXml.Contains("=HYPERLINK") && !icOperasyonExcelXml.Contains("<f>"),
    "Formula-looking values remain plain Excel text");
var firmaExcelXml = ExcelParcasi(YkcRaporExcelService.Olustur(new[] { raporKaydi }, icOperasyon: false), "xl/worksheets/sheet1.xml");
Check(!firmaExcelXml.Contains("Projedeki Marka") && !firmaExcelXml.Contains("Kaynak Marka") && !firmaExcelXml.Contains("Hermetik Kaynak")
      && firmaExcelXml.Contains("Yeni Kullanılan Baca Tipi") && firmaExcelXml.Contains("Yoğuşmalı Yeni"),
    "Firm XLSX exposes the new chimney field without source-device data");
var raporPdf = YkcRaporPdfService.Olustur(new[] { raporKaydi }, icOperasyon: true);
Check(System.Text.Encoding.ASCII.GetString(raporPdf, 0, 5) == "%PDF-", "YKC report export is a PDF");

var typedReport = new YkcRaporKayitDto
{
    Id = 89, TalepTarihi = new DateTime(2026, 9, 15, 10, 30, 0),
    TesisatNo = "00123456789012345678", SozlesmeNo = "000943", AboneNo = "0001234567890",
    EskiKapasite = "20000,5", YeniKapasite = "25000.75", YeniModel = "=1+1",
    RandevuTarihi = new DateTime(2026, 9, 28), RandevuSaati = "14:30", SiradakiKontrolNo = 2,
    Durum = YkcDurumDegerleri.TalepAlindi, HedefUygulama = YkcHedefUygulamaDegerleri.YonetimPaneli
};
var typedReportExcel = YkcRaporExcelService.Olustur([typedReport], true);
var originalCulture = CultureInfo.CurrentCulture;
try
{
    foreach (var culture in new[] { "tr-TR", "en-US" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        foreach (var internalReport in new[] { true, false })
        {
            var excel = YkcRaporExcelService.Olustur([typedReport], internalReport);
            Check(ExcelTarihi(ExcelHucresi(excel, "Talep Tarihi"), typedReport.TalepTarihi)
                && ExcelTarihi(ExcelHucresi(excel, "Kontrol Randevusu"), new DateTime(2026, 9, 28, 14, 30, 0)),
                $"{culture}, internal={internalReport}: request and repeat appointment dates are sortable Excel dates");
            Check(ExcelSayisi(ExcelHucresi(excel, "Yeni Kullanılan Cihaz Kapasitesi"), 25000.75m)
                && (!internalReport || ExcelSayisi(ExcelHucresi(excel, "Projedeki Kapasite"), 20000.5m)),
                $"{culture}, internal={internalReport}: comma and point capacities are numeric, independent of culture");
            Check(ExcelSayisi(ExcelHucresi(excel, "Kontrol Sırası"), 2)
                && ExcelHucresi(excel, "Tesisat No").Value == typedReport.TesisatNo
                && (string?)ExcelHucresi(excel, "Tesisat No").Attribute("t") == "inlineStr"
                && ExcelHucresi(excel, "Sözleşme No").Value == "000943"
                && ExcelHucresi(excel, "Abone No").Value == "0001234567890",
                "Repeat control number is retained while identifiers keep leading zeroes and precision");
        }
    }
}
finally { CultureInfo.CurrentCulture = originalCulture; }
Check(ExcelHucresi(typedReportExcel, "Durum").Value == "Talep Alındı"
    && ExcelHucresi(typedReportExcel, "Hedef Uygulama").Value == "Yönetim Paneli"
    && !ExcelParcasi(typedReportExcel, "xl/worksheets/sheet1.xml").Contains("<f>"),
    "YKC export uses user-facing labels and does not execute formula-like text");
typedReport.Durum = YkcDurumDegerleri.AtamaBekliyor;
typedReport.HedefUygulama = YkcHedefUygulamaDegerleri.Crm187;
typedReport.RandevuTarihi = null;
typedReport.RandevuSaati = null;
typedReport.YeniKapasite = "Bilinmiyor";
var waitingExcel = YkcRaporExcelService.Olustur([typedReport], true);
Check(ExcelHucresi(waitingExcel, "Durum").Value == "İnceleniyor"
    && ExcelHucresi(waitingExcel, "Hedef Uygulama").Value == "187 Acil"
    && (string?)ExcelHucresi(waitingExcel, "Kontrol Randevusu").Attribute("t") == "inlineStr"
    && ExcelHucresi(waitingExcel, "Yeni Kullanılan Cihaz Kapasitesi").Value == "Bilinmiyor",
    "Unplanned appointments and unparseable legacy capacities are preserved without fabricated dates or numbers");
typedReport.RandevuTarihi = new DateTime(2026, 9, 29);
var dateOnlyExcel = YkcRaporExcelService.Olustur([typedReport], true);
Check(ExcelTarihi(ExcelHucresi(dateOnlyExcel, "Kontrol Randevusu"), new DateTime(2026, 9, 29))
    && (string?)ExcelHucresi(dateOnlyExcel, "Kontrol Randevusu").Attribute("s") == "3",
    "An appointment without a time uses a date-only Excel format");

var yetkiBelgesi = new Ys_YetkiBelgesi
{
    Id = 7,
    OlusturmaTarihi = new DateTime(2026, 9, 1, 9, 30, 0),
    YetkiBelgesiBaslangicTarihi = new DateTime(2026, 9, 1),
    YetkiBelgesiBitisTarihi = new DateTime(2027, 9, 1),
    Durum = YetkiBelgesiDurumDegerleri.Onaylandi,
    OnayTarihi = new DateTime(2026, 9, 2, 10, 0, 0),
    OnaylayanKullanici = "test.personel@demo.com",
    Firma = new Ys_Firma
    {
        FirmaAdi = "Demo Yetkili Servis",
        VergiNo = "1234567890",
        Sirket = new Dag_Sirket { SirketAdi = "Çorumgaz Doğalgaz A.Ş." }
    }
};
var belgeExcel = YetkiBelgesiRaporExcelService.Olustur(new[] { yetkiBelgesi }, "Onaylanan Yetki Belgeleri");
var belgeExcelXml = ExcelParcasi(belgeExcel, "xl/worksheets/sheet1.xml");
Check(belgeExcelXml.Contains("Demo Yetkili Servis") && belgeExcelXml.Contains("Onaylandı"),
    "Certificate XLSX contains scoped report data");
Check(ExcelTarihi(ExcelHucresi(belgeExcel, "Yükleme Tarihi"), yetkiBelgesi.OlusturmaTarihi)
    && ExcelTarihi(ExcelHucresi(belgeExcel, "Başlangıç Tarihi"), yetkiBelgesi.YetkiBelgesiBaslangicTarihi!.Value)
    && ExcelTarihi(ExcelHucresi(belgeExcel, "Bitiş Tarihi"), yetkiBelgesi.YetkiBelgesiBitisTarihi)
    && ExcelTarihi(ExcelHucresi(belgeExcel, "Sonuç Tarihi"), yetkiBelgesi.OnayTarihi!.Value),
    "Certificate upload, validity and decision dates use native Excel dates");
var certificateStyles = XDocument.Parse(ExcelParcasi(belgeExcel, "xl/styles.xml"));
Check(certificateStyles.Descendants().Any(x => x.Name.LocalName == "numFmt" && (string?)x.Attribute("formatCode") == "dd.mm.yyyy")
    && certificateStyles.Descendants().Any(x => x.Name.LocalName == "numFmt" && (string?)x.Attribute("formatCode") == "dd.mm.yyyy hh:mm"),
    "Date-only and date-time cells retain Turkish display formats");
var belgePdf = YetkiBelgesiRaporPdfService.Olustur(new[] { yetkiBelgesi }, "Onaylanan Yetki Belgeleri");
Check(System.Text.Encoding.ASCII.GetString(belgePdf, 0, 5) == "%PDF-", "Certificate report export is a PDF");
var servisDetayi = new AdminYetkiliServisDetaySonuc
{
    Servis = new Ys_Firma
    {
        FirmaAdi = "Demo Yetkili Servis",
        YetkiliKisi = "Ayşe Yılmaz",
        FaaliyetIli = "Çorum",
        Telefon = "05550000000",
        Sirket = new Dag_Sirket { SirketAdi = "Çorumgaz Doğalgaz A.Ş." },
        AktifMi = true
    },
    Subeler = new List<Ys_Sube> { new() { Ilce = "Merkez" } }
};
var servisExcel = YetkiliServisKayitDosyasi.ExcelOlustur(servisDetayi);
var servisExcelXml = ExcelParcasi(servisExcel, "xl/worksheets/sheet1.xml");
Check(servisExcelXml.Contains("Demo Yetkili Servis") && servisExcelXml.Contains("Merkez")
      && servisExcelXml.Contains("Ayşe Yılmaz"),
    "Service record XLSX contains firm, responsible person and branch district");
var servisPdf = YetkiliServisKayitDosyasi.PdfOlustur(servisDetayi);
Check(System.Text.Encoding.ASCII.GetString(servisPdf, 0, 5) == "%PDF-", "Service record export is a PDF");
var commissioningRecord = new Ys_DevreyeAlma
{
    Id = 38, TesistatNo = "1311884", SozlesmeNo = "0000943", AboneNo = "0001234567890",
    MusteriAdi = "Serhat Battal", CihazTipi = "Kombi", MusteriTelefon = "05550000000",
    CihazMarka = "Vaillant", CihazModeli = "Vaillant eloBLOCK VE 9 kW elektrikli kombi ısıtma cihazı",
    SeriNo = "200202020", CihazKapasite = "7740", TeknisyenAdi = "Kenan Kılıç",
    Adres = "Bahçelievler Mahallesi, Bahçelievler 6. Sokak, M. Ali Yurdakul Apartmanı No: 17/13 Çorum Merkez",
    Firma = new() { FirmaAdi = "Örnek Yetkili Servis", Sirket = new() { SirketAdi = "Çorumgaz Doğalgaz A.Ş." } },
    OlusturmaTarihi = new DateTime(2026, 10, 1), DevreyeAlmaTarihi = new DateTime(2026, 9, 25, 14, 30, 0),
    Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
};
var devreyeAlmaRaporu = DevreyeAlmaRaporPdfService.YetkiliServisRaporuOlustur(
    [commissioningRecord], new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
var adminCommissioningPdf = DevreyeAlmaRaporPdfService.AdminRaporuOlustur(
    [commissioningRecord], new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
var singleCommissioningPdf = DevreyeAlmaPdfService.Olustur(commissioningRecord);
Check(System.Text.Encoding.ASCII.GetString(devreyeAlmaRaporu, 0, 5) == "%PDF-", "Service commissioning report renders device details as PDF");
Check(System.Text.Encoding.ASCII.GetString(adminCommissioningPdf, 0, 5) == "%PDF-"
    && System.Text.Encoding.ASCII.GetString(singleCommissioningPdf, 0, 5) == "%PDF-",
    "Company and single-record commissioning PDFs render contract details and long device names");
var commissioningExcel = DevreyeAlmaExcelService.Olustur([new Ys_DevreyeAlma
{
    TesistatNo = "00012345678901234567", SozlesmeNo = "0000943", SeriNo = "000321", MusteriTelefon = "05550000000",
    CihazKapasite = "2,5", DevreyeAlmaTarihi = new DateTime(2026, 10, 2, 11, 30, 0)
}]);
Check(ExcelSayisi(ExcelHucresi(commissioningExcel, "Kapasite"), 2.5m)
    && ExcelTarihi(ExcelHucresi(commissioningExcel, "Devreye Alma Tarihi"), new DateTime(2026, 10, 2, 11, 30, 0))
    && ExcelHucresi(commissioningExcel, "Tesisat No").Value == "00012345678901234567"
    && ExcelHucresi(commissioningExcel, "Seri No").Value == "000321"
    && ExcelHucresi(commissioningExcel, "Telefon").Value == "05550000000",
    "Commissioning dates and capacities are typed while serials, phones and long IDs remain unchanged");
Check(ExcelHucresi(commissioningExcel, "Sözleşme No").Value == "0000943"
    && (string?)ExcelHucresi(commissioningExcel, "Sözleşme No").Attribute("t") == "inlineStr",
    "Commissioning export includes the sourced contract number as text, preserving leading zeroes");
var uzunNotluRapor = DevreyeAlmaRaporPdfService.YetkiliServisRaporuOlustur(new[]
{
    new Ys_DevreyeAlma
    {
        TesistatNo = "1311884", MusteriAdi = "Serhat Battal", CihazTipi = "Ocak",
        DevreyeAlmaTarihi = new DateTime(2026, 9, 25), Durum = DevreyeAlmaDurumDegerleri.Tamamlandi,
        Notlar = string.Join(" ", Enumerable.Repeat("Uzun servis işlem notu", 500))
    }
}, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
Check(System.Text.Encoding.ASCII.GetString(uzunNotluRapor, 0, 5) == "%PDF-", "Long service notes continue across PDF pages");
var multiRecordPdf = DevreyeAlmaRaporPdfService.YetkiliServisRaporuOlustur(
    Enumerable.Range(1, 8).Select(index => new Ys_DevreyeAlma
    {
        Id = index, TesistatNo = $"000100{index:D3}", SozlesmeNo = $"000943{index:D2}",
        MusteriAdi = index == 2 ? "Birlik Mantar Sanayi ve Ticaret Limited Şirketi" : $"Örnek Abone {index}",
        Firma = commissioningRecord.Firma, CihazTipi = index % 2 == 0 ? "Ocak" : "Kombi",
        CihazMarka = commissioningRecord.CihazMarka, CihazModeli = commissioningRecord.CihazModeli,
        SeriNo = $"000SERIAL{index}", CihazKapasite = "7740", TeknisyenAdi = "Kenan Kılıç",
        TeknisyenYetkiBelgesiNo = $"000BELGE{index}", Adres = commissioningRecord.Adres,
        DevreyeAlmaTarihi = new DateTime(2026, 9, index), Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
    }), new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
Check(System.Text.Encoding.ASCII.GetString(multiRecordPdf, 0, 5) == "%PDF-",
    "Multi-record commissioning report renders aligned groups, long customer names and page numbering");
if (args.Length == 2 && args[0] == "--form-output")
{
    Directory.CreateDirectory(args[1]);
    File.WriteAllBytes(Path.Combine(args[1], "form-draft.pdf"), formPdf.Bytes);
    File.WriteAllBytes(Path.Combine(args[1], "form-demo.pdf"), demoPdf.Bytes);
    File.WriteAllBytes(Path.Combine(args[1], "form-source.docx"), wordForm.Bytes);
    File.WriteAllBytes(Path.Combine(args[1], "form-five-controls.pdf"), allControlsPdf.Bytes);
    File.WriteAllBytes(Path.Combine(args[1], "ykc-report.pdf"), raporPdf);
    File.WriteAllBytes(Path.Combine(args[1], "ykc-report.xlsx"), icOperasyonExcel);
    File.WriteAllBytes(Path.Combine(args[1], "ykc-typed-report.xlsx"), typedReportExcel);
    File.WriteAllBytes(Path.Combine(args[1], "certificate-report.xlsx"), belgeExcel);
    File.WriteAllBytes(Path.Combine(args[1], "commissioning-report.xlsx"), commissioningExcel);
    File.WriteAllBytes(Path.Combine(args[1], "commissioning-service.pdf"), devreyeAlmaRaporu);
    File.WriteAllBytes(Path.Combine(args[1], "commissioning-company.pdf"), adminCommissioningPdf);
    File.WriteAllBytes(Path.Combine(args[1], "commissioning-single.pdf"), singleCommissioningPdf);
    File.WriteAllBytes(Path.Combine(args[1], "commissioning-multiple.pdf"), multiRecordPdf);
    File.WriteAllBytes(Path.Combine(args[1], "commissioning-long-note.pdf"), uzunNotluRapor);
}
var ekranSimdi = new DateTime(2026, 10, 7, 12, 0, 0);
var ekranYetki = new YkcYetkiOzeti { TalepleriGorebilir = true, AtamaYapabilir = true, Fr265ImzaIslemiYapabilir = true };
var ekranImza = new YkcImzaEntegrasyonDto { KullanilabilirMi = true };
var ekranTalep = new YkcTalepDetayDto
{
    Durum = YkcDurumDegerleri.Atandi, RandevuTarihi = ekranSimdi.Date, RandevuSaati = "12:30",
    EskiCihazTipi = "Kombi", YeniCihazTipi = "Kombi", EskiKapasite = "20000", YeniKapasite = "20000",
    Atamalar = [new() { Id = 10, RandevuTarihi = ekranSimdi.Date, RandevuSaati = "12:30" }]
};
YkcTalepEkranDto Ekran() => YkcTalepIslemKurali.EkranHazirla(ekranTalep, ekranYetki, true, ekranImza, [], ekranSimdi);
Check(Ekran().KontroleGecisGorsun && !Ekran().RandevuZamaniGeldi && Ekran().AtamaYapilabilir,
    "API screen keeps appointment transition disabled until the appointment time");
Check(Ekran().CihazUyarilari.Count == 0, "API screen accepts missing chimney data and equal capacity");
ekranTalep.Durum = YkcDurumDegerleri.SahaIsleminde;
Check(!Ekran().KontrolBolumuAktif, "A stale field-control state cannot enable entry before the appointment");
ekranTalep.YeniKapasite = "21000";
Check(Ekran().CihazUyarilari.Count == 1, "API screen compares device capacity on the server");
ekranTalep.RandevuSaati = "11:30";
ekranTalep.Atamalar[0].RandevuSaati = "11:30";
ekranTalep.Durum = YkcDurumDegerleri.SahaIsleminde;
Check(Ekran().RandevuZamaniGeldi && Ekran().KontrolBolumuAktif && Ekran().AktifKontrolNo == 1,
    "API screen exposes the first technical control after the appointment");
ekranTalep.Kontroller = [new() { KontrolNo = 1, AtamaId = 10, Sonuc = YkcFr265KontrolSonucDegerleri.Uygun }];
Check(Ekran().ImzayaGonderebilir && !Ekran().KontrolBolumuAktif
    && YkcTalepIslemKurali.ImzaGonderimineHazirMi(ekranTalep, ekranSimdi, out _),
    "Screen and signature command share the same current-appointment readiness rule");
ekranTalep.Kontroller[0].AtamaId = 9;
Check(!Ekran().ImzayaGonderebilir && !YkcTalepIslemKurali.ImzaGonderimineHazirMi(ekranTalep, ekranSimdi, out _),
    "An old appointment's suitable control cannot enable signature sending");
ekranTalep.Kontroller[0].AtamaId = 10;
ekranTalep.ImzaSureci = new() { Durum = YkcImzaDurumDegerleri.ImzayaGonderildi, GonderimTarihi = ekranSimdi.AddMinutes(-5) };
Check(!Ekran().ImzayaGonderebilir && !Ekran().AtamaYapilabilir,
    "The five-minute signature lock boundary matches the command and locks assignment");
ekranTalep.ImzaSureci.GonderimTarihi = ekranSimdi.AddMinutes(-5).AddSeconds(-1);
Check(Ekran().ImzayaGonderebilir, "An expired signature submission lock permits retry");
ekranTalep.ImzaSureci.ProviderDocumentId = "provider-1";
Check(!Ekran().ImzayaGonderebilir && Ekran().ImzaDurumuSorgulanabilir,
    "An existing provider document permits polling but never another submission");
ekranTalep.ImzaSureci.Durum = YkcImzaDurumDegerleri.Tamamlandi;
ekranTalep.ImzaSureci.NihaiDosyaId = 12;
Check(!Ekran().TamamlamayaHazir, "A completed provider state without the stored final PDF cannot complete the request");
ekranTalep.Dosyalar = [new() { Id = 12, DosyaTuru = YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai }];
Check(Ekran().TamamlamayaHazir && Ekran().IndirilebilirDosyalar.Count == 1,
    "The verified final PDF enables completion and authorized download");
var readOnlyScreen = YkcTalepIslemKurali.EkranHazirla(ekranTalep, new() { TalepleriGorebilir = true }, true, ekranImza, [], ekranSimdi);
Check(!readOnlyScreen.TamamlamayaHazir && !readOnlyScreen.AtamaYapilabilir && !readOnlyScreen.RedIptalYapabilir
    && !readOnlyScreen.ImzayaGonderebilir, "Read-only access never enables write actions");
var firmScreen = YkcTalepIslemKurali.EkranHazirla(ekranTalep, new() { TalepleriGorebilir = true }, false, ekranImza,
    [new() { Id = "private-team", Secili = true }], ekranSimdi);
Check(!firmScreen.IcOperasyonGorsun && firmScreen.Ekipler.Count == 0 && firmScreen.SeciliEkipId == null && firmScreen.CihazUyarilari.Count == 0
    && !firmScreen.TamamlamayaHazir, "Certified firm screen receives neither internal comparisons nor assignment choices");
ekranImza.DemoModuMu = true;
ekranTalep.ImzaSureci.ProviderDocumentId = "DEMO-YKC-fixture";
Check(Ekran().ImzaliBelgeDemoMu && Ekran().DemoPdfGuncellenebilir,
    "API enables legacy demo PDF refresh for authorized internal operators");
Check(!YkcTalepIslemKurali.EkranHazirla(ekranTalep, ekranYetki, false, ekranImza, [], ekranSimdi).DemoPdfGuncellenebilir,
    "Firm views cannot refresh demo PDFs even with stale internal permission flags");
ekranTalep.Dosyalar[0].DosyaAdi = $"Form_Demo_Nihai_{YkcFr265PdfService.TasarimSurumu}_1.pdf";
Check(!Ekran().DemoPdfGuncellenebilir, "Current demo PDF version needs no refresh");
ekranTalep.ImzaSureci.ProviderDocumentId = "real-provider-fixture";
ekranTalep.Dosyalar[0].DosyaAdi = "old.pdf";
Check(!Ekran().ImzaliBelgeDemoMu && !Ekran().DemoPdfGuncellenebilir,
    "Real provider documents are never treated as demo refresh candidates");
ekranImza.DemoModuMu = false;
ekranTalep.ImzaSureci = null;
ekranTalep.Dosyalar.Clear();
ekranTalep.Durum = YkcDurumDegerleri.AtamaBekliyor;
ekranTalep.Kontroller = Enumerable.Range(1, 5).Select(no => new YkcFr265KontrolDto
    { KontrolNo = no, AtamaId = 10, Sonuc = YkcFr265KontrolSonucDegerleri.UygunDegil }).ToList();
Check(Ekran().KontrolAlaniDoldu && Ekran().AtamaYapilabilir && !Ekran().ImzayaGonderebilir
    && Ekran().AktifKontrolNo == null, "Five unsuitable controls close the current period and retain appointment planning");
ekranTalep.Kontroller.AddRange(Enumerable.Range(6, 5).Select(no => new YkcFr265KontrolDto { KontrolNo = no }));
Check(Ekran().KontrolDonemi == 2 && Ekran().AktifKontrolNo == 1 && Ekran().TumSonucluKontroller.Count == 5
    && Ekran().SonUygunsuzKontrol?.KontrolNo == 5,
    "A new five-control period preserves past results and the last unsuitable reason");
var ekranJson = System.Text.Json.JsonSerializer.Serialize(Ekran());
var ekranRoundTrip = System.Text.Json.JsonSerializer.Deserialize<YkcTalepEkranDto>(ekranJson)!;
Check(ekranRoundTrip.KontrolDonemi == 2 && ekranRoundTrip.AktifKontrolNo == 1
    && ekranRoundTrip.TumSonucluKontroller.Count == 5, "API screen state survives JSON without client-side rule execution");
foreach (var terminal in new[] { YkcDurumDegerleri.Tamamlandi, YkcDurumDegerleri.Reddedildi, YkcDurumDegerleri.Iptal })
{
    ekranTalep.Durum = terminal;
    Check(Ekran().TerminalDurum && !Ekran().AtamaYapilabilir && !Ekran().RedIptalYapabilir && !Ekran().KontrolBolumuAktif,
        "Terminal state disables operational actions: " + terminal);
}
Console.WriteLine($"{passed} checks passed. No application data changed.");

sealed class RecordingOnlineHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public Uri? LastUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("")
        });
    }
}

sealed class StorageTestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "YkcRules";
    public string ContentRootPath { get; set; } = string.Empty;
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
