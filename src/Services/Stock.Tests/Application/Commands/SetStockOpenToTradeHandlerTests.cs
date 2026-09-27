using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Stock.Application.Abstractions;
using Stock.Application.Commands.SetStockOpenToTrade;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class SetStockOpenToTradeHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly IStockWriter _writer;
    private readonly SetStockOpenToTradeHandler _handler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MssqlFixture _fixture;
    private readonly TestDbContext DbContext;

    public SetStockOpenToTradeHandlerTests(MssqlFixture fixture)
    {
        _fixture = fixture;
        DbContext = new TestDbContext(fixture.ConnectionString);
        var unitOfWork = new UnitOfWork(new TestDbConnectionFactory(_fixture.ConnectionString));
        _unitOfWork = unitOfWork;
        _writer = new StockDataWriter(unitOfWork);
        var outboxWriter = new OutboxWriter(unitOfWork);
        var resilence = new ResiliencePipelineBuilder().Build();
        var decorator = new UnitOfWorkDecorator(_unitOfWork, resilence, NullLogger<UnitOfWorkDecorator>.Instance);
        _handler = new SetStockOpenToTradeHandler(_writer, outboxWriter, decorator);
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
    public async Task Handle_ShouldSetIsOpenToTradeToFalse()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: true);

        var found = await _handler.Handle(new SetStockOpenToTradeCommand(stockId, false), CancellationToken.None);

        var isOpenToTrade = await DbContext.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data WHERE id = @Id",
            new { Id = stockId }
        );
        Assert.True(found);
        Assert.False(isOpenToTrade);
    }

    [Fact]
    public async Task Handle_ShouldSetIsOpenToTradeToTrue()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: false);

        await _handler.Handle(new SetStockOpenToTradeCommand(stockId, true), CancellationToken.None);

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

        await _handler.Handle(new SetStockOpenToTradeCommand(stockId, false), CancellationToken.None);

        var outboxCount = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id",
            new { Id = stockId }
        );
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task Handle_ShouldWriteEventWithNewStateAndIncrementedVersion()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, isOpenToTrade: true);

        await _handler.Handle(new SetStockOpenToTradeCommand(stockId, false), CancellationToken.None);
        await _handler.Handle(new SetStockOpenToTradeCommand(stockId, true), CancellationToken.None);

        var events = (await DbContext.Connection.QueryAsync<string>(
                "SELECT payload FROM outbox WHERE aggregate_id = @Id", new { Id = stockId }))
            .Select(p => JsonSerializer.Deserialize<StockToggledStatusEvent>(p)!)
            .OrderBy(e => e.StatusVersion)
            .ToList();

        Assert.Equal([1L, 2L], events.Select(e => e.StatusVersion));
        Assert.Equal([false, true], events.Select(e => e.IsOpenToTrade));
    }

    [Fact]
    public async Task Handle_ShouldReturnFalseAndNotWriteOutboxEvent_WhenStockDoesNotExist()
    {
        var stockId = Guid.NewGuid();

        var found = await _handler.Handle(new SetStockOpenToTradeCommand(stockId, true), CancellationToken.None);

        var outboxCount = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id",
            new { Id = stockId }
        );
        Assert.False(found);
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
