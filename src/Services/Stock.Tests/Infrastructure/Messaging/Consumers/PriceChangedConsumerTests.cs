using Dapper;
using Microsoft.Data.SqlClient;
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

        _fixture.Produce(TopicNames.Price, aggregateId,
            new PriceChangedEvent(aggregateId, 5m, null, DateTimeOffset.UtcNow));

        ProducePriceChange(aggregateId, priceChange: 5m, version: 1);

        var row = await Polling.WaitUntilAsync(
            () => TryGetRowIfVersionAsync(aggregateId, expectedVersion: 1), TimeSpan.FromSeconds(45));
        Assert.Equal(5m, row.Price);
    }

    private async Task<Guid> SeedStockAsync()
    {
        var aggregateId = Guid.NewGuid();
        _fixture.Produce(TopicNames.StockCreated, aggregateId,
            new StockCreatedEvent(aggregateId, "AAPL", true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0)));

        await Polling.WaitUntilAsync(() => TryGetRowAsync(aggregateId), TimeSpan.FromSeconds(45));
        return aggregateId;
    }

    private void ProducePriceChange(Guid aggregateId, decimal priceChange, long version)
    {
        _fixture.Produce(TopicNames.Price, aggregateId,
            new PriceChangedEvent(aggregateId, priceChange, version, DateTimeOffset.UtcNow));
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
