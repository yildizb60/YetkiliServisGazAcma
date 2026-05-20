using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using YetkiliServisGazAcma.Infrastructure;
using YetkiliServisGazAcma.Business.Services;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
QuestPDF.Settings.License = LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<PanelKimlikActionFilter>();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<DagitimSirketService>();
builder.Services.AddScoped<MarkaService>();
builder.Services.AddScoped<YetkiliServisService>();
builder.Services.AddScoped<SertifikaService>();
builder.Services.AddScoped<AdminDashboardService>();
builder.Services.AddScoped<AdminYetkiliServisListeService>();
builder.Services.AddScoped<SehirFirmaKoduService>();
builder.Services.AddScoped<AktifSirketService>();
builder.Services.AddScoped<PanelKimlikService>();
builder.Services.AddScoped<PanelKimlikActionFilter>();
builder.Services.AddScoped<ApiJwtTokenService>();
builder.Services.Configure<ApiIntegrationOptions>(builder.Configuration.GetSection("ApiIntegration"));
builder.Services.AddHttpClient<AdminDashboardApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
builder.Services.AddHttpClient<AdminKullaniciApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
builder.Services.AddHttpClient<AdminYetkiliServisApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
builder.Services.AddHttpClient<MarkaApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
builder.Services.AddHttpClient<DagitimSirketApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
builder.Services.AddHttpClient<YetkiliServisApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
builder.Services.AddHttpClient<UrunKategoriApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection("Sms"));
builder.Services.AddScoped<ISmsProvider, NullSmsProvider>();
builder.Services.AddScoped<SmsDogrulamaService>();

builder.Services.AddIdentity<AppKullanici, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Tek giriş sayfası
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/giris";
    options.LogoutPath = "/cikis";
    options.AccessDeniedPath = "/giris";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.Redirect("/giris");
        return System.Threading.Tasks.Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.Redirect("/giris");
        return System.Threading.Tasks.Task.CompletedTask;
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/ys-panel", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Path = "/ys-panel/index";
    }
    await next();
});
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "ys-panel-root",
    pattern: "ys-panel",
    defaults: new { controller = "YetkiliServisPanel", action = "Index" });

app.MapControllerRoute(
    name: "ys-panel",
    pattern: "ys-panel/{action=Index}/{id?}",
    defaults: new { controller = "YetkiliServisPanel" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<AppKullanici>>();
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();
    var dbContext = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await SeedData.Initialize(userManager, roleManager);

    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("TestData:SeedDemoUsers"))
        await TestDataSeed.Initialize(dbContext, userManager);
}

app.Run();

