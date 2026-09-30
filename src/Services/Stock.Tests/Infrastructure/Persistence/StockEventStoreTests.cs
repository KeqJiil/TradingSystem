using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Stock.Application.Events;
using Stock.Application.Services;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Presentation.Builder;
using Xunit;

namespace Stock.Tests.Infrastructure.Persistence;

public class StockEventStoreTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private const int VersionConflict = 2601;

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

    [Fact]
    public async Task ChangePriceAppendAsync_WritesPriceChangedEventWithAssignedVersionToOutbox()
    {
        await using var unitOfWork = new UnitOfWork(new TestDbConnectionFactory(_fixture.ConnectionString));
        var service = new EventStoreService(
            new StockEventStore(unitOfWork),
            new UnitOfWorkDecorator(unitOfWork, new ResiliencePipelineBuilder().Build(), NullLogger<UnitOfWorkDecorator>.Instance),
            new OutboxWriter(unitOfWork));
        
        var aggregateId = Guid.NewGuid();
        await service.ChangePriceAppendAsync(NewRequest(aggregateId, 3m), CancellationToken.None);
        var request = NewRequest(aggregateId, 7m);

        await service.ChangePriceAppendAsync(request, CancellationToken.None);

        var events = (await DbContext.Connection.QueryAsync<string>(
                "SELECT payload FROM outbox WHERE aggregate_id = @AggregateId AND event_type = @EventType",
                new { AggregateId = aggregateId, EventType = nameof(PriceChangedEvent) }))
            .Select(p => JsonSerializer.Deserialize<PriceChangedEvent>(p)!)
            .OrderBy(e => e.Version)
            .ToList();
        Assert.Equal([1L, 2L], events.Select(e => e.Version!.Value));
        Assert.Equal(new PriceChangedEvent(aggregateId, 7m, 2, request.OccuredAt), events[1]);
    }

    [Fact]
    public async Task VersionConflict_IsRetriedByProductionResiliencePipeline()
    {
        var aggregateId = Guid.NewGuid();
        await new StockEventStore(DbContext).AppendAsync(NewRequest(aggregateId, 1m), CancellationToken.None);
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<SqlException>(() => ProductionPipeline().ExecuteAsync(async _ =>
        {
            attempts++;
            await DbContext.Connection.ExecuteAsync("""
                INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change)
                VALUES (NEWID(), @AggregateId, 1, 'PriceChangedEvent', '{}', 1)
                """, new { AggregateId = aggregateId });
        }).AsTask());

        Assert.Equal(VersionConflict, exception.Number);
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task ConcurrentAppends_ForSameAggregate_AllSucceed_WithContiguousVersions()
    {
        const int writers = 2;
        var aggregateId = Guid.NewGuid();
        var pipeline = ProductionPipeline();
        var unitsOfWork = Enumerable.Range(0, writers)
            .Select(_ => new UnitOfWork(new TestDbConnectionFactory(_fixture.ConnectionString)))
            .ToList();

        try
        {
            var results = await Task.WhenAll(unitsOfWork.Select(unitOfWork => Task.Run(() =>
                new EventStoreService(
                        new StockEventStore(unitOfWork),
                        new UnitOfWorkDecorator(unitOfWork, pipeline, NullLogger<UnitOfWorkDecorator>.Instance),
                        new OutboxWriter(unitOfWork))
                    .ChangePriceAppendAsync(NewRequest(aggregateId, 1m), CancellationToken.None))));

            Assert.All(results, Assert.True);
        }
        finally
        {
            foreach (var unitOfWork in unitsOfWork)
                await unitOfWork.DisposeAsync();
        }

        var versions = await DbContext.Connection.QueryAsync<long>(
            "SELECT version FROM events_store WHERE aggregate_id = @AggregateId ORDER BY version",
            new { AggregateId = aggregateId });
        Assert.Equal(Enumerable.Range(1, writers).Select(v => (long)v), versions);
        Assert.Equal(writers, await CountRows("outbox", aggregateId));
    }

    private static ResiliencePipeline ProductionPipeline()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddResilence();
        return builder.Services.BuildServiceProvider().GetRequiredService<ResiliencePipeline>();
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