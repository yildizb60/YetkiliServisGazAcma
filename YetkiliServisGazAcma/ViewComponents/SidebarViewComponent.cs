using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models.ViewModels;

namespace YetkiliServisGazAcma.ViewComponents;

public sealed class SidebarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        // The panel filter supplies API-verified permissions; this component only composes the menu.
        var path = HttpContext.Request.Path.ToString();
        var activeMenu = ViewData["ActiveMenu"]?.ToString() ?? "";
        var panelArea = ViewData["PanelArea"]?.ToString() ?? "";
        var isPersonelPanel = User.IsInRole("Personel") || string.Equals(panelArea, "Personel", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(panelArea) && path.StartsWith("/personel-panel", StringComparison.OrdinalIgnoreCase));
        var isYetkiliServisPanel = string.Equals(panelArea, "YetkiliServis", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(panelArea) && (
                path.StartsWith("/ys-panel", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/ys-devreyeal", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/ys-yetki-belgesi", StringComparison.OrdinalIgnoreCase)));
        var isSertifikaliFirmaPanel = string.Equals(panelArea, "SertifikaliFirma", StringComparison.OrdinalIgnoreCase)
            || (User.IsInRole("SertifikaliFirma") && path.StartsWith("/ykc", StringComparison.OrdinalIgnoreCase));
        var genelSistemAdminMi = (ViewData["GenelSistemAdminMi"] as bool?)
            ?? (User.IsInRole("GenelSistemAdmin") || User.IsInRole("SuperAdmin"));
        var ykcYetkileri = ViewData["YkcYetkileri"] as YkcYetkiOzeti ?? new YkcYetkiOzeti();

        string Link(string action, string controller) =>
            Url.Action(action, controller) ?? throw new InvalidOperationException($"Menü bağlantısı bulunamadı: {controller}.{action}");

        string Active(string key, params string[] prefixes)
        {
            if (string.Equals(activeMenu, key, StringComparison.OrdinalIgnoreCase))
                return " active";

            return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                ? " active"
                : "";
        }

        string ActiveExact(string key, params string[] exactPaths)
        {
            if (string.Equals(activeMenu, key, StringComparison.OrdinalIgnoreCase))
                return " active";

            return exactPaths.Any(exact => string.Equals(path, exact, StringComparison.OrdinalIgnoreCase))
                ? " active"
                : "";
        }

        string ActiveUnless(string key, string excludedActiveMenu, params string[] prefixes)
        {
            if (string.Equals(activeMenu, excludedActiveMenu, StringComparison.OrdinalIgnoreCase)
                || string.Equals(activeMenu, "YkcTakvim", StringComparison.OrdinalIgnoreCase))
                return "";

            return Active(key, prefixes);
        }

        var onayBekleyenlerUrl = Link("OnayBekleyenler", isPersonelPanel ? "PersonelPanel" : "AdminPanel");
        var groups = new List<SidebarGroup>();
        void Group(string title, string icon, params (string Text, string Url, string Icon, string Active)[] links)
        {
            if (links.Length > 0) groups.Add(new SidebarGroup(title, icon,
                links.Select(x => new SidebarLink(x.Text, x.Url, x.Icon, x.Active,
                    x.Url == onayBekleyenlerUrl ? (ViewData["OnayBekleyen"] as int? ?? 0) : 0)).ToList()));
        }
        var home = isYetkiliServisPanel ? Link("Index", "YetkiliServisPanel")
            : isSertifikaliFirmaPanel ? Link("Index", "Ykc")
            : isPersonelPanel ? Link("Index", "PersonelPanel")
            : Link("Index", "AdminPanel");
        var homeActive = ActiveExact(isSertifikaliFirmaPanel ? "YkcOzet" : "Dashboard", home, home + "/index");
        if (isYetkiliServisPanel)
        {
            Group("Cihaz Devreye Alma İşlemleri", "bi-bag-check",
                ("Yeni Cihaz Devreye Alma Kaydı", Link("Index", "DevreyeAlma"), "bi-plus-lg", ActiveExact("DevreyeAl", "/ys-devreyeal")),
                ("Cihaz Devreye Alma Geçmişi", Link("Gecmis", "DevreyeAlma"), "bi-clock-history", Active("DevreyeGecmis", "/ys-devreyeal/gecmis")),
                ("Cihaz Devreye Alma Raporları", Link("Raporlar", "YetkiliServisPanel"), "bi-bar-chart-line", Active("Raporlar", "/ys-panel/raporlar")));
            Group("Yetkili Servis İşlemleri", "bi-tools",
                ("Yetki Belgem", Link("Index", "YetkiBelgesi"), "bi-file-earmark-check", Active("YetkiBelgesi", "/ys-yetki-belgesi")),
                ("Hizmet Verdiğim Markalar", Link("Markalar", "YetkiliServisPanel"), "bi-tag", Active("Markalar", "/ys-panel/markalar")),
                ("Şubelerim", Link("Subeler", "YetkiliServisPanel"), "bi-building", Active("Subeler", "/ys-panel/subeler")));
            Group("Hesabım", "bi-person-circle",
                ("Profilim", Link("Profil", "YetkiliServisPanel"), "bi-person", Active("Profil", "/ys-panel/profil")));
        }
        else if (isSertifikaliFirmaPanel)
        {
            var firmaYkc = new List<(string, string, string, string)>();
            if (ykcYetkileri.TalepOlusturabilir)
                firmaYkc.Add(("Yeni Cihaz Değişim Talebi", Link("Yeni", "Ykc"), "bi-plus-lg", Active("YkcYeni", "/ykc/yeni")));
            if (ykcYetkileri.TalepleriGorebilir)
            {
                firmaYkc.Add(("Cihaz Değişim Taleplerim", Link("Talepler", "Ykc"), "bi-journal-text", Active("YkcTalepler", "/ykc/talepler", "/ykc/detay", "/ykc/fr265")));
                firmaYkc.Add(("Cihaz Değişim Randevularım", Link("Takvim", "Ykc"), "bi-calendar3", Active("YkcTakvim", "/ykc/takvim")));
            }
            if (ykcYetkileri.RaporlariGorebilir)
                firmaYkc.Add(("Cihaz Değişim Raporları", Link("Raporlar", "Ykc"), "bi-bar-chart-line", Active("YkcRaporlar", "/ykc/raporlar")));
            Group("Cihaz Değişim İşlemleri", "bi-fire", firmaYkc.ToArray());
            Group("Hesabım", "bi-person-circle",
                ("Profilim", Link("Profil", "Ykc"), "bi-person", Active("Profil", "/ykc/profil")));
        }
        else
        {
            var area = isPersonelPanel ? "/personel-panel" : "/AdminPanel";
            if (!isPersonelPanel || ViewData["YetkiBelgesi"] is true)
            {
                var certificates = new List<(string, string, string, string)>();
                if (isPersonelPanel)
                    certificates.Add(("Belge Onay ve Takip", onayBekleyenlerUrl, "bi-file-earmark-check", Active("OnayBekleyenler", area + "/onay-bekleyenler", area + "/onay-gecmisi")));
                else
                {
                    certificates.Add(("Belge Onay ve Takip", onayBekleyenlerUrl, "bi-file-earmark-check", Active("OnayBekleyenler", area + "/onay-bekleyenler", area + "/onay-gecmisi", area + "/yetki-belgesi-uyarilari")));
                }
                Group("Yetki Belgesi İşlemleri", "bi-file-earmark-check", certificates.ToArray());
            }
            if (!isPersonelPanel || ViewData["YetkiRapor"] is true)
            {
                Group("Cihaz Devreye Alma İşlemleri", "bi-bag-check",
                    ("Cihaz Devreye Alma Kayıtları", Link("DevreyeAlmalar", isPersonelPanel ? "PersonelPanel" : "AdminPanel"), "bi-list-check", Active("DevreyeAlmalar", area + "/devreyealmalar")));
            }
            var ykc = new List<(string, string, string, string)>();
            if (!isPersonelPanel || ykcYetkileri.TalepleriGorebilir)
            {
                ykc.Add(("Cihaz Değişim Talepleri", Link("Talepler", "Ykc"), "bi-journal-text", ActiveUnless("YkcTalepler", "YkcRaporlar", "/ykc/talepler", "/ykc/detay", "/ykc/yeni", "/ykc/fr265")));
                ykc.Add(("Yakıcı Cihaz Değişim Randevuları", Link("Takvim", "Ykc"), "bi-calendar3", Active("YkcTakvim", "/ykc/takvim")));
            }
            if (!isPersonelPanel || ykcYetkileri.RaporlariGorebilir)
                ykc.Add(("Cihaz Değişim Raporları", Link("Raporlar", "Ykc"), "bi-bar-chart-line", Active("YkcRaporlar", "/ykc/raporlar")));
            Group("Cihaz Değişim İşlemleri", "bi-fire", ykc.ToArray());
            var services = new List<(string, string, string, string)>();
            if (!isPersonelPanel || ViewData["YetkiServis"] is true)
                services.Add(("Yetkili Servisler", Link("YetkiliServisler", isPersonelPanel ? "PersonelPanel" : "AdminPanel"), "bi-tools", Active("YetkiliServisler", area + "/yetkiliservisler")));
            if ((!isPersonelPanel && genelSistemAdminMi) || (isPersonelPanel && ViewData["YetkiMarka"] is true))
            {
                var brands = isPersonelPanel ? Link("Markalar", "PersonelPanel") : Link("Index", "Marka");
                services.Add(("Yetkili Servis Markaları", brands, "bi-tag", Active("Markalar", brands)));
            }
            if (!isPersonelPanel)
                services.Add(("Şubeler", Link("Subeler", "AdminPanel"), "bi-building", Active("Subeler", area + "/subeler")));
            Group("Yetkili Servis Yönetimi", "bi-tools", services.ToArray());
            if (!isPersonelPanel)
                Group("Kullanıcı ve Yetki Yönetimi", "bi-people",
                    ("Personeller", Link("Personeller", "AdminPanel"), "bi-person-badge", Active("Personeller", area + "/personeller")),
                    ("Kullanıcılar", Link("Kullanicilar", "AdminPanel"), "bi-people", Active("Kullanicilar", area + "/kullanicilar")),
                    ("Yetkiler", Link("Yetkiler", "AdminPanel"), "bi-shield-check", Active("Yetkiler", area + "/yetkiler")));
            if (!isPersonelPanel || ViewData["YetkiRapor"] is true)
            {
                Group("Raporlama", "bi-bar-chart-line",
                    ("Raporlar", Link("Raporlar", isPersonelPanel ? "PersonelPanel" : "AdminPanel"), "bi-graph-up", Active("Raporlar", area + "/raporlar")));
            }
            Group("Hesabım", "bi-person-circle",
                ("Profilim", Link("Profil", isPersonelPanel ? "PersonelPanel" : "AdminPanel"), "bi-person", Active("Profil", area + "/profil")));
        }
        return View(new SidebarViewModel
        {
            ServicePanel = isYetkiliServisPanel, HomeUrl = home, HomeActive = homeActive,
            LogoutUrl = Link("Cikis", "Giris"), LogoUrl = ViewData["PanelLogoUrl"] as string,
            CompanyName = ViewData["PanelSirketAdi"] as string ?? "Şirket", Groups = groups
        });
    }
}
