using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Stock.Application.Events;
using Stock.Application.Services;
using Stock.Infrastructure.Persistence.Implementations;
using Xunit;

namespace Stock.Tests.Infrastructure.Persistence;

public class StockEventStoreTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly MssqlFixture _fixture;
    private TestDbContext DbContext { get; }

    public StockEventStoreTests(MssqlFixture fixture)
    {
        _fixture = fixture;
        DbContext = new TestDbContext(fixture.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }

    [Fact]
    public async Task AppendAsync_ShouldAssignNextVersionPerAggregate()
    {
        var store = new StockEventStore(DbContext);
        var aggregateId = Guid.NewGuid();

        var first = await store.AppendAsync(NewRequest(aggregateId, 1m), CancellationToken.None);
        var second = await store.AppendAsync(NewRequest(aggregateId, 2m), CancellationToken.None);

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
    }

    [Fact]
    public async Task AppendAsync_ShouldReturnNull_WhenEventIdIsAlreadyStored()
    {
        var store = new StockEventStore(DbContext);
        var request = NewRequest(Guid.NewGuid(), 5m);
        await store.AppendAsync(request, CancellationToken.None);

        var duplicate = await store.AppendAsync(request, CancellationToken.None);

        Assert.Null(duplicate);
        Assert.Equal(1, await CountRows("events_store", request.AggregateId));
    }

    [Fact]
    public async Task ChangePriceAppendAsync_ShouldLeaveOneEventAndOneOutboxRow_WhenSameEventArrivesTwice()
    {
        await using var unitOfWork = new UnitOfWork(new TestDbConnectionFactory(_fixture.ConnectionString));
        var service = new EventStoreService(
            new StockEventStore(unitOfWork),
            new UnitOfWorkDecorator(unitOfWork, new ResiliencePipelineBuilder().Build(), NullLogger<UnitOfWorkDecorator>.Instance),
            new OutboxWriter(unitOfWork));
        var request = NewRequest(Guid.NewGuid(), 7m);
        var first = await service.ChangePriceAppendAsync(request, CancellationToken.None);

        var second = await service.ChangePriceAppendAsync(request, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await CountRows("events_store", request.AggregateId));
        Assert.Equal(1, await CountRows("outbox", request.AggregateId));
    }

    private static PriceChangeRequested NewRequest(Guid aggregateId, decimal priceChange)
    {
        return new PriceChangeRequested(Guid.NewGuid(), aggregateId, priceChange, DateTimeOffset.UtcNow);
    }

    private Task<int> CountRows(string table, Guid aggregateId)
    {
        return DbContext.Connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {table} WHERE aggregate_id = @AggregateId",
            new { AggregateId = aggregateId });
    }
}