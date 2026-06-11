using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class SmsServiceCollectionExtensions
    {
        public static IServiceCollection AddSmsServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SmsOptions>(configuration.GetSection("Sms"));

            services.AddHttpClient<AhlatciSmsProvider>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<SmsOptions>>().Value;
                var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                    ? "https://smsnviapi.ahlatci.com.tr"
                    : options.BaseUrl.TrimEnd('/');

                client.BaseAddress = new Uri(baseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            });

            services.AddScoped<NullSmsProvider>();
            services.AddScoped<ISmsProvider>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<SmsOptions>>().Value;

                return string.Equals(options.Provider, "AhlatciSms", StringComparison.OrdinalIgnoreCase)
                    ? serviceProvider.GetRequiredService<AhlatciSmsProvider>()
                    : serviceProvider.GetRequiredService<NullSmsProvider>();
            });
            services.AddScoped<SmsDogrulamaService>();

            return services;
        }
    }
}
