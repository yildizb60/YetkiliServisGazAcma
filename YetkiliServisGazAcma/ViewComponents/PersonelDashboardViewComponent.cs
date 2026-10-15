using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models.ViewModels;

namespace YetkiliServisGazAcma.ViewComponents;

public sealed class PersonelDashboardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(PersonelDashboardDto ozet)
    {
        var ykc = ozet.Ykc;
        var belgeYetkisi = ozet.BelgeYetkisi;
        var raporYetkisi = ozet.RaporYetkisi;
        var servisYetkisi = ozet.ServisYetkisi;
        var markaYetkisi = ozet.MarkaYetkisi;
        var ykcTalepYetkisi = ozet.YkcYetkileri.TalepleriGorebilir;
        var ykcAtamaYetkisi = ozet.YkcYetkileri.AtamaYapabilir;
        var ykcImzaYetkisi = ozet.YkcYetkileri.Fr265ImzaIslemiYapabilir;
        var ykcRaporYetkisi = ozet.YkcYetkileri.RaporlariGorebilir;
        var actions = new List<DashboardTileViewModel>();
        var quickActions = new List<DashboardTileViewModel>();
        var facts = new List<RoleDashboardFactViewModel>();

        var onayBekleyen = ozet.OnayBekleyen;
        var toplamDevreyeAlma = ozet.ToplamDevreyeAlma;
        var buAyDevreyeAlma = ozet.BuAyDevreyeAlma;
        var servisAktif = ozet.AktifServis;

        if (belgeYetkisi)
        {
            var url = Url.Action("OnayBekleyenler", "PersonelPanel")!;
            actions.Add(new DashboardTileViewModel
            {
                Title = "Yetki Belgesi Onayları", Url = url, Icon = "bi bi-file-earmark-check",
                MetricLabel = "Belgeleri değerlendir", Tone = "amber", Featured = onayBekleyen > 0
            });
            quickActions.Add(new DashboardTileViewModel
            {
                Title = "Karar bekleyen yetki belgeleri", Url = url, Icon = "bi bi-file-earmark-check",
                MetricValue = onayBekleyen.ToString(), MetricLabel = "bekleyen", Subtitle = "Yetki belgesi işlemleri",
                Tone = "amber", Featured = onayBekleyen > 0
            });
            facts.Add(new RoleDashboardFactViewModel { Icon = "bi bi-file-earmark-check", Value = onayBekleyen.ToString(), Label = "onay bekleyen belge", Url = url });
        }

        if (ykcTalepYetkisi)
        {
            var taleplerUrl = Url.Action("Talepler", "Ykc")!;
            var takvimUrl = Url.Action("Takvim", "Ykc")!;
            actions.Add(new DashboardTileViewModel
            {
                Title = "Cihaz Değişim İşlemleri", Url = taleplerUrl, Icon = "bi bi-fire",
                MetricLabel = "Talep ve randevuları yönet", Tone = "blue",
                Featured = (ykc?.Incelemede ?? 0) > 0
            });
            quickActions.Add(new DashboardTileViewModel
            {
                Title = "Kontrol randevularını görüntüle", Url = takvimUrl, Icon = "bi bi-calendar2-week",
                MetricValue = (ykc?.RandevuSaha ?? 0).ToString(), MetricLabel = "kontrolde", Subtitle = "Randevu takvimi", Tone = "blue"
            });
            facts.Add(new RoleDashboardFactViewModel { Icon = "bi bi-calendar-check", Value = (ykc?.RandevuSaha ?? 0).ToString(), Label = "kontrol sürecindeki talep", Url = takvimUrl });
        }

        if (raporYetkisi)
        {
            var url = Url.Action("DevreyeAlmalar", "PersonelPanel")!;
            var ayBasi = ozet.AyBaslangici;
            var buAyUrl = Url.Action("DevreyeAlmalar", "PersonelPanel", new
            {
                bas = ayBasi.ToString("yyyy-MM-dd"), bit = ozet.AyBitisi.ToString("yyyy-MM-dd")
            })!;
            actions.Add(new DashboardTileViewModel
            {
                Title = "Cihaz Devreye Alma Kayıtları", Url = url, Icon = "bi bi-tools",
                MetricLabel = "Kayıtları görüntüle", Tone = "green"
            });
            quickActions.Add(new DashboardTileViewModel
            {
                Title = "Cihaz devreye alma arşivi", Url = url, Icon = "bi bi-archive",
                MetricValue = toplamDevreyeAlma.ToString(), MetricLabel = "kayıt", Subtitle = "Kayıt arşivi", Tone = "green"
            });
            facts.Add(new RoleDashboardFactViewModel { Icon = "bi bi-tools", Value = buAyDevreyeAlma.ToString(), Label = "bu ay kaydedilen cihaz", Url = buAyUrl });
        }

        if (servisYetkisi)
        {
            var url = Url.Action("YetkiliServisler", "PersonelPanel")!;
            actions.Add(new DashboardTileViewModel
            {
                Title = "Yetkili Servis Yönetimi", Url = url, Icon = "bi bi-building-check",
                MetricLabel = "Servisleri yönet", Tone = "cyan"
            });
            quickActions.Add(new DashboardTileViewModel
            {
                Title = "Aktif yetkili servis kayıtları", Url = url, Icon = "bi bi-building-check",
                MetricValue = servisAktif.ToString(), MetricLabel = "aktif", Subtitle = "Yetkili servis yönetimi", Tone = "cyan"
            });
            facts.Add(new RoleDashboardFactViewModel { Icon = "bi bi-building-check", Value = servisAktif.ToString(), Label = "aktif yetkili servis", Url = url });
        }

        if (markaYetkisi)
        {
            var url = Url.Action("Markalar", "PersonelPanel")!;
            actions.Add(new DashboardTileViewModel
            {
                Title = "Yetkili Servis Markaları", Url = url, Icon = "bi bi-tags",
                MetricLabel = "Markaları yönet", Tone = "cyan"
            });
            quickActions.Add(new DashboardTileViewModel
            {
                Title = "Marka kapsamını düzenle", Url = url, Icon = "bi bi-tags",
                MetricValue = "Aç", Subtitle = "Marka yönetimi", Tone = "cyan"
            });
        }

        if (ykcRaporYetkisi)
        {
            var url = Url.Action("Raporlar", "Ykc")!;
            actions.Add(new DashboardTileViewModel
            {
                Title = "Cihaz Değişim Raporları", Url = url, Icon = "bi bi-bar-chart-line",
                MetricLabel = "Raporları görüntüle", Tone = "blue"
            });
            quickActions.Add(new DashboardTileViewModel
            {
                Title = "Cihaz değişim raporlarını aç", Url = url, Icon = "bi bi-bar-chart-line",
                MetricValue = "Aç", Subtitle = "Raporlama", Tone = "blue"
            });
        }

        if (actions.Count < 4 && belgeYetkisi)
        {
            actions.Add(new DashboardTileViewModel
            {
                Title = "Yetki Belgesi Geçmişi", Url = Url.Action("OnayGecmisi", "PersonelPanel")!,
                Icon = "bi bi-clock-history", MetricLabel = "Karar geçmişi", Tone = "green"
            });
        }
        if (actions.Count < 4 && ykcTalepYetkisi)
        {
            actions.Add(new DashboardTileViewModel
            {
                Title = "Kontrol Randevuları", Url = Url.Action("Takvim", "Ykc")!,
                Icon = "bi bi-calendar2-week", MetricLabel = "Takvimi görüntüle", Tone = "cyan"
            });
        }

        var profilUrl = Url.Action("Profil", "PersonelPanel")!;
        if (actions.Count == 0)
        {
            actions.Add(new DashboardTileViewModel
            {
                Title = "Profilim", Url = profilUrl, Icon = "bi bi-person-circle",
                MetricLabel = "Hesap bilgilerini görüntüle", Tone = "blue"
            });
        }
        if (quickActions.Count < 3)
        {
            quickActions.Add(new DashboardTileViewModel
            {
                Title = "Profil bilgilerim", Url = profilUrl, Icon = "bi bi-person-circle",
                MetricValue = "Aç", Subtitle = "Hesabım", Tone = "blue"
            });
        }
        if (facts.Count == 0)
            facts.Add(new RoleDashboardFactViewModel { Icon = "bi bi-person-check", Value = "Aktif", Label = "personel hesabı", Url = profilUrl });

        var bekleyenIsler = new List<DashboardTileViewModel>();
        void BekleyenIsEkle(string islem, int? adet, string ikon, string ton)
        {
            bekleyenIsler.Add(new DashboardTileViewModel
            {
                Title = YkcBekleyenIsDegerleri.Etiket(islem)!,
                Url = Url.Action("Talepler", "Ykc", new { bekleyenIs = islem })!,
                Icon = ikon, Tone = ton, MetricValue = adet?.ToString() ?? "-",
                MetricLabel = "talep", Featured = adet > 0 && bekleyenIsler.Count == 0
            });
        }
        if (ykcTalepYetkisi && ykcAtamaYetkisi)
        {
            BekleyenIsEkle(YkcBekleyenIsDegerleri.Inceleme, ykc?.IncelemeBekleyen, "bi bi-inbox", "blue");
            BekleyenIsEkle(YkcBekleyenIsDegerleri.Randevu, ykc?.RandevuBekleyen, "bi bi-calendar-plus", "cyan");
        }
        if (ykcTalepYetkisi && ykcImzaYetkisi)
            BekleyenIsEkle(YkcBekleyenIsDegerleri.Tamamlama, ykc?.TamamlamaBekleyen, "bi bi-file-earmark-check", "green");
        quickActions.InsertRange(0, bekleyenIsler);

        var dashboardModel = new RoleDashboardViewModel
        {
            Variant = "personnel",
            AriaLabel = "Personel operasyon özeti",
            HeroTitle = "Personel Paneli",
            HeroKicker = ozet.Bugun.ToString("dd MMMM yyyy, dddd", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")),
            ActionsTitle = "Görev Alanlarım",
            QuickActionsTitle = "Hızlı İşlemler",
            QuickActionsMeta = "Güncel durum",
            ShowYkcCalendar = ykcTalepYetkisi,
            ShowGeneralCalendar = !ykcTalepYetkisi,
            Facts = facts,
            Actions = actions,
            QuickActions = quickActions
        };
        return View("~/Views/Shared/_RoleDashboard.cshtml", dashboardModel);
    }
}
