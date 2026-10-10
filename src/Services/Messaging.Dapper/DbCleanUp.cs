using Messaging.Dapper.Inbox;
using Messaging.Dapper.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging.Dapper;

internal class DbCleanUp(
    IServiceScopeFactory scopeFactory,
    ILogger<DbCleanUp> logger,
    IOptions<CleansionOptions> options,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(40));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var outboxCleaner = scope.ServiceProvider.GetService<OutboxCleaner>();
                var inboxCleaner = scope.ServiceProvider.GetService<InboxCleaner>();

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