using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using YetkiliServisGazAcma.Business.Services;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
QuestPDF.Settings.License = LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

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

builder.Services.AddScoped<SehirFirmaKodlari>();
builder.Services.AddScoped<ApiKullaniciOturumu>();
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
AddApiClient<YkcApiClient>();
AddApiClient<YetkiliServisPanelApiClient>();
AddApiClient<HomeOzetApiClient>();
AddApiClient<PanelKapsamApiClient>();
AddApiClient<AuthApiClient>();

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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/giris";
    options.LogoutPath = "/cikis";
    options.AccessDeniedPath = "/yetkisiz-erisim";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnValidatePrincipal = ApiKullaniciOturumu.ValidatePrincipalAsync;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.Redirect("/giris");
        return System.Threading.Tasks.Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.Redirect("/yetkisiz-erisim");
        return System.Threading.Tasks.Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();

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
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads/ykc")
        || context.Request.Path.StartsWithSegments("/yetki-belgeleri")
        || context.Request.Path.Equals("/uploads/demo-yetki-belgesi.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
// PDF.js character maps and fallback fonts are public library assets, not uploaded documents.
var pdfAssetTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
pdfAssetTypes.Mappings[".bcmap"] = "application/octet-stream";
pdfAssetTypes.Mappings[".pfb"] = "application/x-font-type1";
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/lib/pdfjs",
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(app.Environment.WebRootPath, "lib", "pdfjs")),
    ContentTypeProvider = pdfAssetTypes
});
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
app.Use(async (context, next) =>
{
    await next();

    var girisDogrulamaIstekMi =
        context.Request.Path.Equals("/giris/sms-dogrula", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.Equals("/giris/sifre-yenile", StringComparison.OrdinalIgnoreCase);

    if (!context.Response.HasStarted
        && context.Response.StatusCode == StatusCodes.Status400BadRequest
        && girisDogrulamaIstekMi)
    {
        context.Session.Clear();
        context.Response.Clear();
        context.Response.Redirect("/giris?temizle=true");
    }
});
app.UseAuthentication();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ApiOturumSuresiDolduException) when (!context.Response.HasStarted)
    {
        context.Response.Clear();
        context.Session.Clear();
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/giris?temizle=true");
    }
});
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

app.Run();

