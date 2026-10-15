using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using YetkiliServisGazAcma.Models.ViewModels;
using YetkiliServisGazAcma.ViewComponents;

namespace Tests.WebNavigation;

internal static class PanelChromeRegression
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var (source, expected) in new (string? Source, string Expected)[]
                {
                    ("ALİ KOÇ", "Ali Koç"), ("IŞIL YILDIRIM", "Işıl Yıldırım"),
                    ("iPEK ışIL", "İpek Işıl"), ("  AYŞE DEMİR  ", "Ayşe Demir"),
                    ("MEHMET ALİ ÖZTÜRK", "Mehmet Ali Öztürk"), ("Ali Koç", "Ali Koç"),
                    (null, "-"), ("  ", "-")
                })
                    Check(AboneAdiSunumu.Bicimlendir(source) == expected,
                        culture + ": subscriber display uses Turkish title casing and preserves missing-name fallback: " + source);
            }
            Check(AboneAdiSunumu.Bicimlendir(null, "Abone adı kayıtlı değil") == "Abone adı kayıtlı değil",
                "Subscriber display preserves the commissioning missing-name caption");
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }

        static string Attribute(string tag, string name)
            => WebUtility.HtmlDecode(Regex.Match(tag, $"(?:^|\\s){Regex.Escape(name)}=\"([^\"]*)\"", RegexOptions.None, RegexTimeout).Groups[1].Value);

        static bool HasAttribute(string tag, string name)
            => Regex.IsMatch(tag, $"\\s{Regex.Escape(name)}(?=\\s|=|>)", RegexOptions.None, RegexTimeout);

        static bool HasClass(string tag, string cssClass)
            => Attribute(tag, "class")
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(cssClass);

        static string[] Tags(string html, string tag)
            => Regex.Matches(html, $"<{Regex.Escape(tag)}\\b[^>]*>", RegexOptions.None, RegexTimeout).Cast<Match>().Select(match => match.Value).ToArray();

        static string Table(string html, string cssClass)
            => Regex.Matches(html, @"<table\b[^>]*>[\s\S]*?</table>", RegexOptions.None, RegexTimeout).Cast<Match>()
                .Single(match => HasClass(Tags(match.Value, "table")[0], cssClass)).Value;

        void CheckDirectory(string html, string storageKey, string tableClass, int[] recordIds, string name)
        {
            var tags = Regex.Matches(html, @"<[a-zA-Z][\w-]*\b[^>]*>", RegexOptions.None, RegexTimeout).Cast<Match>().Select(match => match.Value).ToArray();
            var roots = tags.Where(tag => HasAttribute(tag, "data-history-directory")).ToArray();
            Check(roots.Length == 1 && Attribute(roots[0], "data-storage-key") == storageKey
                && HasClass(roots[0], "df-record-directory")
                && Tags(html, "table").Count(tag => HasClass(tag, tableClass)) == 1
                && Attribute(roots[0], "data-directory-form") == "#ykcFilters"
                && Attribute(roots[0], "data-directory-panel") == "#ykcFiltersPanel"
                && HasAttribute(roots[0], "data-directory-export-all"),
                name + ": shared directory retains its table, canonical form, panel and isolated view-storage selectors");
            Check(Tags(html, "form").Count(tag => Attribute(tag, "id") == "ykcFilters" && Attribute(tag, "method") == "get") == 1
                && Tags(html, "details").Count(tag => Attribute(tag, "id") == "ykcFiltersPanel") == 1
                && tags.Count(tag => HasAttribute(tag, "data-history-view-group")) == 1
                && Tags(html, "button").Count(tag => Attribute(tag, "data-history-view") == "list") == 1
                && Tags(html, "button").Count(tag => Attribute(tag, "data-history-view") == "cards") == 1
                && tags.Count(tag => HasAttribute(tag, "data-history-column-popover")) == 1
                && tags.Count(tag => HasAttribute(tag, "data-history-column-body")) == 1
                && Tags(html, "input").Count(tag => Attribute(tag, "name") == "q" && Attribute(tag, "form") == "ykcFilters") == 1
                && Tags(html, "select").Count(tag => Attribute(tag, "name") == "sirala" && Attribute(tag, "form") == "ykcFilters") == 1
                && Tags(html, "button").Any(tag => HasAttribute(tag, "data-history-filter-toggle") && Attribute(tag, "aria-controls") == "ykcFiltersPanel"),
                name + ": shared toolbar and column popup render once with one GET filter form");
            var records = Tags(html, "tr").Where(tag => HasAttribute(tag, "data-directory-record")).ToArray();
            Check(records.Select(tag => Attribute(tag, "data-export-id")).SequenceEqual(recordIds.Select(id => id.ToString())),
                name + ": list/cards share one DOM row and export identity per scoped record");
            if (recordIds.Length == 0) return;
            var columnFields = Tags(html, "th").Where(tag => HasAttribute(tag, "data-directory-fields")).ToArray();
            var form = Regex.Matches(html, @"<form\b[^>]*>[\s\S]*?</form>", RegexOptions.None, RegexTimeout).Cast<Match>()
                .Single(match => Attribute(Tags(match.Value, "form")[0], "id") == "ykcFilters").Value;
            var controlNames = Tags(form, "input").Concat(Tags(form, "select"))
                .Select(tag => Attribute(tag, "name")).Where(control => !string.IsNullOrWhiteSpace(control)).ToHashSet();
            Check(columnFields.Length > 0 && columnFields.All(tag => !string.IsNullOrWhiteSpace(Attribute(tag, "data-directory-column"))
                && !string.IsNullOrWhiteSpace(Attribute(tag, "data-directory-fields"))
                && Attribute(tag, "data-directory-fields").Split(' ', StringSplitOptions.RemoveEmptyEntries).All(controlNames.Contains)),
                name + ": column filter hooks reference rendered canonical controls");
        }

        void CheckSeparatedColumns(string html, string tableClass, string[] labels, int recordCount, string name)
        {
            var table = Table(html, tableClass);
            var head = Regex.Match(table, @"<thead\b[^>]*>([\s\S]*?)</thead>", RegexOptions.None, RegexTimeout).Groups[1].Value;
            var headers = Regex.Matches(head, @"<th\b[^>]*>([\s\S]*?)</th>", RegexOptions.None, RegexTimeout).Cast<Match>().ToArray();
            var separated = headers.Select((header, index) => (header, index))
                .Where(item => HasClass(item.header.Value, "ykc-column-separated")).ToArray();
            var headingLabels = separated.Select(item => WebUtility.HtmlDecode(
                Regex.Replace(item.header.Groups[1].Value, "<[^>]*>", "", RegexOptions.None, RegexTimeout)).Trim()).ToArray();
            Check(headingLabels.SequenceEqual(labels), name + ": separator hooks cover the intended column headings");
            var body = Regex.Match(table, @"<tbody\b[^>]*>([\s\S]*?)</tbody>", RegexOptions.None, RegexTimeout).Groups[1].Value;
            var rows = Regex.Matches(body, @"<tr\b[^>]*>([\s\S]*?)</tr>", RegexOptions.None, RegexTimeout).Cast<Match>().ToArray();
            Check(rows.Length == recordCount, name + ": column separation retains one row per record");
            foreach (var row in rows)
            {
                var cells = Tags(row.Groups[1].Value, "td");
                var indices = cells.Select((cell, index) => (cell, index))
                    .Where(item => HasClass(item.cell, "ykc-column-separated")).Select(item => item.index);
                Check(cells.Length == headers.Length && indices.SequenceEqual(separated.Select(item => item.index)),
                    name + ": each record has matching header/cell separator hooks");
            }
        }

        // Only MVC rendering services are registered: no API, database or running web server.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SidebarViewComponent).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(SidebarViewComponent).Assembly);
        builder.Services.AddSingleton<IUrlHelperFactory, FixtureUrlHelperFactory>();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var selector = scope.ServiceProvider.GetRequiredService<IViewComponentSelector>();
        Check(selector.SelectComponent("Sidebar")?.TypeInfo.AsType() == typeof(SidebarViewComponent)
            && selector.SelectComponent("Navbar")?.TypeInfo.AsType() == typeof(NavbarViewComponent),
            "MVC discovers both named components used by the panel layout");

        foreach (var (role, type, area, controller) in new[]
        {
            ("GenelSistemAdmin", 4, "Admin", "AdminPanel"),
            ("SirketAdmin", 3, "Admin", "AdminPanel"),
            ("Personel", 2, "Personel", "PersonelPanel"),
            ("YetkiliServis", 1, "YetkiliServis", "YetkiliServisPanel"),
            ("SertifikaliFirma", 5, "SertifikaliFirma", "Ykc")
        })
        {
            var context = Context(role, type, area, scope.ServiceProvider);
            var data = context.ViewData;
            data["GenelSistemAdminMi"] = type == 4;
            data["YkcYetkileri"] = new YkcYetkiOzeti
            {
                TalepleriGorebilir = true, RaporlariGorebilir = true, TalepOlusturabilir = type == 5
            };
            data["YetkiBelgesi"] = data["YetkiRapor"] = data["YetkiServis"] = data["YetkiMarka"] = true;
            data["OnayBekleyen"] = 3;
            data["AktifSirketler"] = new List<PanelSirketDto>
            {
                new() { Id = 7, SirketAdi = "Fixture A" }, new() { Id = 8, SirketAdi = "Fixture B" }
            };
            data["AktifSirketId"] = 8;
            data["PanelSirketAdi"] = "Fixture B";

            var sidebar = Sidebar(context);
            var navbar = Navbar(context);
            var links = sidebar.Groups.SelectMany(x => x.Links).ToList();
            Check(sidebar.HomeUrl == $"/{controller}/Index" && navbar.HomeUrl == sidebar.HomeUrl,
                role + ": sidebar and navbar return to the same role home");
            Check(navbar.ProfileUrl == $"/{controller}/Profil" && navbar.CompanyName == "Fixture B",
                role + ": profile route and selected company are preserved");
            Check(navbar.Notifications.AllCompaniesAllowed == (type == 4)
                && navbar.Notifications.Companies.Single(x => x.Selected).Id == 8,
                role + ": company switcher does not grant all-company scope");
            Check(sidebar.ServicePanel == (type == 1), role + ": original service sidebar class is preserved");
            Check(links.Any(x => x.Url == "/Marka/Index") == (type == 4),
                role + ": shared catalog administration is restricted");
            if (type is 2 or 3 or 4)
                Check(links.Single(x => x.Url == $"/{controller}/OnayBekleyenler").Badge == 3,
                    role + ": approval badge retains the API count");
            if (type == 1)
                Check(!links.Any(x => x.Url.StartsWith("/Ykc/", StringComparison.Ordinal)),
                    "Service menu does not acquire certified-firm operations");
            if (type == 5)
                Check(links.Any(x => x.Url == "/Ykc/Yeni") && !links.Any(x => x.Url.StartsWith("/AdminPanel/", StringComparison.Ordinal)),
                    "Certified firm retains its own request menu without administration");

            var sidebarHtml = await RenderAsync(scope.ServiceProvider, context,
                "/Views/Shared/Components/Sidebar/Default.cshtml", sidebar);
            var navbarHtml = await RenderAsync(scope.ServiceProvider, context,
                "/Views/Shared/Components/Navbar/Default.cshtml", navbar);
            Check(sidebarHtml.Contains("id=\"panelSidebar\"") && sidebarHtml.Contains("data-sidebar-collapse")
                && sidebarHtml.Contains("nav-item-group"), role + ": compiled sidebar retains JS hooks");
            Check(navbarHtml.Contains("id=\"profileBtn\"") && navbarHtml.Contains("id=\"notifMenu\"")
                && navbarHtml.Contains("id=\"companyMenu\""), role + ": compiled navbar retains dropdown hooks");
            Check(navbarHtml.Contains("name=\"returnUrl\"") && !navbarHtml.Contains("name=\"Model.ReturnUrl\"")
                && navbarHtml.Contains("name=\"__RequestVerificationToken\"")
                && WebUtility.HtmlDecode(navbarHtml).Contains("/ykc/raporlar?sayfa=2&durum=1"),
                role + ": company form preserves its field names, query and antiforgery token");
        }

        var personnel = Context("Personel", 2, "", scope.ServiceProvider);
        personnel.ViewData["ActiveMenu"] = "YkcRaporlar";
        personnel.ViewData["YkcYetkileri"] = new YkcYetkiOzeti { RaporlariGorebilir = true };
        personnel.ViewData["OnayBekleyen"] = 2;
        var reportMenu = Sidebar(personnel);
        Check(reportMenu.Groups.SelectMany(x => x.Links).Select(x => x.Url)
            .Order().SequenceEqual(new[] { "/PersonelPanel/Profil", "/Ykc/Raporlar" }.Order()),
            "Report-only personnel on a shared YKC route sees no admin, request or calendar menus");
        Check(reportMenu.Groups.Single(x => x.Links.Any(y => y.Url == "/Ykc/Raporlar")).Expanded,
            "The current report group stays expanded");
        Check(Navbar(personnel).Notifications.Items.Single().Path == "/PersonelPanel/OnayBekleyenler",
            "Personnel notifications retain the personnel panel on shared YKC routes");

        personnel.ViewData["YkcYetkileri"] = new YkcYetkiOzeti();
        personnel.ViewData["OnayBekleyen"] = 0;
        personnel.ViewData["PanelSirketAdi"] = "Different company";
        Check(Sidebar(personnel).Groups.SelectMany(x => x.Links).All(x => x.Url == "/PersonelPanel/Profil")
            && Navbar(personnel).Notifications.Count == 0 && Navbar(personnel).CompanyName == "Different company",
            "A different company with no permissions does not retain the previous menu or notifications");
        Check(!Navbar(personnel).Notifications.ShowCompanySelector,
            "A single-company context does not create an empty company switcher");

        personnel.ViewData["PanelTitle"] = "Cihaz Değişim İşlemleri";
        Check(Navbar(personnel).Module == null, "Breadcrumb does not repeat an identical module and page title");
        personnel.ViewData["PanelModule"] = "Explicit module";
        Check(Navbar(personnel).Module == "Explicit module", "Explicit page modules are retained");
        ((AppKullanici)personnel.ViewData["Kullanici"]!).AdSoyad = "  ilker";
        Check(Navbar(personnel).Initial == "İ", "Turkish initials remain correct");
        ((AppKullanici)personnel.ViewData["Kullanici"]!).AdSoyad = "<script>alert(1)</script>";
        var encoded = await RenderAsync(scope.ServiceProvider, personnel,
            "/Views/Shared/Components/Navbar/Default.cshtml", Navbar(personnel));
        Check(!encoded.Contains("<script>alert(1)</script>") && WebUtility.HtmlDecode(encoded).Contains("<script>alert(1)</script>"),
            "Navbar user data is HTML encoded");

        Check(selector.SelectComponent("PersonelDashboard")?.TypeInfo.AsType() == typeof(PersonelDashboardViewComponent),
            "Personnel dashboard is discovered as a presentation-only ViewComponent");
        RoleDashboardViewModel Dashboard(PersonelDashboardDto dto) =>
            (RoleDashboardViewModel)((ViewViewComponentResult)new PersonelDashboardViewComponent
            {
                ViewComponentContext = new ViewComponentContext { ViewContext = personnel }, Url = new FixtureUrlHelper(personnel)
            }.Invoke(dto)).ViewData!.Model!;
        var summary = new PersonelDashboardDto
        {
            Bugun = new DateTime(2030, 4, 5), AyBaslangici = new DateTime(2030, 4, 1), AyBitisi = new DateTime(2030, 4, 30),
            BelgeYetkisi = true, OnayBekleyen = 97
        };
        var dashboardModel = Dashboard(summary);
        Check(dashboardModel.ShowGeneralCalendar && !dashboardModel.ShowYkcCalendar
            && dashboardModel.Actions.All(x => !x.Url.Contains("/Ykc/", StringComparison.Ordinal)),
            "Certificate-only dashboard keeps the plain calendar without request links");
        Check(dashboardModel.Facts.Single().Value == "97" && dashboardModel.HeroKicker!.Contains("2030"),
            "Dashboard displays the API's count and date without recalculating either");
        summary = new PersonelDashboardDto { Bugun = summary.Bugun, YkcYetkileri = new() { RaporlariGorebilir = true } };
        dashboardModel = Dashboard(summary);
        Check(dashboardModel.ShowGeneralCalendar && dashboardModel.Actions.Single().Url == "/Ykc/Raporlar",
            "Report-only personnel get reports, not request management or the appointment calendar");
        summary.YkcYetkileri = new() { TalepleriGorebilir = true, AtamaYapabilir = true, Fr265ImzaIslemiYapabilir = true };
        summary.Ykc = new() { IncelemeBekleyen = 13, RandevuBekleyen = 7, TamamlamaBekleyen = 2 };
        dashboardModel = Dashboard(summary);
        Check(dashboardModel.ShowYkcCalendar && !dashboardModel.ShowGeneralCalendar
            && dashboardModel.QuickActions.Take(3).Select(x => x.MetricValue).SequenceEqual(["13", "7", "2"]),
            "Operational dashboard renders the three API-provided pending-work counts in their existing order");
        dashboardModel = Dashboard(new() { Bugun = summary.Bugun });
        Check(dashboardModel.Actions.Single().Url == "/PersonelPanel/Profil" && dashboardModel.ShowGeneralCalendar,
            "Empty permissions do not retain controls from a previous company");

        var firmDetail = new YkcTalepDetayDto
        {
            Id = 42, MusteriAdi = "<script>fixture</script>", Durum = YkcDurumDegerleri.AtamaBekliyor,
            Kontroller = [new() { KontrolNo = 1, Sonuc = YkcFr265KontrolSonucDegerleri.UygunDegil, Aciklama = "API failure reason" }]
        };
        firmDetail.Ekran = YkcTalepIslemKurali.EkranHazirla(firmDetail, new() { TalepleriGorebilir = true },
            false, new(), [], DateTime.Now);
        var firmHtml = await RenderAsync(scope.ServiceProvider, personnel, "/Views/Ykc/_FirmaTalepDetay.cshtml", firmDetail);
        Check(firmHtml.Contains("API failure reason") && !firmHtml.Contains("<script>fixture</script>"),
            "Compiled firm detail consumes API-provided failure reason and still HTML-encodes customer data");
        Check(firmHtml.Contains("ykc-firm-data-section") && !firmHtml.Contains("ykc-assignment-form"),
            "Firm detail retains its layout classes without internal assignment controls");

        // Render-only role fixtures do not bypass or exercise controller authorization.
        foreach (var (role, type) in new[]
        {
            ("GenelSistemAdmin", 4), ("SirketAdmin", 3), ("Personel", 2),
            ("YetkiliServis", 1), ("SertifikaliFirma", 5)
        })
        {
            var context = Context(role, type, "", scope.ServiceProvider);
            context.ViewData["Filtre"] = new YkcTalepListeFiltre { SirketId = 7, Q = "fixture" };
            var reportPermission = type is 2 or 3 or 4;
            context.ViewData["YkcYetkileri"] = new YkcYetkiOzeti { TalepleriGorebilir = true, RaporlariGorebilir = reportPermission, KendiTalepleriniIndirebilir = type == 5 };
            var requests = new YkcTalepListeSonuc
            {
                Toplam = 2, Sayfa = 1, SayfaBoyutu = 10,
                Talepler = [new()
                {
                    Id = 75, MusteriAdi = "HALİME KALINSAZLIOĞLU", FirmaAdi = "Fixture firm",
                    TesisatNo = "1319851", SozlesmeNo = "000943", Durum = YkcDurumDegerleri.TalepAlindi,
                    ProjedekiCihazBilgisi = new() { Marka = "Private source brand", BacaTipi = "Bacali", Kapasite = "20000" },
                    YeniCihazBilgisi = new() { Tip = "Kombi", Marka = "Bosch", BacaTipi = "Hermetik", Kapasite = "18834" }
                }, new()
                {
                    Id = 76, TesisatNo = "<script>fixture</script>", SozlesmeNo = null,
                    RandevuTarihi = new DateTime(2026, 10, 7), RandevuSaati = "09:00"
                }]
            };
            context.ViewData["Ozet"] = new YkcDashboardOzetDto { Toplam = requests.Toplam, Incelemede = 1 };
            var html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Talepler.cshtml", requests);
            var decoded = WebUtility.HtmlDecode(html);
            context.HttpContext.Items["fixture.url-values"] = true;
            var clearHtml = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Talepler.cshtml", requests);
            context.HttpContext.Items.Remove("fixture.url-values");
            var clearLink = Tags(clearHtml, "a").Single(tag => Attribute(tag, "href").Contains("Talepler", StringComparison.OrdinalIgnoreCase)
                && HasClass(tag, "df-btn-secondary"));
            Check(Attribute(clearLink, "href").Contains("sirketId=7") && !Attribute(clearLink, "href").Contains("fixture"),
                role + ": clearing request filters preserves the selected company without retaining text filters");
            Check(decoded.Contains("title=\"Halime Kalınsazlıoğlu\">Halime Kalınsazlıoğlu</div>")
                && requests.Talepler[0].MusteriAdi == "HALİME KALINSAZLIOĞLU",
                role + ": request names and tooltips share Turkish display casing without changing API data");
            var requestHeaders = Regex.Matches(Table(html, "ykc-data-table"), @"<th\b[^>]*>([\s\S]*?)</th>", RegexOptions.None, RegexTimeout).Cast<Match>().ToArray();
            Check(Regex.Matches(decoded, "title=\"Randevu planlanacak\">Randevu planlanacak</div>", RegexOptions.None, RegexTimeout).Count == 1,
                role + ": compact appointment styling preserves the full wording and tooltip");
            Check(decoded.Contains("class=\"ykc-primary-cell ykc-scheduled-appointment\" title=\"07.10.2026 09:00\"")
                && decoded.Contains("datetime=\"2026-10-07\"><i class=\"bi bi-calendar3\" aria-hidden=\"true\"></i>07.10.2026</time>")
                && decoded.Contains("<i class=\"bi bi-clock\" aria-hidden=\"true\"></i>09:00</span>"),
                role + ": scheduled appointments preserve date/time values with separate decorative calendar and clock icons");
            Check(decoded.Contains(">Talep Alındı</span>") && decoded.Contains(">Bilinmiyor</span>")
                && !decoded.Contains("ykc-next-action") && !decoded.Contains(">Detay</span>"),
                role + ": process states are readable without a redundant detail button");
            Check(requestHeaders.Any(header => HasClass(Tags(header.Value, "th")[0], "ykc-list-installation-cell")
                    && WebUtility.HtmlDecode(header.Groups[1].Value).Trim() == "Tesisat No")
                && requestHeaders.Any(header => HasClass(Tags(header.Value, "th")[0], "ykc-list-contract-cell")
                    && WebUtility.HtmlDecode(header.Groups[1].Value).Trim() == "Sözleşme No")
                && !decoded.Contains("Tesisat ve Sözleşme Bilgileri"), role + ": request identifiers have separate column headings");
            Check(decoded.Contains("data-label=\"Tesisat No\"") && decoded.Contains("data-label=\"Sözleşme No\"")
                && decoded.Contains("title=\"1319851\">1319851</div>")
                && decoded.Contains("title=\"000943\">000943</div>")
                && decoded.Contains("title=\"-\">-</div>") && !html.Contains("<script>fixture</script>"),
                role + ": separate cells preserve identifiers, leading zeroes, missing values and HTML encoding");
            var deviceText = WebUtility.HtmlDecode(Regex.Replace(Table(html, "ykc-data-table"), "<[^>]*>", "",
                RegexOptions.None, RegexTimeout));
            Check(decoded.Contains("Private source brand") == (role != "SertifikaliFirma")
                && decoded.Contains("20000 kcal/h") == (role != "SertifikaliFirma")
                && deviceText.Contains("Baca tipi: Bacali") == (role != "SertifikaliFirma")
                && decoded.Contains("18834 kcal/h") && deviceText.Contains("Baca tipi: Hermetik"),
                role + ": device facts and existing source-data visibility are unchanged");
            CheckSeparatedColumns(html, "ykc-data-table", role == "SertifikaliFirma"
                ? ["Yakıcı Cihaz Tipi", "Yeni Kullanılan Cihaz", "Kontrol Randevusu", "Süreç"]
                : ["Yakıcı Cihaz Tipi", "Projedeki Cihaz", "Yeni Kullanılan Cihaz", "Kontrol Randevusu", "Süreç"],
                requests.Talepler.Count, role + " requests");
            CheckDirectory(html, "ykc-requests-view", "ykc-data-table", requests.Talepler.Select(record => record.Id).ToArray(), role + " requests");
            var requestColumnCount = requestHeaders.Length;
            Check(requestColumnCount == (role == "SertifikaliFirma" ? 9 : 11),
                role + ": final request column count includes the shared selection column");
            Check(Tags(Table(html, "ykc-data-table"), "i").Count(tag => HasClass(tag, "ykc-device-change-arrow"))
                    == (role == "SertifikaliFirma" ? 0 : requests.Talepler.Count)
                && !html.Contains("ykc-device-change-heading-arrow") && !html.Contains("ykc-device-arrow-cell"),
                role + ": device transition arrows belong to product facts, not headings or separate columns");
            var requestChecks = Tags(html, "input").Where(tag => HasAttribute(tag, "data-directory-select")).ToArray();
            var requestExports = Tags(html, "button").Where(tag => HasAttribute(tag, "data-directory-export")).ToArray();
            Check(requestChecks.Select(tag => Attribute(tag, "value")).SequenceEqual(new[] { "75", "76" })
                && Tags(html, "input").Any(tag => HasAttribute(tag, "data-directory-select-all"))
                && Tags(html, "a").Count(tag => HasClass(tag, "ykc-code") && HasAttribute(tag, "data-directory-open")) == requests.Talepler.Count
                && Tags(html, "tr").Count(tag => HasAttribute(tag, "data-directory-row-open")) == requests.Talepler.Count,
                role + ": request rows retain scoped selection in both list and cards");
            Check(reportPermission || type == 5
                ? requestExports.Length == 2 && requestExports.All(tag => !HasAttribute(tag, "disabled"))
                    && requestExports.Any(tag => Attribute(tag, "data-export-url") == "/Ykc/RaporPdf")
                    && requestExports.Any(tag => Attribute(tag, "data-export-url") == "/Ykc/RaporExcel")
                : requestExports.Length == 0 && !Tags(html, "div").Any(tag => HasClass(tag, "ys-history-export")),
                role + ": request toolbar exports retain PDF/Excel endpoints without widening report permission");
            var fixtureDirectory = Environment.GetEnvironmentVariable("YSGA_RAZOR_FIXTURE_DIR");
            if (!string.IsNullOrWhiteSpace(fixtureDirectory))
            {
                Directory.CreateDirectory(fixtureDirectory);
                await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, role + "-requests.html"), html);
            }
            foreach (var (date, time, calendar, clock) in new (DateTime?, string?, bool, bool)[]
            {
                (new DateTime(2026, 10, 7), null, true, false),
                (null, "09:00", false, true),
                (null, " ", false, false)
            })
            {
                requests.Talepler[1].RandevuTarihi = date;
                requests.Talepler[1].RandevuSaati = time;
                requests.Talepler[1].SiradakiKontrolNo = 2;
                var appointmentHtml = WebUtility.HtmlDecode(await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Talepler.cshtml", requests));
                var appointmentTable = Table(appointmentHtml, "ykc-data-table");
                Check(Tags(appointmentTable, "i").Count(tag => HasClass(tag, "bi-calendar3")) == (calendar ? 1 : 0)
                    && Tags(appointmentTable, "i").Count(tag => HasClass(tag, "bi-clock")) == (clock ? 1 : 0)
                    && appointmentTable.Contains(calendar || clock ? "2. kontrol</span>" : "2. kontrol randevusu planlanacak"),
                    role + $": partial appointment calendar={calendar}/clock={clock} omits empty icons and preserves repeat-control context");
            }
            requests.Talepler[1].RandevuTarihi = new DateTime(2026, 10, 7);
            requests.Talepler[1].RandevuSaati = "09:00";
            requests.Talepler[1].SiradakiKontrolNo = null;
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Talepler.cshtml", new YkcTalepListeSonuc());
            Check(html.Contains($"colspan=\"{requestColumnCount}\"")
                && !Tags(html, "input").Any(tag => HasAttribute(tag, "data-directory-select"))
                && Tags(html, "input").Count(tag => HasAttribute(tag, "data-directory-select-all") && HasAttribute(tag, "disabled")) == 1
                && Tags(html, "button").Count(tag => HasAttribute(tag, "data-directory-export")) == (reportPermission || type == 5 ? 2 : 0)
                && Tags(html, "button").Where(tag => HasAttribute(tag, "data-directory-export")).All(tag => HasAttribute(tag, "disabled")),
                role + ": empty requests span the updated columns and disable selection/export");
            CheckDirectory(html, "ykc-requests-view", "ykc-data-table", [], role + " empty requests");

            if (reportPermission)
            {
                context.ViewData["YkcYetkileri"] = new YkcYetkiOzeti { TalepleriGorebilir = true, RaporlariGorebilir = false };
                var withoutReportPermission = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Talepler.cshtml", requests);
                Check(!Tags(withoutReportPermission, "button").Any(tag => HasAttribute(tag, "data-directory-export"))
                    && !Tags(withoutReportPermission, "div").Any(tag => HasClass(tag, "ys-history-export"))
                    && Tags(Table(withoutReportPermission, "ykc-data-table"), "th").Length == requestColumnCount,
                    role + ": disabling report permission removes only the request export menu");
                CheckDirectory(withoutReportPermission, "ykc-requests-view", "ykc-data-table",
                    requests.Talepler.Select(record => record.Id).ToArray(), role + " requests without report permission");
                context.ViewData["YkcYetkileri"] = new YkcYetkiOzeti { TalepleriGorebilir = true, RaporlariGorebilir = true };
            }

            var internalRole = type is 2 or 3 or 4;
            var detail = new YkcTalepDetayDto
            {
                Id = 71, MusteriAdi = "Örnek Abone", Durum = YkcDurumDegerleri.Atandi,
                RandevuTarihi = new DateTime(2026, 10, 7), RandevuSaati = "09:00", AtananEkip = "Örnek ekip",
                TesisatNo = "1000132", SozlesmeNo = "432237", AboneNo = "34760217156",
                Adres = "Örnek Mahallesi, Örnek Sokak No: 4/0", FirmaAdi = "Örnek Firma", SirketAdi = "Örnek Dağıtım Şirketi",
                YeniCihazTipi = "Ocak", YeniMarka = "Bosch", YeniKapasite = "2750", YeniBacaTipi = "Hermetik",
                IkinciElCihazMi = false, TalepTarihi = new DateTime(2026, 10, 5, 9, 18, 0)
            };
            detail.Ekran = YkcTalepIslemKurali.EkranHazirla(detail,
                new() { TalepleriGorebilir = true, AtamaYapabilir = internalRole },
                internalRole, new(), [], new DateTime(2026, 10, 6));
            context.RouteData.Values["controller"] = "Ykc";
            context.RouteData.Values["action"] = "Detay";
            context.ViewData["PanelTitle"] = "Cihaz Değişim Talebi Detayı";
            context.ViewData["ActiveMenu"] = "YkcTalepler";
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Detay.cshtml", detail);
            decoded = WebUtility.HtmlDecode(html);
            Check(decoded.Contains("class=\"ykc-appointment-focus\"") && decoded.Contains("datetime=\"2026-10-07\"")
                && decoded.Contains("07.10.2026") && decoded.Contains("<b>09:00</b>"),
                role + ": detail prominently displays the unchanged appointment date and time");
            Check(internalRole
                ? decoded.IndexOf("ykc-appointment-focus", StringComparison.Ordinal) < decoded.IndexOf("ykc-staff-record-body", StringComparison.Ordinal)
                    && decoded.Contains("data-appointment-transition")
                : decoded.Contains("ykc-control-heading") && !decoded.Contains("ykc-assignment-form"),
                role + ": appointment appears in the primary heading without changing operation permissions");
            if (!string.IsNullOrWhiteSpace(fixtureDirectory))
                await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, role + "-detail.html"), html);
            detail.RandevuTarihi = null;
            detail.RandevuSaati = null;
            detail.Durum = YkcDurumDegerleri.TalepAlindi;
            detail.Ekran = YkcTalepIslemKurali.EkranHazirla(detail, new() { TalepleriGorebilir = true },
                internalRole, new(), [], new DateTime(2026, 10, 6));
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Detay.cshtml", detail);
            Check(!html.Contains("class=\"ykc-appointment-focus\""), role + ": unplanned requests do not acquire an empty appointment label");
        }

        foreach (var (role, type) in new[]
        {
            ("GenelSistemAdmin", 4), ("SirketAdmin", 3), ("Personel", 2), ("SertifikaliFirma", 5)
        })
        {
            var context = Context(role, type, "", scope.ServiceProvider);
            context.ViewData["YkcYetkileri"] = new YkcYetkiOzeti { TalepleriGorebilir = true, RaporlariGorebilir = true };
            context.ViewData["Filtre"] = new YkcTalepListeFiltre
            {
                SirketId = 7, BaslangicTarihi = new DateTime(2026, 10, 1), BitisTarihi = new DateTime(2026, 10, 12)
            };
            context.ViewData["OtomatikDetayTalepId"] = 75;
            var reports = new YkcRaporSonuc
            {
                Toplam = 2, Sayfa = 1, SayfaBoyutu = 10,
                DurumOzetleri = [new() { Durum = YkcDurumDegerleri.TalepAlindi, Sayi = 1 }, new() { Durum = YkcDurumDegerleri.Tamamlandi, Sayi = 1 }],
                EkipOzetleri = [new() { Ad = "Fixture long inspection team name <script>team</script>", Sayi = 1 }],
                FirmaOzetleri = [new() { Ad = "Private distribution company", Sayi = 2 }],
                Kayitlar = [new()
                {
                    Id = 75, MusteriAdi = "ALİ IŞIK", FirmaAdi = "Fixture certified firm",
                    TesisatNo = "1319851", SozlesmeNo = "000943", AboneNo = "fixture-subscriber-75",
                    FirmaVergiNo = "fixture-tax-75", FirmaFaaliyetIli = "Fixture city", SirketAdi = "Fixture company",
                    EskiCihazTipi = "Kombi", EskiMarka = "Private report source brand", EskiKapasite = "20000", EskiBacaTipi = "Bacali",
                    YeniCihazTipi = "Ocak", YeniMarka = "Bosch", YeniModel = "Fixture complete device model",
                    YeniKapasite = "2750", YeniBacaTipi = "Hermetik", IkinciElCihazMi = false,
                    AtananEkip = "Fixture team", Bolge = "Fixture zone", Durum = YkcDurumDegerleri.Tamamlandi,
                    TalepTarihi = new DateTime(2026, 10, 5, 9, 18, 0), RandevuTarihi = new DateTime(2026, 10, 7), RandevuSaati = "09:00",
                    ImzaliNihaiBelgeVar = true, ImzaliNihaiDosyaId = 91, ImzaDurumu = YkcImzaDurumDegerleri.Tamamlandi
                }, new() { Id = 76, MusteriAdi = "<script>fixture</script>", Durum = YkcDurumDegerleri.TalepAlindi }]
            };
            var html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Raporlar.cshtml", reports);
            var decoded = WebUtility.HtmlDecode(html);
            Check(decoded.Contains("title=\"Ali Işık\">Ali Işık</div>") && decoded.Contains("data-musteri=\"Ali Işık\"")
                && reports.Kayitlar[0].MusteriAdi == "ALİ IŞIK"
                && html.Contains("&lt;Script&gt;Fixture&lt;/Script&gt;"),
                role + ": report cards and modal names use encoded Turkish display casing without changing export data");
            var firm = role == "SertifikaliFirma";
            var insights = Regex.Match(html, @"<section class=""ykc-report-insights""[^>]*>[\s\S]*?</section>", RegexOptions.None, RegexTimeout).Value;
            var decodedInsights = WebUtility.HtmlDecode(insights);
            Check(Tags(insights, "details").Length == 1 && Tags(insights, "details").All(tag => !HasAttribute(tag, "open"))
                && decodedInsights.Contains("ys-history-menu ykc-report-breakdown")
                && decodedInsights.Contains("ys-history-menu-body ykc-report-distribution")
                && !html.Contains("const breakdowns =")
                && !decodedInsights.Contains("Filtrelenen kayıt")
                && decodedInsights.Contains("<dt>Kontrol sürecinde</dt><dd>0</dd>")
                && decodedInsights.Contains("<dt>Tamamlanan</dt><dd>1</dd>"),
                role + ": toolbar summary keeps API totals and one shared distribution menu without duplicate handlers or record counts");
            Check(decodedInsights.Contains("Private distribution company") == !firm
                && decodedInsights.Contains(firm ? "Süreç dağılımı" : "Firma dağılımı")
                && insights.Contains("&lt;script&gt;team&lt;/script&gt;") && !insights.Contains("<script>team</script>"),
                role + ": distribution names stay encoded and company summaries remain hidden from certified firms");
            var reportFilterForm = Regex.Matches(html, @"<form\b[^>]*>[\s\S]*?</form>", RegexOptions.None, RegexTimeout)
                .Cast<Match>().Single(match => Attribute(Tags(match.Value, "form")[0], "id") == "ykcFilters").Value;
            var filterControls = Tags(reportFilterForm, "input").Concat(Tags(reportFilterForm, "select"))
                .Where(tag => Attribute(tag, "type") != "hidden").ToArray();
            var filterLabels = Tags(reportFilterForm, "label");
            var controlIds = Tags(html, "input").Concat(Tags(html, "select")).Select(tag => Attribute(tag, "id")).ToArray();
            Check(filterLabels.Length == (firm ? 12 : 14) && filterLabels.Length == filterControls.Length
                && filterLabels.All(label => !string.IsNullOrWhiteSpace(Attribute(label, "for"))
                    && controlIds.Count(id => id == Attribute(label, "for")) == 1)
                && filterControls.All(control => filterLabels.Count(label => Attribute(label, "for") == Attribute(control, "id")) == 1),
                role + ": every compiled report filter label resolves to exactly one control without JavaScript");
            CheckSeparatedColumns(html, "ykc-report-table", firm
                ? ["Yakıcı Cihaz Tipi", "Yeni Kullanılan Cihaz", "Durum"]
                : ["Firma", "Yakıcı Cihaz Tipi", "Projedeki Cihaz", "Yeni Kullanılan Cihaz", "Ekip", "Durum"],
                reports.Kayitlar.Count, role + " reports");
            CheckDirectory(html, "ykc-reports-view", "ykc-report-table", reports.Kayitlar.Select(record => record.Id).ToArray(), role + " reports");
            var reportColumnCount = Tags(Table(html, "ykc-report-table"), "th").Length;
            Check(reportColumnCount == (firm ? 9 : 12), role + ": final report column count retains separate identifiers and role-specific device fields");
            Check(decoded.Contains("data-directory-column=\"installation\" data-directory-fields=\"tesisatNo\">Tesisat No")
                && decoded.Contains("data-directory-column=\"contract\" data-directory-fields=\"sozlesmeNo\">Sözleşme No"),
                role + ": report identifier headings have independent filters");
            Check(decoded.Contains("Private report source brand") == !firm
                && decoded.Contains($"data-eski-marka=\"{(firm ? "-" : "Private report source brand")}\"")
                && decoded.Contains($"data-eski-kapasite=\"{(firm ? "-" : "20000")}\"")
                && decoded.Contains($"data-eski-baca=\"{(firm ? "-" : "Bacali")}\""),
                role + ": report table and modal retain role-specific source-device privacy");
            var deviceText = WebUtility.HtmlDecode(Regex.Replace(Table(html, "ykc-report-table"), "<[^>]*>", "",
                RegexOptions.None, RegexTimeout));
            Check(deviceText.Contains("Kapasite: 2750") && deviceText.Contains("Baca tipi: Hermetik")
                && decoded.Contains("Fixture complete device model") && decoded.Contains("000943")
                && decoded.Contains("data-yeni-kapasite=\"2750\"") && decoded.Contains("data-yeni-baca=\"Hermetik\"")
                && decoded.Contains("data-yeni-model=\"Fixture complete device model\"") && !html.Contains("<script>fixture</script>"),
                role + ": reports preserve device facts, leading zeroes and encoded subscriber text");

            var rowChecks = Tags(html, "input").Where(tag => HasClass(tag, "ykc-report-row-check")).ToArray();
            Check(rowChecks.Length == 2 && rowChecks.Any(tag => tag.Contains("value=\"75\""))
                && rowChecks.Any(tag => tag.Contains("value=\"76\""))
                && rowChecks.All(tag => HasAttribute(tag, "data-directory-select") && !HasAttribute(tag, "disabled"))
                && Tags(html, "input").Any(tag => HasAttribute(tag, "data-directory-select-all") && !HasAttribute(tag, "disabled"))
                && Tags(html, "span").Any(tag => HasAttribute(tag, "data-directory-selection-count")),
                role + ": reports retain scoped row selection, select-all and the shared toolbar selection summary");
            var exports = Tags(html, "button").Where(tag => HasAttribute(tag, "data-directory-export")).ToArray();
            Check(exports.Length == 2 && exports.All(tag => !HasAttribute(tag, "disabled"))
                && exports.Any(tag => Attribute(tag, "data-export-url") == "/Ykc/RaporPdf")
                && exports.Any(tag => Attribute(tag, "data-export-url") == "/Ykc/RaporExcel"),
                role + ": shared selected-or-filtered exports retain both PDF/Excel endpoints");
            var detailButtons = Tags(html, "button").Where(tag => HasClass(tag, "ykc-row-view-details")).ToArray();
            Check(detailButtons.Length == 2 && detailButtons.Any(tag => tag.Contains("data-id=\"75\""))
                && detailButtons.Any(tag => tag.Contains("data-id=\"76\""))
                && detailButtons.All(tag => HasAttribute(tag, "data-directory-open"))
                && detailButtons.All(tag => tag.Contains("data-detail-url=\"/Ykc/Detay\""))
                && detailButtons.Any(tag => tag.Contains("data-signed-file-url=\"/Ykc/DosyaAc\""))
                && html.Contains("id=\"ykcReportDetailModal\"") && html.Contains("data-open-request-id=\"75\"")
                && html.Contains("data-ykc-modal-close") && html.Contains("id=\"ykcModalSignedFile\"")
                && html.Contains("id=\"ykcModalYeniKapasite\"") && html.Contains("id=\"ykcModalYeniBaca\""),
                role + ": report detail modal, auto-open target and signed-document hooks are preserved");
            var fixtureDirectory = Environment.GetEnvironmentVariable("YSGA_RAZOR_FIXTURE_DIR");
            if (!string.IsNullOrWhiteSpace(fixtureDirectory))
            {
                Directory.CreateDirectory(fixtureDirectory);
                await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, role + "-reports.html"), html);
            }
            context.ViewData["OtomatikDetayTalepId"] = null;
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Raporlar.cshtml", new YkcRaporSonuc());
            var emptyReport = WebUtility.HtmlDecode(html);
            Check(emptyReport.Contains("Atanmış ekip bulunmuyor.") && emptyReport.Contains("Dağılım verisi bulunmuyor.")
                && emptyReport.Contains("<dt>Tamamlanan</dt><dd>0</dd>"),
                role + ": empty report distributions keep explicit empty states and zero totals");
            Check(html.Contains($"colspan=\"{reportColumnCount}\"")
                && !Tags(html, "input").Any(tag => HasClass(tag, "ykc-report-row-check"))
                && Tags(html, "input").Where(tag => HasAttribute(tag, "data-directory-select-all")).All(tag => HasAttribute(tag, "disabled"))
                && Tags(html, "button").Where(tag => HasAttribute(tag, "data-directory-export")).Count(tag => HasAttribute(tag, "disabled")) == 2,
                role + ": empty reports retain column span and disabled export/selection controls");
            CheckDirectory(html, "ykc-reports-view", "ykc-report-table", [], role + " empty reports");
        }

        foreach (var area in new[] { "/AdminPanel", "/personel-panel", "/ys-devreyeal" })
        {
            var context = Context("Personel", 2, "Personel", scope.ServiceProvider);
            context.ViewData["DevreyeAlmaAlan"] = area;
            context.ViewData["FirmaIlceleri"] = new Dictionary<int, string> { [7] = "Merkez" };
            IReadOnlyList<DevreyeAlmaKayitDto> records = area == "/ys-devreyeal"
                ? new List<YsDevreyeAlmaDto> { new()
                {
                    Id = 42, FirmaId = 7, FirmaAdi = "Test firm", FirmaFaaliyetIli = "Çorum",
                    MusteriAdi = "<script>customer</script>", MusteriTelefon = "05551112233", TesistatNo = "123456", SozlesmeNo = "000943",
                    MarkaAdi = "Vaillant", CihazModeli = "Long device model", CihazKapasite = "20000",
                    Adres = "Test address", Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
                } }
                : new List<AdminDevreyeAlmaDto> { new()
                {
                    Id = 42, FirmaId = 7, FirmaAdi = "Test firm", FirmaFaaliyetIli = "Çorum",
                    MusteriAdi = "<script>customer</script>", MusteriTelefon = "05551112233", TesistatNo = "123456", SozlesmeNo = "000943",
                    MarkaAdi = "Vaillant", CihazModeli = "Long device model", CihazKapasite = "20000",
                    Adres = "Test address", Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
                } };
            var html = await RenderAsync(scope.ServiceProvider, context, "/Views/Shared/_DevreyeAlmaTable.cshtml", records);
            Check(html.Contains("123456") && html.Contains("000943") && html.Contains("Vaillant")
                && html.Contains("20000") && html.Contains("Long device model") && html.Contains("Test address")
                && html.Contains("Merkez") && !html.Contains("<script>customer</script>"),
                area + ": shared DTO table keeps identifiers, device facts, address and HTML encoding");
            Check(html.Contains("df-commissioning-record-row") && html.Contains("df-commissioning-detail-row")
                && html.Contains(area == "/ys-devreyeal" ? "colspan=\"9\"" : "colspan=\"10\""),
                area + ": shared DTO table preserves the existing layout and detail column count");
            Check(html.Contains("<dt>Telefon:</dt><dd>05551112233</dd>") && Tags(html, "div").Any(tag => HasAttribute(tag, "title")),
                area + ": compact cards preserve customer phone in details and full names in titles");
            records[0].MusteriAdi = "İPEK IŞIL YILDIRIM";
            var nameHtml = WebUtility.HtmlDecode(await RenderAsync(scope.ServiceProvider, context, "/Views/Shared/_DevreyeAlmaTable.cshtml", records));
            Check(nameHtml.Contains("title=\"İpek Işıl Yıldırım\">İpek Işıl Yıldırım</div>")
                && nameHtml.Contains("aria-label=\"İpek Işıl Yıldırım kaydını seç\"")
                && nameHtml.Contains("aria-label=\"İpek Işıl Yıldırım işlem ayrıntılarını görüntüle\"")
                && records[0].MusteriAdi == "İPEK IŞIL YILDIRIM",
                area + ": shared commissioning names, tooltips and accessible labels use presentation-only title casing");
        }
        foreach (var (role, type, view) in new[]
        {
            ("YetkiliServis", 1, "DevreyeAlma/Gecmis"), ("Personel", 2, "PersonelPanel/DevreyeAlmalar"),
            ("SirketAdmin", 3, "AdminPanel/DevreyeAlmalar"), ("GenelSistemAdmin", 4, "AdminPanel/DevreyeAlmalar")
        })
        {
            var context = Context(role, type, "", scope.ServiceProvider);
            context.HttpContext.Request.QueryString = new QueryString("?sozlesme=000943");
            context.ViewData["SeciliSozlesme"] = "000943";
            context.RouteData.Values["controller"] = view.Split('/')[0];
            object records = type == 1 ? new List<YsDevreyeAlmaDto>() : new List<AdminDevreyeAlmaDto>();
            var html = WebUtility.HtmlDecode(await RenderAsync(scope.ServiceProvider, context, "/Views/" + view + ".cshtml", records));
            Check(html.Contains("<label for=\"sozlesme\">Sözleşme No</label>")
                && html.Contains("name=\"sozlesme\" value=\"000943\"")
                && html.Contains("open=\"open\"") && html.Contains("Filtre uygulandı"),
                role + ": compiled commissioning filters retain the contract number and open when it is the only filter");
            Check(html.Contains("method=\"get\"") && html.Contains("Temizle")
                && html.Contains("data-export-url="), role + ": contract search preserves list GET, reset and export controls");
            Check(html.Contains("data-history-directory") && html.Contains("data-history-view=\"cards\"")
                && html.Contains("commissioning-history.js") && html.Contains("commissioning-history.css"),
                role + ": commissioning list, grid and filters use the shared directory");
            var historyFixtureDirectory = Environment.GetEnvironmentVariable("YSGA_RAZOR_FIXTURE_DIR");
            if (!string.IsNullOrWhiteSpace(historyFixtureDirectory))
            {
                context.HttpContext.Request.Path = type == 1 ? "/ys-devreyeal/gecmis" : type == 2 ? "/personel-panel/devreyealmalar" : "/AdminPanel/devreyealmalar";
                context.HttpContext.Request.QueryString = QueryString.Empty;
                context.ViewData["SeciliSozlesme"] = null;
                context.ViewData["PanelSirketAdi"] = "Çorumgaz Doğalgaz A.Ş.";
                context.ViewData["Kullanici"] = new AppKullanici { AdSoyad = type == 1 ? "Örnek Yetkili Servis" : type == 2 ? "Örnek Personel" : "Örnek Yönetici", KullaniciTipi = type };
                context.ViewData["MarkaList"] = new List<string> { "Alarko", "Bosch", "DemirDöküm", "Vaillant" };
                context.ViewData["Markalar"] = new List<AdminMarkaSecenekDto> { new() { MarkaAdi = "Alarko" }, new() { MarkaAdi = "Bosch" }, new() { MarkaAdi = "DemirDöküm" }, new() { MarkaAdi = "Vaillant" } };
                context.ViewData["CihazTipleri"] = new List<string> { "Kombi", "Ocak" };
                context.ViewData["Sehirler"] = new List<string> { "Çorum" };
                context.ViewData["FirmaIlceleri"] = new Dictionary<int, string> { [7] = "Merkez" };
                var names = new[] { "AHMET YILMAZ", "AYŞE DEMİR", "MEHMET KAYA", "ELİF ÇELİK", "MUSTAFA AKSOY", "ZEYNEP ŞAHİN", "ALİ YILDIZ", "FATMA ARSLAN", "HASAN KOÇ", "ESRA AYDIN", "EMRE KILIÇ", "DENİZ YALÇIN" };
                var previewRecords = names.Select((name, i) =>
                {
                    DevreyeAlmaKayitDto kayit = type == 1 ? new YsDevreyeAlmaDto() : new AdminDevreyeAlmaDto();
                    kayit.Id = 100 + i;
                    kayit.FirmaId = 7;
                    kayit.MusteriAdi = name;
                    kayit.TesistatNo = (1319854 - i).ToString();
                    kayit.SozlesmeNo = (461344 - i).ToString();
                    kayit.FirmaAdi = "Örnek Yetkili Servis";
                    kayit.FirmaFaaliyetIli = "Çorum";
                    kayit.CihazTipi = i % 4 == 2 ? "Ocak" : "Kombi";
                    kayit.CihazMarka = new[] { "Bosch", "Vaillant", "DemirDöküm", "Alarko" }[i % 4];
                    kayit.CihazModeli = i % 4 == 0 ? "Bosch Dijital Yoğuşmalı Kombi Condens 2500 W" : i % 4 == 1 ? "ecoTEC Plus VUW 24 kW" : i % 4 == 2 ? "Ankastre ocak" : "Seradens Super";
                    kayit.DevreyeAlmaTarihi = new DateTime(2026, 10, 12).AddDays(-i);
                    kayit.Durum = DevreyeAlmaDurumDegerleri.Tamamlandi;
                    kayit.Adres = "Örnek Mahallesi, Örnek Sokak No: 10/2 Çorum";
                    kayit.SeriNo = "ORNEK-" + i;
                    kayit.CihazKapasite = "20000";
                    kayit.TeknisyenAdi = "Örnek Teknisyen";
                    kayit.TeknisyenYetkiBelgesiNo = "YS-100";
                    return kayit;
                }).ToList();
                object previewModel = type == 1 ? previewRecords.Cast<YsDevreyeAlmaDto>().ToList() : previewRecords.Cast<AdminDevreyeAlmaDto>().ToList();
                context.ViewData["Sayfalama"] = new DevreyeAlmaSayfalamaDto
                {
                    Sayfa = 1, SayfaBoyutu = previewRecords.Count, ToplamKayit = 36,
                    AktarimIdleri = Enumerable.Range(100, 36).ToList()
                };
                Directory.CreateDirectory(historyFixtureDirectory);
                var dosya = type switch { 1 => "commissioning-history.html", 2 => "commissioning-personnel.html", 3 => "commissioning-company-admin.html", _ => "commissioning-admin.html" };
                var previewHtml = await RenderAsync(scope.ServiceProvider, context, "/Views/" + view + ".cshtml", previewModel);
                Check(previewHtml.Contains("data-commissioning-select") && previewHtml.Contains("data-record-id=\"100\"")
                    && previewHtml.Contains("df-commissioning-detail-panel") && previewHtml.Contains("name=\"q\""),
                    role + ": shared row selection, card identity, detail panel and API search remain available");
                Check(WebUtility.HtmlDecode(previewHtml).Contains("title=\"Ahmet Yılmaz\">Ahmet Yılmaz</div>")
                    && previewRecords[0].MusteriAdi == "AHMET YILMAZ",
                    role + ": compiled commissioning page uses shared subscriber title casing");
                Check(previewHtml.Contains("12 / 36") && previewHtml.Contains("aria-label=\"Sonraki sayfa\"")
                    && previewHtml.Contains("data-history-paged=\"true\""),
                    role + ": compiled preview uses shared API totals and server pagination");
                await File.WriteAllTextAsync(Path.Combine(historyFixtureDirectory, dosya), previewHtml);
            }
        }
        {
            var context = Context("GenelSistemAdmin", 4, "Admin", scope.ServiceProvider);
            context.HttpContext.Items["fixture.url-values"] = true;
            context.HttpContext.Request.QueryString = new QueryString("?kontrolNo=2&sayfa=999");
            context.ViewData["YkcYetkileri"] = new YkcYetkiOzeti { TalepleriGorebilir = true, RaporlariGorebilir = true };
            context.ViewData["Filtre"] = new YkcTalepListeFiltre
            {
                SirketId = 7, KontrolNo = 2, AboneNo = "00077", BekleyenIs = "randevu",
                SozlesmeNo = "000943", Sirala = "abone-artan", Sayfa = 999, SayfaBoyutu = 10
            };
            var html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Talepler.cshtml",
                new YkcTalepListeSonuc { Toplam = 23, Sayfa = 3, SayfaBoyutu = 10, Talepler = [new() { Id = 75 }] });
            var exports = Tags(html, "button").Where(tag => HasAttribute(tag, "data-directory-export")).ToArray();
            Check(exports.Length == 2 && exports.All(tag =>
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri("https://fixture.test" + Attribute(tag, "data-export-url")).Query);
                return query["sirketId"] == "7" && query["kaynak"] == "talepler" && query["sayfa"] == "3"
                    && query["sayfaBoyutu"] == "10" && query["kontrolNo"] == "2" && query["aboneNo"] == "00077"
                    && query["bekleyenIs"] == "randevu" && query["sozlesmeNo"] == "000943" && query["sirala"] == "abone-artan";
            }), "YKC exports use the effective company/filter and clamped return page rather than the raw GET query");
            Check(Tags(html, "form").Count(tag => HasAttribute(tag, "data-directory-export-form") && Attribute(tag, "method") == "post") == 1
                && Tags(html, "input").Count(tag => Attribute(tag, "name") == "__RequestVerificationToken") == 1
                && Tags(html, "div").Count(tag => HasAttribute(tag, "data-directory-export-fields")) == 1,
                "Compiled directory renders one hidden POST export form with one antiforgery token and body-fields container");

            context.ViewData["Filtre"] = new YkcTalepListeFiltre { Sayfa = 2, SayfaBoyutu = 25 };
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Raporlar.cshtml",
                new YkcRaporSonuc { Toplam = 80, Sayfa = 2, SayfaBoyutu = 25, Kayitlar = [new() { Id = 75 }] });
            Check(Tags(html, "button").Where(tag => HasAttribute(tag, "data-directory-export")).All(tag =>
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri("https://fixture.test" + Attribute(tag, "data-export-url")).Query);
                return !query.ContainsKey("sirketId") && query["kaynak"] == "rapor" && query["sayfa"] == "2" && query["sayfaBoyutu"] == "25";
            }), "All-company report export preserves intentional empty scope and return-page size");

            context.ViewData["TalepId"] = 75;
            context.ViewData["Filtre"] = new YkcTalepListeFiltre
            {
                SirketId = 7, TesisatNo = "00123", SozlesmeNo = "000943", MusteriAdi = "A & B",
                Firma = "Firma", Il = "City", Ilce = "District", Bolge = "Zone", Ekip = "Team",
                Marka = "Brand", HedefUygulama = "Target", Durum = 1, Q = "<query>",
                Sirala = "abone-artan", CihazTipi = "Kombi",
                BaslangicTarihi = new(2026, 10, 1), BitisTarihi = new(2026, 10, 12),
                RandevuBaslangicTarihi = new(2026, 10, 2), RandevuBitisTarihi = new(2026, 10, 13)
            };
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Ykc/Raporlar.cshtml",
                new YkcRaporSonuc { Toplam = 80, Sayfa = 2, SayfaBoyutu = 25, Kayitlar = [new() { Id = 75 }] });
            var reportPages = Tags(html, "a").Where(tag => Attribute(tag, "href").Contains("sayfa=", StringComparison.Ordinal))
                .Select(tag => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
                    new Uri("https://fixture.test" + Attribute(tag, "href")).Query)).ToArray();
            Check(reportPages.Length > 0 && reportPages.All(query =>
                query["sirketId"] == "7" && query["talepId"] == "75" && query["sayfaBoyutu"] == "25"
                && query["tesisatNo"] == "00123" && query["sozlesmeNo"] == "000943" && query["musteriAdi"] == "A & B"
                && query["firma"] == "Firma" && query["il"] == "City" && query["ilce"] == "District"
                && query["bolge"] == "Zone" && query["ekip"] == "Team" && query["marka"] == "Brand"
                && query["hedefUygulama"] == "Target" && query["durum"] == "1" && query["q"] == "<query>"
                && query["sirala"] == "abone-artan" && query["cihazTipi"] == "Kombi"
                && query["bas"] == "2026-10-01" && query["bit"] == "2026-10-12"
                && query["randevuBas"] == "2026-10-02" && query["randevuBit"] == "2026-10-13"),
                "Report page links preserve company scope and every filter");
            var reportExports = Tags(html, "button").Where(tag => HasAttribute(tag, "data-directory-export"))
                .Select(tag => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
                    new Uri("https://fixture.test" + Attribute(tag, "data-export-url")).Query)).ToArray();
            Check(reportExports.Length == 2 && reportExports.All(query =>
                query["kaynak"] == "rapor" && query["sayfa"] == "2"
                && query.Count == reportPages[0].Count + 1
                && reportPages[0].Where(pair => pair.Key != "sayfa").All(pair => query[pair.Key] == pair.Value))
                && reportPages.All(query => !query.ContainsKey("kaynak")),
                "Report paging, PDF and Excel share all filter values without leaking the export source into page links");

            context.ViewData["DevreyeAlmaAlan"] = "/AdminPanel";
            context.ViewData["SeciliSirketId"] = 8;
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Shared/_DevreyeAlmaInlineDetails.cshtml", new AdminDevreyeAlmaDto { Id = 42 });
            var links = Tags(html, "a").Where(tag => HasClass(tag, "df-commissioning-export")).ToArray();
            Check(links.Length == 2 && links.All(tag => Attribute(tag, "href").Contains("sirketId=8", StringComparison.Ordinal)),
                "Compiled per-row PDF and Excel GET links carry the directory's explicit company scope");
            context.ViewData["SeciliSirketId"] = null;
            html = await RenderAsync(scope.ServiceProvider, context, "/Views/Shared/_DevreyeAlmaInlineDetails.cshtml", new AdminDevreyeAlmaDto { Id = 42 });
            Check(Tags(html, "a").Where(tag => HasClass(tag, "df-commissioning-export")).All(tag => !Attribute(tag, "href").Contains("sirketId", StringComparison.Ordinal)),
                "Compiled all-company per-row links do not invent session company scope");
        }
        foreach (var adminBranch in new[] { true, false })
        {
            var html = await RenderAsync(scope.ServiceProvider, personnel, "/Views/Shared/_BranchEditor.cshtml",
                new BranchEditorViewModel
                {
                    Branch = new AdminSubeDto { Id = 8, FirmaId = 7, SubeAdi = "Branch <script>x</script>", AktifMi = true },
                    Firms = [new AdminSubeFirmaDto { Id = 7, FirmaAdi = "Selected firm" }],
                    CanSelectFirm = adminBranch, PostUrl = "/branch-save"
                });
            Check(html.Contains("ys-branch-editor") && html.Contains("branch-save")
                && html.Contains("value=\"true\"") && !html.Contains("<script>x</script>")
                && html.Contains("branchFirm") == adminBranch,
                "Shared branch DTO retains drawer fields, encoding and firm selection scope: " + adminBranch);
        }


        var sharedContext = Context("GenelSistemAdmin", 4, "Admin", scope.ServiceProvider);
        var editedUser = new AdminKullaniciListeDto
        {
            Id = "edited-user", AdSoyad = "Shared DTO User", Email = "shared@fixture.test",
            KullaniciTipi = KullaniciTipiDegerleri.Personel, SirketId = 7, SirketAdi = "Shared DTO Company", AktifMi = true
        };
        sharedContext.ViewData["ToplamPersonel"] = 1;
        sharedContext.ViewData["EslesenPersonel"] = 1;
        sharedContext.ViewData["YetkiListeOzeti"] = new PersonelYetkiListeOzeti
        {
            ToplamPersonel = 1, YetkiliPersonel = 1,
            Sirketler = [new() { SirketId = 7, SirketAdi = "Shared DTO Company" }]
        };
        sharedContext.ViewData["Personeller"] = new List<AdminKullaniciListeDto> { editedUser };
        sharedContext.ViewData["Kullanicilar"] = new List<AdminKullaniciListeDto> { editedUser };
        sharedContext.ViewData["Personel"] = editedUser;
        sharedContext.ViewData["Hedef"] = editedUser;
        sharedContext.ViewData["Sirketler"] = new List<AdminSirketSecenekDto> { new() { Id = 7, SirketAdi = "Shared DTO Company" } };
        sharedContext.ViewData["Firmalar"] = new List<AdminFirmaSecenekDto> { new() { Id = 12, FirmaAdi = "Shared DTO Firm", SirketId = 7, SirketAdi = "Shared DTO Company" } };
        sharedContext.ViewData["SirketYetkileri"] = new Dictionary<string, List<AdminSirketYetkiOzetDto>>
        {
            ["edited-user"] = [new() { SirketId = 7, SirketAdi = "Shared DTO Company", Yetkiler = [YetkiTipleri.RAPOR_GOR] }]
        };
        foreach (var page in new[] { "Kullanicilar", "Personeller", "KullaniciDuzenle", "Yetkiler", "YetkiDuzenle" })
        {
            var html = await RenderAsync(scope.ServiceProvider, sharedContext, "/Views/AdminPanel/" + page + ".cshtml", new object());
            Check(html.Contains("Shared DTO User") && html.Contains("shared@fixture.test") && html.Contains("Shared DTO Company"),
                page + ": full Razor page consumes shared user/company DTOs without losing ViewBag data");
        }


        foreach (var adminCertificate in new[] { true, false })
        {
            foreach (var canApprove in new[] { true, false })
            {
                YetkiBelgesiKayitDto certificate = adminCertificate ? new AdminYetkiBelgesiOnayDto() : new YetkiBelgesiDto();
                certificate.Id = 34;
                certificate.FirmaAdi = "Certificate <script>firm</script>";
                certificate.SirketAdi = "Certificate Company";
                certificate.VergiNo = "1234567890";
                certificate.FirmaYetkiliKisi = "Certificate Contact";
                certificate.FirmaTelefon = "05551234567";
                certificate.FirmaAdres = "Certificate Address";
                certificate.YetkiBelgesiBitisTarihi = DateTime.Today.AddMonths(1);
                certificate.OlusturmaTarihi = DateTime.Today;
                certificate.Onaylanabilir = canApprove;
                certificate.DosyaYolu = "fixture.pdf";
                var html = await RenderAsync(scope.ServiceProvider, personnel, "/Views/Shared/_YetkiBelgesiOnayTable.cshtml",
                    Tuple.Create<IReadOnlyList<YetkiBelgesiKayitDto>, string, string>([certificate], "bekleyen", "personel"));
                Check(html.Contains("Certificate Company") && html.Contains("Certificate Contact")
                    && html.Contains("05551234567") && html.Contains("Certificate Address")
                    && !html.Contains("<script>firm</script>") && html.Contains("df-approval-detail-grid"),
                    $"Certificate shared DTO preserves details, encoding and markup: admin={adminCertificate}, approve={canApprove}");
                Check(html.Contains("df-approval-action is-approve") == canApprove
                    && html.Contains("df-approval-action is-reject") && html.Contains("name=\"__RequestVerificationToken\""),
                    $"Certificate controls obey API approval eligibility without losing rejection/antiforgery: {adminCertificate}/{canApprove}");
            }
        }

        foreach (var panel in new[] { "admin", "personel", "yetki" })
        {
            foreach (var days in new int?[] { null, -1, 0, 1 })
            {
                var certificate = new YetkiBelgesiDto
                {
                    Id = 35, FirmaAdi = "Expiry fixture", OlusturmaTarihi = DateTime.Today,
                    YetkiBelgesiBitisTarihi = days.HasValue ? DateTime.Today.AddDays(days.Value) : null,
                    Onaylanabilir = days >= 0
                };
                var html = await RenderAsync(scope.ServiceProvider, personnel, "/Views/Shared/_YetkiBelgesiOnayTable.cshtml",
                    Tuple.Create<IReadOnlyList<YetkiBelgesiKayitDto>, string, string>([certificate], "bekleyen", panel));
                var text = System.Net.WebUtility.HtmlDecode(html);
                Check(!text.Contains("01.01.0001") && !text.Contains("0001-01-01")
                    && text.Contains("df-certificate-status is-expired") == (days < 0)
                    && text.Contains("df-approval-action is-approve") == certificate.Onaylanabilir,
                    $"Certificate date display preserves API eligibility and never invents expiry: {panel}/{days}");
                Check(days.HasValue
                    ? text.Contains(days < 0 ? "1 gün geçti" : $"{days} gün kaldı")
                        && text.Contains($"data-bitis=\"{certificate.YetkiBelgesiBitisTarihi:yyyy-MM-dd}\"")
                    : text.Contains("data-bitis=\"\"") && text.Contains("<span>Tanımlı değil</span>")
                        && text.Contains("<strong>Tanımlı değil</strong>") && !text.Contains("is-valid"),
                    $"Certificate date, filter and remaining-days text handle absent/today/expired dates: {panel}/{days}");
            }
        }

        foreach (var days in new int?[] { null, -1, 0, 1 })
        {
            var expiry = days.HasValue ? DateTime.Today.AddDays(days.Value) : (DateTime?)null;
            var certificate = new AdminYetkiBelgesiOnayDto
            {
                Id = 36, FirmaAdi = "Nullable expiry", YetkiBelgesiBitisTarihi = expiry,
                Durum = YetkiBelgesiDurumDegerleri.Onaylandi, OlusturmaTarihi = DateTime.Today
            };
            var history = WebUtility.HtmlDecode(await RenderAsync(scope.ServiceProvider, personnel,
                "/Views/PersonelPanel/OnayGecmisi.cshtml", new List<AdminYetkiBelgesiOnayDto> { certificate }));
            Check(!history.Contains("01.01.0001") && !history.Contains("0001-01-01")
                && (expiry.HasValue ? history.Contains($"datetime=\"{expiry:yyyy-MM-dd}\"") : history.Contains("Tanımlı değil")),
                "Personnel certificate history distinguishes absent expiry from real dates: " + days);
            var certificateContext = Context("YetkiliServis", 1, "YetkiliServis", scope.ServiceProvider);
            var serviceCertificate = new YetkiBelgesiDto
            {
                Id = 36, YetkiBelgesiBitisTarihi = expiry, OlusturmaTarihi = DateTime.Today,
                Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor
            };
            var serviceHtml = WebUtility.HtmlDecode(await RenderAsync(scope.ServiceProvider, certificateContext,
                "/Views/YetkiBelgesi/Index.cshtml", new List<YetkiBelgesiDto> { serviceCertificate }));
            var row = Tags(serviceHtml, "tr").Single(tag => HasClass(tag, "sert-row"));
            Check(Attribute(row, "data-durum") == (days < 0 ? "Süresi Doldu" : "Onay Bekliyor")
                && !serviceHtml.Contains("01.01.0001") && (expiry.HasValue || serviceHtml.Contains("Tanımlı değil")),
                "Service certificate list does not classify missing or today's expiry as expired: " + days);
            var summaryButtons = Regex.Matches(serviceHtml, @"<button\b[^>]*data-belge-durum=""Süresi Doldu""[\s\S]*?</button>", RegexOptions.None, RegexTimeout);
            Check(summaryButtons.Count == 1 && summaryButtons[0].Value.Contains($"<strong>{(days < 0 ? 1 : 0)}</strong>"),
                "Service expired counter matches the nullable-date row state: " + days);
            var warningContext = Context("GenelSistemAdmin", 4, "Admin", scope.ServiceProvider);
            warningContext.ViewData["Yaklasan"] = new List<AdminYetkiBelgesiOnayDto> { certificate };
            warningContext.ViewData["Gecmis"] = new List<AdminYetkiBelgesiOnayDto> { certificate };
            var warnings = WebUtility.HtmlDecode(await RenderAsync(scope.ServiceProvider, warningContext,
                "/Views/AdminPanel/YetkiBelgesiUyarilari.cshtml", new object()));
            Check(!warnings.Contains("01.01.0001") && !warnings.Contains("0001-01-01")
                && (expiry.HasValue || warnings.Contains("Tanımlı değil") && !warnings.Contains("df-warning-row-critical")),
                "Admin warning rows never derive a date or critical countdown from null: " + days);
        }

        var managedFirm = new AdminYetkiliServisDto
        {
            Id = 12, FirmaAdi = "Editor <script>firm</script>", SirketAdi = "Editor Company", Telefon = "05559876543",
            YetkiliKisi = "Editor Contact", AktifMi = true,
            Markalar = [new() { Id = 3, MarkaAdi = "Selected inactive brand" }],
            Kategoriler = [new() { Id = 4, Ad = "Selected category" }]
        };
        var editorContext = Context("Personel", 2, "Personel", scope.ServiceProvider);
        editorContext.ViewData["Markalar"] = new List<MarkaApiDto> { new() { Id = 3, MarkaAdi = "Selected inactive brand", AktifMi = false } };
        editorContext.ViewData["Kategoriler"] = managedFirm.Kategoriler;
        editorContext.ViewData["SeciliMarkalar"] = new List<int> { 3 };
        editorContext.ViewData["SeciliKategoriler"] = new List<int> { 4 };
        editorContext.ViewData["ServisDuzenleme"] = true;
        var editorHtml = await RenderAsync(scope.ServiceProvider, editorContext, "/Views/Shared/_YetkiliServisEditor.cshtml", managedFirm);
        Check(editorHtml.Contains("Selected inactive brand (Pasif)") && editorHtml.Contains("Selected category")
            && editorHtml.Contains("name=\"markaIds\" value=\"3\" checked") && editorHtml.Contains("name=\"kategoriIds\" value=\"4\" checked")
            && editorHtml.Contains("05559876543") && !editorHtml.Contains("<script>firm</script>"),
            "Firm editor retains selected inactive brands, category checks, field values and encoding");
        foreach (var isAdmin in new[] { true, false })
        {
            editorContext.ViewData["ServiceDirectoryAdmin"] = isAdmin;
            editorContext.ViewData["ServiceDirectoryCanManage"] = true;
            var html = await RenderAsync(scope.ServiceProvider, editorContext, "/Views/Shared/_YetkiliServisKayitlari.cshtml",
                new List<AdminYetkiliServisDto> { managedFirm });
            Check(html.Contains("Selected inactive brand") && html.Contains("Selected category")
                && html.Contains("Editor Company") && html.Contains("df-service-grid") && !html.Contains("<script>firm</script>"),
                "Both service directories consume flat DTOs without losing company and authorization detail: " + isAdmin);
        }

        var serviceContext = Context("YetkiliServis", 1, "YetkiliServis", scope.ServiceProvider);
        serviceContext.ViewData["Firma"] = new YsPanelFirmaDto
        {
            Id = 12, FirmaAdi = "Panel Fixture Firm", Telefon = "05557654321", Email = "panel@fixture.test",
            OlusturmaTarihi = new DateTime(2025, 3, 4), Sirket = new() { SirketAdi = "Panel Fixture Company" }
        };
        serviceContext.ViewData["TumMarkalar"] = new List<YsPanelMarkaDto> { new() { Id = 3, MarkaAdi = "Panel Fixture Brand", AktifMi = true, Aciklama = "Brand note" } };
        serviceContext.ViewData["TumKategoriler"] = new List<YsPanelUrunKategoriDto> { new() { Id = 4, Ad = "Panel Fixture Category" } };
        serviceContext.ViewData["SeciliMarkaIds"] = new List<int> { 3 };
        serviceContext.ViewData["SeciliKategoriIds"] = new List<int> { 4 };
        serviceContext.ViewData["FirmaMarkalar"] = new List<YsPanelFirmaMarkaDto> { new() { MarkaId = 3, Marka = new() { Id = 3, MarkaAdi = "Panel Fixture Brand", AktifMi = true } } };
        serviceContext.ViewData["Subeler"] = new List<AdminSubeDto> { new() { Id = 8, FirmaId = 12, SubeAdi = "Panel Fixture Branch", AktifMi = true } };
        foreach (var (page, expected) in new[]
        {
            ("Index", "Panel Fixture Firm"), ("IlkKurulum", "Panel Fixture Brand"),
            ("Markalar", "Panel Fixture Brand"), ("Profil", "04.03.2025"), ("Subeler", "Panel Fixture Branch")
        })
        {
            var html = await RenderAsync(scope.ServiceProvider, serviceContext, "/Views/YetkiliServisPanel/" + page + ".cshtml", new object());
            Check(html.Contains(expected), page + ": service panel renders shared DTO fields instead of silently falling back to empty data");
        }

        var profileFirm = (YsPanelFirmaDto)serviceContext.ViewData["Firma"]!;
        profileFirm.YetkiBelgeleri = [new() { Id = 19, Durum = YetkiBelgesiDurumDegerleri.Onaylandi,
            YetkiBelgesiBitisTarihi = DateTime.Today.AddYears(1) }];
        profileFirm.GosterilenYetkiBelgesiId = 19;
        profileFirm.YetkiBelgesiDurumu = "API certificate decision";
        profileFirm.GecerliYetkiBelgesiVar = false;
        var profileHtml = await RenderAsync(scope.ServiceProvider, serviceContext, "/Views/YetkiliServisPanel/Profil.cshtml", new object());
        Check(profileHtml.Contains("API certificate decision") && profileHtml.Contains("df-pill-danger")
            && profileHtml.Contains(DateTime.Today.AddYears(1).ToString("dd.MM.yyyy")),
            "Profile uses API validity and selected certificate instead of inferring validity from a future expiry");
        profileFirm.GecerliYetkiBelgesiVar = true;
        profileFirm.YetkiBelgeleri[0].YetkiBelgesiBitisTarihi = DateTime.Today;
        profileHtml = await RenderAsync(scope.ServiceProvider, serviceContext, "/Views/YetkiliServisPanel/Profil.cshtml", new object());
        Check(profileHtml.Contains("df-pill-success"), "Profile preserves API approval on the inclusive expiry day");

        foreach (var reportType in new[] { "ykc", "devreye", "onayli", "bekleyen", "reddedilen" })
        {
            var context = Context("Personel", 2, "Personel", scope.ServiceProvider);
            // Stale chrome permissions must not override the report API's allowed choices.
            context.ViewData["YetkiRapor"] = context.ViewData["YetkiBelgesi"] = context.ViewData["YetkiYkcRapor"] = true;
            var report = new PersonelRaporDto
            {
                RaporTipi = reportType,
                IzinliRaporTipleri = reportType is "ykc" or "devreye" ? [reportType] : ["devreye", "onayli", "bekleyen", "reddedilen"],
                Toplam = 19, Tamamlanan = 17,
                BasTarih = new DateTime(2026, 9, 1), BitTarih = new DateTime(2026, 9, 30),
                DonemEtiketleri = ["API chart label"], DonemSayilari = [19],
                KirilimEtiketleri = ["Fixture <brand>"], KirilimSayilari = [19],
                DurumEtiketleri = ["API status"], DurumSayilari = [19]
            };
            var html = await RenderAsync(scope.ServiceProvider, context, "/Views/PersonelPanel/Raporlar.cshtml", report);
            Check(html.Contains("df-personnel-operations-report") && html.Contains("df-ops-monthly-chart")
                && html.Contains("API chart label") && html.Contains("2026-09-01") && html.Contains("2026-09-30")
                && !html.Contains("<brand>"), "Typed personnel report retains layout, dates, chart values and HTML encoding: " + reportType);
            Check(html.Contains("value=\"ykc\"") == (reportType == "ykc")
                && html.Contains("value=\"devreye\"") == (reportType != "ykc")
                && html.Contains("value=\"bekleyen\"") == (reportType is "onayli" or "bekleyen" or "reddedilen"),
                "Report selector uses API permissions rather than unrelated ViewBag flags: " + reportType);
        }

        var directory = new YetkiliServisRehberEkranDto
        {
            Sorgu = new() { Il = "City", Ilce = "District", MarkaId = 8, KategoriId = 2, Q = "A & B" },
            Sonuc = new()
            {
                Page = 2, PageSize = 10, TotalCount = 25,
                Items = [new() { FirmaAdi = "Fixture <service>", Telefon = "0555 111 22 33", Markalar = ["Brand"] }]
            }
        };
        var directoryHtml = await RenderAsync(scope.ServiceProvider, personnel, "/Views/YetkiliServisler/Index.cshtml", directory);
        Check(directoryHtml.Contains("service-card") && directoryHtml.Contains("25 kayıt listeleniyor")
            && directoryHtml.Contains("tel:05551112233") && !directoryHtml.Contains("<service>"),
            "Directory renders the shared API response with unchanged cards, count, contact links and encoding");
        directoryHtml = WebUtility.HtmlDecode(directoryHtml);
        Check(directoryHtml.Contains("page=3") && directoryHtml.Contains("pageSize=10")
            && directoryHtml.Contains("il=City") && directoryHtml.Contains("ilce=District")
            && directoryHtml.Contains("markaId=8") && directoryHtml.Contains("kategoriId=2")
            && directoryHtml.Contains("q=A+%26+B"), "Directory paging preserves every API query selection");
        directoryHtml = await RenderAsync(scope.ServiceProvider, personnel, "/Views/YetkiliServisler/Index.cshtml", new YetkiliServisRehberEkranDto());
        Check(directoryHtml.Contains("class=\"empty\"") && !directoryHtml.Contains("class=\"service-card\""),
            "Empty directory response remains renderable without an intermediate view model");

        foreach (var type in typeof(MarkaApiClient).Assembly.GetTypes()
            .Where(x => x.Name.EndsWith("ApiClient", StringComparison.Ordinal) && x.Name != "AuthApiClient"))
            Check(type.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .All(x => x.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)),
                type.Name + ": transport contracts are not nested inside the client");

        Console.WriteLine($"{passed} panel component checks passed. No server or application data used.");
    }

    private static ViewContext Context(string role, int type, string area, IServiceProvider services)
    {
        var http = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "chrome-fixture"),
                new Claim(ClaimTypes.Name, "chrome-fixture"), new Claim(ClaimTypes.Role, role)], "Fixture"))
        };
        http.Request.Path = "/ykc/raporlar";
        http.Request.QueryString = new QueryString("?sayfa=2&durum=1");
        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            ["Kullanici"] = new AppKullanici { Id = "chrome-fixture", AdSoyad = "Fixture User", KullaniciTipi = type },
            ["PanelArea"] = area, ["PanelTitle"] = "Cihaz Değişim Raporları", ["ActiveMenu"] = "YkcRaporlar"
        };
        return new ViewContext { HttpContext = http, ViewData = data, RouteData = new RouteData(), ActionDescriptor = new ActionDescriptor() };
    }

    private static SidebarViewModel Sidebar(ViewContext context)
        => (SidebarViewModel)((ViewViewComponentResult)new SidebarViewComponent
        {
            ViewComponentContext = new ViewComponentContext { ViewContext = context }, Url = new FixtureUrlHelper(context)
        }.Invoke()).ViewData!.Model!;

    private static NavbarViewModel Navbar(ViewContext context)
        => (NavbarViewModel)((ViewViewComponentResult)new NavbarViewComponent
        {
            ViewComponentContext = new ViewComponentContext { ViewContext = context }, Url = new FixtureUrlHelper(context)
        }.Invoke()).ViewData!.Model!;

    private static async Task<string> RenderAsync(IServiceProvider services, ViewContext source, string path, object model)
    {
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var view = engine.GetView(null, path, isMainPage: false);
        if (!view.Success) throw new InvalidOperationException("Compiled view missing: " + path);
        using var writer = new StringWriter();
        await view.View.RenderAsync(new ViewContext(source, view.View,
            new ViewDataDictionary(source.ViewData) { Model = model },
            new TempDataDictionary(source.HttpContext, new MemoryTempData()), writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    private sealed class FixtureUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context) => new FixtureUrlHelper(context);
    }

    private sealed class FixtureUrlHelper(ActionContext context) : IUrlHelper
    {
        public ActionContext ActionContext => context;
        public string? Action(UrlActionContext action)
        {
            var path = $"/{action.Controller}/{action.Action}";
            if (!context.HttpContext.Items.ContainsKey("fixture.url-values")) return path;
            var values = new RouteValueDictionary(action.Values).Where(x => x.Value != null)
                .ToDictionary(x => x.Key, x => (string?)Convert.ToString(x.Value, System.Globalization.CultureInfo.InvariantCulture));
            return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(path, values);
        }
        public string? Content(string? path) => path?.Replace("~/", "/", StringComparison.Ordinal);
        public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true && !url.StartsWith("//", StringComparison.Ordinal);
        public string? Link(string? name, object? values) => throw new NotSupportedException();
        public string? RouteUrl(UrlRouteContext context) => throw new NotSupportedException();
    }
}
