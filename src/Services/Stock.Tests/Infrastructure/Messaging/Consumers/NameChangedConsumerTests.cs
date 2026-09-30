using Dapper;
using Microsoft.Data.SqlClient;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

[Collection(StockServiceCollection.Name)]
public class NameChangedConsumerTests
{
    private readonly StockServiceHostFixture _fixture;

    public NameChangedConsumerTests(StockServiceHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HappyPath_NameChangedEvent_UpdatesNameAndNameVersion()
    {
        var aggregateId = Guid.NewGuid();
        await SeedStockAsync(aggregateId);

        _fixture.Produce(TopicNames.StockNameChanged, aggregateId, new NameChangedEvent(aggregateId, "MSFT", 1));

        var row = await Polling.WaitUntilAsync(() => TryGetNameIfVersionAsync(aggregateId, 1),
            TimeSpan.FromSeconds(45));
        Assert.Equal("MSFT", row.Name);
    }

    [Fact]
    public async Task NameChangeArrivesBeforeStockCreated_IsAppliedOnceReadModelExists()
    {
        var aggregateId = Guid.NewGuid();
        _fixture.Produce(TopicNames.StockNameChanged, aggregateId, new NameChangedEvent(aggregateId, "MSFT", 1));

        await SeedStockAsync(aggregateId);

        var row = await Polling.WaitUntilAsync(() => TryGetNameIfVersionAsync(aggregateId, 1),
            TimeSpan.FromSeconds(60));
        Assert.Equal("MSFT", row.Name);
    }

    private async Task SeedStockAsync(Guid aggregateId)
    {
        _fixture.Produce(TopicNames.StockCreated, aggregateId,
            new StockCreatedEvent(aggregateId, "AAPL", true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0)));

        await Polling.WaitUntilAsync(() => TryGetNameIfVersionAsync(aggregateId, 0), TimeSpan.FromSeconds(45));
    }

    private async Task<NameRow?> TryGetNameIfVersionAsync(Guid aggregateId, long expectedVersion)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<NameRow>("""
            SELECT "name" AS Name, "name_version" AS NameVersion
            FROM "stock_data_projection"
            WHERE "aggregate_id" = @AggregateId AND "name_version" >= @ExpectedVersion
            """, new { AggregateId = aggregateId, ExpectedVersion = expectedVersion });
    }

    private record NameRow(string Name, long NameVersion);
}
