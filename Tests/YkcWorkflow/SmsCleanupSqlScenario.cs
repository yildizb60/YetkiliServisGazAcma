using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace Tests.YkcWorkflow;

internal static class SmsCleanupSqlScenario
{
    public static async Task RunAsync()
    {
        var name = "YsSmsCleanupTest_" + Guid.NewGuid().ToString("N");
        var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true";
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        await using var db = new AppDbContext(dbOptions);
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(builder => builder.UseSqlServer(connection));
        await using var provider = services.BuildServiceProvider();
        var settings = new SmsOptions();
        var options = Options.Create(settings);
        var clock = new FixedClock(DateTimeOffset.Now);
        var now = clock.GetLocalNow().DateTime;
        using var cleanup = new SmsDogrulamaTemizlikService(provider.GetRequiredService<IServiceScopeFactory>(),
            options, clock, NullLogger<SmsDogrulamaTemizlikService>.Instance);
        var created = false;
        var passed = 0;
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
            passed++;
        }

        try
        {
            created = await db.Database.EnsureCreatedAsync();
            if (!created) throw new InvalidOperationException("Test database already exists.");
            Check(settings.ExpiredCodeRetentionDays == 30 && settings.CodeCleanupIntervalHours == 24
                && settings.CodeCleanupEnabled, "Default cleanup retains expired codes for 30 days and runs daily");
            var user = new AppKullanici { UserName = "sms-cleanup-fixture", PhoneNumber = "05550000000" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            SmsDogrulamaKodu Code(DateTime expiry, bool used = false, bool removed = false) => new()
            {
                KullaniciId = user.Id, Telefon = user.PhoneNumber!, GecerlilikTarihi = expiry,
                KullanildiMi = used, SilindiMi = removed, OlusturmaTarihi = now.AddDays(-60)
            };
            var oldUnused = Code(now.AddDays(-31));
            var oldUsed = Code(now.AddDays(-31), used: true);
            var oldRemoved = Code(now.AddDays(-31), removed: true);
            var boundary = Code(now.AddDays(-30));
            var recent = Code(now.AddDays(-29));
            var justExpired = Code(now.AddMinutes(-1));
            var valid = Code(now.AddHours(1));
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            valid.KodHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{user.Id}:{code}")));
            var log = new SmsGonderimLog
            {
                KullaniciId = user.Id, Telefon = user.PhoneNumber!, Mesaj = "Fixture message",
                Saglayici = "Fixture", BasariliMi = true, OlusturmaTarihi = now.AddDays(-60)
            };
            db.AddRange(oldUnused, oldUsed, oldRemoved, boundary, recent, justExpired, valid, log);
            await db.SaveChangesAsync();

            settings.CodeCleanupEnabled = false;
            Check(await cleanup.TemizleAsync() == 0 && await db.SmsDogrulamaKodlari.CountAsync() == 7,
                "Disabled cleanup does not delete any verification codes");
            settings.CodeCleanupEnabled = true;
            Check(await cleanup.TemizleAsync() == 3, "Expired unused, used and archived codes beyond retention are deleted");
            var remaining = await db.SmsDogrulamaKodlari.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
            Check(remaining.SequenceEqual(new[] { boundary.Id, recent.Id, justExpired.Id, valid.Id }),
                "The retention boundary, recent expired codes and valid codes are preserved");
            Check(await cleanup.TemizleAsync() == 0, "Repeated cleanup is idempotent");
            Check(await db.SmsGonderimLoglari.CountAsync() == 1
                && (await db.SmsGonderimLoglari.AsNoTracking().SingleAsync()).Mesaj == log.Mesaj
                && await db.Users.CountAsync() == 1, "Delivery history and user accounts are untouched");
            var verification = new SmsDogrulamaService(db, options, new NullSmsProvider(options),
                new SehirFirmaKoduService(new ConfigurationBuilder().Build(), db));
            Check((await verification.KodDogrulaAsync(user.Id, code)).Basarili,
                "A preserved valid code still verifies successfully after cleanup");
            Check(!(await verification.KodDogrulaAsync(user.Id, code)).Basarili,
                "Cleanup does not weaken one-time code consumption");

            settings.ExpiredCodeRetentionDays = 7;
            Check(await cleanup.TemizleAsync() == 2 && await db.SmsDogrulamaKodlari.CountAsync() == 2,
                "The configured retention period changes which expired codes are eligible");
            var expiresNow = Code(now);
            db.Add(expiresNow);
            await db.SaveChangesAsync();
            settings.ExpiredCodeRetentionDays = 0;
            Check(await cleanup.TemizleAsync() == 1
                && await db.SmsDogrulamaKodlari.AnyAsync(x => x.Id == expiresNow.Id)
                && await db.SmsDogrulamaKodlari.AnyAsync(x => x.Id == valid.Id),
                "Zero retention still excludes valid codes and the exact expiry boundary");
            using (var cancellation = new CancellationTokenSource())
            {
                await cancellation.CancelAsync();
                try
                {
                    await cleanup.TemizleAsync(cancellation.Token);
                    throw new InvalidOperationException("Cancelled cleanup unexpectedly completed.");
                }
                catch (OperationCanceledException)
                {
                    Check(await db.SmsDogrulamaKodlari.CountAsync() == 2,
                        "Cancellation reaches SQL and leaves the verification records unchanged");
                }
            }

            settings.ExpiredCodeRetentionDays = 30;
            db.SmsDogrulamaKodlari.AddRange(Enumerable.Range(0, 5001).Select(_ => Code(now.AddDays(-31))));
            await db.SaveChangesAsync();
            Check(await cleanup.TemizleAsync() == 5000 && await db.SmsDogrulamaKodlari.CountAsync() == 3,
                "Each cleanup is bounded to ten batches of 500 instead of an unbounded delete");
            Check(await cleanup.TemizleAsync() == 1 && await db.SmsDogrulamaKodlari.CountAsync() == 2,
                "The next cleanup finishes the backlog without affecting retained codes");

            var startupCode = Code(now.AddDays(-31));
            db.Add(startupCode);
            await db.SaveChangesAsync();
            await cleanup.StartAsync(CancellationToken.None);
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (await db.SmsDogrulamaKodlari.AnyAsync(x => x.Id == startupCode.Id, timeout.Token))
                    await Task.Delay(25, timeout.Token);
                Check(await db.SmsDogrulamaKodlari.CountAsync() == 2,
                    "The background worker automatically cleans on startup without an HTTP request");
            }
            finally
            {
                await cleanup.StopAsync(CancellationToken.None);
            }
            Check(cleanup.ExecuteTask?.IsCompletedSuccessfully == true,
                "API shutdown cancels the daily timer without an unhandled exception");
            Check(await db.SmsGonderimLoglari.CountAsync() == 1, "Batched and background cleanup preserve delivery logs");
            Console.WriteLine($"{passed} SMS cleanup SQL checks passed.");
        }
        finally
        {
            if (created && db.Database.GetDbConnection().Database == name
                && name.StartsWith("YsSmsCleanupTest_", StringComparison.Ordinal))
                await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }
}
