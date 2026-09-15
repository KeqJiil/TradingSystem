using Confluent.Kafka;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

[Collection(StockServiceCollection.Name)]
public class PriceChangedConsumerTests
{
    private readonly StockServiceHostFixture _fixture;

    public PriceChangedConsumerTests(StockServiceHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HappyPath_SingleInOrderPriceChange_UpdatesPriceAndVersion()
    {
        var aggregateId = await SeedStockAsync();

        ProducePriceChange(aggregateId, priceChange: 5m, version: 1);

        var row = await Polling.WaitUntilAsync(
            () => TryGetRowIfVersionAsync(aggregateId, expectedVersion: 1), TimeSpan.FromSeconds(45));
        Assert.Equal(5m, row.Price);
    }

    [Fact]
    public async Task VersionIsNull_EventIsSkipped_NoDbChange_AndConsumerKeepsProcessingSubsequentMessages()
    {
        var aggregateId = await SeedStockAsync();

        var producer = _fixture.Services.GetRequiredService<IProducer<string, PriceChangedEvent>>();
        producer.Produce(TopicNames.Price, new Message<string, PriceChangedEvent>
        {
            Key = aggregateId.ToString(),
            Value = new PriceChangedEvent(aggregateId, 5m, null, DateTimeOffset.UtcNow)
        });
        producer.Flush(TimeSpan.FromSeconds(10));

        ProducePriceChange(aggregateId, priceChange: 5m, version: 1);

        var row = await Polling.WaitUntilAsync(
            () => TryGetRowIfVersionAsync(aggregateId, expectedVersion: 1), TimeSpan.FromSeconds(45));
        Assert.Equal(5m, row.Price);
    }

    [Fact]
    public async Task OutOfOrderVersions_BufferedUntilGapFills_ThenAppliedInOrder()
    {
        var aggregateId = await SeedStockAsync();

        ProducePriceChange(aggregateId, priceChange: 3m, version: 2); 

        await Task.Delay(TimeSpan.FromSeconds(2));
        var bufferedRow = await TryGetRowAsync(aggregateId);
        Assert.Equal(0L, bufferedRow!.Version);
        Assert.Equal(0m, bufferedRow.Price);

        ProducePriceChange(aggregateId, priceChange: 2m, version: 1); 

        var finalRow = await Polling.WaitUntilAsync(
            () => TryGetRowIfVersionAsync(aggregateId, expectedVersion: 2), TimeSpan.FromSeconds(45));
        Assert.Equal(5m, finalRow.Price);
    }

    private async Task<Guid> SeedStockAsync()
    {
        var aggregateId = Guid.NewGuid();
        var producer = _fixture.Services.GetRequiredService<IProducer<string, StockCreatedEvent>>();
        producer.Produce(TopicNames.StockCreated, new Message<string, StockCreatedEvent>
        {
            Key = aggregateId.ToString(),
            Value = new StockCreatedEvent(aggregateId, "AAPL", true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0))
        });
        producer.Flush(TimeSpan.FromSeconds(10));

        await Polling.WaitUntilAsync(() => TryGetRowAsync(aggregateId), TimeSpan.FromSeconds(45));
        return aggregateId;
    }

    private void ProducePriceChange(Guid aggregateId, decimal priceChange, long version)
    {
        var producer = _fixture.Services.GetRequiredService<IProducer<string, PriceChangedEvent>>();
        producer.Produce(TopicNames.Price, new Message<string, PriceChangedEvent>
        {
            Key = aggregateId.ToString(),
            Value = new PriceChangedEvent(aggregateId, priceChange, version, DateTimeOffset.UtcNow)
        });
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    private async Task<ProjectionRow?> TryGetRowAsync(Guid aggregateId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<ProjectionRow>("""
            SELECT "price" AS Price, "version" AS Version
            FROM "stock_data_projection"
            WHERE "aggregate_id" = @AggregateId
            """, new { AggregateId = aggregateId });
    }

    private async Task<ProjectionRow?> TryGetRowIfVersionAsync(Guid aggregateId, long expectedVersion)
    {
        var row = await TryGetRowAsync(aggregateId);
        return row?.Version == expectedVersion ? row : null;
    }

    private record ProjectionRow(decimal Price, long Version);
}
