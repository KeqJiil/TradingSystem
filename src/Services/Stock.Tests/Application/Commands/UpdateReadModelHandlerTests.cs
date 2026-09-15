using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Commands.UpdateReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class UpdateReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockReadModelWriter Writer { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private UpdateReadModelHandler Handler { get; init; }
    private TestDbContext DbContext { get; init; }

    public UpdateReadModelHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Writer = new StockReadModelWriter(dbContext);
        Handler = new UpdateReadModelHandler(Writer);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await StockTestSchema.EnsureStockDataProjectionCreatedAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await DbContext.Connection.ExecuteAsync("DROP TABLE IF EXISTS stock_data_projection");
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldUpdateReadModel()
    {
        var aggregateId = Guid.NewGuid();
        var createCommand = new UpdateReadModelCommand(aggregateId, 1, 1L);
        await Seed(aggregateId, 100m, 0L);

        var result = await Handler.Handle(createCommand, CancellationToken.None);

        var updatedStock = await DbContext.Connection.QuerySingleAsync<StockReadModel>("""
            SELECT
                aggregate_id AS AggregateId,
                name AS Name,
                price AS Price,
                currency AS Currency,
                version AS Version
            FROM stock_data_projection
            WHERE aggregate_id = @AggregateId
            """, new { AggregateId = aggregateId });

        Assert.Equal(ReadModelUpdateOutcome.Applied, result);
        Assert.Equal(101m, updatedStock.Price);
        Assert.Equal(1L, updatedStock.Version);
    }

    [Fact]
    public async Task Handle_ShouldReturnGap_WhenVersionMismatch()
    {
        var aggregateId = Guid.NewGuid();
        var createCommand = new UpdateReadModelCommand(aggregateId, 1, 2L);
        await Seed(aggregateId, 100m, 0L);

        var result = await Handler.Handle(createCommand, CancellationToken.None);
        var updatedStock = await DbContext.Connection.QuerySingleAsync<StockReadModel>("""
            SELECT
                aggregate_id AS AggregateId,
                name AS Name,
                price AS Price,
                currency AS Currency,
                version AS Version
            FROM stock_data_projection
            WHERE aggregate_id = @AggregateId
            """, new { AggregateId = aggregateId });

        Assert.Equal(ReadModelUpdateOutcome.Gap, result);
        Assert.Equal(100m, updatedStock.Price);
        Assert.Equal(0L, updatedStock.Version);
    }

    [Fact]
    public async Task Handle_ShouldReturnStale_WhenVersionAlreadyApplied()
    {
        var aggregateId = Guid.NewGuid();
        var createCommand = new UpdateReadModelCommand(aggregateId, 1, 0L);
        await Seed(aggregateId, 100m, 1L);

        var result = await Handler.Handle(createCommand, CancellationToken.None);
        var updatedStock = await DbContext.Connection.QuerySingleAsync<StockReadModel>("""
            SELECT
                aggregate_id AS AggregateId,
                name AS Name,
                price AS Price,
                currency AS Currency,
                version AS Version
            FROM stock_data_projection
            WHERE aggregate_id = @AggregateId
            """, new { AggregateId = aggregateId });

        Assert.Equal(ReadModelUpdateOutcome.Stale, result);
        Assert.Equal(100m, updatedStock.Price);
        Assert.Equal(1L, updatedStock.Version);
    }

    private record StockReadModel
    {
        public Guid AggregateId { get; init; }
        public string Name { get; init; }
        public decimal Price { get; init; }
        public string Currency { get; init; }
        public long Version { get; init; }
    }

    private async Task Seed(Guid aggregateId, decimal initialPrice, long initialVersion)
    {
        var sql = """
                  INSERT INTO stock_data_projection (
                      aggregate_id, name, is_open_to_trade, price, version, trading_start_time, trading_end_time, currency
                  ) VALUES (
                      @AggregateId, @Name, @IsOpenToTrade, @Price, @Version, @TradingStartTime, @TradingEndTime, @Currency
                  )
                  """;

        await DbContext.Connection.ExecuteAsync(sql,
            new
            {
                AggregateId = aggregateId,
                Name = "TestStock",
                IsOpenToTrade = true,
                Price = initialPrice,
                Version = initialVersion,
                TradingStartTime = TimeOnly.FromDateTime(DateTime.Now),
                TradingEndTime = TimeOnly.FromDateTime(DateTime.Now.AddHours(1)),
                Currency = "USD"
            });
    }
}