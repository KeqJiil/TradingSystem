using System.Text.Json;
using Dapper;
using Stock.Application.Commands.RequestDailyReadModels;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class RequestDailyReadModelsHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly TestDbContext _dbContext;
    private readonly RequestDailyReadModelsHandler _handler;

    public RequestDailyReadModelsHandlerTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        _handler = new RequestDailyReadModelsHandler(new OutboxWriter(_dbContext), new StockDataReader(_dbContext));
    }

    public async Task InitializeAsync()
    {
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(250)]
    public async Task Handle_WritesExactlyOneDailyRequestPerStock_AcrossBatchBoundaries(int stocks)
    {
        var stockIds = await SeedStocks(stocks);

        await _handler.Handle(new RequestDailyReadModelsCommand(DateTime.UtcNow), CancellationToken.None);

        var aggregateIds = await _dbContext.Connection.QueryAsync<Guid>(
            "SELECT aggregate_id FROM outbox WHERE event_type = @EventType",
            new { EventType = nameof(DailyReadModelRequested) });
        Assert.Equal(stockIds.Order(), aggregateIds.Order());
    }

    [Fact]
    public async Task Handle_PayloadCarriesStockIdAndDateOfRequest()
    {
        var stockId = (await SeedStocks(1)).Single();
        var requestDate = new DateTime(2026, 3, 5, 14, 30, 0, DateTimeKind.Utc);

        await _handler.Handle(new RequestDailyReadModelsCommand(requestDate), CancellationToken.None);

        var payload = JsonSerializer.Deserialize<DailyReadModelRequested>(
            await _dbContext.Connection.QuerySingleAsync<string>(
                "SELECT payload FROM outbox WHERE aggregate_id = @Id", new { Id = stockId }))!;
        Assert.Equal(new DailyReadModelRequested(stockId, new DateOnly(2026, 3, 5)), payload);
    }

    private async Task<List<Guid>> SeedStocks(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

        await _dbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, 'Stock', 1, '09:00', '17:00', 'USD')",
            ids.Select(id => new { Id = id }));

        return ids;
    }
}
