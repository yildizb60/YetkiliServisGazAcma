using System.Net;
using System.Security.Claims;
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
using YetkiliServisGazAcma.Models.ViewModels;
using YetkiliServisGazAcma.ViewComponents;

internal static class PanelChromeRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
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


        foreach (var area in new[] { "/AdminPanel", "/personel-panel", "/ys-devreyeal" })
        {
            var context = Context("Personel", 2, "Personel", scope.ServiceProvider);
            context.ViewData["DevreyeAlmaAlan"] = area;
            context.ViewData["FirmaIlceleri"] = new Dictionary<int, string> { [7] = "Merkez" };
            IReadOnlyList<DevreyeAlmaKayitDto> records = area == "/ys-devreyeal"
                ? new List<YsDevreyeAlmaDto> { new()
                {
                    Id = 42, FirmaId = 7, FirmaAdi = "Test firm", FirmaFaaliyetIli = "Çorum",
                    MusteriAdi = "<script>customer</script>", TesistatNo = "123456", SozlesmeNo = "000943",
                    MarkaAdi = "Vaillant", CihazModeli = "Long device model", CihazKapasite = "20000",
                    Adres = "Test address", Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
                } }
                : new List<AdminDevreyeAlmaDto> { new()
                {
                    Id = 42, FirmaId = 7, FirmaAdi = "Test firm", FirmaFaaliyetIli = "Çorum",
                    MusteriAdi = "<script>customer</script>", TesistatNo = "123456", SozlesmeNo = "000943",
                    MarkaAdi = "Vaillant", CihazModeli = "Long device model", CihazKapasite = "20000",
                    Adres = "Test address", Durum = DevreyeAlmaDurumDegerleri.Tamamlandi
                } };
            var html = await RenderAsync(scope.ServiceProvider, context, "/Views/Shared/_DevreyeAlmaTable.cshtml", records);
            Check(html.Contains("123456") && html.Contains("000943") && html.Contains("Vaillant")
                && html.Contains("20000") && html.Contains("Long device model") && html.Contains("Test address")
                && html.Contains("Merkez") && !html.Contains("<script>customer</script>"),
                area + ": shared DTO table keeps identifiers, device facts, address and HTML encoding");
            Check(html.Contains("df-commissioning-record-row") && html.Contains("df-commissioning-detail-row")
                && html.Contains(area == "/ys-devreyeal" ? "colspan=\"8\"" : "colspan=\"10\""),
                area + ": shared DTO table preserves the existing layout and detail column count");
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
        public string? Action(UrlActionContext action) => $"/{action.Controller}/{action.Action}";
        public string? Content(string? path) => path?.Replace("~/", "/", StringComparison.Ordinal);
        public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true && !url.StartsWith("//", StringComparison.Ordinal);
        public string? Link(string? name, object? values) => throw new NotSupportedException();
        public string? RouteUrl(UrlRouteContext context) => throw new NotSupportedException();
    }
}
