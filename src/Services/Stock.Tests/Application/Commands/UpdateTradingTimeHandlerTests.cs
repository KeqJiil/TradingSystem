using Dapper;
using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Stock.Application.Exceptions;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class UpdateTradingTimeHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly UpdateTradingTimeHandler _handler;
    private readonly TestDbContext _dbContext;

    public UpdateTradingTimeHandlerTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        _handler = new UpdateTradingTimeHandler(new StockReadModelWriter(_dbContext));
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
    public async Task Handle_ShouldSetTradingTimeAndTradingTimeVersion()
    {
        var aggregateId = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, aggregateId);

        await _handler.Handle(Command(aggregateId, 10, 18, version: 1), CancellationToken.None);

        Assert.Equal((new TimeOnly(10, 0), new TimeOnly(18, 0), 1L), await ReadTime(aggregateId));
    }

    [Fact]
    public async Task Handle_SameVersionTwice_ShouldKeepFirstApplied()
    {
        var aggregateId = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, aggregateId);

        await _handler.Handle(Command(aggregateId, 10, 18, version: 1), CancellationToken.None);
        await _handler.Handle(Command(aggregateId, 11, 19, version: 1), CancellationToken.None);

        Assert.Equal((new TimeOnly(10, 0), new TimeOnly(18, 0), 1L), await ReadTime(aggregateId));
    }

    [Fact]
    public async Task Handle_OlderVersionAfterNewer_ShouldBeIgnored()
    {
        var aggregateId = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, aggregateId);

        await _handler.Handle(Command(aggregateId, 11, 19, version: 2), CancellationToken.None);
        await _handler.Handle(Command(aggregateId, 10, 18, version: 1), CancellationToken.None);

        Assert.Equal((new TimeOnly(11, 0), new TimeOnly(19, 0), 2L), await ReadTime(aggregateId));
    }

    [Fact]
    public async Task Handle_ShouldThrowReadModelNotFound_WhenReadModelDoesNotExist()
    {
        var aggregateId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ReadModelNotFoundException>(() =>
            _handler.Handle(Command(aggregateId, 10, 18, version: 1), CancellationToken.None));

        Assert.Equal(aggregateId, exception.AggregateId);
    }

    private static UpdateTradingTimeCommand Command(Guid aggregateId, int startHour, int closeHour, long version)
    {
        return new UpdateTradingTimeCommand(aggregateId, new TimeOnly(startHour, 0), new TimeOnly(closeHour, 0),
            version);
    }

    private async Task<(TimeOnly Start, TimeOnly End, long Version)> ReadTime(Guid aggregateId)
    {
        var row = await _dbContext.Connection.QuerySingleAsync<(TimeSpan Start, TimeSpan End, long Version)>(
            "SELECT trading_start_time, trading_end_time, trading_time_version FROM stock_data_projection WHERE aggregate_id = @Id",
            new { Id = aggregateId });
        return (TimeOnly.FromTimeSpan(row.Start), TimeOnly.FromTimeSpan(row.End), row.Version);
    }
}
