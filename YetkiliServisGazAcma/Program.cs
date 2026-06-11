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
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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
    options.Filters.AddService<ApiIntegrationExceptionFilter>();
    options.Filters.AddService<PanelKimlikActionFilter>();
});

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection ayari eksik. appsettings.Local.json veya environment variable ile tanimlayin.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(defaultConnection));

builder.Services.AddScoped<DagitimSirketService>();
builder.Services.AddScoped<MarkaService>();
builder.Services.AddScoped<YetkiliServisService>();
builder.Services.AddScoped<AdminDashboardService>();
builder.Services.AddScoped<AdminYetkiliServisListeService>();
builder.Services.AddScoped<SehirFirmaKoduService>();
builder.Services.AddScoped<AktifSirketService>();
builder.Services.AddScoped<PanelKimlikService>();
builder.Services.AddScoped<PanelKimlikActionFilter>();
builder.Services.AddScoped<ApiIntegrationExceptionFilter>();
builder.Services.AddScoped<ApiJwtTokenService>();
builder.Services.Configure<ApiIntegrationOptions>(builder.Configuration.GetSection("ApiIntegration"));
builder.Services.AddHostedService<LocalApiProcessService>();
AddApiClient<AdminDashboardApiClient>();
AddApiClient<AdminKullaniciApiClient>();
AddApiClient<AdminYetkiliServisApiClient>();
AddApiClient<AdminYetkiBelgesiOnayApiClient>();
AddApiClient<AdminSubeApiClient>();
AddApiClient<AdminRaporApiClient>();
AddApiClient<YetkiBelgesiApiClient>();
AddApiClient<MarkaApiClient>();
AddApiClient<DagitimSirketApiClient>();
AddApiClient<YetkiliServisApiClient>();
AddApiClient<UrunKategoriApiClient>();
AddApiClient<PersonelPanelApiClient>();
AddApiClient<YetkiliServisDevreyeAlmaApiClient>();
AddApiClient<YetkiliServisPanelApiClient>();
AddApiClient<HomeOzetApiClient>();
AddApiClient<PanelKapsamApiClient>();
builder.Services.AddSmsServices(builder.Configuration);

void AddApiClient<TClient>() where TClient : class
{
    builder.Services.AddHttpClient<TClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
    })
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<ApiIntegrationOptions>>().Value;
        var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();

        if (environment.IsDevelopment()
            && Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && IsLocalApiHost(uri.Host))
        {
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        }

        return new HttpClientHandler();
    });
}

static bool IsLocalApiHost(string host)
{
    return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
}

builder.Services.AddIdentity<AppKullanici, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
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

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

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

