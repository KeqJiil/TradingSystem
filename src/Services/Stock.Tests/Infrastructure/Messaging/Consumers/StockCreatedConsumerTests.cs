using Confluent.Kafka;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

[Collection(StockServiceCollection.Name)]
public class StockCreatedConsumerTests
{
    private readonly StockServiceHostFixture _fixture;

    public StockCreatedConsumerTests(StockServiceHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HappyPath_StockCreatedEvent_CreatesStockDataProjectionRow()
    {
        var aggregateId = Guid.NewGuid();
        var evt = new StockCreatedEvent(aggregateId, "AAPL", true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0));

        Produce(evt);

        var row = await Polling.WaitUntilAsync(() => TryGetRowAsync(aggregateId), TimeSpan.FromSeconds(45));

        Assert.Equal("AAPL", row.Name);
        Assert.True(row.IsOpenToTrade);
        Assert.Equal("USD", row.Currency);
        Assert.Equal(0m, row.Price);
        Assert.Equal(0L, row.Version);
        Assert.Equal(new TimeOnly(9, 30), row.TradingStartTime);
        Assert.Equal(new TimeOnly(16, 0), row.TradingEndTime);
    }

    [Fact]
    public async Task StockCreatedEvent_NameExceedsColumnWidth_NeverPersisted_RawEventRoutedToFatalDlq()
    {
        var aggregateId = Guid.NewGuid();
        var oversizedName = new string('A', 50);
        var evt = new StockCreatedEvent(aggregateId, oversizedName, true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0));

        Produce(evt);

        var fatalMessage = KafkaTestConsumer.WaitForMessage<StockCreatedEvent>(
            _fixture.BootstrapAddress,
            TopicNames.StockCreated + new DeadLetterOptions().TopicSuffix + ".fatal",
            m => m.AggregateId == aggregateId,
            TimeSpan.FromSeconds(45));

        Assert.Equal(evt, fatalMessage.Message.Value); 
        Assert.Null(await TryGetRowAsync(aggregateId));
    }

    private void Produce(StockCreatedEvent evt)
    {
        var producer = _fixture.Services.GetRequiredService<IProducer<string, StockCreatedEvent>>();
        producer.Produce(TopicNames.StockCreated,
            new Message<string, StockCreatedEvent> { Key = evt.AggregateId.ToString(), Value = evt });
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    private async Task<ProjectionRow?> TryGetRowAsync(Guid aggregateId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<ProjectionRow>("""
            SELECT
                "name" AS Name,
                "is_open_to_trade" AS IsOpenToTrade,
                "price" AS Price,
                "version" AS Version,
                "currency" AS Currency,
                "trading_start_time" AS TradingStartTime,
                "trading_end_time" AS TradingEndTime
            FROM "stock_data_projection"
            WHERE "aggregate_id" = @AggregateId
            """, new { AggregateId = aggregateId });
    }

    private record ProjectionRow(string Name, bool IsOpenToTrade, decimal Price, long Version, string Currency,
        TimeOnly TradingStartTime, TimeOnly TradingEndTime);
}
