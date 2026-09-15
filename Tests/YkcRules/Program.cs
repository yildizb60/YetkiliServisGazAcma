using YetkiliServisGazAcma.Business.Services;
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

var appointment = new DateTime(2030, 1, 2, 15, 0, 0);
Check(YkcRandevuKurali.Cakisiyor(appointment, appointment, 0), "Same minute conflicts without invented visit duration");
Check(!YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(1), 0), "Different minute allowed with default interval");
Check(YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(9), 10), "Configured interval respected");
Check(!YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(10), 10), "Exact interval boundary allowed");
Check(YkcRandevuKurali.Cakisiyor(appointment, appointment.AddMinutes(-9), 10), "Interval is symmetric");
Check(YkcRandevuKurali.Cakisiyor(appointment.Date, appointment.Date.AddMinutes(-5), 10), "Interval crosses midnight");
Check(YkcKontrolAkisKurali.YeniRandevuGerekli(YkcFr265KontrolSonucDegerleri.UygunDegil),
    "Unsuccessful control requires a new appointment");
Check(!YkcKontrolAkisKurali.YeniRandevuGerekli(YkcFr265KontrolSonucDegerleri.Uygun),
    "Successful control continues to form and signature");

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
    YeniCihazTipi = "Kombi",
    YeniMarka = "Yeni Marka",
    YeniModel = "=HYPERLINK(\"https://example.invalid\")",
    YeniKapasite = "24000 kcal/h",
    Il = "Çorum",
    Ilce = "Merkez",
    Durum = YkcDurumDegerleri.Tamamlandi,
    ImzaliNihaiBelgeVar = true
};
var icOperasyonExcel = YkcRaporExcelService.Olustur(new[] { raporKaydi }, icOperasyon: true);
var icOperasyonExcelXml = ExcelParcasi(icOperasyonExcel, "xl/worksheets/sheet1.xml");
Check(icOperasyonExcel[0] == (byte)'P' && icOperasyonExcel[1] == (byte)'K', "YKC export is a real XLSX package");
Check(icOperasyonExcelXml.Contains("Projedeki Marka") && icOperasyonExcelXml.Contains("Berrin Yıldız"),
    "Internal XLSX contains Turkish headings and report data");
Check(icOperasyonExcelXml.Contains("=HYPERLINK") && !icOperasyonExcelXml.Contains("<f>"),
    "Formula-looking values remain plain Excel text");
var firmaExcelXml = ExcelParcasi(YkcRaporExcelService.Olustur(new[] { raporKaydi }, icOperasyon: false), "xl/worksheets/sheet1.xml");
Check(!firmaExcelXml.Contains("Projedeki Marka") && !firmaExcelXml.Contains("Kaynak Marka"),
    "Firm XLSX does not expose source-device columns");
var raporPdf = YkcRaporPdfService.Olustur(new[] { raporKaydi }, icOperasyon: true);
Check(System.Text.Encoding.ASCII.GetString(raporPdf, 0, 5) == "%PDF-", "YKC report export is a PDF");
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
