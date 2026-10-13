using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;
using YetkiliServisGazAcma.Entities;

namespace Tests.WebNavigation;

internal static class ExportContractRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        var endpoints = new (Type Controller, string Action)[]
        {
            (typeof(AdminPanelController), nameof(AdminPanelController.RaporlarPdf)),
            (typeof(AdminPanelController), nameof(AdminPanelController.RaporlarExcel)),
            (typeof(PersonelPanelController), nameof(PersonelPanelController.DevreyeAlmalarPdf)),
            (typeof(PersonelPanelController), nameof(PersonelPanelController.DevreyeAlmalarExcel)),
            (typeof(YetkiliServisPanelController), nameof(YetkiliServisPanelController.RaporlarPdf)),
            (typeof(YetkiliServisPanelController), nameof(YetkiliServisPanelController.RaporlarExcel)),
            (typeof(YkcController), nameof(YkcController.RaporPdf)),
            (typeof(YkcController), nameof(YkcController.RaporExcel))
        };
        foreach (var (controller, action) in endpoints)
        {
            var method = controller.GetMethod(action)!;
            Check(method.IsDefined(typeof(HttpGetAttribute)) && method.IsDefined(typeof(HttpPostAttribute))
                && method.IsDefined(typeof(AutoValidateAntiforgeryTokenAttribute))
                && method.GetCustomAttribute<RequestFormLimitsAttribute>()!.ValueCountLimit >= 5001 + 30
                && method.GetParameters().Single(p => p.Name == "ids").GetCustomAttribute<FromQueryAttribute>() == null,
                controller.Name + "." + action + ": GET compatibility, protected POST and body ID binding");
        }
        foreach (var action in new[] { nameof(AdminPanelController.DevreyeAlmaPdf), nameof(AdminPanelController.DevreyeAlmaExcel) })
            Check(typeof(AdminPanelController).GetMethod(action)!.IsDefined(typeof(HttpGetAttribute))
                && !typeof(AdminPanelController).GetMethod(action)!.IsDefined(typeof(HttpPostAttribute)),
                "Per-row commissioning download stays GET: " + action);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddControllersWithViews();
        using (var provider = services.BuildServiceProvider())
        {
            var attribute = new AutoValidateAntiforgeryTokenAttribute();
            var filter = (IAsyncAuthorizationFilter)attribute.CreateInstance(provider);
            async Task<AuthorizationFilterContext> Validate(DefaultHttpContext http)
            {
                http.RequestServices = provider;
                var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [filter]);
                await filter.OnAuthorizationAsync(context);
                return context;
            }
            var get = new DefaultHttpContext();
            get.Request.Method = "GET";
            Check((await Validate(get)).Result == null, "Legacy GET exports do not require an antiforgery token");
            var missing = new DefaultHttpContext();
            missing.Request.Method = "POST";
            missing.Request.ContentType = "application/x-www-form-urlencoded";
            missing.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["ids"] = "42" });
            Check((await Validate(missing)).Result is AntiforgeryValidationFailedResult,
                "Bulk POST without a valid antiforgery token is rejected before the API export");
            var tokenContext = new DefaultHttpContext { RequestServices = provider };
            var tokens = provider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(tokenContext);
            var cookie = Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(tokenContext.Response.Headers.SetCookie.ToString());
            var post = new DefaultHttpContext();
            post.Request.Method = "POST";
            post.Request.Headers.Cookie = cookie.Name + "=" + cookie.Value;
            post.Request.ContentType = "application/x-www-form-urlencoded";
            post.Request.Form = new FormCollection(new Dictionary<string, StringValues>
            {
                [tokens.FormFieldName] = tokens.RequestToken, ["ids"] = "42"
            });
            Check((await Validate(post)).Result == null, "Token emitted by the hidden export form authorizes POST body submission");
        }

        // Exercise the real collection binder with the form shape emitted by the shared JS.
        foreach (var count in new[] { 1000, 5000, 5001 })
        {
            var values = Enumerable.Range(1000000, count).Select(id => id.ToString()).ToArray();
            var http = new DefaultHttpContext();
            http.Request.ContentType = "application/x-www-form-urlencoded";
            using var content = new FormUrlEncodedContent(values.Select(id => new KeyValuePair<string, string>("ids", id)));
            http.Request.Body = new MemoryStream(await content.ReadAsByteArrayAsync());
            var limits = typeof(AdminPanelController).GetMethod(nameof(AdminPanelController.RaporlarPdf))!
                .GetCustomAttribute<RequestFormLimitsAttribute>()!;
            var form = await http.Request.ReadFormAsync(new Microsoft.AspNetCore.Http.Features.FormOptions
            {
                ValueCountLimit = limits.ValueCountLimit
            });
            var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(List<int>));
            var binding = DefaultModelBindingContext.CreateBindingContext(
                new ActionContext(http, new RouteData(), new ActionDescriptor()),
                new FormValueProvider(BindingSource.Form, form, System.Globalization.CultureInfo.InvariantCulture),
                metadata, bindingInfo: null, modelName: "ids");
            var binder = new CollectionModelBinder<int>(
                new SimpleTypeModelBinder(typeof(int), NullLoggerFactory.Instance), NullLoggerFactory.Instance);
            await binder.BindModelAsync(binding);
            Check(binding.Result.Model is List<int> ids && ids.SequenceEqual(Enumerable.Range(1000000, count))
                && binding.ModelState.ErrorCount == 0,
                count + " repeated form IDs bind intact without raising the global MVC collection limit");
        }

        foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin", "Personel", "YetkiliServis" })
        foreach (var excel in new[] { false, true })
        {
            using var fixture = new NavigationFixture(role);
            fixture.Handler.PersonnelPermissions[7] = [YetkiTipleri.RAPOR_GOR];
            fixture.Http.Request.Method = "POST";
            var ids = Enumerable.Range(1000000, 5000).ToList();
            var result = role switch
            {
                "YetkiliServis" => excel ? await fixture.ServicePanelController.RaporlarExcel(null, null, ids)
                    : await fixture.ServicePanelController.RaporlarPdf(null, null, ids),
                "Personel" => excel ? await fixture.PersonnelController.DevreyeAlmalarExcel(ids)
                    : await fixture.PersonnelController.DevreyeAlmalarPdf(ids),
                _ => excel ? await fixture.AdminPanelController.RaporlarExcel(null, null, ids, 8)
                    : await fixture.AdminPanelController.RaporlarPdf(null, null, ids, 8)
            };
            var payload = fixture.Handler.LastCommissioningExportFilter;
            Check(result is FileContentResult && fixture.Handler.CommissioningCalls == 1
                && payload.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(ids)
                && (role == "YetkiliServis" || payload.GetProperty("sirketId").GetInt32() == (role == "GenelSistemAdmin" ? 8 : 7)),
                $"{role}, Excel={excel}: all 5000 IDs reach the one authenticated API export path with verified scope");
        }

        foreach (var role in new[] { "GenelSistemAdmin", "SirketAdmin" })
        foreach (int? company in new int?[] { null, 8 })
        foreach (var excel in new[] { false, true })
        {
            using var fixture = new NavigationFixture(role);
            fixture.Http.Session.SetInt32("AktifSirketId:fixture-user", 7);
            var result = excel ? await fixture.AdminPanelController.DevreyeAlmaExcel(42, company)
                : await fixture.AdminPanelController.DevreyeAlmaPdf(42, company);
            var payload = fixture.Handler.LastCommissioningExportFilter;
            var expected = role == "GenelSistemAdmin" ? company : 7;
            Check(result is FileContentResult && payload.GetProperty("id").GetInt32() == 42
                && (expected.HasValue ? payload.GetProperty("sirketId").GetInt32() == expected
                    : payload.GetProperty("sirketId").ValueKind == System.Text.Json.JsonValueKind.Null),
                $"{role}, company={company}, Excel={excel}: per-row scope matches explicit or all-company list, not session company");
        }

        foreach (var excel in new[] { false, true })
        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.ServiceUnavailable })
        {
            using var fixture = new NavigationFixture("GenelSistemAdmin");
            fixture.Http.Session.SetInt32("AktifSirketId:fixture-user", 7);
            await fixture.Controller.Talepler(null, null, null, null, null, null, null, null, null, null, null, null, null);
            var effective = fixture.Handler.LastRequestFilter!;
            Check(effective.SirketId == 7, "Request list resolves its active-company scope before exporting");
            fixture.Handler.FileStatus = status;
            fixture.Handler.FileBody = "{\"mesaj\":\"Fixture export rejected\"}";
            fixture.Http.Request.Method = "POST";
            fixture.Http.Request.ContentType = "application/x-www-form-urlencoded";
            fixture.Http.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["ids"] = "54" });
            var result = (RedirectToActionResult)(excel
                ? await fixture.Controller.RaporExcel("000123", "Firm", "City", "District", "Region", null, "Brand", null, 2,
                    new(2026, 9, 1), new(2026, 9, 30), [54], sirketId: effective.SirketId,
                    q: "000943", sirala: "abone-artan", musteriAdi: "Customer", sozlesmeNo: "000943", cihazTipi: "Kombi",
                    sayfa: 3, kaynak: "talepler", aboneNo: "00077", kontrolNo: 2, bekleyenIs: "randevu",
                    randevuBas: new(2026, 10, 1), randevuBit: new(2026, 10, 31))
                : await fixture.Controller.RaporPdf("000123", "Firm", "City", "District", "Region", null, "Brand", null, 2,
                    new(2026, 9, 1), new(2026, 9, 30), [54], sirketId: effective.SirketId,
                    q: "000943", sirala: "abone-artan", musteriAdi: "Customer", sozlesmeNo: "000943", cihazTipi: "Kombi",
                    sayfa: 3, kaynak: "talepler", aboneNo: "00077", kontrolNo: 2, bekleyenIs: "randevu",
                    randevuBas: new(2026, 10, 1), randevuBit: new(2026, 10, 31)));
            var returned = result.RouteValues!;
            Check(result.ActionName == nameof(YkcController.Talepler)
                && (int)returned["sirketId"]! == 7 && (int)returned["sayfa"]! == 3
                && (string)returned["aboneNo"]! == "00077" && (int)returned["kontrolNo"]! == 2
                && (string)returned["bekleyenIs"]! == "randevu" && (string)returned["sozlesmeNo"]! == "000943"
                && (string)returned["sirala"]! == "abone-artan" && (string)returned["randevuBas"]! == "2026-10-01"
                && fixture.Handler.LastReportFilter!.SirketId == 7 && fixture.Handler.LastReportFilter.KayitIdleri!.SequenceEqual([54]),
                $"YKC {status}, Excel={excel}: POST selection retains effective scope, source, page and request-only filters");
        }
        using (var fixture = new NavigationFixture("GenelSistemAdmin"))
        {
            fixture.Http.Session.SetInt32("AktifSirketId:fixture-user", 7);
            await fixture.Report(null);
            Check(fixture.Handler.LastReportFilter!.SirketId == null, "Intentional all-company report ignores selected session company");
            await fixture.Controller.RaporPdf(null, null, null, null, null, null, null, null, null, null, null, null);
            Check(fixture.Handler.LastReportFilter!.SirketId == null && fixture.Handler.LastReportFilter.KayitIdleri == null,
                "Legacy unselected GET report remains all-company and filtered-all");
            fixture.Http.Request.ContentType = "application/x-www-form-urlencoded";
            fixture.Http.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["ids"] = "" });
            await fixture.Controller.RaporPdf(null, null, null, null, null, null, null, null, null, null, null, null);
            Check(fixture.Handler.LastReportFilter!.KayitIdleri is { Count: 0 }, "Explicit empty POST IDs cannot expand to all records");
        }
        Console.WriteLine($"{passed} export contract regression checks passed.");
    }
}
