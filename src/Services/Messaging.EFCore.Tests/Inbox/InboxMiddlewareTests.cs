using Dapper;
using Messaging.Abstractions;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Messaging.EFCore.Tests.Inbox;

public class InboxMiddlewareTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private const string Group = "orders";

    private static readonly ConsumeOutcome Success = new(MessageConsumeResult.Success);

    private readonly CapturingLogger<InboxMiddleware<TestDbContext>> _logger = new();
    private ServiceProvider _provider = null!;

    [Theory]
    [InlineData("", Group)]
    [InlineData("message", "")]
    public async Task OnDelivery_MissingMessageIdOrGroup_DeadLettersWithoutCallingHandler(string messageId,
        string group)
    {
        var called = false;

        var outcome = await DeliverAsync(messageId, group, _ =>
        {
            called = true;
            return Task.FromResult(Success);
        });

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        Assert.False(called);
    }

    [Fact]
    public async Task OnDelivery_AlreadyInInbox_ReturnsSuccessWithoutCallingHandler()
    {
        var id = NewId();
        await SeedInboxAsync(id, Group);
        var called = false;

        var outcome = await DeliverAsync(id, Group, _ =>
        {
            called = true;
            return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.DeadLetter, "must be ignored"));
        });

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.False(called);
    }

    [Fact]
    public async Task OnDelivery_HandlerSavesAndReturnsSuccess_WritesHandlerRowAndInboxRow()
    {
        var id = NewId();

        var outcome = await DeliverAsync(id, Group, Saves(id, Success));

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Equal(1, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Theory]
    [InlineData(MessageConsumeResult.Retry)]
    [InlineData(MessageConsumeResult.DeadLetter)]
    public async Task OnDelivery_HandlerReturnsFailureWithoutSaving_NoInboxRowAndRedeliveryCallsHandler(
        MessageConsumeResult kind)
    {
        var id = NewId();
        var calls = 0;
        var failure = new ConsumeOutcome(kind, "failed");

        var first = await DeliverAsync(id, Group, _ =>
        {
            calls++;
            return Task.FromResult(failure);
        });

        Assert.Equal(failure, first);
        Assert.Equal(0, await CountInboxAsync(id, Group));

        var second = await DeliverAsync(id, Group, async sp =>
        {
            calls++;
            return await Saves(id, Success)(sp);
        });

        Assert.Equal(MessageConsumeResult.Success, second.Kind);
        Assert.Equal(2, calls);
        Assert.Equal(1, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_HandlerReturnsSuccessWithoutSaving_WritesInboxRowWithoutWarning()
    {
        var id = NewId();
        var expected = new ConsumeOutcome(MessageConsumeResult.Success, "handled");

        var outcome = await DeliverAsync(id, Group, _ => Task.FromResult(expected));

        Assert.Equal(expected, outcome);
        Assert.Equal(1, await CountInboxAsync(id, Group));
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task OnDelivery_HandlerReturnsSuccessWithUnsavedChanges_DiscardsThemAndLogsWarning()
    {
        var id = NewId();

        var outcome = await DeliverAsync(id, Group, sp =>
        {
            sp.GetRequiredService<TestDbContext>().Add(NewOutbox(id));
            return Task.FromResult(Success);
        });

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Equal(0, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(id));
    }

    [Fact]
    public async Task OnDelivery_HandlerSavesButReturnsRetry_LogsErrorAndRedeliveryIsSkipped()
    {
        var id = NewId();
        var calls = 0;

        var first = await DeliverAsync(id, Group, async sp =>
        {
            calls++;
            return await Saves(id, new ConsumeOutcome(MessageConsumeResult.Retry, "try later"))(sp);
        });

        Assert.Equal(MessageConsumeResult.Retry, first.Kind);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(id));

        var second = await DeliverAsync(id, Group, async sp =>
        {
            calls++;
            return await Saves(id, Success)(sp);
        });

        Assert.Equal(MessageConsumeResult.Success, second.Kind);
        Assert.Equal(1, calls);
        Assert.Equal(1, await CountEffectsAsync(id));
    }

    [Fact]
    public async Task OnDelivery_HandlerReturnsRetryWithoutSaving_LogsNoError()
    {
        var id = NewId();

        await DeliverAsync(id, Group, _ => Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Retry)));

        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task OnDelivery_InboxCommittedConcurrentlyBeforeHandlerSave_ReturnsSuccessAndDropsHandlerRow()
    {
        var id = NewId();

        var outcome = await DeliverAsync(id, Group, async sp =>
        {
            await SeedInboxAsync(id, Group);
            return await Saves(id, new ConsumeOutcome(MessageConsumeResult.DeadLetter, "must be ignored"))(sp);
        });

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Equal(0, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_InboxCommittedConcurrentlyWithoutHandlerSave_ReturnsHandlerOutcome()
    {
        var id = NewId();
        var expected = new ConsumeOutcome(MessageConsumeResult.Success, "handled");

        var outcome = await DeliverAsync(id, Group, async _ =>
        {
            await SeedInboxAsync(id, Group);
            return expected;
        });

        Assert.Equal(expected, outcome);
        Assert.Equal(1, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_HandlerThrows_PropagatesAndLeavesNoRows()
    {
        var id = NewId();

        await Assert.ThrowsAsync<InvalidOperationException>(() => DeliverAsync(id, Group, sp =>
        {
            sp.GetRequiredService<TestDbContext>().Add(NewOutbox(id));
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal(0, await CountEffectsAsync(id));
        Assert.Equal(0, await CountInboxAsync(id, Group));
    }

    [Fact]
    public async Task OnDelivery_SameMessageIdInDifferentGroups_ProcessedInBoth()
    {
        var id = NewId();

        var first = await DeliverAsync(id, "orders", Saves(id, Success));
        var second = await DeliverAsync(id, "portfolio", Saves(id, Success));

        Assert.Equal(MessageConsumeResult.Success, first.Kind);
        Assert.Equal(MessageConsumeResult.Success, second.Kind);
        Assert.Equal(2, await CountEffectsAsync(id));
        Assert.Equal(1, await CountInboxAsync(id, "orders"));
        Assert.Equal(1, await CountInboxAsync(id, "portfolio"));
    }

    public Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(db.ConnectionString));
        services.AddMessaging(b => b.AddInbox<TestDbContext>(), o => o.BootstrapServers = "localhost:9092");
        services.AddSingleton<ILogger<InboxMiddleware<TestDbContext>>>(_logger);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    private static string NewId()
    {
        return Guid.NewGuid().ToString();
    }

    private async Task<ConsumeOutcome> DeliverAsync(string messageId, string group,
        Func<IServiceProvider, Task<ConsumeOutcome>> handler)
    {
        await using var scope = _provider.CreateAsyncScope();
        var middleware = scope.ServiceProvider
            .GetKeyedServices<IMessageDeliveryMiddleware>("MessagingDeliveryMiddleware")
            .OfType<InboxMiddleware<TestDbContext>>()
            .Single();

        var context = new DeliveryContext<string>("topic", group, 0, 0, messageId, "String", "payload",
            DateTimeOffset.UtcNow, new Dictionary<string, string>());

        return await middleware.OnDeliveryAsync(context, () => handler(scope.ServiceProvider), CancellationToken.None);
    }

    private static Func<IServiceProvider, Task<ConsumeOutcome>> Saves(string messageId, ConsumeOutcome outcome)
    {
        return async sp =>
        {
            var context = sp.GetRequiredService<TestDbContext>();
            context.Add(NewOutbox(messageId));
            await context.SaveChangesAsync();
            return outcome;
        };
    }

    private static OutboxEntity NewOutbox(string messageId)
    {
        return new OutboxEntity
        {
            MessageId = messageId,
            MessageType = "Test",
            Topic = "topic",
            MessageKey = "key",
            Payload = [1],
            Headers = "{}"
        };
    }

    private async Task SeedInboxAsync(string messageId, string group)
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("INSERT INTO messaging.inbox (consumer_group, message_id) VALUES (@Group, @Id)",
            new { Group = group, Id = messageId });
    }

    private async Task<int> CountEffectsAsync(string messageId)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM messaging.outbox WHERE message_id = @Id",
            new { Id = messageId });
    }

    private async Task<int> CountInboxAsync(string messageId, string group)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM messaging.inbox WHERE message_id = @Id AND consumer_group = @Group",
            new { Id = messageId, Group = group });
    }
}
