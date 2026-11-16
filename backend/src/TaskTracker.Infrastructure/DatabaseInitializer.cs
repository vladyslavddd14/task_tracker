using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskTracker.Infrastructure.Persistence;
using TaskTracker.Infrastructure.Seed;

namespace TaskTracker.Infrastructure;

public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    /// <summary>Applies pending migrations (safe to run repeatedly) and optionally seeds demo data.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await MigrateWithRetryAsync(db, logger, ct);

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (config.GetValue<bool>("SEED_DEMO"))
        {
            var email = config["SEED_DEMO_EMAIL"];
            var password = config["SEED_DEMO_PASSWORD"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "SEED_DEMO is enabled, so SEED_DEMO_EMAIL and SEED_DEMO_PASSWORD must be set.");
            }

            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(email, password, ct);
            logger.LogInformation("Demo data is ready for {Email}", email);
        }
    }

    /// <summary>The database container can be slower to start than the API, so connection errors are retried.</summary>
    private static async Task MigrateWithRetryAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Database is not ready (attempt {Attempt}/{Max}): {Message}", attempt, MaxAttempts,
                    ex.Message);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }
}
