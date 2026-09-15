using Confluent.Kafka;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

[Collection(StockServiceCollection.Name)]
public class StockToggleStatusEventConsumerTests
{
    private readonly StockServiceHostFixture _fixture;

    public StockToggleStatusEventConsumerTests(StockServiceHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HappyPath_StockToggledStatusEvent_FlipsIsOpenToTrade()
    {
        var aggregateId = Guid.NewGuid();
        await SeedStockAsync(aggregateId, isOpenToTrade: true);

        ProduceToggle(aggregateId);

        await Polling.WaitUntilAsync(async () => await TryGetIsOpenToTradeAsync(aggregateId) == false,
            TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task ToggleForUnknownAggregate_DoesNotThrow_AndConsumerKeepsProcessingSubsequentMessages()
    {
        var unknownAggregateId = Guid.NewGuid();
        ProduceToggle(unknownAggregateId);

        var knownAggregateId = Guid.NewGuid();
        await SeedStockAsync(knownAggregateId, isOpenToTrade: true);
        ProduceToggle(knownAggregateId);

        await Polling.WaitUntilAsync(async () => await TryGetIsOpenToTradeAsync(knownAggregateId) == false,
            TimeSpan.FromSeconds(45));
    }

    private void ProduceToggle(Guid aggregateId)
    {
        var producer = _fixture.Services.GetRequiredService<IProducer<string, StockToggledStatusEvent>>();
        var evt = new StockToggledStatusEvent(aggregateId, DateTimeOffset.UtcNow);
        producer.Produce(TopicNames.StockStatusToggled,
            new Message<string, StockToggledStatusEvent> { Key = aggregateId.ToString(), Value = evt });
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    private async Task SeedStockAsync(Guid aggregateId, bool isOpenToTrade)
    {
        var producer = _fixture.Services.GetRequiredService<IProducer<string, StockCreatedEvent>>();
        var evt = new StockCreatedEvent(aggregateId, "AAPL", isOpenToTrade, "USD", new TimeOnly(9, 30),
            new TimeOnly(16, 0));
        producer.Produce(TopicNames.StockCreated,
            new Message<string, StockCreatedEvent> { Key = aggregateId.ToString(), Value = evt });
        producer.Flush(TimeSpan.FromSeconds(10));

        await Polling.WaitUntilAsync(async () => await TryGetIsOpenToTradeAsync(aggregateId) is not null,
            TimeSpan.FromSeconds(45));
    }

    private async Task<bool?> TryGetIsOpenToTradeAsync(Guid aggregateId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        var result = await connection.QuerySingleOrDefaultAsync<bool?>("""
            SELECT "is_open_to_trade" FROM "stock_data_projection" WHERE "aggregate_id" = @AggregateId
            """, new { AggregateId = aggregateId });
        return result;
    }
}
