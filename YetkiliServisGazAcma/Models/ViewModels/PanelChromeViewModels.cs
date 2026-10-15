namespace YetkiliServisGazAcma.Models.ViewModels;

public sealed class SidebarViewModel
{
    public bool ServicePanel { get; init; }
    public string HomeUrl { get; init; } = "";
    public string HomeActive { get; init; } = "";
    public string LogoutUrl { get; init; } = "";
    public string? LogoUrl { get; init; }
    public string CompanyName { get; init; } = "Şirket";
    public List<SidebarGroup> Groups { get; init; } = [];
}

public sealed record SidebarLink(string Text, string Url, string Icon, string Active, int Badge = 0);
public sealed record SidebarGroup(string Title, string Icon, List<SidebarLink> Links)
{
    public bool Expanded => Links.Any(x => x.Active.Length > 0);
}

public sealed class NavbarViewModel
{
    public string Name { get; init; } = "";
    public string Initial { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Module { get; init; }
    public string CompanyName { get; init; } = "";
    public string Role { get; init; } = "";
    public string? HomeUrl { get; init; }
    public string? ProfileUrl { get; init; }
    public string? LogoutUrl { get; init; }
    public PanelNotificationsViewModel Notifications { get; init; } = new();
}

public sealed class PanelNotificationsViewModel
{
    public List<PanelCompanyOption> Companies { get; init; } = [];
    public bool ShowCompanySelector { get; init; }
    public bool AllCompaniesAllowed { get; init; }
    public bool AllCompaniesSelected { get; init; }
    public string CompanySelectionUrl { get; init; } = "";
    public string ReturnUrl { get; init; } = "";
    public string Scope { get; init; } = "";
    public List<PanelNotification> Items { get; init; } = [];
    public int Count => Items.Count;
    public string CountLabel => Count > 99 ? "99+" : Count.ToString();
}

public sealed record PanelCompanyOption(int Id, string Name, bool Selected);
public sealed record PanelNotification(string Key, string Title, string Text, string Time,
    string Icon, string Tone, string Path, int Type);
