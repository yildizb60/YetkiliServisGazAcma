using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class LocalApiProcessService : BackgroundService
    {
        private readonly ApiIntegrationOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LocalApiProcessService> _logger;
        private Process? _process;
        private bool _portConflictLogged;

        public LocalApiProcessService(
            IOptions<ApiIntegrationOptions> options,
            IWebHostEnvironment environment,
            ILogger<LocalApiProcessService> logger)
        {
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_environment.IsDevelopment()
                || !_options.Enabled
                || !_options.AutoStartLocalApiInDevelopment
                || !IsLocalApiUrl(_options.BaseUrl))
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var apiHazir = await IsApiReadyAsync(stoppingToken);
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    if (!apiHazir)
                    {
                        if (await IsApiPortInUseAsync(stoppingToken))
                        {
                            if (!_portConflictLogged)
                            {
                                _logger.LogWarning(
                                    "Yerel API adresi kullanimda ancak API hazirlik denetimi basarisiz. Ikinci bir API sureci baslatilmadi: {Url}",
                                    _options.BaseUrl);
                                _portConflictLogged = true;
                            }
                        }
                        else
                        {
                            _portConflictLogged = false;
                            StartLocalApi();
                            await WaitForApiAsync(stoppingToken);
                        }
                    }
                    else
                    {
                        _portConflictLogged = false;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Yerel API izlenirken beklenmeyen bir hata olustu.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private void StartLocalApi()
        {
            if (_process is { HasExited: false })
                return;

            if (_process != null)
            {
                _logger.LogWarning(
                    "Yerel API sureci beklenmedik sekilde kapandi. ExitCode: {ExitCode}. Yeniden baslatiliyor.",
                    _process.ExitCode);
                _process.Dispose();
                _process = null;
            }

            var apiProjectPath = ResolveApiProjectPath();
            if (apiProjectPath == null)
            {
                _logger.LogWarning("Yerel API otomatik baslatilamadi. API proje dosyasi bulunamadi.");
                return;
            }

            var apiProjectDirectory = Path.GetDirectoryName(apiProjectPath)!;
            var apiUrl = _options.BaseUrl.TrimEnd('/');
            var apiAssemblyPath = ResolveApiAssemblyPath(apiProjectDirectory);
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = apiProjectDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (apiAssemblyPath != null)
            {
                startInfo.ArgumentList.Add(apiAssemblyPath);
            }
            else
            {
                startInfo.ArgumentList.Add("run");
                startInfo.ArgumentList.Add("--project");
                startInfo.ArgumentList.Add(apiProjectPath);
                startInfo.ArgumentList.Add("--no-launch-profile");
                startInfo.ArgumentList.Add("-p:UseAppHost=false");
            }

            startInfo.ArgumentList.Add("--urls");
            startInfo.ArgumentList.Add(apiUrl);
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            using var parent = Process.GetCurrentProcess();
            startInfo.Environment["YSGA_DEV_PARENT_PID"] = parent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            startInfo.Environment["YSGA_DEV_PARENT_START_TICKS"] = parent.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);

            try
            {
                _process = Process.Start(startInfo);
                if (_process == null)
                {
                    _logger.LogWarning("Yerel API sureci baslatilamadi.");
                    return;
                }

                _logger.LogInformation(
                    "Yerel API gelistirme icin baslatildi. Pid: {Pid}, Url: {Url}, Kaynak: {Kaynak}",
                    _process.Id,
                    apiUrl,
                    apiAssemblyPath ?? apiProjectPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yerel API otomatik baslatilirken hata olustu.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop the restart loop before terminating the child process.
            await base.StopAsync(cancellationToken);
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);

                _process?.Dispose();
                _process = null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Yerel API sureci kapatilirken hata olustu.");
            }
        }

        private async Task WaitForApiAsync(CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));

            while (!timeoutCts.IsCancellationRequested)
            {
                if (await IsApiReadyAsync(timeoutCts.Token))
                    return;

                if (_process is { HasExited: true })
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

        private async Task<bool> IsApiPortInUseAsync(CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var uri))
                return false;

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(uri.Host, uri.Port, cancellationToken);
                return client.Connected;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (SocketException)
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

        private static string? ResolveApiAssemblyPath(string apiProjectDirectory)
        {
            var debugDirectory = Path.Combine(apiProjectDirectory, "bin", "Debug");
            if (!Directory.Exists(debugDirectory))
                return null;

            return Directory
                .EnumerateFiles(
                    debugDirectory,
                    "YetkiliServisGazAcma.API.dll",
                    SearchOption.AllDirectories)
                .Where(path => File.Exists(Path.ChangeExtension(path, ".deps.json")))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
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
