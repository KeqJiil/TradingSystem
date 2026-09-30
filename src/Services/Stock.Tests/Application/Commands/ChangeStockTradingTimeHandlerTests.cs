using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ChangeStockTradingTime;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class ChangeStockTradingTimeHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockWriter Writer { get; init; }
    private ChangeStockTradingTimeHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }
    private IUnitOfWork UnitOfWork { get; init; }

    public ChangeStockTradingTimeHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        var unitOfWork = new UnitOfWork(new TestDbConnectionFactory(MssqlFixture.ConnectionString));
        UnitOfWork = unitOfWork;
        Writer = new StockDataWriter(unitOfWork);
        var decorator = new UnitOfWorkDecorator(unitOfWork, new ResiliencePipelineBuilder().Build(),
            NullLogger<UnitOfWorkDecorator>.Instance);
        Handler = new ChangeStockTradingTimeHandler(Writer, new OutboxWriter(unitOfWork), decorator);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await UnitOfWork.DisposeAsync();
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldChangeStockTradingTime()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, TimeOnly.FromTimeSpan(TimeSpan.FromHours(1)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(23)));
        var command = new ChangeStockTradingTimeCommand(stockId, new TimeOnly(9, 0), new TimeOnly(17, 0));

        await Handler.Handle(command, CancellationToken.None);

        var updatedStock = await DbContext.Connection.QuerySingleAsync(
            "SELECT trading_start_time, trading_end_time FROM stock_data WHERE id = @Id",
            new { Id = stockId }
        );

        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromTimeSpan((TimeSpan)updatedStock.trading_start_time));
        Assert.Equal(new TimeOnly(17, 0), TimeOnly.FromTimeSpan((TimeSpan)updatedStock.trading_end_time));
    }

    [Fact]
    public async Task Handle_ShouldWriteTimeChangedEventWithIncrementedVersion()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId);

        var found = await Handler.Handle(
            new ChangeStockTradingTimeCommand(stockId, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            CancellationToken.None);

        var @event = JsonSerializer.Deserialize<TimeChangedEvent>(await DbContext.Connection.QuerySingleAsync<string>(
            "SELECT payload FROM outbox WHERE aggregate_id = @Id", new { Id = stockId }))!;
        Assert.True(found);
        Assert.Equal(1L, @event.Version);
        Assert.Equal(new TimeOnly(9, 0), @event.TradingStartTime);
        Assert.Equal(new TimeOnly(17, 0), @event.TradingCloseTime);
    }

    [Fact]
    public async Task Handle_ShouldReturnFalseAndWriteNoEvent_WhenStockDoesNotExist()
    {
        var stockId = Guid.NewGuid();

        var found = await Handler.Handle(
            new ChangeStockTradingTimeCommand(stockId, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            CancellationToken.None);

        Assert.False(found);
        Assert.Equal(0, await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id", new { Id = stockId }));
    }

    private async Task Seed(Guid id, TimeOnly? openTime = null, TimeOnly? closeTime = null)
    {
        await DbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, 1, @OpenTime, @CloseTime, @Currency)",
            new
            {
                Id = id,
                Name = "OldName",
                OpenTime = openTime ?? TimeOnly.FromTimeSpan(TimeSpan.FromHours(1)),
                CloseTime = closeTime ?? TimeOnly.FromTimeSpan(TimeSpan.FromHours(23)),
                Currency = "USD"
            });
    }
}