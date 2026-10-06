using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using YetkiliServisGazAcma.ViewComponents;

// All HTTP responses are fixtures; no network requests or application data changes.
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

var firmDetail = new YkcTalepDetayDto
{
    Durum = YkcDurumDegerleri.AtamaBekliyor,
    Kontroller = [new() { KontrolNo = 1, Sonuc = YkcFr265KontrolSonucDegerleri.UygunDegil, Aciklama = "Fixture reason" }]
};
foreach (var state in new[] { YkcDurumDegerleri.AtamaBekliyor, YkcDurumDegerleri.Atandi, YkcDurumDegerleri.SahaIsleminde })
{
    firmDetail.Durum = state;
    Check(YkcDurumSunumu.SonUygunsuzKontrol(firmDetail)?.Aciklama == "Fixture reason",
        $"Firm sees the last failure reason during planning, appointment and inspection: {state}");
}
firmDetail.Kontroller.Add(new() { KontrolNo = 6 });
Check(YkcDurumSunumu.SonUygunsuzKontrol(firmDetail)?.KontrolNo == 1,
    "A pending new control cycle does not hide the last recorded failure");
firmDetail.Kontroller.Add(new() { KontrolNo = 7, Sonuc = YkcFr265KontrolSonucDegerleri.Uygun });
Check(YkcDurumSunumu.SonUygunsuzKontrol(firmDetail) == null,
    "A later suitable control clears the previous failure summary");
firmDetail.Kontroller.RemoveAt(firmDetail.Kontroller.Count - 1);
foreach (var state in new[] { YkcDurumDegerleri.TalepAlindi, YkcDurumDegerleri.Tamamlandi, YkcDurumDegerleri.Iptal, YkcDurumDegerleri.Reddedildi })
{
    firmDetail.Durum = state;
    Check(YkcDurumSunumu.SonUygunsuzKontrol(firmDetail) == null,
        $"Historical control reasons do not override the request's current outcome: {state}");
}
firmDetail.Kontroller.Clear();
firmDetail.Durum = YkcDurumDegerleri.Atandi;
Check(YkcDurumSunumu.SonUygunsuzKontrol(firmDetail) == null, "A request with no recorded control gets no invented failure");

foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin" })
{
    using var fixture = new NavigationFixture(role);
    for (var i = 1; i <= 50; i++)
    {
        var id = "person-" + i;
        fixture.Handler.PermissionList.Personeller.Add(new AppKullanici { Id = id, AdSoyad = $"Personel {i:00}", Email = $"person{i}@fixture.test" });
        fixture.Handler.PermissionList.SirketYetkileri[id] = [new AdminSirketYetkiOzeti
        {
            SirketId = 7, SirketAdi = i % 2 == 0 ? "SürmeliGAZ" : "Çorumgaz",
            Yetkiler = [YetkiTipleri.YKC_TALEP_GOR]
        }];
    }
    var first = (ViewResult)await fixture.AdminPanelController.Yetkiler();
    var firstRows = (List<AppKullanici>)first.ViewData["Personeller"]!;
    Check(firstRows.Count == 10 && firstRows.First().Id == "person-1" && firstRows.Last().Id == "person-10"
        && (int)first.ViewData["ToplamPersonel"]! == 50 && (int)first.ViewData["ToplamSayfa"]! == 5,
        role + ": 50 personnel are displayed as five compact pages without changing overall counters");
    Check(role != "SirketAdmin" || fixture.Handler.PermissionScope == 7,
        role + ": permission list pagination preserves company scope");
    var last = (ViewResult)await fixture.AdminPanelController.Yetkiler(sayfa: int.MaxValue);
    Check((int)last.ViewData["Sayfa"]! == 5 && ((List<AppKullanici>)last.ViewData["Personeller"]!).First().Id == "person-41",
        role + ": out-of-range pages clamp to the final valid page");
    var low = (ViewResult)await fixture.AdminPanelController.Yetkiler(sayfa: -1);
    Check((int)low.ViewData["Sayfa"]! == 1, role + ": negative pages return to the first page");
    var search = (ViewResult)await fixture.AdminPanelController.Yetkiler("  person49@fixture.test  ", 5);
    Check(((List<AppKullanici>)search.ViewData["Personeller"]!).Single().Id == "person-49"
        && (int)search.ViewData["EslesenPersonel"]! == 1 && (int)search.ViewData["Sayfa"]! == 1
        && (int)search.ViewData["ToplamPersonel"]! == 50,
        role + ": email search spans all pages and preserves the full overview count");
    search = (ViewResult)await fixture.AdminPanelController.Yetkiler("PERSONEL 03");
    Check(((List<AppKullanici>)search.ViewData["Personeller"]!).Single().Id == "person-3",
        role + ": personnel search is case-insensitive");
    search = (ViewResult)await fixture.AdminPanelController.Yetkiler("sürmeligaz");
    Check((int)search.ViewData["EslesenPersonel"]! == 25 && (int)search.ViewData["ToplamSayfa"]! == 3,
        role + ": company search handles Turkish casing and retains pagination");
    search = (ViewResult)await fixture.AdminPanelController.Yetkiler("no matching person");
    Check(((List<AppKullanici>)search.ViewData["Personeller"]!).Count == 0
        && (int)search.ViewData["EslesenPersonel"]! == 0 && (int)search.ViewData["Sayfa"]! == 1,
        role + ": empty search results retain a valid empty page");
    var save = (RedirectToActionResult)await fixture.AdminPanelController.YetkiDuzenle("person-31", [7],
        new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["yetkiler_7"] = YetkiTipleri.YKC_TALEP_GOR }), "Personel", 4);
    Check(save.ActionName == nameof(AdminPanelController.Yetkiler) && (string?)save.RouteValues!["q"] == "Personel"
        && (int)save.RouteValues["sayfa"]! == 4,
        role + ": saving permissions returns to the same search and page");
}
using (var fixture = new NavigationFixture("Personel"))
{
    Check(await fixture.AdminPanelController.Yetkiler("anything", 2) is ForbidResult && fixture.Handler.PermissionListCalls == 0,
        "Permission list search and pagination do not grant management rights to personnel");
}

foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin" })
{
    using var fixture = new NavigationFixture(role);
    var direct = (RedirectToActionResult)await fixture.AdminPanelController.SubeDuzenle(8);
    Check(direct.ActionName == nameof(AdminPanelController.Subeler) && direct.RouteValues!["duzenle"] is 8,
        role + ": direct branch edit links return to the list and open the correct drawer");
    fixture.Http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
    var edit = (ViewResult)await fixture.AdminPanelController.SubeDuzenle(8);
    Check(edit.ViewName == "~/Views/AdminPanel/SubeDuzenle.cshtml"
        && ((YetkiliServisGazAcma.Entities.Ys_Sube)edit.ViewData["Sube"]!).Id == 8,
        role + ": AJAX branch editing returns the form for the scoped record");
    Check(fixture.Handler.LastBranchPayload.GetProperty("id").GetInt32() == 8
        && (role != "SirketAdmin" || fixture.Handler.LastBranchPayload.GetProperty("sirketId").GetInt32() == 7),
        role + ": drawer lookup retains record and company scope");
    fixture.Handler.BranchAvailable = false;
    Check(await fixture.AdminPanelController.SubeDuzenle(8) is RedirectResult
        && fixture.AdminPanelController.TempData.ContainsKey("Hata"),
        role + ": an unavailable branch never returns an editable form");
    fixture.Handler.BranchAvailable = true;
    await fixture.AdminPanelController.SubeEkle(10, "Branch", "City", "District", "05550000000", "Address", true);
    Check(fixture.Handler.LastBranchPath.EndsWith("/ekle")
        && fixture.Handler.LastBranchPayload.GetProperty("firmaId").GetInt32() == 10
        && fixture.Handler.LastBranchPayload.GetProperty("aktifMi").GetBoolean(),
        role + ": creating a branch retains its firm and active state");
    await fixture.AdminPanelController.SubeDuzenle(8, 10, "Edited branch", "City", "District", "05550000000", "Address", false);
    Check(fixture.Handler.LastBranchPath.EndsWith("/guncelle")
        && fixture.Handler.LastBranchPayload.GetProperty("id").GetInt32() == 8
        && !fixture.Handler.LastBranchPayload.GetProperty("aktifMi").GetBoolean(),
        role + ": editing a branch retains its ID and unchecked active state");
}
using (var fixture = new NavigationFixture("Personel"))
{
    fixture.Http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
    Check(await fixture.AdminPanelController.SubeDuzenle(8) is ForbidResult && fixture.Handler.BranchCalls == 0,
        "A drawer request does not grant personnel branch management permission");
}
using (var fixture = new NavigationFixture("YetkiliServis"))
{
    var direct = (RedirectToActionResult)await fixture.ServicePanelController.SubeDuzenle(8);
    Check(direct.ActionName == nameof(YetkiliServisPanelController.Subeler) && direct.RouteValues!["duzenle"] is 8,
        "Service branch links retain the same drawer navigation");
    fixture.Http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
    Check(await fixture.ServicePanelController.SubeDuzenle(8) is ViewResult,
        "Service branch editing still loads the firm's own branch");
    Check(await fixture.ServicePanelController.SubeDuzenle(9) is RedirectResult,
        "Service branch editing cannot load a branch outside the firm's profile");
}

foreach (var mode in new[] { "gun", "ay", "yil", "invalid" })
{
    using var fixture = new NavigationFixture("Personel");
    fixture.Http.Request.QueryString = new QueryString("?takvimTarih=2028-02-29&takvimGorunum=" + mode
        + "&takvimAbone=ignored&takvimPersonel=ignored");
    var result = (ViewViewComponentResult)await fixture.Calendar.InvokeAsync(genelTakvim: true);
    var model = (YkcRandevuOzetiModel)result.ViewData!.Model!;
    Check(model.GenelTakvim && model.Baslik == "Takvim" && model.Tarih == new DateTime(2028, 2, 29)
        && model.Gorunum == (mode == "invalid" ? "ay" : mode),
        "General calendar preserves the selected date and validates the view mode: " + mode);
    Check(model.Gun == null && model.AyGunleri.Count == 0 && model.Filtre.Musteri == null
        && fixture.Handler.CalendarFilters.Count == 0 && fixture.Handler.CalendarPermissionCalls == 0,
        "General calendar has no appointment data or operational filters: " + mode);
    fixture.Http.Request.QueryString = new QueryString("?takvimTarih=not-a-date");
    result = (ViewViewComponentResult)await fixture.Calendar.InvokeAsync(genelTakvim: true);
    Check(((YkcRandevuOzetiModel)result.ViewData!.Model!).Tarih == DateTime.Today,
        "General calendar falls back to today for an invalid date: " + mode);
}

using (var fixture = new NavigationFixture("Personel"))
{
    fixture.Http.Request.QueryString = new QueryString("?takvimTarih=2026-10-05&takvimAbone=Subscriber&takvimPersonel=Team");
    async Task<YkcRandevuOzetiModel> CalendarModel() =>
        (YkcRandevuOzetiModel)((ViewViewComponentResult)await fixture.Calendar.InvokeAsync()).ViewData!.Model!;

    var model = await CalendarModel();
    Check(model.GenelTakvim && model.Baslik == "Takvim" && model.Gun == null
        && model.Filtre.Musteri == null && fixture.Handler.CalendarFilters.Count == 0,
        "Without appointment read permission the calendar stays plain and does not query records");

    fixture.Handler.CalendarCompanies.Add(7);
    model = await CalendarModel();
    Check(!model.GenelTakvim && model.Gun?.Kayitlar.Single().Musteri == "Fixture subscriber"
        && model.Gun.Kayitlar.Single().Id == 54 && model.AyGunleri.Count == 1,
        "Granting read permission restores appointment names, detail IDs and day counts");
    Check(model.Filtre.Musteri == "Subscriber" && model.Filtre.Personel == "Team"
        && fixture.Handler.CalendarFilters.Count == 2
        && fixture.Handler.CalendarFilters.All(x => x.AktifSirketId == 7),
        "Authorized calendar filters are applied only within the active company");

    fixture.Http.Session.SetInt32("AktifSirketId:fixture-user", 8);
    model = await CalendarModel();
    Check(model.GenelTakvim && model.Gun == null && model.AyGunleri.Count == 0
        && fixture.Handler.CalendarFilters.Count == 2,
        "Switching to a company without permission removes appointment data without querying it");

    fixture.Handler.CalendarCompanies.Add(8);
    model = await CalendarModel();
    Check(!model.GenelTakvim && fixture.Handler.CalendarFilters.Count == 4
        && fixture.Handler.CalendarFilters.Skip(2).All(x => x.AktifSirketId == 8),
        "Permission granted in the new company loads only that company's calendar");

    fixture.Handler.CalendarCompanies.Remove(8);
    model = await CalendarModel();
    Check(model.GenelTakvim && model.Gun == null && model.Filtre.Personel == null
        && fixture.Handler.CalendarFilters.Count == 4 && fixture.Handler.CalendarPermissionCalls == 5,
        "Revoking permission restores the plain calendar instead of retaining data or hiding the calendar");

    fixture.Handler.CalendarPermissionUnavailable = true;
    model = await CalendarModel();
    Check(model.GenelTakvim && model.Gun == null && fixture.Handler.CalendarFilters.Count == 4,
        "Unverified permissions never expose operational calendar links or trigger a data query");
}

foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "Personel" })
{
    using var fixture = new NavigationFixture(role);
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi }, "takvim");
    Check(result.ActionName == nameof(YkcController.Raporlar)
        && (int)result.RouteValues!["detayTalepId"]! == 54
        && !result.RouteValues.ContainsKey("talepId")
        && fixture.Controller.TempData.ContainsKey("Basarili"),
        role + ": successful completion requests the exact report modal and keeps the success message");
}

using (var fixture = new NavigationFixture("Personel"))
{
    fixture.Handler.Success = false;
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi }, "takvim");
    Check(result.ActionName == nameof(YkcController.Detay) && (string?)result.RouteValues!["kaynak"] == "takvim"
        && fixture.Controller.TempData.ContainsKey("Hata"), "Failed completion stays on detail with its error and source");
}

using (var fixture = new NavigationFixture("Personel", reportPermission: false))
{
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi });
    Check(result.ActionName == nameof(YkcController.Detay), "Completion without report permission stays on detail");
    var report = (RedirectResult)await fixture.Report(54, openDetail: true);
    Check(report.Url == "/yetkisiz-erisim" && fixture.Handler.LastReportFilter == null,
        "A report URL cannot bypass report permission");
}

using (var fixture = new NavigationFixture("Personel", signaturePermission: false))
{
    var result = (RedirectResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.Tamamlandi });
    Check(result.Url == "/yetkisiz-erisim" && fixture.Handler.OperationCalls == 0,
        "Completion still requires signature permission before calling the API");
}

using (var fixture = new NavigationFixture("Personel"))
{
    var result = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(
        new() { TalepId = 54, Durum = YkcDurumDegerleri.SahaIsleminde });
    Check(result.ActionName == nameof(YkcController.Detay), "An intermediate status stays on detail");
    var signature = (RedirectToActionResult)await fixture.Controller.ImzaDurumSorgula(54);
    Check(signature.ActionName == nameof(YkcController.Detay), "Signature status alone does not complete the request");

    var report = (ViewResult)await fixture.Report(54);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri!.SequenceEqual([54])
        && (int)report.ViewData["TalepId"]! == 54, "Report filtering uses the exact request ID");
    Check(fixture.Handler.LastReportFilter.SirketId == 7, "Exact-record navigation retains the active company scope");
    await fixture.Report(null);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri == null, "All reports clears the request restriction");
    await fixture.Report(-1);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri is { Count: 0 }, "An invalid ID cannot expand to all records");

    foreach (var excel in new[] { false, true })
    {
        await fixture.Export(excel, 54);
        Check(fixture.Handler.LastReportFilter!.KayitIdleri!.SequenceEqual([54]),
            (excel ? "Excel" : "PDF") + " export retains the focused request");
        fixture.Http.Request.QueryString = new QueryString("?ids=55");
        await fixture.Export(excel, 54, [55]);
        Check(fixture.Handler.LastReportFilter!.KayitIdleri is { Count: 0 },
            "Selected export IDs cannot widen the focused report");
        fixture.Http.Request.QueryString = QueryString.Empty;
    }
}

using (var fixture = new NavigationFixture("Personel"))
{
    fixture.Handler.ReportResult.Kayitlar.Add(new() { Id = 54 });
    var report = (ViewResult)await fixture.Report(54, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] is 54,
        "The automatic modal targets a returned, scoped report record");
    Check(fixture.Handler.LastReportFilter!.KayitIdleri == null
        && fixture.Handler.LastReportFilter.DetayTalepId == 54
        && fixture.Handler.LastReportFilter.SirketId == 7 && report.ViewData["TalepId"] == null,
        "Legacy detail links locate the record without hiding other company records");
    fixture.Handler.ReportResult.Kayitlar.Add(new() { Id = 55 });
    report = (ViewResult)await fixture.Report(null, detailId: 54);
    Check(report.ViewData["OtomatikDetayTalepId"] is 54 && report.ViewData["TalepId"] == null
        && fixture.Handler.LastReportFilter!.KayitIdleri == null && fixture.Handler.LastReportFilter.DetayTalepId == 54
        && ((YkcRaporSonuc)report.Model!).Kayitlar.Count == 2,
        "Report detail navigation opens the target and retains neighbouring records");
    report = (ViewResult)await fixture.Report(54);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "Normal report navigation does not reopen the modal");
    report = (ViewResult)await fixture.Report(56, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "A record absent from the scoped result cannot open automatically");
    report = (ViewResult)await fixture.Report(null, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "The automatic modal requires an explicit request ID");
    report = (ViewResult)await fixture.Report(-1, openDetail: true);
    Check(report.ViewData["OtomatikDetayTalepId"] == null,
        "An invalid request ID cannot trigger an automatic modal");
    report = (ViewResult)await fixture.Report(null, detailId: -1);
    Check(report.ViewData["OtomatikDetayTalepId"] == null && fixture.Handler.LastReportFilter!.DetayTalepId == null,
        "An invalid detail target does not trigger automatic navigation");
    report = (ViewResult)await fixture.Report(54, detailId: 54);
    Check(fixture.Handler.LastReportFilter!.KayitIdleri!.SequenceEqual([54]),
        "An explicit report filter remains separate from the detail target");
}
foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized,
    HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable })
{
    using var fixture = new NavigationFixture("Personel");
    fixture.Handler.CertificateStatus = status;
    var result = (ObjectResult)await fixture.CertificateController.Dosya(26);
    var expected = status is HttpStatusCode.InternalServerError ? 503 : (int)status;
    var message = result.Value as string ?? "";
    Check(result.StatusCode == expected, "Certificate download preserves the correct status for " + status);
    Check(status switch
    {
        HttpStatusCode.NotFound => message.Contains("dosya") && !message.Contains("Veri servisine"),
        HttpStatusCode.Forbidden => message.Contains("yetkiniz"),
        HttpStatusCode.Unauthorized => message.Contains("Oturumunuzun"),
        _ => message.Contains("Veri servisine")
    }, "Certificate download distinguishes the reason for " + status);
}
using (var fixture = new NavigationFixture("Personel"))
{
    var result = (FileContentResult)await fixture.CertificateController.Dosya(26);
    Check(result.FileContents.SequenceEqual(new byte[] { 37, 80, 68, 70 }) && result.ContentType == "application/pdf"
        && fixture.Http.Response.Headers.CacheControl == "private, no-store",
        "A successful certificate download retains the original bytes and private caching");
    fixture.Handler.CertificateUnavailable = true;
    var unavailable = (ObjectResult)await fixture.CertificateController.Dosya(26);
    Check(unavailable.StatusCode == 503 && ((string)unavailable.Value!).Contains("Veri servisine"),
        "A network failure remains a service-unavailable response, not a missing file");
}
foreach (var (role, home) in new[]
{
    ("Personel", "/personel-panel"), ("SirketAdmin", "/AdminPanel"),
    ("GenelSistemAdmin", "/AdminPanel"), ("YetkiliServis", "/ys-panel"), ("SertifikaliFirma", "/ykc")
})
{
    using var fixture = new NavigationFixture(role, reportPermission: false);
    foreach (var previousPage in new[] { "/personel-panel/raporlar", "/ykc/detay/54", "https://example.invalid/" })
    {
        var redirect = (RedirectResult)await fixture.CompanyController.SirketSec(8, previousPage);
        Check(redirect.Url == home && fixture.Http.Session.GetInt32("AktifSirketId:fixture-user") == 8,
            role + ": company switch starts at its home, not an old scoped record or forbidden report");
    }
    var rejected = (RedirectToActionResult)await fixture.CompanyController.SirketSec(99, "/personel-panel/raporlar");
    Check(rejected.ActionName == nameof(PanelSirketController.SirketSec)
        && fixture.Http.Session.GetInt32("AktifSirketId:fixture-user") == 8,
        role + ": an unauthorized company cannot replace the current scope");
}
Check(YkcDurumSunumu.Etiket(YkcDurumDegerleri.TalepAlindi) == "Talep Alındı"
    && YkcDurumSunumu.Etiket(YkcDurumDegerleri.AtamaBekliyor) == "İnceleniyor",
    "Web status labels retain the shared report wording");
foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "YetkiliServis" })
{
    using var fixture = new NavigationFixture(role);
    var start = new DateTime(2026, 9, 1);
    var end = new DateTime(2026, 9, 30);
    var controller = role == "YetkiliServis" ? (Controller)fixture.ServicePanelController : fixture.AdminPanelController;
    foreach (var excel in new[] { false, true })
    {
        fixture.Handler.CommissioningStatus = HttpStatusCode.BadRequest;
        fixture.Handler.CommissioningBody = "{\"basarili\":false,\"mesaj\":\"Tek dosyada en fazla 5000 kayıt dışa aktarılabilir.\"}";
        var before = fixture.Handler.CommissioningCalls;
        var rejected = (RedirectToActionResult)await fixture.CommissioningExport(role == "YetkiliServis", excel, start, end);
        Check(rejected.ActionName == "Raporlar" && (DateTime)rejected.RouteValues!["bas"]! == start
            && (DateTime)rejected.RouteValues["bit"]! == end
            && (role == "YetkiliServis" || (int)rejected.RouteValues["sirketId"]! == 7),
            $"{role}, Excel={excel}: failed downloads return to the same report date and company filters");
        Check(((string?)controller.TempData["Hata"])?.Contains("5000") == true
            && fixture.Handler.CommissioningCalls == before + 1,
            $"{role}, Excel={excel}: validation messages reach the user without outage text or retries");
        fixture.Handler.CommissioningBody = "<html>internal diagnostic text</html>";
        await fixture.CommissioningExport(role == "YetkiliServis", excel, start, end);
        Check((string?)controller.TempData["Hata"] == "Rapor oluşturulamadı. Tarih aralığını ve kayıt seçimini kontrol edin.",
            $"{role}, Excel={excel}: malformed validation payloads use a safe user-facing message");
        fixture.Handler.CommissioningStatus = HttpStatusCode.OK;
        var success = (FileContentResult)await fixture.CommissioningExport(role == "YetkiliServis", excel, start, end);
        Check(success.FileContents.SequenceEqual(new byte[] { 37, 80, 68, 70 }),
            $"{role}, Excel={excel}: successful report downloads retain their original bytes");
    }
}
using (var fixture = new NavigationFixture("SertifikaliFirma"))
{
    var input = new YkcCihazKarsilastirmaIstek
    {
        SorguReferansi = "fixture-reference", TesisatNo = "00123", SozlesmeNo = "00456",
        YeniCihazTipi = "Kombi", YeniMarka = "E.C.A", YeniBacaTipi = "Hermetik", YeniKapasite = "25000"
    };
    var result = (JsonResult)await fixture.Controller.CihazKarsilastir(input);
    Check(result.Value is YkcCihazKarsilastirmaSonuc { Basarili: true, Uyarilar.Count: 1 }
        && fixture.Handler.LastComparison?.SorguReferansi == input.SorguReferansi
        && fixture.Handler.LastComparison.TesisatNo == "00123"
        && fixture.Handler.LastComparison.YeniKapasite == "25000",
        "Firm comparison forwards the authorized source reference and new fields without changing identifiers");
    fixture.Handler.ComparisonUnavailable = true;
    Check(await fixture.Controller.CihazKarsilastir(input) is ObjectResult
        { StatusCode: 503, Value: YkcCihazKarsilastirmaSonuc { Basarili: false } },
        "Comparison outage is reported without marking the device as matching");
    fixture.Controller.ModelState.AddModelError("YeniMarka", "Too long");
    var previousCalls = fixture.Handler.ComparisonCalls;
    Check(await fixture.Controller.CihazKarsilastir(input) is BadRequestResult
        && fixture.Handler.ComparisonCalls == previousCalls, "Invalid comparison fields never reach the API");
}
using (var fixture = new NavigationFixture("Personel"))
{
    Check(await fixture.Controller.CihazKarsilastir(new()) is StatusCodeResult { StatusCode: 403 }
        && fixture.Handler.ComparisonCalls == 0, "Read permission does not authorize creating-device comparison");
}
Check(typeof(YkcController).GetMethod(nameof(YkcController.CihazKarsilastir))!
    .IsDefined(typeof(ValidateAntiForgeryTokenAttribute), inherit: true),
    "New comparison endpoint requires an antiforgery token");
Console.WriteLine($"{passed} navigation checks passed. No application data changed.");

sealed class NavigationFixture : IDisposable
{
    private readonly HttpClient client;
    public FixtureHandler Handler { get; }
    public DefaultHttpContext Http { get; }
    public YkcController Controller { get; }
    public YetkiBelgesiController CertificateController { get; }
    public PanelSirketController CompanyController { get; }
    public YetkiliServisPanelController ServicePanelController { get; }
    public AdminPanelController AdminPanelController { get; }
    public YkcRandevuOzetiViewComponent Calendar { get; }

    public NavigationFixture(string role, bool reportPermission = true, bool signaturePermission = true)
    {
        Handler = new FixtureHandler(role);
        client = new HttpClient(Handler) { BaseAddress = new Uri("https://fixture.invalid/") };
        Http = new DefaultHttpContext
        {
            Session = new MemorySession(),
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "fixture-user"), new Claim(ClaimTypes.Role, role)], "Fixture"))
        };
        Http.Session.SetString("API.UserId", "fixture-user");
        Http.Session.SetString("API.AccessToken", "fixture-token");
        Http.Session.SetString("API.Expires", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        var accessor = new HttpContextAccessor { HttpContext = Http };
        var session = new ApiKullaniciOturumu(new AuthApiClient(client), accessor);
        var options = Options.Create(new ApiIntegrationOptions { Enabled = true });
        var tokens = new ApiJwtTokenService(accessor);
        CertificateController = new YetkiBelgesiController(
            new YetkiBelgesiApiClient(client, options, tokens, NullLogger<YetkiBelgesiApiClient>.Instance), session, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http }
        };
        var companies = new PanelKapsamApiClient(client, options, tokens, NullLogger<PanelKapsamApiClient>.Instance);
        ServicePanelController = new YetkiliServisPanelController(session,
            new YetkiliServisPanelApiClient(client, options, tokens, NullLogger<YetkiliServisPanelApiClient>.Instance), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        AdminPanelController = new AdminPanelController(session, null!,
            new AktifSirketService(accessor, session, companies),
            new AdminDashboardApiClient(client, options, tokens, NullLogger<AdminDashboardApiClient>.Instance),
            new AdminKullaniciApiClient(client, options, tokens, NullLogger<AdminKullaniciApiClient>.Instance), null!, null!,
            new AdminSubeApiClient(client, options, tokens, NullLogger<AdminSubeApiClient>.Instance),
            new AdminRaporApiClient(client, options, tokens, NullLogger<AdminRaporApiClient>.Instance), null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        CompanyController = new PanelSirketController(session, new AktifSirketService(accessor, session, companies))
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        var api = new YkcApiClient(client, options, tokens, NullLogger<YkcApiClient>.Instance,
            new AktifSirketService(accessor, session, companies));
        Calendar = new YkcRandevuOzetiViewComponent(session, companies, api,
            new AktifSirketService(accessor, session, companies), NullLogger<YkcRandevuOzetiViewComponent>.Instance)
        {
            ViewComponentContext = new ViewComponentContext { ViewContext = new ViewContext { HttpContext = Http } }
        };
        Controller = new YkcController(session, api, NullLogger<YkcController>.Instance, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
            TempData = new TempDataDictionary(Http, new MemoryTempData())
        };
        Controller.ViewBag.YkcYetkileri = new YkcYetkiOzeti
        {
            TalepleriGorebilir = true, AtamaYapabilir = true, TalepOlusturabilir = role == "SertifikaliFirma",
            Fr265ImzaIslemiYapabilir = signaturePermission, RaporlariGorebilir = reportPermission
        };
    }

    public Task<IActionResult> Report(int? id, bool openDetail = false, int? detailId = null) => Controller.Raporlar(
        null, null, null, null, null, null, null, null, null, null, null, talepId: id, detayAc: openDetail, detayTalepId: detailId);

    public Task<IActionResult> Export(bool excel, int id, List<int>? ids = null) => excel
        ? Controller.RaporExcel(null, null, null, null, null, null, null, null, null, null, null, ids, id)
        : Controller.RaporPdf(null, null, null, null, null, null, null, null, null, null, null, ids, id);

    public Task<IActionResult> CommissioningExport(bool service, bool excel, DateTime start, DateTime end) => service
        ? excel ? ServicePanelController.RaporlarExcel(start, end, [1]) : ServicePanelController.RaporlarPdf(start, end, [1])
        : excel ? AdminPanelController.RaporlarExcel(start, end, [1], 7) : AdminPanelController.RaporlarPdf(start, end, [1], 7);

    public void Dispose() => client.Dispose();
}

sealed class FixtureHandler(string role) : HttpMessageHandler
{
    public AdminYetkiListeSonuc PermissionList { get; } = new();
    public int PermissionListCalls { get; private set; }
    public int? PermissionScope { get; private set; }
    public HashSet<int> CalendarCompanies { get; } = [];
    public List<YkcTakvimFiltre> CalendarFilters { get; } = [];
    public int CalendarPermissionCalls { get; private set; }
    public bool CalendarPermissionUnavailable { get; set; }
    public int BranchCalls { get; private set; }
    public bool BranchAvailable { get; set; } = true;
    public string LastBranchPath { get; private set; } = "";
    public JsonElement LastBranchPayload { get; private set; }
    public bool Success { get; set; } = true;
    public HttpStatusCode CertificateStatus { get; set; } = HttpStatusCode.OK;
    public bool CertificateUnavailable { get; set; }
    public HttpStatusCode CommissioningStatus { get; set; } = HttpStatusCode.OK;
    public string CommissioningBody { get; set; } = "{}";
    public int CommissioningCalls { get; private set; }
    public int OperationCalls { get; private set; }
    public int ComparisonCalls { get; private set; }
    public bool ComparisonUnavailable { get; set; }
    public YkcCihazKarsilastirmaIstek? LastComparison { get; private set; }
    public YkcRaporSonuc ReportResult { get; } = new();
    public YkcTalepListeFiltre? LastReportFilter { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        object result;
        if (path == "/api/admin-panel/dashboard")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { OnayBekleyen = 0 }) };
        if (path == "/api/admin-panel/yetkiler/liste")
        {
            PermissionListCalls++;
            var scope = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var company = scope.GetProperty("sirketId");
            PermissionScope = company.ValueKind == JsonValueKind.Null ? null : company.GetInt32();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(PermissionList) };
        }
        if (path == "/api/admin-panel/yetkiler/guncelle")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { Basarili = true }) };
        if (path == "/api/panel-kapsam/ykc-yetkileri")
        {
            CalendarPermissionCalls++;
            if (CalendarPermissionUnavailable) throw new HttpRequestException("Fixture permissions unavailable");
            var scope = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var company = scope.GetProperty("aktifSirketId").GetInt32();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new YkcYetkiOzeti
            {
                TalepleriGorebilir = CalendarCompanies.Contains(company)
            }) };
        }
        if (path == "/api/ykc/takvim")
        {
            var filter = (await request.Content!.ReadFromJsonAsync<YkcTakvimFiltre>(cancellationToken))!;
            CalendarFilters.Add(filter);
            if (!CalendarCompanies.Contains(filter.AktifSirketId ?? 0))
                throw new InvalidOperationException("Calendar records requested without company permission");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new YkcTakvimSonuc
            {
                Filtre = filter, Toplam = 1,
                Kayitlar = [new YkcTakvimKayit { Id = 54, Musteri = "Fixture subscriber", Tarih = filter.Baslangic }],
                Gunler = [new YkcTakvimGunOzeti { Tarih = filter.Baslangic, Toplam = 1 }]
            }) };
        }
        if (path == "/api/admin-panel/kullanicilar/yonetim-yetkisi")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { YetkiliMi = role is "GenelSistemAdmin" or "SirketAdmin" }) };
        if (path.StartsWith("/api/admin-panel/subeler/", StringComparison.Ordinal))
        {
            BranchCalls++;
            LastBranchPath = path;
            LastBranchPayload = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                Basarili = BranchAvailable,
                Sube = BranchAvailable ? new { Id = 8, FirmaId = 10, SubeAdi = "Fixture branch", AktifMi = true } : null,
                Firmalar = new[] { new { Id = 10, FirmaAdi = "Fixture firm" } }
            }) };
        }
        if (path == "/api/ys-panel/profil")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                Id = 10, FirmaAdi = "Fixture firm", Subeler = new[] { new { Id = 8, FirmaId = 10, SubeAdi = "Fixture branch", AktifMi = true } }
            }) };
        if (path == "/api/ys-panel/bildirimler")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { Bildirimler = Array.Empty<string>(), BildirimSayisi = 0 }) };
        if (path.StartsWith("/api/admin-panel/devreye-almalar/rapor/", StringComparison.Ordinal)
            || path.StartsWith("/api/ys-panel/raporlar/", StringComparison.Ordinal))
        {
            CommissioningCalls++;
            if (request.Headers.Authorization?.Parameter != "fixture-token")
                throw new InvalidOperationException("Report download must forward the authenticated token");
            return new HttpResponseMessage(CommissioningStatus)
            {
                Content = CommissioningStatus == HttpStatusCode.OK
                    ? new ByteArrayContent([37, 80, 68, 70])
                    : new StringContent(CommissioningBody, System.Text.Encoding.UTF8, "application/json")
            };
        }
        if (path == "/api/yetki-belgesi/dosya-indir")
        {
            if (CertificateUnavailable) throw new HttpRequestException("Fixture network failure");
            if (request.Headers.Authorization?.Parameter != "fixture-token")
                throw new InvalidOperationException("Certificate download must forward the authenticated token");
            var response = new HttpResponseMessage(CertificateStatus) { Content = new ByteArrayContent([37, 80, 68, 70]) };
            response.Content.Headers.ContentType = new("application/pdf");
            return response;
        }
        if (path == "/api/auth/me")
            result = new OturumSonucu { Basarili = true, Kullanici = new OturumKullaniciDto(
                "fixture-user", "fixture", null, "Fixture", null, role switch
                {
                    "GenelSistemAdmin" => KullaniciTipiDegerleri.GenelSistemAdmin,
                    "SirketAdmin" => KullaniciTipiDegerleri.SirketAdmin,
                    "YetkiliServis" => KullaniciTipiDegerleri.YetkiliServis,
                    "SertifikaliFirma" => KullaniciTipiDegerleri.SertifikaliFirma,
                    _ => KullaniciTipiDegerleri.Personel
                }, null, 7, [role]) };
        else if (path == "/api/panel-kapsam/sirketler")
            result = new[] { new { Id = 7, SirketAdi = "Fixture company" }, new { Id = 8, SirketAdi = "Second company" } };
        else if (path.StartsWith("/api/ykc/talepler/rapor", StringComparison.Ordinal))
        {
            LastReportFilter = await request.Content!.ReadFromJsonAsync<YkcTalepListeFiltre>(cancellationToken);
            if (path.EndsWith("/pdf", StringComparison.Ordinal) || path.EndsWith("/excel", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
            result = ReportResult;
        }
        else if (path == "/api/ykc/cihaz-karsilastir")
        {
            ComparisonCalls++;
            if (request.Headers.Authorization?.Parameter != "fixture-token")
                throw new InvalidOperationException("Comparison must use the authenticated API token");
            if (ComparisonUnavailable) throw new HttpRequestException("Fixture comparison unavailable");
            LastComparison = await request.Content!.ReadFromJsonAsync<YkcCihazKarsilastirmaIstek>(cancellationToken);
            result = new YkcCihazKarsilastirmaSonuc { Basarili = true, Uyarilar = ["Marka proje kaydıyla farklı."] };
        }
        else if (path is "/api/ykc/talepler/durum-guncelle" or "/api/ykc/talepler/imza-durum-sorgula")
        {
            OperationCalls++;
            result = new YkcIslemSonuc { Basarili = Success, Mesaj = "Fixture result", Id = 54 };
        }
        else throw new InvalidOperationException("Unexpected fixture route: " + path);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
    }
}

sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

sealed class MemorySession : ISession
{
    private readonly Dictionary<string, byte[]> data = [];
    public bool IsAvailable => true;
    public string Id => "fixture-session";
    public IEnumerable<string> Keys => data.Keys;
    public void Clear() => data.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => data.Remove(key);
    public void Set(string key, byte[] value) => data[key] = value;
    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => data.TryGetValue(key, out value);
}
