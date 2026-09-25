namespace YetkiliServisGazAcma.Models.ViewModels
{
    public sealed class RoleDashboardViewModel
    {
        public string Variant { get; set; } = "default";
        public string AriaLabel { get; set; } = "Rol bazlı ana sayfa";
        public string HeroTitle { get; set; } = string.Empty;
        public string HeroKicker { get; set; } = string.Empty;
        public string HeroKickerIcon { get; set; } = "bi bi-calendar3";
        public string HeroImageUrl { get; set; } = "/images/hero/personel-operasyon-dashboard.png";
        public string ActionsTitle { get; set; } = "Görev Alanlarım";
        public string QuickActionsTitle { get; set; } = "Hızlı İşlemler";
        public string QuickActionsMeta { get; set; } = "Güncel durum";
        public bool ShowYkcCalendar { get; set; }
        public string? YkcCalendarTitle { get; set; }
        public AuthorizedServiceCalendarViewModel? AuthorizedServiceCalendar { get; set; }
        public List<RoleDashboardFactViewModel> Facts { get; set; } = new();
        public List<DashboardTileViewModel> Actions { get; set; } = new();
        public List<DashboardTileViewModel> QuickActions { get; set; } = new();
        public RoleDashboardSidePanelViewModel? SidePanel { get; set; }
    }

    public sealed class RoleDashboardFactViewModel
    {
        public string Icon { get; set; } = "bi bi-check2-square";
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public sealed class RoleDashboardSidePanelViewModel
    {
        public string Title { get; set; } = "Güncel Özet";
        public string Icon { get; set; } = "bi bi-grid-1x2";
        public string ItemsTitle { get; set; } = "Öncelikler";
        public string? ActionText { get; set; }
        public string? ActionUrl { get; set; }
        public List<RoleDashboardMetricViewModel> Metrics { get; set; } = new();
        public List<DashboardTileViewModel> Items { get; set; } = new();
    }

    public sealed class RoleDashboardMetricViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi bi-activity";
        public string Tone { get; set; } = "blue";
        public string? Url { get; set; }
    }

    public sealed class AuthorizedServiceCalendarViewModel
    {
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string ViewMode { get; set; } = "ay";
        public string HistoryUrl { get; set; } = "/devreyealma/gecmis";
        public bool IsDataComplete { get; set; } = true;
        public List<AuthorizedServiceCalendarItemViewModel> Items { get; set; } = new();
    }

    public sealed class AuthorizedServiceCalendarItemViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string InstallationNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string StatusCssClass { get; set; } = "df-pill-warning";
        public bool IsComplete { get; set; }
        public string DetailsUrl { get; set; } = string.Empty;
    }
}
