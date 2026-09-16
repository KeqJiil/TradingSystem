using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class ToggleStatusReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly ToggleStatusReadModelHandler _handler;
    private readonly IStockReadModelWriter _writer;
    private readonly TestDbContext _dbContext;
    private readonly MssqlFixture _fixture;

    public ToggleStatusReadModelHandlerTests(MssqlFixture fixture)
    {
        _fixture = fixture;
        _dbContext = new TestDbContext(_fixture.ConnectionString);
        _writer = new StockReadModelWriter(_dbContext);
        _handler = new ToggleStatusReadModelHandler(_writer);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldToggleIsOpenToTradeFromTrueToFalse()
    {
        var aggregateId = Guid.NewGuid();
        await Seed(aggregateId, isOpenToTrade: true);
        var command = new ToggleStatusReadModelCommand(aggregateId);

        await _handler.Handle(command, CancellationToken.None);

        var isOpenToTrade = await _dbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data_projection WHERE aggregate_id = @Id",
            new { Id = aggregateId }
        );
        Assert.False(isOpenToTrade);
    }

    [Fact]
    public async Task Handle_ShouldToggleIsOpenToTradeFromFalseToTrue()
    {
        var aggregateId = Guid.NewGuid();
        await Seed(aggregateId, isOpenToTrade: false);
        var command = new ToggleStatusReadModelCommand(aggregateId);

        await _handler.Handle(command, CancellationToken.None);

        var isOpenToTrade = await _dbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data_projection WHERE aggregate_id = @Id",
            new { Id = aggregateId }
        );
        Assert.True(isOpenToTrade);
    }

    [Fact]
    public async Task Handle_ShouldNotFail_WhenReadModelDoesNotExist()
    {
        var command = new ToggleStatusReadModelCommand(Guid.NewGuid());

        var exception = await Record.ExceptionAsync(() => _handler.Handle(command, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_ShouldNotAffectOtherAggregates()
    {
        var toggledId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await Seed(toggledId, isOpenToTrade: true);
        await Seed(otherId, isOpenToTrade: true);
        var command = new ToggleStatusReadModelCommand(toggledId);

        await _handler.Handle(command, CancellationToken.None);

        var otherIsOpenToTrade = await _dbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data_projection WHERE aggregate_id = @Id",
            new { Id = otherId }
        );
        Assert.True(otherIsOpenToTrade);
    }

    private async Task Seed(Guid aggregateId, bool isOpenToTrade)
    {
        await _dbContext.Connection.ExecuteAsync("""
            INSERT INTO stock_data_projection
                (aggregate_id, name, is_open_to_trade, price, version, trading_start_time, trading_end_time, currency)
            VALUES
                (@AggregateId, @Name, @IsOpenToTrade, @Price, @Version, @OpenTime, @CloseTime, @Currency)
            """,
            new
            {
                AggregateId = aggregateId,
                Name = "StockName",
                IsOpenToTrade = isOpenToTrade,
                Price = 0m,
                Version = 0L,
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(17),
                Currency = "USD"
            });
    }
}
