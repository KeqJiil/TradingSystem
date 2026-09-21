using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Application.Exceptions;
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
    public async Task Handle_ShouldSetIsOpenToTradeAndStatusVersion()
    {
        var aggregateId = Guid.NewGuid();
        await Seed(aggregateId, isOpenToTrade: true);

        await _handler.Handle(new ToggleStatusReadModelCommand(aggregateId, false, 1), CancellationToken.None);

        var (isOpenToTrade, statusVersion) = await ReadStatus(aggregateId);
        Assert.False(isOpenToTrade);
        Assert.Equal(1, statusVersion);
    }

    [Fact]
    public async Task Handle_Duplicate_ShouldNotFlipStatusBack()
    {
        var aggregateId = Guid.NewGuid();
        await Seed(aggregateId, isOpenToTrade: true);
        var command = new ToggleStatusReadModelCommand(aggregateId, false, 1);

        await _handler.Handle(command, CancellationToken.None);
        await _handler.Handle(command, CancellationToken.None);

        var (isOpenToTrade, statusVersion) = await ReadStatus(aggregateId);
        Assert.False(isOpenToTrade);
        Assert.Equal(1, statusVersion);
    }

    [Fact]
    public async Task Handle_OlderVersionAfterNewer_ShouldBeIgnored()
    {
        var aggregateId = Guid.NewGuid();
        await Seed(aggregateId, isOpenToTrade: true);

        await _handler.Handle(new ToggleStatusReadModelCommand(aggregateId, true, 2), CancellationToken.None);
        await _handler.Handle(new ToggleStatusReadModelCommand(aggregateId, false, 1), CancellationToken.None);

        var (isOpenToTrade, statusVersion) = await ReadStatus(aggregateId);
        Assert.True(isOpenToTrade);
        Assert.Equal(2, statusVersion);
    }

    [Fact]
    public async Task Handle_ShouldThrowReadModelNotFound_WhenReadModelDoesNotExist()
    {
        var aggregateId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ReadModelNotFoundException>(() =>
            _handler.Handle(new ToggleStatusReadModelCommand(aggregateId, false, 1), CancellationToken.None));

        Assert.Equal(aggregateId, exception.AggregateId);
    }

    [Fact]
    public async Task Handle_ShouldNotAffectOtherAggregates()
    {
        var toggledId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await Seed(toggledId, isOpenToTrade: true);
        await Seed(otherId, isOpenToTrade: true);

        await _handler.Handle(new ToggleStatusReadModelCommand(toggledId, false, 1), CancellationToken.None);

        var (otherIsOpenToTrade, otherStatusVersion) = await ReadStatus(otherId);
        Assert.True(otherIsOpenToTrade);
        Assert.Equal(0, otherStatusVersion);
    }

    private Task<(bool IsOpenToTrade, long StatusVersion)> ReadStatus(Guid aggregateId)
    {
        return _dbContext.Connection.QuerySingleAsync<(bool IsOpenToTrade, long StatusVersion)>(
            "SELECT is_open_to_trade, status_version FROM stock_data_projection WHERE aggregate_id = @Id",
            new { Id = aggregateId });
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
