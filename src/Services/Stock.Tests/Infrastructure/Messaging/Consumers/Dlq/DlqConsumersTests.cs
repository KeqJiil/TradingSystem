using Dapper;
using Microsoft.Data.SqlClient;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq;

[Collection(StockServiceCollection.Name)]
public class DlqConsumersTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly StockServiceHostFixture _fixture;

    public DlqConsumersTests(StockServiceHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StockCreatedRetry_CreatesReadModel()
    {
        var aggregateId = Guid.NewGuid();

        _fixture.Produce(RetryTopic(TopicNames.StockCreated), aggregateId, NewStockCreatedEvent(aggregateId));

        await Polling.WaitUntilAsync(async () => await ProjectionVersionAsync(aggregateId) is not null, Timeout);
    }

    [Fact]
    public async Task StockToggledStatusRetry_AppliesStatusToReadModel()
    {
        var aggregateId = await SeedStockAsync();

        _fixture.Produce(RetryTopic(TopicNames.StockStatusToggled), aggregateId,
            new StockToggledStatusEvent(aggregateId, DateTimeOffset.UtcNow, false, 1));

        await Polling.WaitUntilAsync(async () => await QueryAsync<bool?>(
            "SELECT is_open_to_trade FROM stock_data_projection WHERE aggregate_id = @Id", aggregateId) == false,
            Timeout);
    }

    [Fact]
    public async Task PriceChangeRequestedRetry_AppendsEventToEventStore()
    {
        var aggregateId = Guid.NewGuid();

        _fixture.Produce(RetryTopic(TopicNames.PriceChangeRequested), aggregateId,
            new PriceChangeRequestedEvent(Guid.NewGuid(), aggregateId, 4m, DateTimeOffset.UtcNow));

        await Polling.WaitUntilAsync(async () => await QueryAsync<long?>(
            "SELECT version FROM events_store WHERE aggregate_id = @Id", aggregateId) == 1, Timeout);
    }

    [Fact]
    public async Task PriceChangedRetry_ReplaysReadModelFromEventStore()
    {
        var aggregateId = await SeedStockAsync();
        await using (var connection = new SqlConnection(_fixture.ConnectionString))
        {
            await connection.ExecuteAsync("""
                INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change, occured_at)
                VALUES (NEWID(), @Id, 1, 'PriceChangedEvent', '{}', 2, SYSDATETIMEOFFSET())
                """, new { Id = aggregateId });
        }

        _fixture.Produce(RetryTopic(TopicNames.Price), aggregateId,
            new PriceChangedEvent(aggregateId, 2m, 1, DateTimeOffset.UtcNow));

        await Polling.WaitUntilAsync(async () => await ProjectionVersionAsync(aggregateId) == 1, Timeout);
        Assert.Equal(2m, await QueryAsync<decimal?>(
            "SELECT price FROM stock_data_projection WHERE aggregate_id = @Id", aggregateId));
    }

    private async Task<Guid> SeedStockAsync()
    {
        var aggregateId = Guid.NewGuid();
        _fixture.Produce(TopicNames.StockCreated, aggregateId, NewStockCreatedEvent(aggregateId));
        await Polling.WaitUntilAsync(async () => await ProjectionVersionAsync(aggregateId) is not null, Timeout);
        return aggregateId;
    }

    private static StockCreatedEvent NewStockCreatedEvent(Guid aggregateId)
    {
        return new StockCreatedEvent(aggregateId, "AAPL", true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0));
    }

    private static string RetryTopic(string sourceTopic)
    {
        return sourceTopic + new DeadLetterOptions().TopicSuffix + ".retry";
    }

    private Task<long?> ProjectionVersionAsync(Guid aggregateId)
    {
        return QueryAsync<long?>("SELECT version FROM stock_data_projection WHERE aggregate_id = @Id", aggregateId);
    }

    private async Task<T?> QueryAsync<T>(string sql, Guid aggregateId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<T>(sql, new { Id = aggregateId });
    }
}
