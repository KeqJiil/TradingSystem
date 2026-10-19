using Confluent.Kafka;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Options;
using Xunit;
using DomainPriceChanged = Stock.Application.Events.PriceChangedEvent;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

[Collection(StockServiceCollection.Name)]
public class PriceChangeRequestedConsumerTests
{
    private readonly StockServiceHostFixture _fixture;

    public PriceChangeRequestedConsumerTests(StockServiceHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HappyPath_PriceChangeRequestedEvent_AppendsEventStoreRow_AndPublishesPriceChanged()
    {
        var aggregateId = Guid.NewGuid();
        var evt = new PriceChangeRequestedEvent(Guid.NewGuid(), aggregateId, 12.5m, DateTimeOffset.UtcNow);

        Produce(evt);

        var eventRow = await Polling.WaitUntilAsync(() => TryGetEventStoreRowAsync(aggregateId), TimeSpan.FromSeconds(45));
        Assert.Equal(1L, eventRow.Version);
        Assert.Equal(12.5m, eventRow.PriceChange);
        Assert.Equal(nameof(PriceChangedEvent), eventRow.EventType);

        var published = await Polling.WaitUntilAsync(async () =>
            (await _fixture.Published.OfTypeAsync<DomainPriceChanged>(aggregateId.ToString())).SingleOrDefault(),
            TimeSpan.FromSeconds(45));
        Assert.Equal(new DomainPriceChanged(aggregateId, 12.5m, 1, evt.OccurredAt), published.Message);
    }

    [Fact]
    public async Task PriceChangeRequestedEvent_PriceChangeOverflowsColumnPrecision_NeverAppended_RawEventRoutedToFatalDlq()
    {
        var aggregateId = Guid.NewGuid();
        var overflowingPriceChange = decimal.Parse("12345678901234567");
        var evt = new PriceChangeRequestedEvent(Guid.NewGuid(), aggregateId, overflowingPriceChange, DateTimeOffset.UtcNow);

        Produce(evt);

        var fatalMessage = KafkaTestConsumer.WaitForMessage<PriceChangeRequestedEvent>(
            _fixture.BootstrapAddress,
            StockServiceHostFixture.DeadLetterTopic("price-change-requested-events-group", TopicNames.PriceChangeRequested),
            m => m.AggregateId == aggregateId,
            TimeSpan.FromSeconds(45));

        Assert.Equal(evt, fatalMessage.Message.Value); 
        Assert.Null(await TryGetEventStoreRowAsync(aggregateId));
    }

    private void Produce(PriceChangeRequestedEvent evt)
    {
        _fixture.Produce(TopicNames.PriceChangeRequested, evt.AggregateId, evt);
    }

    private async Task<EventStoreRow?> TryGetEventStoreRowAsync(Guid aggregateId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<EventStoreRow>("""
            SELECT "version" AS Version, "price_change" AS PriceChange, "event_type" AS EventType
            FROM "events_store"
            WHERE "aggregate_id" = @AggregateId
            """, new { AggregateId = aggregateId });
    }

    private record EventStoreRow(long Version, decimal PriceChange, string EventType);
}
