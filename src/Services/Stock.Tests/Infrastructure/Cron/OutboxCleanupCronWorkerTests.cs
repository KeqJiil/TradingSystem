using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Persistence;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Stock.Tests.Infrastructure.Cron;

public class OutboxCleanupCronWorkerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDbContext _dbContext;
    private readonly ServiceProvider _provider;
    private readonly OutboxCleanupCronWorker _worker;

    public OutboxCleanupCronWorkerTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        _provider = new ServiceCollection()
            .AddScoped<IOutboxCleaner>(_ => new OutboxWriter(_dbContext))
            .BuildServiceProvider();
        _worker = new OutboxCleanupCronWorker(_provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeSystemClock(Now), NullLogger<OutboxCleanupCronWorker>.Instance);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_DeletesCompletedRowsOlderThanSevenDays_AndKeepsNewerOnes()
    {
        var expired = await InsertCompletedRowAsync(Now.AddDays(-8));
        var retained = await InsertCompletedRowAsync(Now.AddDays(-6));

        await _worker.ExecuteAsync(CancellationToken.None);

        var remaining = (await _dbContext.Connection.QueryAsync<Guid>("SELECT id FROM outbox")).ToList();
        Assert.DoesNotContain(expired, remaining);
        Assert.Equal([retained], remaining);
    }

    private async Task<Guid> InsertCompletedRowAsync(DateTimeOffset createdAt)
    {
        var id = Guid.NewGuid();

        await _dbContext.Connection.ExecuteAsync("""
                                                 INSERT INTO outbox (id, aggregate_id, payload, event_type, status, created_at)
                                                 VALUES (@Id, @AggregateId, '{}', 'TestEvent', 'COMPLETED', @CreatedAt)
                                                 """,
            new { Id = id, AggregateId = Guid.NewGuid(), CreatedAt = createdAt });

        return id;
    }
}
