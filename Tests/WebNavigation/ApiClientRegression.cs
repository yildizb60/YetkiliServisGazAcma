using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

internal static class ApiClientRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + name);
            passed++;
            Console.WriteLine("PASS: " + name);
        }
        var user = new AppKullanici { Id = "fixture-user", UserName = "fixture" };
        using (var fixture = new NavigationFixture("GenelSistemAdmin"))
        {
            await fixture.Brands.TumunuGetirAsync(user, "Va", false);
            Check(fixture.Handler.LastBrandPayload.GetProperty("q").GetString() == "Va"
                && !fixture.Handler.LastBrandPayload.GetProperty("aktifMi").GetBoolean()
                && fixture.Handler.LastBrandPayload.GetProperty("tumunuGetir").GetBoolean(),
                "Brand filtering is sent to the API as a typed request");
            await fixture.Brands.AktifleriGetirAsync();
            Check(!fixture.Handler.LastBrandPayload.GetProperty("tumunuGetir").GetBoolean(),
                "Anonymous catalogue request cannot ask for inactive brands");
            fixture.Handler.BrandBody = "{\"basarili\":true,\"mesaj\":\"Saved\",\"id\":12}";
            Check((await fixture.Brands.EkleAsync(user, new() { MarkaAdi = "Fixture" })) is { Basarili: true, Id: 12 },
                "Shared command response retains success, message and ID");
            fixture.Handler.BrandStatus = HttpStatusCode.BadRequest;
            fixture.Handler.BrandBody = "{\"basarili\":false,\"mesaj\":\"Marka zaten var.\"}";
            Check((await fixture.Brands.EkleAsync(user, new() { MarkaAdi = "Fixture" })) is { Basarili: false, Mesaj: "Marka zaten var." },
                "Brand business validation remains actionable instead of becoming an outage");
            foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError })
            {
                fixture.Handler.BrandStatus = status;
                try { await fixture.Brands.GetirAsync(user, 12); throw new InvalidOperationException("Expected API error"); }
                catch (ApiIntegrationException ex) { Check(ex.StatusCode == (int)status, "Brand lookup preserves HTTP status " + status); }
            }
            fixture.Handler.BrandStatus = HttpStatusCode.OK;
            fixture.Handler.BrandBody = "<html>internal error</html>";
            try { await fixture.Brands.TumunuGetirAsync(user); throw new InvalidOperationException("Expected malformed response"); }
            catch (ApiIntegrationException ex) { Check(ex.StatusCode == 502 && !ex.Message.Contains("internal error"), "Malformed API body never leaks diagnostic HTML"); }
        }
        foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "Personel" })
        {
            using var fixture = new NavigationFixture(role);
            var start = new DateTime(2026, 9, 1);
            var end = new DateTime(2026, 9, 30);
            await fixture.Controller.Raporlar(null, null, null, null, null, null, null, null, null, start, end, sirketId: 8);
            Check(fixture.Handler.LastReportFilter!.SirketId == (role == "GenelSistemAdmin" ? 8 : 7)
                && fixture.Handler.LastReportFilter.BaslangicTarihi == start && fixture.Handler.LastReportFilter.BitisTarihi == end,
                role + ": company/date report context survives navigation without widening personnel scope");
            await fixture.AdminPanelController.DevreyeAlmalar(null, null, null, start, end, 8);
            Check(fixture.Handler.LastCommissioningFilter.GetProperty("sirketId").GetInt32() == (role == "GenelSistemAdmin" ? 8 : 7),
                role + ": commissioning list honors verified report company");
        }
        using (var fixture = new NavigationFixture("Personel"))
        {
            foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound,
                HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError })
            {
                fixture.Handler.FileStatus = status;
                fixture.Handler.FileBody = "{\"mesaj\":\"Tarih aralığını kontrol edin.\"}";
                try { await fixture.YkcClient.RaporPdfAsync(user, new()); throw new InvalidOperationException("Expected file error"); }
                catch (ApiIntegrationException ex)
                {
                    Check(ex.StatusCode == (int)status && (status != HttpStatusCode.BadRequest || ex.Message == "Tarih aralığını kontrol edin."),
                        "File request distinguishes " + status);
                }
            }
            foreach (var failure in new[] { "network", "timeout" })
            {
                fixture.Handler.TransportFailure = failure;
                try { await fixture.YkcClient.RaporExcelAsync(user, new()); throw new InvalidOperationException("Expected transport error"); }
                catch (ApiIntegrationException ex) { Check(ex.StatusCode == 503, "File transport error classified as outage: " + failure); }
            }
            fixture.Handler.TransportFailure = null;
            fixture.Handler.FileStatus = HttpStatusCode.Forbidden;
            var result = (RedirectToActionResult)await fixture.Controller.RaporPdf("123", "Firm", null, null, null, null, null, null,
                1, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), null, sirketId: 7);
            Check((string?)result.RouteValues!["tesisatNo"] == "123" && (string?)result.RouteValues["bas"] == "2026-09-01"
                && (string?)fixture.Controller.TempData["Hata"] == "Bu işlem veya dosya için erişim yetkiniz yok.",
                "Failed export returns to the same filter with the actual access error");
            var saved = (RedirectToActionResult)await fixture.Controller.DurumGuncelle(new() { TalepId = 54, Durum = YkcDurumDegerleri.AtamaBekliyor },
                geri: "/ykc/talepler?durum=1&sayfa=3");
            Check((string?)saved.RouteValues!["geri"] == "/ykc/talepler?durum=1&sayfa=3",
                "Detail actions retain the originating filtered list");
        }
        foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "YetkiliServis" })
        {
            using var fixture = new NavigationFixture(role);
            fixture.Http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            fixture.Handler.BranchAvailable = false;
            var result = role == "YetkiliServis"
                ? await fixture.ServicePanelController.SubeEkle("Branch", "City", "District", "Phone", "Address", true)
                : await fixture.AdminPanelController.SubeEkle(10, "Branch", "City", "District", "Phone", "Address", true);
            Check(result is JsonResult json && JsonSerializer.SerializeToElement(json.Value).GetProperty("basarili").GetBoolean() == false,
                role + ": rejected drawer save returns JSON without redirecting away from entered data");
        }
        Console.WriteLine($"{passed} API client regression checks passed.");
    }
}
