using Dapper;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging.EFCore.Tests.Cleanup;

public class CleanupServiceTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly OutboxTable _table = new(db.ConnectionString);
    private readonly CapturingLogger<CleanupService<TestDbContext>> _logger = new();
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _table.ClearAsync();
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM inbox");
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Start_DefaultRetention_DeletesExpiredRowsAndKeepsRecentOnes()
    {
        var now = DateTimeOffset.UtcNow;
        var oldInbox = await SeedInboxAsync(now.AddDays(-31));
        var recentInbox = await SeedInboxAsync(now.AddDays(-29));
        var oldOutbox = await SeedOutboxAsync("COMPLETED", now.AddDays(-8));
        var recentOutbox = await SeedOutboxAsync("COMPLETED", now.AddDays(-6));

        await RunOnceAsync(b => b.AddInbox<TestDbContext>().AddOutbox<TestDbContext>());

        Assert.False(await InboxExistsAsync(oldInbox));
        Assert.True(await InboxExistsAsync(recentInbox));
        Assert.Null(await _table.GetAsync(oldOutbox));
        Assert.NotNull(await _table.GetAsync(recentOutbox));
    }

    [Fact]
    public async Task Start_ConfiguredRetention_UsesConfiguredValues()
    {
        var now = DateTimeOffset.UtcNow;
        var inbox = await SeedInboxAsync(now.AddHours(-2));
        var outbox = await SeedOutboxAsync("COMPLETED", now.AddHours(-2));

        await RunOnceAsync(b => b.AddInbox<TestDbContext>().AddOutbox<TestDbContext>(), services =>
            services.ConfigureCleanup(o =>
            {
                o.InboxRetentionMinutes = 60;
                o.OutboxRetentionMinutes = 60;
            }));

        Assert.False(await InboxExistsAsync(inbox));
        Assert.Null(await _table.GetAsync(outbox));
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("PROCESSING")]
    [InlineData("DEAD")]
    public async Task Start_ExpiredUnfinishedOrDeadOutboxRow_KeepsIt(string status)
    {
        var id = await SeedOutboxAsync(status, DateTimeOffset.UtcNow.AddDays(-30));

        await RunOnceAsync(b => b.AddOutbox<TestDbContext>());

        Assert.NotNull(await _table.GetAsync(id));
    }

    [Fact]
    public async Task Start_OnlyInboxRegistered_CleansInboxAndLeavesOutbox()
    {
        var now = DateTimeOffset.UtcNow;
        var inbox = await SeedInboxAsync(now.AddDays(-31));
        var outbox = await SeedOutboxAsync("COMPLETED", now.AddDays(-8));

        await RunOnceAsync(b => b.AddInbox<TestDbContext>());

        Assert.False(await InboxExistsAsync(inbox));
        Assert.NotNull(await _table.GetAsync(outbox));
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Start_OnlyOutboxRegistered_CleansOutboxAndLeavesInbox()
    {
        var now = DateTimeOffset.UtcNow;
        var inbox = await SeedInboxAsync(now.AddDays(-31));
        var outbox = await SeedOutboxAsync("COMPLETED", now.AddDays(-8));

        await RunOnceAsync(b => b.AddOutbox<TestDbContext>());

        Assert.True(await InboxExistsAsync(inbox));
        Assert.Null(await _table.GetAsync(outbox));
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Start_RowsDeleted_LogsDeletedCounts()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedOutboxAsync("COMPLETED", now.AddDays(-8));
        await SeedOutboxAsync("COMPLETED", now.AddDays(-8));
        await SeedInboxAsync(now.AddDays(-31));

        await RunOnceAsync(b => b.AddInbox<TestDbContext>().AddOutbox<TestDbContext>());

        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information &&
                                              e.Message.Contains("2 outbox messages and 1 inbox messages"));
    }

    [Fact]
    public async Task Start_DatabaseUnavailable_LogsErrorAndKeepsRunning()
    {
        var missingDatabase = new SqlConnectionStringBuilder(db.ConnectionString)
            { InitialCatalog = $"missing_{Guid.NewGuid():N}" }.ConnectionString;

        var service = await StartAsync(b => b.AddInbox<TestDbContext>().AddOutbox<TestDbContext>(),
            connectionString: missingDatabase);
        await Wait.UntilAsync(() => Task.FromResult(HasLog(LogLevel.Error)), "cleanup error logged");

        Assert.False(service.ExecuteTask!.IsCompleted);
        await service.StopAsync(CancellationToken.None);
    }

    private async Task RunOnceAsync(Action<IMessagingBuilder> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var service = await StartAsync(configure, configureServices);
        await Wait.UntilAsync(() => Task.FromResult(HasLog(LogLevel.Information) || HasLog(LogLevel.Error)),
            "cleanup pass finished");
        await service.StopAsync(CancellationToken.None);
    }

    private async Task<CleanupService<TestDbContext>> StartAsync(Action<IMessagingBuilder> configure,
        Action<IServiceCollection>? configureServices = null, string? connectionString = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(connectionString ?? db.ConnectionString));
        services.AddMessaging(configure, o => o.BootstrapServers = "localhost:9092");
        services.AddSingleton<ILogger<CleanupService<TestDbContext>>>(_logger);
        configureServices?.Invoke(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var service = _provider.GetServices<IHostedService>().OfType<CleanupService<TestDbContext>>().Single();
        await service.StartAsync(CancellationToken.None);
        return service;
    }

    private bool HasLog(LogLevel level)
    {
        lock (_logger.Entries) return _logger.Entries.Any(e => e.Level == level);
    }

    private async Task<string> SeedInboxAsync(DateTimeOffset createdAt)
    {
        var id = Guid.NewGuid().ToString();
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO inbox (consumer_group, message_id, created_at) VALUES ('orders', @Id, @CreatedAt)",
            new { Id = id, CreatedAt = createdAt });
        return id;
    }

    private async Task<bool> InboxExistsAsync(string messageId)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inbox WHERE message_id = @Id",
            new { Id = messageId }) == 1;
    }

    private Task<long> SeedOutboxAsync(string status, DateTimeOffset createdAt)
    {
        return _table.SeedAsync(new OutboxSeed { Status = status, CreatedAt = createdAt });
    }
}
