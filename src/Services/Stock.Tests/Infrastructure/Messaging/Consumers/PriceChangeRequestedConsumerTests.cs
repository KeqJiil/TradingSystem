using Dapper;
using Microsoft.Data.SqlClient;
using Google.Protobuf.WellKnownTypes;
using Stock.Infrastructure.Topics;
using Xunit;
using TradingSystem.Contracts.ProtobufClasses.Stock;
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
        var evt = NewRequest(aggregateId, "12.5");

        Produce(evt);

        var eventRow = await Polling.WaitUntilAsync(() => TryGetEventStoreRowAsync(aggregateId), TimeSpan.FromSeconds(45));
        Assert.Equal(1L, eventRow.Version);
        Assert.Equal(12.5m, eventRow.PriceChange);
        Assert.Equal(nameof(PriceChangedEvent), eventRow.EventType);

        var published = await Polling.WaitUntilAsync(async () =>
            (await _fixture.Published.OfTypeAsync<DomainPriceChanged>(aggregateId.ToString())).SingleOrDefault(),
            TimeSpan.FromSeconds(45));
        Assert.Equal(new DomainPriceChanged(aggregateId, 12.5m, 1, evt.OccurredAt.ToDateTimeOffset()), published.Message);
    }

    [Fact]
    public async Task PriceChangeRequestedEvent_PriceChangeOverflowsColumnPrecision_NeverAppended_RawEventRoutedToFatalDlq()
    {
        var aggregateId = Guid.NewGuid();
        var evt = NewRequest(aggregateId, "12345678901234567");

        Produce(evt);

        var fatalMessage = KafkaTestConsumer.WaitForMessage<PriceChangeRequestedEvent>(
            _fixture.BootstrapAddress, _fixture.SerializerFor<PriceChangeRequestedEvent>(),
            StockServiceHostFixture.DeadLetterTopic("price-change-requested-events-group", StockTopic.PriceChangeRequested.Name()),
            m => m.AggregateId == aggregateId.ToString(),
            TimeSpan.FromSeconds(45));

        Assert.Equal(evt, fatalMessage.Message.Value); 
        Assert.Null(await TryGetEventStoreRowAsync(aggregateId));
    }

    private static PriceChangeRequestedEvent NewRequest(Guid aggregateId, string priceChange)
    {
        return new PriceChangeRequestedEvent
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId.ToString(),
            PriceChange = priceChange,
            OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };
    }

    private void Produce(PriceChangeRequestedEvent evt)
    {
        _fixture.Produce(StockTopic.PriceChangeRequested.Name(), Guid.Parse(evt.AggregateId), evt);
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
