using Dapper;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class MetadataVersionInterleavingTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly UpdateNameReadModelHandler _nameHandler;
    private readonly UpdateTradingTimeHandler _timeHandler;
    private readonly ToggleStatusReadModelHandler _statusHandler;
    private readonly TestDbContext _dbContext;

    public MetadataVersionInterleavingTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        var writer = new StockReadModelWriter(_dbContext);
        _nameHandler = new UpdateNameReadModelHandler(writer);
        _timeHandler = new UpdateTradingTimeHandler(writer);
        _statusHandler = new ToggleStatusReadModelHandler(writer);
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
    public async Task SharedMetadataVersion_EachFieldAppliesItsOwnLatestChange_RegardlessOfArrivalOrder()
    {
        var aggregateId = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, aggregateId);

        await _statusHandler.Handle(new ToggleStatusReadModelCommand(aggregateId, false, 3), CancellationToken.None);
        await _nameHandler.Handle(new UpdateNameReadModelCommand(aggregateId, "NameV4", 4), CancellationToken.None);
        await _timeHandler.Handle(
            new UpdateTradingTimeCommand(aggregateId, new TimeOnly(10, 0), new TimeOnly(18, 0), 2),
            CancellationToken.None);
        await _nameHandler.Handle(new UpdateNameReadModelCommand(aggregateId, "NameV1", 1), CancellationToken.None);

        var row = await _dbContext.Connection.QuerySingleAsync<Row>("""
            SELECT name AS Name, name_version AS NameVersion,
                   trading_start_time AS TradingStart, trading_time_version AS TradingTimeVersion,
                   is_open_to_trade AS IsOpenToTrade, status_version AS StatusVersion
            FROM stock_data_projection
            WHERE aggregate_id = @Id
            """, new { Id = aggregateId });

        Assert.Equal("NameV4", row.Name);
        Assert.Equal(4, row.NameVersion);
        Assert.Equal(TimeSpan.FromHours(10), row.TradingStart);
        Assert.Equal(2, row.TradingTimeVersion);
        Assert.False(row.IsOpenToTrade);
        Assert.Equal(3, row.StatusVersion);
    }

    private sealed record Row(
        string Name,
        long NameVersion,
        TimeSpan TradingStart,
        long TradingTimeVersion,
        bool IsOpenToTrade,
        long StatusVersion);
}
