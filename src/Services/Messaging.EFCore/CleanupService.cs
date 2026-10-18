using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging.EFCore;

internal class CleanupService<TContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<CleanupService<TContext>> logger,
    IOptions<CleanupOptions> options,
    TimeProvider clock) : BackgroundService where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var outboxCleaner = scope.ServiceProvider.GetService<OutboxCleanser<TContext>>();
                var inboxCleaner = scope.ServiceProvider.GetService<InboxCleanser<TContext>>();

                var outboxRetentionDate = clock.GetUtcNow().AddMinutes(-options.Value.OutboxRetentionMinutes);
                var inboxRetentionDate = clock.GetUtcNow().AddMinutes(-options.Value.InboxRetentionMinutes);

                var outboxDeleted = outboxCleaner is null
                    ? 0
                    : await outboxCleaner.CleanupAsync(options.Value.OutboxBatchSize, outboxRetentionDate,
                        stoppingToken);
                var inboxDeleted = inboxCleaner is null
                    ? 0
                    : await inboxCleaner.CleanupAsync(options.Value.InboxBatchSize, inboxRetentionDate,
                        stoppingToken);

                logger.LogInformation("Cleaned up {OutboxDeleted} outbox messages and {InboxDeleted} inbox messages",
                    outboxDeleted, inboxDeleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "An unexpected error occurred while cleaning up outbox and inbox messages");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}