using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
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

// No database or external providers: checks cannot alter application records.
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
YkcTalepDetayDto Detail() => new() {
    EskiMarka = "Source brand", EskiKapasite = "20000", YeniMarka = "New brand", MusteriAdi = "Customer",
    AtananEkip = "Internal team", HedefUygulama = "Internal target",
    Atamalar = new() { new YkcAtamaDto() },
    Gecmis = new() { new YkcGecmisDto { IslemTipi = "AtamaYapildi", KullaniciAdi = "Internal user", Aciklama = "Internal note" } }
};
var official = Detail();
YkcFirmaSunumu.Hazirla(official, resmiForm: true);
Check(official.EskiMarka == "Source brand" && official.EskiKapasite == "20000", "Official form retains source device fields");
Check(official.Atamalar.Count == 0 && official.AtananEkip == null && official.HedefUygulama == null,
    "Official form response does not expose internal routing");
Check(official.Gecmis[0].Aciklama == null && official.Gecmis[0].KullaniciAdi == null,
    "Official form response does not expose internal assignment note");
var screen = Detail();
YkcFirmaSunumu.Hazirla(screen, resmiForm: false);
Check(screen.EskiMarka == null && screen.EskiKapasite == null && screen.YeniMarka == "New brand" && screen.MusteriAdi == "Customer",
    "Firm screen hides source device without losing request data");
var form = new YkcTalepDetayDto {
    Id = 42, TesisatNo = "1000132", MusteriAdi = "Test Abone",
    FirmaAdi = "Test Sertifikali Firma", FirmaYetkiliKisi = "Test Yetkili",
    TalepTarihi = new DateTime(2026, 9, 10), TuketimNoktasi = "Daire 4", BaglantiNesnesi = "Bina 12",
    Adres = "Test Mahallesi, Test Sokak No: 4/2, Merkez",
    EskiCihazTipi = "Kombi", YeniCihazTipi = "Kombi", EskiMarka = "Proje markasi", YeniMarka = "Yeni marka",
    EskiKapasite = "20000", YeniKapasite = "20000", IkinciElCihazMi = false,
    Kontroller = new() { new YkcFr265KontrolDto { KontrolNo = 1, Sonuc = YkcFr265KontrolSonucDegerleri.Uygun } }
};
var wordForm = new YkcFr265FormService().WordOlustur(form);
using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(wordForm.Bytes)))
using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
{
    var xml = reader.ReadToEnd();
    Check(xml.Contains("Daire 4") && xml.Contains("Bina 12"), "Official form retains supplied unit and building fields");
}
var formPdf = YkcFr265PdfService.Olustur(form);
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
var belgePdf = YetkiBelgesiRaporPdfService.Olustur(new[] { yetkiBelgesi }, "Onaylanan Yetki Belgeleri");
Check(System.Text.Encoding.ASCII.GetString(belgePdf, 0, 5) == "%PDF-", "Certificate report export is a PDF");
if (args.Length == 2 && args[0] == "--form-output")
{
    Directory.CreateDirectory(args[1]);
    File.WriteAllBytes(Path.Combine(args[1], "form-draft.pdf"), formPdf.Bytes);
    File.WriteAllBytes(Path.Combine(args[1], "form-demo.pdf"), demoPdf.Bytes);
    File.WriteAllBytes(Path.Combine(args[1], "form-source.docx"), wordForm.Bytes);
    File.WriteAllBytes(Path.Combine(args[1], "ykc-report.pdf"), raporPdf);
    File.WriteAllBytes(Path.Combine(args[1], "ykc-report.xlsx"), icOperasyonExcel);
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
