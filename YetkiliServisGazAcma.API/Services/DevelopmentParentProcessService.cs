using System.Diagnostics;
using System.Globalization;

namespace YetkiliServisGazAcma.API.Services;

public sealed class DevelopmentParentProcessService(
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ILogger<DevelopmentParentProcessService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var parentIdValue = Environment.GetEnvironmentVariable("YSGA_DEV_PARENT_PID");
        if (!environment.IsDevelopment() || string.IsNullOrWhiteSpace(parentIdValue))
            return;

        if (!int.TryParse(parentIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parentId)
            || parentId <= 0
            || !long.TryParse(Environment.GetEnvironmentVariable("YSGA_DEV_PARENT_START_TICKS"),
                NumberStyles.None, CultureInfo.InvariantCulture, out var parentStartTicks))
        {
            logger.LogWarning("Otomatik API ust surec bilgisi gecersiz; API kapatiliyor.");
            lifetime.StopApplication();
            return;
        }

        try
        {
            using var parent = Process.GetProcessById(parentId);
            // PID reuse must not attach the API lifetime to an unrelated process.
            if (parent.StartTime.ToUniversalTime().Ticks == parentStartTicks)
                await parent.WaitForExitAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogDebug(ex, "Otomatik API ust sureci artik izlenemiyor.");
        }

        if (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Web sureci kapandi; otomatik baslatilan yerel API kapatiliyor.");
            lifetime.StopApplication();
        }
    }
}
