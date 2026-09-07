using YetkiliServisGazAcma.Business.Services;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

// No database or external providers: checks cannot alter application records.
using var snapshots = new YkcSorguKaydiService();
var reference = snapshots.Ekle("firm-a", new YkcTalepKaydetDto {
    TesisatNo = "100", SozlesmeNo = "200", FirmaId = 7, SirketId = 3,
    EskiCihazTipi = "Kombi", EskiMarka = "Source brand", EskiKapasite = "20000",
    ProjeNo = "source-project", SayacNo = "source-meter", Bolge = "source-region",
    MusteriAdi = "Source customer"
});
YkcTalepKaydetDto Request() => new() {
    SorguReferansi = reference, TesisatNo = "100", SozlesmeNo = "200",
    FirmaId = 99, SirketId = 99, EskiMarka = "Forged", EskiKapasite = "1",
    ProjeNo = "forged-project", SayacNo = "forged-meter", Bolge = "forged-region",
    YeniMarka = "User entered", YeniKapasite = "25000"
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

Check(YkcCihazUyumKurali.Kapasite("20000,5", out var capacity) && capacity == 20000.5m, "Decimal comma accepted");
Check(!YkcCihazUyumKurali.Kapasite("string", out _), "Placeholder capacity rejected");
Check(!YkcCihazUyumKurali.Kapasite("0", out _) && !YkcCihazUyumKurali.Kapasite("-5", out _), "Capacity must be positive");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "B", "X", "Y", "20000", "25000").Count == 3,
    "Mismatches produce warnings without mutating values");
Check(YkcCihazUyumKurali.Uyarilar("Kombi", "Kombi", "A", "A", null, null, "20000", "20000.0").Count == 0,
    "Equal device values do not warn");
Console.WriteLine($"{passed} checks passed. No application data changed.");
