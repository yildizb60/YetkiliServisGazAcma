using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class SmsDogrulamaTemizlikService(
    IServiceScopeFactory scopeFactory,
    IOptions<SmsOptions> options,
    TimeProvider clock,
    ILogger<SmsDogrulamaTemizlikService> logger) : BackgroundService
{
    private const int BatchSize = 500;
    private const int MaxBatches = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CodeCleanupEnabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.CodeCleanupIntervalHours), clock);
        try
        {
            do
            {
                try
                {
                    await TemizleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "SMS dogrulama kodlari temizlenemedi; sonraki calismada yeniden denenecek.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // API shutdown cancels both the database command and the timer.
        }
    }

    public async Task<int> TemizleAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.CodeCleanupEnabled)
            return 0;

        // Verification expiry is stored in local time by SmsDogrulamaService.
        var cutoff = clock.GetLocalNow().DateTime.AddDays(-options.Value.ExpiredCodeRetentionDays);
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var total = 0;
        for (var batch = 0; batch < MaxBatches; batch++)
        {
            var deleted = await context.SmsDogrulamaKodlari
                .Where(x => x.GecerlilikTarihi < cutoff)
                .OrderBy(x => x.Id)
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);
            total += deleted;
            if (deleted < BatchSize)
                break;
        }

        if (total > 0)
            logger.LogInformation("Saklama suresini asan {Count} SMS dogrulama kaydi temizlendi.", total);
        return total;
    }
}
