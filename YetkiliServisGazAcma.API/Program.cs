using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using System.Text;
using System.Security.Claims;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Business.Services.Online;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.API.Swagger;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddHostedService<DevelopmentParentProcessService>();

var publicCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("PublicApiCors", policy =>
    {
        if (publicCorsOrigins.Length > 0)
        {
            policy.WithOrigins(publicCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase)))
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var publicPermitLimit = Math.Max(1, builder.Configuration.GetValue<int?>("RateLimiting:PublicPermitLimit") ?? 120);
var publicQueueLimit = Math.Max(0, builder.Configuration.GetValue<int?>("RateLimiting:PublicQueueLimit") ?? 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("MobilImza", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Clamp(builder.Configuration.GetValue<int?>("RateLimiting:ImzaPermitLimit") ?? 120, 1, 1000), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("Authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddFixedWindowLimiter("PublicApi", limiter =>
    {
        limiter.PermitLimit = publicPermitLimit;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = publicQueueLimit;
    });
});

// Veritabanı
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection ayari eksik. appsettings.Local.json veya environment variable ile tanimlayin.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(defaultConnection));

// Identity
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

builder.Services.AddScoped<DagitimSirketService>();
builder.Services.AddScoped<MarkaService>();
builder.Services.AddScoped<YetkiliServisService>();
builder.Services.AddScoped<YetkiliServisIlkKurulumService>();
builder.Services.AddScoped<YetkiBelgesiService>();
builder.Services.AddScoped<YkcTalepService>();
builder.Services.AddSingleton<YkcSorguKaydiService>();
builder.Services.AddOptions<YkcPlanlamaOptions>()
    .Bind(builder.Configuration.GetSection("YkcPlanlama"))
    .Validate(x => x.AsgariAralikDakika is >= 0 and <= 240, "YkcPlanlama:AsgariAralikDakika 0-240 aralığında olmalıdır.")
    .Validate(x => x.Ekipler.All(e => !string.IsNullOrWhiteSpace(e.Id) && e.SirketId > 0
        && !string.IsNullOrWhiteSpace(e.Il) && !string.IsNullOrWhiteSpace(e.Bolge)
        && !string.IsNullOrWhiteSpace(e.Ad) && e.YonlendirmeTipi is "CRM187" or "Mühendis")
        && x.Ekipler.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() == x.Ekipler.Count,
        "YkcPlanlama:Ekipler şirket/il/bölge, benzersiz kimlik ve geçerli yönlendirme içermelidir.")
    .ValidateOnStart();
builder.Services.AddScoped<AdminDashboardService>();
builder.Services.AddScoped<AdminYetkiliServisListeService>();
builder.Services.AddScoped<SehirFirmaKoduService>();
builder.Services.AddScoped<AdminYetkiliServisYonetimApiService>();
builder.Services.AddScoped<AdminSubeApiService>();
builder.Services.AddScoped<AdminRaporApiService>();
builder.Services.AddScoped<AdminYetkiBelgesiOnayApiService>();
builder.Services.AddScoped<AdminPersonelYetkiApiService>();
builder.Services.AddScoped<YetkiliServisPanelYonetimApiService>();
builder.Services.AddScoped<DevreyeAlmaExportApiService>();
builder.Services.AddScoped<YkcFr265FormService>();
builder.Services.AddScoped<YkcYetkiService>();
builder.Services.AddScoped<YkcImzaAkisService>();
var ykcImzaProvider = builder.Configuration["YkcImza:Provider"];
builder.Services.Configure<MobilImzaOptions>(builder.Configuration.GetSection("YkcImza:Mobil"));
if (string.Equals(ykcImzaProvider, "Mobil", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IYkcImzaProvider, MobilYkcImzaProvider>();
}
else if (builder.Environment.IsDevelopment()
    && string.Equals(ykcImzaProvider, "Demo", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IYkcImzaProvider, DemoYkcImzaProvider>();
}
else
{
    builder.Services.AddSingleton<IYkcImzaProvider, YapilandirilmamisYkcImzaProvider>();
}
builder.Services.AddSmsServices(builder.Configuration);
builder.Services.AddSertifikaliFirmaKimlikServices(builder.Configuration);
builder.Services.AddDataProtection();
builder.Services.AddScoped<OturumTokenService>();
builder.Services.AddOptions<SmsOptions>()
    .Validate(options => builder.Environment.IsDevelopment() || !options.TestMode,
        "SMS TestMode yalnızca Development ortamında kullanılabilir.")
    .ValidateOnStart();
builder.Services.Configure<OnlineServiceOptions>(builder.Configuration.GetSection("OnlineService"));
builder.Services.AddHttpClient<OnlineCihazBilgileriClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OnlineServiceOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]!;
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key ayari eksik. appsettings.Local.json veya environment variable ile tanimlayin.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppKullanici>>();
                var user = await users.GetUserAsync(context.Principal!);
                var stamp = context.Principal!.FindFirstValue("stamp");
                if (user?.AktifMi != true || string.IsNullOrEmpty(stamp)
                    || stamp != await users.GetSecurityStampAsync(user)
                    || context.Principal!.FindFirstValue("KullaniciTipi") != user.KullaniciTipi.ToString())
                {
                    context.Fail("Oturum geçersiz.");
                    return;
                }
                var roles = await users.GetRolesAsync(user);
                if (!roles.OrderBy(x => x).SequenceEqual(context.Principal!.FindAll(ClaimTypes.Role).Select(x => x.Value).OrderBy(x => x)))
                {
                    context.Fail("Kullanıcı yetkileri değişti.");
                    return;
                }
                if (!KullaniciRolTutarlilikKurali.FirmaRolleriUyumlu(user.KullaniciTipi, roles))
                    context.Fail("Kullanıcı tipi ve firma rolleri uyuşmuyor.");
            }
        };
    });

builder.Services.AddAuthentication().AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, MobilImzaAuthenticationHandler>(MobilImzaAuthenticationHandler.SchemeName, _ => { });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(MobilImzaAuthenticationHandler.SchemeName, new AuthorizationPolicyBuilder(MobilImzaAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser().RequireClaim(MobilImzaAuthenticationHandler.CompanyClaim).Build());
    options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// Swagger — Token girişli, sadece api/ route'ları
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Yetkili Servis Gaz Açma API",
        Version = "v1",
        Description = "Yetkili servis yönetim sistemi API'si"
    });

    // Sadece api/ ile başlayan route'ları göster
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        return apiDesc.RelativePath?.StartsWith("api/") ?? false;
    });

    // JWT desteği
    c.AddSecurityDefinition("MobilImzaKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Imza-Key",
        Description = "Mobil imza entegrasyonu için şirketlerle sınırlandırılmış anahtar. Kullanıcı JWT token'ı değildir."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT token giriniz."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    c.OperationFilter<ApiSwaggerResponseOperationFilter>();
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    if (context.Request.Path.StartsWithSegments("/api/auth"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("GlobalExceptionHandler");

            if (feature?.Error != null)
                logger.LogError(feature.Error, "API istegi islenirken beklenmeyen hata olustu.");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Sunucu hatasi",
                Detail = "Istek islenirken beklenmeyen bir hata olustu."
            });
        });
    });
}

var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Yetkili Servis API v1");
        c.RoutePrefix = string.Empty;
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
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
app.UseStaticFiles();
app.UseRouting();
app.UseCors("PublicApiCors");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment()
    && (app.Configuration.GetValue<bool>("Seed:CreateDefaultUsers") || app.Configuration.GetValue<bool>("TestData:SeedDemoUsers")))
{
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppKullanici>>();
    await YetkiliServisGazAcma.API.Infrastructure.SeedData.Initialize(users,
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        app.Configuration.GetValue<bool>("Seed:CreateDefaultUsers"));
    if (app.Configuration.GetValue<bool>("TestData:SeedDemoUsers"))
        await YetkiliServisGazAcma.API.Infrastructure.TestDataSeed.Initialize(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(), users);
}
app.Run();
