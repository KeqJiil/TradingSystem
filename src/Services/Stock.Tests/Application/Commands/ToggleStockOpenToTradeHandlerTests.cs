using System.Text.Json;
using Dapper;
using Polly;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ToggleStockOpenToTrade;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class ToggleStockOpenToTradeHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly IStockWriter _writer;
    private readonly ToggleStockOpenToTradeHandler _handler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MssqlFixture _fixture;
    private readonly TestDbContext DbContext;

    public ToggleStockOpenToTradeHandlerTests(MssqlFixture fixture)
    {
        _fixture = fixture;
        DbContext = new TestDbContext(fixture.ConnectionString);
        _writer = new StockDataWriter(DbContext);
        var outboxWriter = new OutboxWriter(DbContext);
        var resilence = new ResiliencePipelineBuilder().Build();
        _unitOfWork = new UnitOfWork(new TestDbConnectionFactory(_fixture.ConnectionString));
        var decorator = new UnitOfWorkDecorator(_unitOfWork, resilence);
        _handler = new ToggleStockOpenToTradeHandler(_writer, outboxWriter, decorator);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await _unitOfWork.DisposeAsync();
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldToggleIsOpenToTradeFromTrueToFalse()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: true);
        var command = new ToggleStockOpenToTradeCommand(stockId);

        await _handler.Handle(command, CancellationToken.None);

        var isOpenToTrade = await DbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data WHERE id = @Id",
            new { Id = stockId }
        );
        Assert.False(isOpenToTrade);
    }

    [Fact]
    public async Task Handle_ShouldToggleIsOpenToTradeFromFalseToTrue()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: false);
        var command = new ToggleStockOpenToTradeCommand(stockId);

        await _handler.Handle(command, CancellationToken.None);

        var isOpenToTrade = await DbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data WHERE id = @Id",
            new { Id = stockId }
        );
        Assert.True(isOpenToTrade);
    }

    [Fact]
    public async Task Handle_ShouldWriteOutboxEvent()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: true);
        var command = new ToggleStockOpenToTradeCommand(stockId);

        await _handler.Handle(command, CancellationToken.None);

        var outboxCount = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id",
            new { Id = stockId }
        );
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task Handle_CalledTwice_ShouldToggleBackToOriginalState()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: true);
        var command = new ToggleStockOpenToTradeCommand(stockId);

        await _handler.Handle(command, CancellationToken.None);
        await _handler.Handle(command, CancellationToken.None);

        var isOpenToTrade = await DbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data WHERE id = @Id",
            new { Id = stockId }
        );
        Assert.True(isOpenToTrade);

        var outboxCount = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id",
            new { Id = stockId }
        );
        Assert.Equal(2, outboxCount);
    }

    [Fact]
    public async Task Handle_ShouldWriteEventWithNewStateAndIncrementedStatusVersion()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: true);
        var command = new ToggleStockOpenToTradeCommand(stockId);

        await _handler.Handle(command, CancellationToken.None);
        await _handler.Handle(command, CancellationToken.None);

        var events = (await DbContext.Connection.QueryAsync<string>(
                "SELECT payload FROM outbox WHERE aggregate_id = @Id", new { Id = stockId }))
            .Select(p => JsonSerializer.Deserialize<StockToggledStatusEvent>(p)!)
            .OrderBy(e => e.StatusVersion)
            .ToList();

        Assert.Equal([1L, 2L], events.Select(e => e.StatusVersion));
        Assert.Equal([false, true], events.Select(e => e.IsOpenToTrade));
    }

    [Fact]
    public async Task Handle_ShouldNotWriteOutboxEvent_WhenStockDoesNotExist()
    {
        var stockId = Guid.NewGuid();

        await _handler.Handle(new ToggleStockOpenToTradeCommand(stockId), CancellationToken.None);

        var outboxCount = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id",
            new { Id = stockId }
        );
        Assert.Equal(0, outboxCount);
    }

    private async Task Seed(Guid id, bool isOpenToTrade)
    {
        await DbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, @IsOpenToTrade, @OpenTime, @CloseTime, @Currency)",
            new
            {
                Id = id,
                Name = "StockName",
                IsOpenToTrade = isOpenToTrade,
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(17),
                Currency = "USD"
            });
    }
}
