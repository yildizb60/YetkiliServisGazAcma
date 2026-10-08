using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models.ViewModels;

namespace YetkiliServisGazAcma.ViewComponents;

public sealed class NavbarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var kullanici = ViewData["Kullanici"] as AppKullanici;
        var adSoyad = kullanici?.AdSoyad ?? "Sistem Kullanıcısı";
        var basHarf = !string.IsNullOrWhiteSpace(adSoyad) ? adSoyad.Trim().Substring(0, 1).ToUpper(new System.Globalization.CultureInfo("tr-TR")) : "K";
        var baslik = (ViewData["PanelTitle"] ?? ViewData["Title"])?.ToString() ?? "Panel";
        var activeMenu = ViewData["ActiveMenu"]?.ToString() ?? "";
        var altBaslik = ViewData["PanelModule"]?.ToString() ?? activeMenu switch
        {
            "DevreyeAl" or "DevreyeGecmis" or "DevreyeAlmalar" => "Cihaz Devreye Alma",
            "YetkiliServisler" or "Markalar" or "Subeler" => "Yetkili Servis Yönetimi",
            "YetkiBelgesi" or "OnayBekleyenler" or "OnayGecmisi" or "YetkiBelgesiUyarilari" => "Yetki Belgesi İşlemleri",
            "Kullanicilar" or "Personeller" or "Yetkiler" => "Kullanıcı ve Yetki Yönetimi",
            "Raporlar" => "Raporlama",
            "Profil" => "Hesabım",
            "YkcOzet" or "YkcYeni" or "YkcTalepler" or "YkcTakvim" or "YkcRaporlar" => "Cihaz Değişim İşlemleri",
            _ => null
        };
        var sirketAdi = ViewData["PanelSirketAdi"] as string ?? "Genel Yönetim";
        var panelController = kullanici?.KullaniciTipi switch
        {
            KullaniciTipiDegerleri.YetkiliServis => "YetkiliServisPanel",
            KullaniciTipiDegerleri.Personel => "PersonelPanel",
            KullaniciTipiDegerleri.SertifikaliFirma => "Ykc",
            _ => "AdminPanel"
        };
        var panelUrl = Url.Action("Index", panelController);
        var profilUrl = Url.Action("Profil", panelController);

        var rol = kullanici?.KullaniciTipi switch
        {
            1 => "Yetkili Servis",
            2 => "Personel",
            3 => (ViewData["GenelSistemAdminMi"] as bool? ?? false) ? "Genel Sistem Admini" : "Şirket Admini",
            4 => "Genel Sistem Admini",
            5 => "Sertifikalı Firma",
            _ => "Kullanıcı"
        };
        return View(new NavbarViewModel
        {
            Name = adSoyad, Initial = basHarf, Title = baslik,
            Module = string.Equals(altBaslik, baslik, StringComparison.CurrentCultureIgnoreCase) ? null : altBaslik,
            CompanyName = sirketAdi, Role = rol, HomeUrl = panelUrl, ProfileUrl = profilUrl,
            LogoutUrl = Url.Action("Cikis", "Giris"), Notifications = Notifications(panelController)
        });
    }

    private PanelNotificationsViewModel Notifications(string panelController)
    {
        var panelTipi = panelController == "PersonelPanel" ? "personel"
            : panelController == "AdminPanel" ? "admin" : "ys";

        var onayBekleyen = ViewData["OnayBekleyen"] as int? ?? 0;
        var suresiBitecek = ViewData["SuresiBitecek"] as int? ?? 0;
        var bildirimler = ViewData["Bildirimler"] as IEnumerable<string>;
        var aktifSirketler = (ViewData["AktifSirketler"] as IEnumerable<YetkiliServisGazAcma.Business.Services.PanelSirketDto>)?.ToList()
            ?? new List<YetkiliServisGazAcma.Business.Services.PanelSirketDto>();
        int? aktifSirketId = ViewData["AktifSirketId"] as int?;
        var genelSistemAdminMi = ViewData["GenelSistemAdminMi"] as bool? ?? false;
        var sirketSeciciGoster = HttpContext?.User?.Identity?.IsAuthenticated == true
            && (aktifSirketler.Count > 1 || genelSistemAdminMi);
        var returnUrl = (HttpContext?.Request?.Path.ToString() ?? "/")
            + (HttpContext?.Request?.QueryString.ToString() ?? "");

        var onayPath = Url.Action("OnayBekleyenler", panelTipi == "personel" ? "PersonelPanel" : "AdminPanel") ?? "#";
        var surePath = panelTipi == "ys"
            ? Url.Action("Index", "YetkiBelgesi") ?? "#"
            : Url.Action("YetkiBelgesiUyarilari", "AdminPanel") ?? "#";
        var bildirimKapsami = HttpContext?.User?.Identity?.Name ?? "anonim";
        var sirketSecimPath = Url.Action("SirketSec", "PanelSirket") ?? "/panel/sirket-sec";
        var satirlar = new List<PanelNotification>();

        if (bildirimler != null)
        {
            foreach (var b in bildirimler.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var sistemPath = panelTipi == "ys" ? Url.Action("Index", "YetkiliServisPanel") ?? "#" : onayPath;
                satirlar.Add(new PanelNotification($"sistem|{b}", "Sistem Bildirimi", b!, "Şimdi", "bi-chat-right", "df-notif-icon-primary", sistemPath, 3));
            }
        }

        if (onayBekleyen > 0)
        {
            satirlar.Add(new PanelNotification($"onay|{onayBekleyen}", "Yetki Belgesi Onayı", $"{onayBekleyen} adet yetki belgesi onay bekliyor.", "Bekleyen işlem", "bi-journal-text", "df-notif-icon-warning", onayPath, 1));
        }

        if (suresiBitecek > 0)
        {
            satirlar.Add(new PanelNotification($"sure|{suresiBitecek}", "Süre Uyarısı", $"{suresiBitecek} adet yetki belgesi 30 gün içinde bitiyor.", "Yaklaşan bitiş", "bi-clock", "df-notif-icon-danger", surePath, 2));
        }

        return new PanelNotificationsViewModel
        {
            ShowCompanySelector = sirketSeciciGoster, AllCompaniesAllowed = genelSistemAdminMi,
            AllCompaniesSelected = !aktifSirketId.HasValue, CompanySelectionUrl = sirketSecimPath,
            Companies = aktifSirketler.Select(x => new PanelCompanyOption(x.Id, x.SirketAdi ?? "Şirket", x.Id == aktifSirketId)).ToList(),
            ReturnUrl = returnUrl, Scope = bildirimKapsami, Items = satirlar
        };
    }
}
