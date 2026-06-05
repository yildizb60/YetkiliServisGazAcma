using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class LocalApiProcessService : IHostedService
    {
        private readonly ApiIntegrationOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LocalApiProcessService> _logger;
        private Process? _process;

        public LocalApiProcessService(
            IOptions<ApiIntegrationOptions> options,
            IWebHostEnvironment environment,
            ILogger<LocalApiProcessService> logger)
        {
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment()
                || !_options.Enabled
                || !_options.AutoStartLocalApiInDevelopment
                || !IsLocalApiUrl(_options.BaseUrl))
            {
                return;
            }

            if (await IsApiReadyAsync(cancellationToken))
                return;

            var apiProjectPath = ResolveApiProjectPath();
            if (apiProjectPath == null)
            {
                _logger.LogWarning("Yerel API otomatik baslatilamadi. API proje dosyasi bulunamadi.");
                return;
            }

            var apiProjectDirectory = Path.GetDirectoryName(apiProjectPath)!;
            var apiUrl = _options.BaseUrl.TrimEnd('/');

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = apiProjectDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(apiProjectPath);
            startInfo.ArgumentList.Add("--urls");
            startInfo.ArgumentList.Add(apiUrl);
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

            try
            {
                _process = Process.Start(startInfo);
                if (_process == null)
                {
                    _logger.LogWarning("Yerel API sureci baslatilamadi.");
                    return;
                }

                _logger.LogInformation("Yerel API gelistirme icin baslatildi. Pid: {Pid}, Url: {Url}", _process.Id, apiUrl);
                await WaitForApiAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yerel API otomatik baslatilirken hata olustu.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Yerel API sureci kapatilirken hata olustu.");
            }

            return Task.CompletedTask;
        }

        private async Task WaitForApiAsync(CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));

            while (!timeoutCts.IsCancellationRequested)
            {
                if (await IsApiReadyAsync(timeoutCts.Token))
                    return;

                await Task.Delay(750, timeoutCts.Token).ContinueWith(_ => { }, CancellationToken.None);
            }
        }

        private async Task<bool> IsApiReadyAsync(CancellationToken cancellationToken)
        {
            try
            {
                var baseUri = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
                using var handler = CreateDevelopmentHandler(baseUri);
                using var client = new HttpClient(handler)
                {
                    BaseAddress = baseUri,
                    Timeout = TimeSpan.FromSeconds(2)
                };

                using var response = await client.GetAsync("swagger/v1/swagger.json", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private string? ResolveApiProjectPath()
        {
            var webRoot = _environment.ContentRootPath;
            var candidate = Path.GetFullPath(Path.Combine(
                webRoot,
                "..",
                "YetkiliServisGazAcma.API",
                "YetkiliServisGazAcma.API.csproj"));

            return File.Exists(candidate) ? candidate : null;
        }

        private static bool IsLocalApiUrl(string? baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                return false;

            return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
        }

        private HttpClientHandler CreateDevelopmentHandler(Uri baseUri)
        {
            var handler = new HttpClientHandler();

            if (string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && IsLocalApiUrl(baseUri.ToString()))
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        }
    }
}
