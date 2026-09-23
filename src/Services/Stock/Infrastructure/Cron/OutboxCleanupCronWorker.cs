using Microsoft.Extensions.Internal;
using Stock.Infrastructure.Persistence;

namespace Stock.Infrastructure.Cron;

public class OutboxCleanupCronWorker(
    IServiceScopeFactory serviceScopeFactory,
    ISystemClock clock,
    ILogger<OutboxCleanupCronWorker> logger)
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private const int BatchSize = 5000;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var cleaner = scope.ServiceProvider.GetRequiredService<IOutboxCleaner>();

        var deleted = await cleaner.CleanupAsync(clock.UtcNow - Retention, BatchSize, stoppingToken);

        logger.LogInformation("Outbox cleanup removed {Count} completed rows older than {Retention}", deleted,
            Retention);
    }
}
