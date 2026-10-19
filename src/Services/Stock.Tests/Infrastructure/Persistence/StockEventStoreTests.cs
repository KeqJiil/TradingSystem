using Dapper;
using MediatR;
using Messaging;
using Messaging.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Stock.Application.Commands.ChangePrice;
using Stock.Application.Events;
using Stock.Application.Services;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Infrastructure.Topics;
using Stock.Presentation.Builder;
using Xunit;

namespace Stock.Tests.Infrastructure.Persistence;

public class StockEventStoreTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private const int VersionConflict = 2601;

    private readonly TestDbContext _db = new(fixture.ConnectionString);
    private ServiceProvider _provider = null!;

    private PublishedMessages Published => _provider.GetRequiredService<PublishedMessages>();

    public async Task InitializeAsync()
    {
        _provider = BuildProvider();
        await _db.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task AppendAsync_ShouldAssignNextVersionPerAggregate()
    {
        var store = new StockEventStore(_db);
        var aggregateId = Guid.NewGuid();

        var first = await store.AppendAsync(NewRequest(aggregateId, 1m), CancellationToken.None);
        var second = await store.AppendAsync(NewRequest(aggregateId, 2m), CancellationToken.None);

        Assert.Equal(1, first!.Version);
        Assert.Equal(2, second!.Version);
    }

    [Fact]
    public async Task AppendAsync_ShouldReturnNull_WhenEventIdIsAlreadyStored()
    {
        var store = new StockEventStore(_db);
        var request = NewRequest(Guid.NewGuid(), 5m);
        await store.AppendAsync(request, CancellationToken.None);

        var duplicate = await store.AppendAsync(request, CancellationToken.None);

        Assert.Null(duplicate);
        Assert.Equal(1, await CountEvents(request.AggregateId));
    }

    [Fact]
    public async Task ChangePriceAppendAsync_ShouldLeaveOneEventAndOnePublishedMessage_WhenSameEventArrivesTwice()
    {
        var request = NewRequest(Guid.NewGuid(), 7m);

        var first = await ChangePriceAppendAsync(_provider, request);
        var second = await ChangePriceAppendAsync(_provider, request);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await CountEvents(request.AggregateId));
        Assert.Single(await Published.OfTypeAsync<PriceChangedEvent>(request.AggregateId.ToString()));
    }

    [Fact]
    public async Task ChangePriceAppendAsync_PublishesPriceChangedEventWithAssignedVersion()
    {
        var aggregateId = Guid.NewGuid();
        await ChangePriceAppendAsync(_provider, NewRequest(aggregateId, 3m));
        var request = NewRequest(aggregateId, 7m);

        await ChangePriceAppendAsync(_provider, request);

        var published = await Published.OfTypeAsync<PriceChangedEvent>(aggregateId.ToString());
        Assert.All(published, p => Assert.Equal(StockTopic.Price.Name(), p.Topic));
        Assert.Equal([1L, 2L], published.Select(p => p.Message.Version!.Value));
        Assert.Equal(new PriceChangedEvent(aggregateId, 7m, 2, request.OccuredAt), published[1].Message);
    }

    [Fact]
    public async Task ChangePriceAppendAsync_PublishFails_StoresNoEvent()
    {
        var request = NewRequest(Guid.NewGuid(), 7m);
        await using var failing = BuildProvider(b => b.AddPublishMiddleware<FailingPublishMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => ChangePriceAppendAsync(failing, request));

        Assert.Equal(0, await CountEvents(request.AggregateId));
    }

    [Fact]
    public async Task VersionConflict_IsRetriedByProductionResiliencePipeline()
    {
        var aggregateId = Guid.NewGuid();
        await new StockEventStore(_db).AppendAsync(NewRequest(aggregateId, 1m), CancellationToken.None);
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<SqlException>(() => ProductionPipeline().ExecuteAsync(async _ =>
        {
            attempts++;
            await _db.Connection.ExecuteAsync("""
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

        await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            await using var scope = _provider.CreateAsyncScope();
            var request = NewRequest(aggregateId, 1m);
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ChangePriceCommand(
                request.EventId, request.AggregateId, request.PriceChange, request.OccuredAt));
        })));

        var versions = await _db.Connection.QueryAsync<long>(
            "SELECT version FROM events_store WHERE aggregate_id = @AggregateId ORDER BY version",
            new { AggregateId = aggregateId });
        Assert.Equal(Enumerable.Range(1, writers).Select(v => (long)v), versions);
        Assert.Equal(writers, (await Published.OfTypeAsync<PriceChangedEvent>(aggregateId.ToString())).Count);
    }

    private static async Task<bool> ChangePriceAppendAsync(IServiceProvider provider, PriceChangeRequested request)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventStoreService>()
            .ChangePriceAppendAsync(request, CancellationToken.None);
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

    private Task<int> CountEvents(Guid aggregateId)
    {
        return _db.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM events_store WHERE aggregate_id = @AggregateId", new { AggregateId = aggregateId });
    }

    private ServiceProvider BuildProvider(Action<IMessagingBuilder>? configure = null)
    {
        return StockServices.Build(fixture.ConnectionString, configure);
    }
}
