using Dapper;
using Messaging.Abstractions;
using Messaging.Dapper.Inbox;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Messaging.Dapper.Tests.Integration.Inbox;

public class InboxMiddlewareTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private const string Group = "orders";

    private readonly CapturingLogger<InboxMiddleware> _logger = new();
    private ServiceProvider _provider = null!;

    [Fact]
    public async Task OnDelivery_MissingMessageId_DeadLettersWithoutCallingHandler()
    {
        var called = false;

        var outcome = await DeliverAsync("", Group, _ =>
        {
            called = true;
            return Task.FromResult(Success);
        });

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        Assert.False(called);
    }

    [Fact]
    public async Task OnDelivery_NewMessageCommittedViaUnitOfWork_CommitsHandlerRowAndInboxRow()
    {
        var id = NewId();

        var outcome = await DeliverAsync(id, Group, CommitsViaUnitOfWork(id, Success));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Equal(1, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_DuplicateCommittedViaUnitOfWork_RollsBackHandlerRowAndReturnsSuccess()
    {
        var id = NewId();
        await SeedInboxAsync(id, Group);

        var outcome = await DeliverAsync(id, Group,
            CommitsViaUnitOfWork(id, new ConsumeOutcome(MessageConsumeResult.DeadLetter, "must be ignored")));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Equal(0, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_NewMessageWithoutTransaction_InsertsInboxRowAndPropagatesOutcome()
    {
        var id = NewId();
        var expected = new ConsumeOutcome(MessageConsumeResult.Success, "handled");

        var outcome = await DeliverAsync(id, Group, WithoutTransaction(id, expected));

        Assert.Equal(expected, outcome);
        Assert.Equal(1, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_DuplicateWithoutTransaction_ReturnsSuccessButHandlerRan()
    {
        var id = NewId();
        await SeedInboxAsync(id, Group);

        var outcome = await DeliverAsync(id, Group, WithoutTransaction(id, Success));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Equal(1, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_HandlerReturnsRetry_NoInboxRowAndRedeliveryIsProcessed()
    {
        var id = NewId();
        var calls = 0;

        var first = await DeliverAsync(id, Group, _ =>
        {
            calls++;
            return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Retry, "try later"));
        });

        Assert.Equal(MessageConsumeResult.Retry, first.Kind);
        Assert.Equal(0, await CountInboxAsync(id, Group));

        var second = await DeliverAsync(id, Group, async sp =>
        {
            calls++;
            return await CommitsViaUnitOfWork(id, Success)(sp);
        });

        Assert.Equal(MessageConsumeResult.Success, second.Kind);
        Assert.Equal(2, calls);
        Assert.Equal(1, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_HandlerCommitsViaUnitOfWorkButReturnsRetry_LogsErrorAndRedeliveryIsSkipped()
    {
        var id = NewId();
        var calls = 0;

        var first = await DeliverAsync(id, Group, async sp =>
        {
            calls++;
            return await CommitsViaUnitOfWork(id, new ConsumeOutcome(MessageConsumeResult.Retry, "try later"))(sp);
        });

        Assert.Equal(MessageConsumeResult.Retry, first.Kind);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(id));

        var second = await DeliverAsync(id, Group, async sp =>
        {
            calls++;
            return await CommitsViaUnitOfWork(id, Success)(sp);
        });

        Assert.Equal(MessageConsumeResult.Success, second.Kind);
        Assert.Equal(2, calls);
        Assert.Equal(1, await CountEffectsAsync(id));
    }

    [Fact]
    public async Task OnDelivery_HandlerReturnsRetryWithoutCommit_LogsNoError()
    {
        var id = NewId();

        await DeliverAsync(id, Group, WithoutTransaction(id, new ConsumeOutcome(MessageConsumeResult.Retry)));

        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task OnDelivery_HandlerThrowsInsideUnitOfWork_PropagatesAndLeavesNoRows()
    {
        var id = NewId();

        await Assert.ThrowsAsync<InvalidOperationException>(() => DeliverAsync(id, Group, async sp =>
        {
            await sp.GetRequiredService<IUnitOfWorkDecorator>().ExecuteAsync(async () =>
            {
                await WriteEffectAsync(sp, id);
                throw new InvalidOperationException("boom");
            }, CancellationToken.None);
            return Success;
        }));

        Assert.Equal(0, await CountEffectsAsync(id));
        Assert.Equal(0, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_SameMessageIdInDifferentGroups_ProcessedInBoth()
    {
        var id = NewId();

        var first = await DeliverAsync(id, "orders", CommitsViaUnitOfWork(id, Success));
        var second = await DeliverAsync(id, "portfolio", CommitsViaUnitOfWork(id, Success));

        Assert.Equal(MessageConsumeResult.Success, first.Kind);
        Assert.Equal(MessageConsumeResult.Success, second.Kind);
        Assert.Equal(2, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, "orders"));
        Assert.Equal(1, await CountInboxAsync(id, "portfolio"));
    }
    
    public async Task InitializeAsync()
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("""
                                      IF OBJECT_ID('dbo.handler_effects', 'U') IS NULL
                                          CREATE TABLE handler_effects (message_id VARCHAR(100) NOT NULL);
                                      """);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnitOfWork(_ => new SqlConnection(db.ConnectionString));
        services.AddMessaging(b => b.AddInbox(), o => o.BootstrapServers = "localhost:9092");
        services.AddSingleton<ILogger<InboxMiddleware>>(_logger);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    private static readonly ConsumeOutcome Success = new(MessageConsumeResult.Success);

    private static string NewId() => Guid.NewGuid().ToString();

    private async Task<ConsumeOutcome> DeliverAsync(string messageId, string group,
        Func<IServiceProvider, Task<ConsumeOutcome>> handler)
    {
        await using var scope = _provider.CreateAsyncScope();
        var middleware = scope.ServiceProvider
            .GetKeyedServices<IMessageDeliveryMiddleware>("MessagingDeliveryMiddleware")
            .OfType<InboxMiddleware>()
            .Single();

        var context = new DeliveryContext<string>("topic", group, 0, 0, messageId, "String", "payload",
            DateTimeOffset.UtcNow, new Dictionary<string, string>());

        return await middleware.OnDeliveryAsync(context, () => handler(scope.ServiceProvider), CancellationToken.None);
    }

    private static Func<IServiceProvider, Task<ConsumeOutcome>> CommitsViaUnitOfWork(string messageId,
        ConsumeOutcome outcome)
    {
        return async sp =>
        {
            await sp.GetRequiredService<IUnitOfWorkDecorator>()
                .ExecuteAsync(() => WriteEffectAsync(sp, messageId), CancellationToken.None);
            return outcome;
        };
    }

    private static Func<IServiceProvider, Task<ConsumeOutcome>> WithoutTransaction(string messageId,
        ConsumeOutcome outcome)
    {
        return async sp =>
        {
            await WriteEffectAsync(sp, messageId);
            return outcome;
        };
    }

    private static async Task WriteEffectAsync(IServiceProvider sp, string messageId)
    {
        var context = sp.GetRequiredService<ITransactionContext>();
        await context.EnsureConnectionOpenAsync(CancellationToken.None);
        await context.Connection.ExecuteAsync("INSERT INTO handler_effects (message_id) VALUES (@Id)",
            new { Id = messageId }, context.Transaction);
    }

    private async Task SeedInboxAsync(string messageId, string group)
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("INSERT INTO inbox (consumer_group, message_id) VALUES (@Group, @Id)",
            new { Group = group, Id = messageId });
    }

    private async Task<int> CountEffectsAsync(string messageId)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM handler_effects WHERE message_id = @Id", new { Id = messageId });
    }

    private async Task<int> CountInboxAsync(string messageId, string group)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM inbox WHERE consumer_group = @Group AND message_id = @Id",
            new { Group = group, Id = messageId });
    }
}
